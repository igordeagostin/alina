using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Alina.Core.Habilidades;

/// <summary>
/// Implementação do gerador conversacional de habilidade. Conversa com o LLM sem
/// ferramentas (para ele não sair executando nada durante o planejamento) e pede
/// a resposta em JSON, separando a fala da Alina do rascunho proposto.
/// </summary>
public sealed class GeradorHabilidade : IGeradorHabilidade
{
    private const string InstrucaoSistema =
        "Você é a Alina ajudando o usuário a criar uma nova \"habilidade\": um documento em " +
        "Markdown que você mesma vai consultar depois, quando a tarefa for relevante. " +
        "Pense como no \"modo de planejamento\": converse em português do Brasil, de forma direta e sem " +
        "bajulação, e faça perguntas objetivas só quando faltar algo essencial (o que a habilidade faz, " +
        "quando aplicá-la, comandos, caminhos, convenções). Uma pergunta de cada vez.\n\n" +
        "Assim que tiver o suficiente, PROPONHA a habilidade em vez de seguir perguntando.\n\n" +
        "Responda SEMPRE apenas com um objeto JSON (sem texto fora dele e sem cercas de código) com os campos:\n" +
        "- \"mensagem\": o que dizer ao usuário no chat (pergunta ou, ao propor, um resumo curto e o convite para revisar). Nunca coloque o Markdown completo aqui.\n" +
        "- \"pronto\": true quando estiver propondo a habilidade; false enquanto ainda coleta informação.\n" +
        "- \"titulo\": título curto da habilidade (só quando pronto=true).\n" +
        "- \"descricao\": uma linha que descreve a habilidade, para um índice (só quando pronto=true).\n" +
        "- \"conteudo\": o documento completo em Markdown, bem estruturado (títulos, passos, blocos de código " +
        "quando fizer sentido), escrito como instruções para o seu \"eu futuro\" executar a tarefa (só quando pronto=true).";

    private static readonly JsonSerializerOptions OpcoesJson = new(JsonSerializerDefaults.Web);

    private readonly IChatClient _client;

    public GeradorHabilidade(IChatClient client) => _client = client;

    public async Task<RespostaGeracao> ContinuarAsync(
        IReadOnlyList<ChatMessage> historico, CancellationToken cancellationToken = default)
    {
        List<ChatMessage> request = new List<ChatMessage>(historico.Count + 1)
        {
            new(ChatRole.System, InstrucaoSistema),
        };
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
