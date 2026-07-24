using System.Text;
using Alina.Core.Habilidades;
using Alina.Core.Memory;
using Alina.Core.Models;
using Alina.Core.Permissoes;
using Alina.Core.Personalidade;
using Alina.Core.Tools;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Alina.Core.Orchestration;

/// <summary>
/// Implementação do orquestrador sobre um <see cref="IChatClient"/> já configurado
/// com invocação de funções (o loop de tool-calling é resolvido pelo pipeline do
/// Microsoft.Extensions.AI).
/// </summary>
public sealed class ChatOrchestrator : IOrchestrator
{
    /// <summary>Quantas memórias mais relevantes injetar (além das fixadas) por turno.</summary>
    private const int TopKMemories = 5;

    /// <summary>
    /// Tamanho estimado (em caracteres) a partir do qual o histórico começa a ser
    /// compactado. Mantém a latência e o custo de cada turno estáveis mesmo em
    /// conversas de horas — sem compactação, o tempo até a primeira palavra cresceria
    /// junto com a conversa até estourar o contexto do modelo.
    /// </summary>
    private const int LimiteCaracteresHistorico = 80_000;

    /// <summary>Mensagens mais recentes que nunca entram no resumo (janela viva).</summary>
    private const int MensagensPreservadas = 24;

    private readonly IChatClient _client;
    private readonly ToolRegistry _tools;
    private readonly IConversationStore _store;
    private readonly IProfileStore _profile;
    private readonly IMemoryRetriever _memory;
    private readonly IHabilidadeStore _habilidades;
    private readonly IPoliticaPermissao? _politica;
    private readonly IPersonalidadeStore? _personalidade;
    private readonly IOpcoesGeracao? _opcoesGeracao;
    private readonly ILogger<ChatOrchestrator> _logger;

    private Conversation _current = new();
    private string? _preferences;
    private bool _preferencesLoaded;

    private Task<string>? _resumoEmPreparo;
    private Conversation? _conversaDoResumo;
    private int _mensagensNoResumo;

    public ChatOrchestrator(
        IChatClient client,
        ToolRegistry tools,
        IConversationStore store,
        IProfileStore profile,
        IMemoryRetriever memory,
        IHabilidadeStore habilidades,
        IPoliticaPermissao? politica = null,
        IPersonalidadeStore? personalidade = null,
        IOpcoesGeracao? opcoesGeracao = null,
        ILogger<ChatOrchestrator>? logger = null)
    {
        _client = client;
        _tools = tools;
        _store = store;
        _profile = profile;
        _memory = memory;
        _habilidades = habilidades;
        _politica = politica;
        _personalidade = personalidade;
        _opcoesGeracao = opcoesGeracao;
        _logger = logger ?? NullLogger<ChatOrchestrator>.Instance;
    }

    public Conversation Current => _current;

    public void StartNew() => _current = new Conversation();

    public async Task<bool> ResumeAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        Conversation? loaded = await _store.LoadAsync(conversationId, cancellationToken);
        if (loaded is null)
        {
            return false;
        }

        _current = loaded;
        return true;
    }

    public Task<string> SendAsync(string userText, CancellationToken cancellationToken = default)
        => SendAsync(userText, progressoResposta: null, cancellationToken);

    public async Task<string> SendAsync(string userText, IProgress<string>? progressoResposta, CancellationToken cancellationToken = default)
    {
        AplicarResumoProntoSeHouver();

        _current.Messages.Add(new ChatMessage(ChatRole.User, userText));

        if (_current.Title == "Nova conversa" && !string.IsNullOrWhiteSpace(userText))
        {
            _current.Title = userText.Length > 60 ? userText[..60] + "…" : userText;
        }

        string systemPrompt = await GetSystemPromptAsync(userText, cancellationToken);

        List<ChatMessage> request = new List<ChatMessage>(_current.Messages.Count + 1)
        {
            new(ChatRole.System, systemPrompt),
        };
        request.AddRange(_current.Messages);

        ChatOptions options = new ChatOptions { Tools = _tools.AsAIFunctions() };

        double? temperatura = _opcoesGeracao?.Temperatura;
        if (temperatura is not null)
        {
            options.Temperature = (float)temperatura.Value;
        }

        _logger.LogDebug("Enviando {Count} mensagens ao LLM ({Tools} tools).", request.Count, options.Tools.Count);

        // Streaming: cada pedaço de texto sai na hora para quem estiver ouvindo, e as
        // mensagens (inclusive chamadas/resultados de tools) são reconstruídas ao final.
        List<ChatResponseUpdate> updates = new List<ChatResponseUpdate>();
        StringBuilder texto = new StringBuilder();

        try
        {
            await foreach (ChatResponseUpdate update in _client.GetStreamingResponseAsync(request, options, cancellationToken))
            {
                updates.Add(update);

                string pedaco = update.Text;
                if (pedaco.Length > 0)
                {
                    texto.Append(pedaco);
                    progressoResposta?.Report(pedaco);
                }
            }
        }
        catch when (updates.Count > 0)
        {
            // Interrompida (ou o provedor caiu) no meio do turno: o que já foi feito —
            // texto parcial e ferramentas executadas — fica na conversa. É o que permite
            // retomar do ponto exato em vez de repetir trabalho como se nada tivesse havido.
            await PreservarTurnoParcialAsync(updates);
            throw;
        }

        _current.Messages.AddMessages(updates);
        _current.UpdatedAt = DateTimeOffset.Now;

        await _store.SaveAsync(_current, cancellationToken);
        AgendarCompactacaoSeNecessario();

        return texto.ToString();
    }

    private async Task PreservarTurnoParcialAsync(List<ChatResponseUpdate> updates)
    {
        int antes = _current.Messages.Count;
        _current.Messages.AddMessages(updates);
        CompletarChamadasSemResultado(antes);
        _current.Messages.Add(new ChatMessage(
            ChatRole.User,
            "[nota do sistema] O turno acima foi interrompido no meio. O que está registrado até aqui " +
            "FOI executado de verdade — ao retomar o assunto, continue do ponto em que parou, sem refazer."));
        _current.UpdatedAt = DateTimeOffset.Now;

        try
        {
            await _store.SaveAsync(_current, CancellationToken.None);
        }
        catch
        {
            // A persistência não pode mascarar a causa original da interrupção.
        }
    }

    /// <summary>
    /// Fecha as chamadas de ferramenta que ficaram sem resultado (o turno foi cancelado
    /// entre a chamada e a resposta): provedores rejeitam histórico com tool call órfã.
    /// </summary>
    private void CompletarChamadasSemResultado(int desde)
    {
        List<string> pendentes = new List<string>();

        for (int i = desde; i < _current.Messages.Count; i++)
        {
            foreach (AIContent conteudo in _current.Messages[i].Contents)
            {
                if (conteudo is FunctionCallContent chamada)
                {
                    pendentes.Add(chamada.CallId);
                }
                else if (conteudo is FunctionResultContent resultado)
                {
                    pendentes.Remove(resultado.CallId);
                }
            }
        }

        foreach (string callId in pendentes)
        {
            _current.Messages.Add(new ChatMessage(
                ChatRole.Tool,
                [new FunctionResultContent(callId, "(interrompida antes de terminar — resultado desconhecido)")]));
        }
    }

    public void RegistrarNota(string nota)
    {
        if (string.IsNullOrWhiteSpace(nota))
        {
            return;
        }

        _current.Messages.Add(new ChatMessage(ChatRole.User, $"[nota do sistema] {nota}"));
        _current.UpdatedAt = DateTimeOffset.Now;
    }

    public async Task<string> SummarizeConversationAsync(CancellationToken cancellationToken = default)
    {
        if (_current.Messages.Count == 0)
        {
            return string.Empty;
        }

        List<ChatMessage> request = new List<ChatMessage>(_current.Messages.Count + 1)
        {
            new(ChatRole.System,
                "Resuma a conversa a seguir em português do Brasil, de forma concisa e objetiva. " +
                "Capture fatos duradouros, preferências, decisões e procedimentos relevantes; ignore trivialidades. " +
                "Escreva apenas o resumo, sem preâmbulo."),
        };
        request.AddRange(_current.Messages);

        // Resumo puro: sem tools, para o modelo não tentar agir durante a síntese.
        ChatResponse response = await _client.GetResponseAsync(request, new ChatOptions(), cancellationToken);
        return response.Text.Trim();
    }

    private async Task<string> GetSystemPromptAsync(string userText, CancellationToken cancellationToken)
    {
        // Preferências (arquivo fixo) são lidas uma vez. A memória NÃO é despejada
        // inteira: só um índice leve fica sempre visível e o conteúdo completo entra
        // via itens fixados + top-K mais relevantes ao comando atual (recuperação seletiva).
        if (!_preferencesLoaded)
        {
            _preferences = await _profile.GetPreferencesAsync(cancellationToken);
            _preferencesLoaded = true;
        }

        // As fontes são independentes; buscá-las juntas tira do caminho crítico o que não
        // for o mais lento — em geral a busca semântica, que envolve uma chamada de rede.
        Task<IReadOnlyList<MemoryIndexEntry>> indexTask = _memory.GetIndexAsync(cancellationToken);
        Task<IReadOnlyList<MemoryItem>> pinnedTask = _memory.GetPinnedAsync(cancellationToken);
        Task<IReadOnlyList<MemoryItem>> relevantTask = _memory.SearchAsync(userText, TopKMemories, cancellationToken);
        Task<IReadOnlyList<HabilidadeResumo>> habilidadesTask = _habilidades.ListarAsync(cancellationToken);
        Task<PerfilPersonalidade?> perfilTask = _personalidade is null
            ? Task.FromResult<PerfilPersonalidade?>(null)
            : ObterPerfilAsync(cancellationToken);

        await Task.WhenAll(indexTask, pinnedTask, relevantTask, habilidadesTask, perfilTask);

        IReadOnlyList<MemoryIndexEntry> index = indexTask.Result;
        IReadOnlyList<MemoryItem> pinned = pinnedTask.Result;
        IReadOnlyList<MemoryItem> relevant = relevantTask.Result;

        // Fixadas primeiro, depois relevantes, sem duplicar.
        HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        List<MemoryItem> detailed = new List<MemoryItem>(pinned.Count + relevant.Count);
        foreach (MemoryItem? item in pinned.Concat(relevant))
        {
            if (seen.Add(item.Id))
            {
                detailed.Add(item);
            }
        }

        IReadOnlyList<HabilidadeResumo> habilidades = habilidadesTask.Result;
        PerfilPersonalidade? perfil = perfilTask.Result;

        _logger.LogDebug(
            "Memória: {Index} no índice, {Detailed} detalhadas ({Pinned} fixadas + {Relevant} relevantes); {Habilidades} habilidades.",
            index.Count, detailed.Count, pinned.Count, relevant.Count, habilidades.Count);

        return SystemPromptBuilder.Build(
            _tools.Tools, _preferences, index, detailed, habilidades, _politica?.Opcoes.DiretoriosConfiaveis, perfil);
    }

    private async Task<PerfilPersonalidade?> ObterPerfilAsync(CancellationToken cancellationToken)
        => await _personalidade!.ObterAsync(cancellationToken);

    /// <summary>
    /// Compactação em duas etapas para nunca custar latência a um turno: o resumo do
    /// trecho antigo é gerado em segundo plano sobre uma cópia e, quando fica pronto,
    /// o turno seguinte o aplica numa troca instantânea (<see cref="AplicarResumoProntoSeHouver"/>).
    /// </summary>
    private void AgendarCompactacaoSeNecessario()
    {
        if (_resumoEmPreparo is not null || EstimarTamanho(_current.Messages) <= LimiteCaracteresHistorico)
        {
            return;
        }

        int corte = EncontrarCorteDeTurno();
        if (corte <= 1)
        {
            return;
        }

        List<ChatMessage> trecho = new List<ChatMessage>(_current.Messages.Take(corte));
        _conversaDoResumo = _current;
        _mensagensNoResumo = corte;
        _resumoEmPreparo = Task.Run(() => ResumirTrechoAsync(trecho));
    }

    private void AplicarResumoProntoSeHouver()
    {
        if (_resumoEmPreparo is null || !_resumoEmPreparo.IsCompleted)
        {
            return;
        }

        Task<string> pronto = _resumoEmPreparo;
        _resumoEmPreparo = null;

        if (!pronto.IsCompletedSuccessfully
            || string.IsNullOrWhiteSpace(pronto.Result)
            || !ReferenceEquals(_conversaDoResumo, _current)
            || _current.Messages.Count < _mensagensNoResumo)
        {
            return;
        }

        List<ChatMessage> compactadas = new List<ChatMessage>(_current.Messages.Count - _mensagensNoResumo + 1)
        {
            new(ChatRole.User,
                "[resumo da conversa até aqui — os detalhes completos foram compactados] " + pronto.Result.Trim()),
        };
        compactadas.AddRange(_current.Messages.Skip(_mensagensNoResumo));

        _logger.LogDebug(
            "Histórico compactado: {Antes} mensagens viraram {Depois}.", _current.Messages.Count, compactadas.Count);

        _current.Messages = compactadas;
    }

    /// <summary>
    /// Índice do início da janela viva, recuado até uma fronteira de turno (mensagem do
    /// usuário) para o resumo nunca separar uma chamada de ferramenta do seu resultado.
    /// </summary>
    private int EncontrarCorteDeTurno()
    {
        int corte = _current.Messages.Count - MensagensPreservadas;

        while (corte > 1 && _current.Messages[corte].Role != ChatRole.User)
        {
            corte--;
        }

        return corte;
    }

    private async Task<string> ResumirTrechoAsync(List<ChatMessage> trecho)
    {
        List<ChatMessage> request = new List<ChatMessage>(trecho.Count + 1)
        {
            new(ChatRole.System,
                "Resuma a conversa a seguir em português do Brasil, de forma densa e fiel, preservando: " +
                "fatos, decisões, caminhos de arquivos, nomes, comandos executados e seus resultados, " +
                "pendências e o assunto em andamento. O resumo substituirá essas mensagens na memória de " +
                "trabalho da assistente, então nada essencial pode se perder. Escreva apenas o resumo."),
        };
        request.AddRange(trecho);

        ChatResponse response = await _client.GetResponseAsync(request, new ChatOptions(), CancellationToken.None);
        return response.Text.Trim();
    }

    private static int EstimarTamanho(List<ChatMessage> mensagens)
    {
        int total = 0;
        foreach (ChatMessage mensagem in mensagens)
        {
            foreach (AIContent conteudo in mensagem.Contents)
            {
                total += conteudo switch
                {
                    TextContent texto => texto.Text.Length,
                    FunctionResultContent resultado => resultado.Result?.ToString()?.Length ?? 0,
                    FunctionCallContent chamada => chamada.Name.Length + 64,
                    _ => 32,
                };
            }
        }

        return total;
    }
}
