using System.Text.Json;
using System.Text.Json.Serialization;
using Alina.Core.IO;
using Alina.Core.Permissoes;
using Microsoft.Extensions.AI;

namespace Alina.Core.Ferramentas;

/// <summary>
/// Gerador conversacional de ferramenta. Conversa com o LLM sem ferramentas (para
/// ele planejar sem sair executando) e pede a resposta em JSON, separando a fala da
/// Alina do rascunho proposto. A árvore dos diretórios confiáveis é injetada para a
/// Alina montar caminhos reais sem precisar perguntar.
/// </summary>
public sealed class GeradorFerramenta : IGeradorFerramenta
{
    private const string InstrucaoSistema =
        "Você é a Alina ajudando o usuário a criar uma nova \"ferramenta\": um comando externo que você " +
        "passará a expor a si mesma como uma ação chamável (function-calling), sem novo build do app. " +
        "Diferente de uma \"habilidade\" (que é só um documento que você lê), uma ferramenta EXECUTA um " +
        "processo do sistema (powershell, git, node, um script .ps1/.py, etc.).\n\n" +
        "Pense como no \"modo de planejamento\": converse em português do Brasil, direta e sem bajulação, e " +
        "faça perguntas objetivas só quando faltar algo essencial (o que a ferramenta faz, qual comando/executável, " +
        "quais parâmetros o modelo preenche, se é perigosa a ponto de exigir confirmação). Uma pergunta por vez.\n\n" +
        "Assim que tiver o suficiente, PROPONHA a ferramenta em vez de seguir perguntando.\n\n" +
        "Regras da definição:\n" +
        "- Os argumentos usam placeholders {parametro} que serão substituídos pelos valores informados na hora.\n" +
        "- Cada placeholder usado deve ter um parâmetro correspondente na lista.\n" +
        "- Cada argumento é passado literalmente ao processo (não há shell reinterpretando): para lógica de shell, " +
        "invoque explicitamente (ex.: comando 'powershell' com argumentos ['-NoProfile','-Command','...']).\n" +
        "- 'exigeConfirmacao' deve ser true para qualquer coisa destrutiva ou com efeito colateral relevante; " +
        "só use false para ações seguras e idempotentes (o que dispensa o SIM/NÃO).\n\n" +
        "Responda SEMPRE apenas com um objeto JSON (sem texto fora dele e sem cercas de código) com os campos:\n" +
        "- \"mensagem\": o que dizer ao usuário no chat (pergunta ou, ao propor, um resumo curto e o convite para revisar).\n" +
        "- \"pronto\": true quando estiver propondo a ferramenta; false enquanto ainda coleta informação.\n" +
        "Quando pronto=true, inclua também:\n" +
        "- \"nome\": identificador snake_case da ferramenta (ex.: \"deploy_diario\").\n" +
        "- \"descricao\": uma linha dizendo o que a ferramenta faz (ajuda você a decidir quando usá-la).\n" +
        "- \"comando\": o executável a chamar (ex.: \"powershell\", \"git\", \"node\", \"cmd\").\n" +
        "- \"argumentos\": array de strings com os argumentos (podendo conter {placeholders}).\n" +
        "- \"exigeConfirmacao\": booleano.\n" +
        "- \"parametros\": array de objetos { \"nome\", \"descricao\", \"obrigatorio\" } — um por placeholder.";

    private static readonly JsonSerializerOptions OpcoesJson = new(JsonSerializerDefaults.Web);

    private readonly IChatClient _client;
    private readonly IPoliticaPermissao? _politica;

    public GeradorFerramenta(IChatClient client, IPoliticaPermissao? politica = null)
    {
        _client = client;
        _politica = politica;
    }

    public async Task<RespostaGeracaoFerramenta> ContinuarAsync(
        IReadOnlyList<ChatMessage> historico, CancellationToken cancellationToken = default)
    {
        List<ChatMessage> request = new List<ChatMessage>(historico.Count + 2)
        {
            new(ChatRole.System, InstrucaoSistema),
        };

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
            return new RespostaGeracaoFerramenta(
                bruto.Length == 0 ? "Não consegui montar a ferramenta agora. Pode reformular?" : bruto,
                Rascunho: null);
        }

        string mensagem = string.IsNullOrWhiteSpace(dto.Mensagem)
            ? "Pronto, montei um rascunho. Quer revisar?"
            : dto.Mensagem!.Trim();

        bool completo = dto.Pronto
            && !string.IsNullOrWhiteSpace(dto.Nome)
            && !string.IsNullOrWhiteSpace(dto.Comando);

        RascunhoFerramenta? rascunho = completo
            ? new RascunhoFerramenta(MontarDefinicao(dto), mensagem)
            : null;

        return new RespostaGeracaoFerramenta(mensagem, rascunho);
    }

    private static DefinicaoFerramenta MontarDefinicao(RascunhoDto dto) => new DefinicaoFerramenta
    {
        Nome = dto.Nome!.Trim(),
        Descricao = dto.Descricao?.Trim() ?? string.Empty,
        Comando = dto.Comando!.Trim(),
        Argumentos = dto.Argumentos ?? Array.Empty<string>(),
        ExigeConfirmacao = dto.ExigeConfirmacao,
        Parametros = (dto.Parametros ?? new List<ParametroDto>())
            .Where(p => !string.IsNullOrWhiteSpace(p.Nome))
            .Select(p => new ParametroFerramenta
            {
                Nome = p.Nome!.Trim(),
                Descricao = p.Descricao?.Trim() ?? string.Empty,
                Obrigatorio = p.Obrigatorio,
            })
            .ToList(),
    };

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
            "já lidos do disco. Use estes caminhos absolutos ao montar a ferramenta e NÃO peça ao " +
            "usuário para listar o que já aparece aqui.\n\n" + arvore;
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
        public string? Nome { get; set; }
        public string? Descricao { get; set; }
        public string? Comando { get; set; }
        public string[]? Argumentos { get; set; }

        [JsonPropertyName("exigeConfirmacao")]
        public bool ExigeConfirmacao { get; set; } = true;

        public List<ParametroDto>? Parametros { get; set; }
    }

    private sealed class ParametroDto
    {
        public string? Nome { get; set; }
        public string? Descricao { get; set; }
        public bool Obrigatorio { get; set; } = true;
    }
}
