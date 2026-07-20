using Alina.Core.Memory;
using Alina.Core.Models;
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

    private readonly IChatClient _client;
    private readonly ToolRegistry _tools;
    private readonly IConversationStore _store;
    private readonly IProfileStore _profile;
    private readonly IMemoryRetriever _memory;
    private readonly ILogger<ChatOrchestrator> _logger;

    private Conversation _current = new();
    private string? _preferences;
    private bool _preferencesLoaded;

    public ChatOrchestrator(
        IChatClient client,
        ToolRegistry tools,
        IConversationStore store,
        IProfileStore profile,
        IMemoryRetriever memory,
        ILogger<ChatOrchestrator>? logger = null)
    {
        _client = client;
        _tools = tools;
        _store = store;
        _profile = profile;
        _memory = memory;
        _logger = logger ?? NullLogger<ChatOrchestrator>.Instance;
    }

    public Conversation Current => _current;

    public void StartNew() => _current = new Conversation();

    public async Task<bool> ResumeAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        var loaded = await _store.LoadAsync(conversationId, cancellationToken);
        if (loaded is null)
        {
            return false;
        }

        _current = loaded;
        return true;
    }

    public async Task<string> SendAsync(string userText, CancellationToken cancellationToken = default)
    {
        _current.Messages.Add(new ChatMessage(ChatRole.User, userText));

        if (_current.Title == "Nova conversa" && !string.IsNullOrWhiteSpace(userText))
        {
            _current.Title = userText.Length > 60 ? userText[..60] + "…" : userText;
        }

        var systemPrompt = await GetSystemPromptAsync(userText, cancellationToken);

        var request = new List<ChatMessage>(_current.Messages.Count + 1)
        {
            new(ChatRole.System, systemPrompt),
        };
        request.AddRange(_current.Messages);

        var options = new ChatOptions { Tools = _tools.AsAIFunctions() };

        _logger.LogDebug("Enviando {Count} mensagens ao LLM ({Tools} tools).", request.Count, options.Tools.Count);

        var response = await _client.GetResponseAsync(request, options, cancellationToken);

        // Inclui mensagens intermediárias (chamadas/resultados de tools) e a resposta final.
        _current.Messages.AddRange(response.Messages);
        _current.UpdatedAt = DateTimeOffset.Now;

        await _store.SaveAsync(_current, cancellationToken);

        return response.Text;
    }

    public async Task<string> SummarizeConversationAsync(CancellationToken cancellationToken = default)
    {
        if (_current.Messages.Count == 0)
        {
            return string.Empty;
        }

        var request = new List<ChatMessage>(_current.Messages.Count + 1)
        {
            new(ChatRole.System,
                "Resuma a conversa a seguir em português do Brasil, de forma concisa e objetiva. " +
                "Capture fatos duradouros, preferências, decisões e procedimentos relevantes; ignore trivialidades. " +
                "Escreva apenas o resumo, sem preâmbulo."),
        };
        request.AddRange(_current.Messages);

        // Resumo puro: sem tools, para o modelo não tentar agir durante a síntese.
        var response = await _client.GetResponseAsync(request, new ChatOptions(), cancellationToken);
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

        var index = await _memory.GetIndexAsync(cancellationToken);
        var pinned = await _memory.GetPinnedAsync(cancellationToken);
        var relevant = await _memory.SearchAsync(userText, TopKMemories, cancellationToken);

        // Fixadas primeiro, depois relevantes, sem duplicar.
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var detailed = new List<MemoryItem>(pinned.Count + relevant.Count);
        foreach (var item in pinned.Concat(relevant))
        {
            if (seen.Add(item.Id))
            {
                detailed.Add(item);
            }
        }

        _logger.LogDebug(
            "Memória: {Index} no índice, {Detailed} detalhadas ({Pinned} fixadas + {Relevant} relevantes).",
            index.Count, detailed.Count, pinned.Count, relevant.Count);

        return SystemPromptBuilder.Build(_tools.Tools, _preferences, index, detailed);
    }
}
