using System.Text.Json;
using Alina.Core.IO;
using Alina.Core.Permissoes;
using Microsoft.Extensions.AI;

namespace Alina.Core.Habilidades;

/// <summary>
/// Implementação do gerador conversacional de habilidade. Conversa com o LLM sem
/// ferramentas (para ele não sair executando nada durante o planejamento) e pede
/// a resposta em JSON, separando a fala da Alina do rascunho proposto. Para que a
/// Alina "enxergue" os projetos sem executar nada, a árvore dos diretórios
/// confiáveis é injetada no contexto a cada turno. Quando há uma habilidade em
/// contexto, a mesma conversa vira edição ou treino do documento existente.
/// </summary>
public sealed class GeradorHabilidade : IGeradorHabilidade
{
    private const string FormatoResposta =
        "Responda SEMPRE apenas com um objeto JSON (sem texto fora dele e sem cercas de código) com os campos:\n" +
        "- \"mensagem\": o que dizer ao usuário no chat (pergunta ou, ao propor, um resumo curto do que mudou e o convite para revisar). Nunca coloque o Markdown completo aqui.\n" +
        "- \"pronto\": true quando estiver propondo a habilidade; false enquanto ainda coleta informação.\n" +
        "- \"titulo\": título curto da habilidade (só quando pronto=true).\n" +
        "- \"descricao\": uma linha que descreve a habilidade, para um índice (só quando pronto=true).\n" +
        "- \"conteudo\": o documento completo em Markdown, bem estruturado (títulos, passos, blocos de código " +
        "quando fizer sentido), escrito como instruções para o seu \"eu futuro\" executar a tarefa (só quando pronto=true).";

    private const string InstrucaoCriacao =
        "Você é a Alina ajudando o usuário a criar uma nova \"habilidade\": um documento em " +
        "Markdown que você mesma vai consultar depois, quando a tarefa for relevante. " +
        "Pense como no \"modo de planejamento\": converse em português do Brasil, de forma direta e sem " +
        "bajulação, e faça perguntas objetivas só quando faltar algo essencial (o que a habilidade faz, " +
        "quando aplicá-la, comandos, caminhos, convenções). Uma pergunta de cada vez.\n\n" +
        "Assim que tiver o suficiente, PROPONHA a habilidade em vez de seguir perguntando.\n\n" +
        FormatoResposta;

    private const string InstrucaoEdicao =
        "Você é a Alina ajudando o usuário a editar uma \"habilidade\" que já existe: um documento em " +
        "Markdown que você mesma consulta quando a tarefa for relevante. O documento atual vem logo a seguir.\n\n" +
        "Converse em português do Brasil, de forma direta e sem bajulação, e pergunte só o que for " +
        "essencial para aplicar a mudança. Uma pergunta de cada vez. Assim que entender o pedido, " +
        "PROPONHA a versão revisada em vez de seguir perguntando.\n\n" +
        "Devolva sempre o documento inteiro em \"conteudo\", preservando literalmente tudo o que o usuário " +
        "não pediu para mudar. Mantenha o título e a descrição atuais, a não ser que o pedido implique trocá-los.\n\n" +
        FormatoResposta;

    private const string InstrucaoTreino =
        "Você é a Alina treinando uma \"habilidade\" que já existe: o usuário executa a habilidade de " +
        "verdade, traz o que aconteceu e diz o que ficou errado ou faltou. O documento atual vem logo a seguir.\n\n" +
        "No histórico, as mensagens que começam com \"[teste]\" são o pedido que foi executado e as que " +
        "começam com \"[resultado]\" são a resposta que você deu naquela execução. Trate-as como evidência: " +
        "encontre o trecho do documento que levou ao erro (passo faltando, comando errado, caminho inválido, " +
        "gatilho mal descrito) e corrija exatamente esse ponto.\n\n" +
        "Converse em português do Brasil, direta e sem bajulação. Quando a orientação do usuário for clara, " +
        "PROPONHA a versão corrigida em vez de perguntar. Devolva sempre o documento inteiro em \"conteudo\", " +
        "preservando o que já funciona.\n\n" +
        FormatoResposta;

    private static readonly JsonSerializerOptions OpcoesJson = new(JsonSerializerDefaults.Web);

    private readonly IChatClient _client;
    private readonly IPoliticaPermissao? _politica;

    public GeradorHabilidade(IChatClient client, IPoliticaPermissao? politica = null)
    {
        _client = client;
        _politica = politica;
    }

    public async Task<RespostaGeracao> ContinuarAsync(
        IReadOnlyList<ChatMessage> historico,
        ContextoHabilidade? contexto = null,
        CancellationToken cancellationToken = default)
    {
        List<ChatMessage> request = new List<ChatMessage>(historico.Count + 3)
        {
            new(ChatRole.System, InstrucaoDe(contexto)),
        };

        if (contexto is not null)
        {
            request.Add(new(ChatRole.System, MontarContextoHabilidade(contexto.Atual)));
        }

        string? contextoDiretorios = MontarContextoDiretorios();
        if (contextoDiretorios is not null)
        {
            request.Add(new(ChatRole.System, contextoDiretorios));
        }

        request.AddRange(historico);

        ChatResponse response = await _client.GetResponseAsync(request, new ChatOptions(), cancellationToken);
        string bruto = response.Text?.Trim() ?? string.Empty;

        RascunhoDto? dto = Desserializar(bruto);
        if (dto is null)
        {
            return new RespostaGeracao(
                bruto.Length == 0 ? "Não consegui montar a habilidade agora. Pode reformular?" : bruto,
                Rascunho: null);
        }

        string mensagem = string.IsNullOrWhiteSpace(dto.Mensagem)
            ? "Pronto, montei um rascunho. Quer revisar?"
            : dto.Mensagem!.Trim();

        bool temConteudo = dto.Pronto
            && !string.IsNullOrWhiteSpace(dto.Titulo)
            && !string.IsNullOrWhiteSpace(dto.Conteudo);

        RascunhoHabilidade? rascunho = temConteudo
            ? new RascunhoHabilidade(dto.Titulo!.Trim(), dto.Descricao?.Trim() ?? string.Empty, dto.Conteudo!.Trim())
            : null;

        return new RespostaGeracao(mensagem, rascunho);
    }

    private static string InstrucaoDe(ContextoHabilidade? contexto) => contexto?.Modo switch
    {
        ModoConversaHabilidade.Edicao => InstrucaoEdicao,
        ModoConversaHabilidade.Treino => InstrucaoTreino,
        _ => InstrucaoCriacao,
    };

    private static string MontarContextoHabilidade(Habilidade atual) =>
        "Habilidade atual, exatamente como está salva. É esta que você está revisando:\n\n" +
        $"titulo: {atual.Nome}\ndescricao: {atual.Descricao}\n\n---\n{atual.Conteudo}";

    private string? MontarContextoDiretorios()
    {
        IReadOnlyList<string>? raizes = _politica?.Opcoes.DiretoriosConfiaveis;
        if (raizes is null || raizes.Count == 0)
        {
            return null;
        }

        string arvore = ArvoreDiretorios.Montar(raizes, profundidadeMaxima: 3);
        if (arvore.Length == 0)
        {
            return null;
        }

        return "Diretórios confiáveis do usuário (permissão total) e os projetos dentro deles, " +
            "já lidos do disco. Trate esta listagem como a fonte real dos caminhos: use estes " +
            "caminhos absolutos ao montar a habilidade e NÃO peça ao usuário para listar o que " +
            "já aparece aqui.\n\n" + arvore;
    }

    private static RascunhoDto? Desserializar(string texto)
    {
        string json = ExtrairJson(texto);
        if (json.Length == 0)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<RascunhoDto>(json, OpcoesJson);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string ExtrairJson(string texto)
    {
        string t = texto.Trim();

        if (t.StartsWith("```", StringComparison.Ordinal))
        {
            int quebra = t.IndexOf('\n');
            if (quebra >= 0)
            {
                t = t[(quebra + 1)..];
            }

            int cerca = t.LastIndexOf("```", StringComparison.Ordinal);
            if (cerca >= 0)
            {
                t = t[..cerca];
            }
        }

        int abre = t.IndexOf('{');
        int fecha = t.LastIndexOf('}');
        return abre >= 0 && fecha > abre ? t[abre..(fecha + 1)] : string.Empty;
    }

    private sealed class RascunhoDto
    {
        public string? Mensagem { get; set; }
        public bool Pronto { get; set; }
        public string? Titulo { get; set; }
        public string? Descricao { get; set; }
        public string? Conteudo { get; set; }
    }
}
