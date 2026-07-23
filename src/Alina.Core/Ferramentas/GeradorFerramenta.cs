using System.Text.Json;
using Alina.Core.Geracao;
using Alina.Core.IO;
using Alina.Core.Permissoes;
using Alina.Core.Tools;
using Microsoft.Extensions.AI;

namespace Alina.Core.Ferramentas;

/// <summary>
/// Gerador conversacional de ferramenta. Conversa com o LLM sem ferramentas (para
/// ele planejar sem sair executando) e pede a resposta em JSON, separando a fala da
/// Alina do rascunho proposto. A árvore dos diretórios confiáveis é injetada para a
/// Alina montar caminhos reais sem precisar perguntar. Quando há uma ferramenta em
/// contexto, a mesma conversa vira edição da definição existente.
/// </summary>
public sealed class GeradorFerramenta : IGeradorFerramenta
{
    private const string InstrucaoCriacao =
        "Você é a Alina ajudando o usuário a criar uma nova \"ferramenta\": um comando externo que você " +
        "passará a expor a si mesma como uma ação chamável (function-calling), sem novo build do app. " +
        "Diferente de uma \"habilidade\" (que é só um documento que você lê), uma ferramenta EXECUTA um " +
        "processo do sistema (powershell, git, node, um script .ps1/.py, etc.).\n\n" +
        "Pense como no \"modo de planejamento\": converse em português do Brasil, direta e sem bajulação, e " +
        "faça perguntas objetivas só quando faltar algo essencial (o que a ferramenta faz, qual comando/executável, " +
        "quais parâmetros o modelo preenche, se é perigosa a ponto de exigir confirmação). Uma pergunta por vez.\n\n" +
        "Assim que tiver o suficiente, PROPONHA a ferramenta em vez de seguir perguntando.\n\n" +
        RegrasEFormato;

    private const string InstrucaoEdicao =
        "Você é a Alina ajudando o usuário a editar uma \"ferramenta\" que já existe: um comando externo " +
        "que você expõe a si mesma como ação chamável (function-calling). A definição atual vem logo a seguir.\n\n" +
        "Converse em português do Brasil, direta e sem bajulação, e pergunte só o que for essencial para " +
        "aplicar a mudança. Uma pergunta por vez. Assim que entender o pedido, PROPONHA a definição revisada " +
        "em vez de seguir perguntando.\n\n" +
        "Devolva sempre a definição inteira, preservando os campos que o usuário não pediu para mudar " +
        "(inclusive o nome, a não ser que o pedido implique renomear).\n\n" +
        RegrasEFormato;

    private const string RegrasEFormato =
        RegrasFerramenta.Regras + "\n\n" +
        "Responda SEMPRE apenas com um objeto JSON (sem texto fora dele e sem cercas de código) com os campos:\n" +
        "- \"mensagem\": o que dizer ao usuário no chat (pergunta ou, ao propor, um resumo curto e o convite para revisar).\n" +
        "- \"pronto\": true quando estiver propondo a ferramenta; false enquanto ainda coleta informação.\n" +
        "Quando pronto=true, inclua também:\n" +
        RegrasFerramenta.Campos;

    private static readonly JsonSerializerOptions OpcoesJson = new(JsonSerializerDefaults.Web);

    private static readonly JsonSerializerOptions OpcoesContexto = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly IChatClient _client;
    private readonly IPoliticaPermissao? _politica;
    private readonly ToolRegistry? _existentes;

    public GeradorFerramenta(
        IChatClient client,
        IPoliticaPermissao? politica = null,
        ToolRegistry? existentes = null)
    {
        _client = client;
        _politica = politica;
        _existentes = existentes;
    }

    public async Task<RespostaGeracaoFerramenta> ContinuarAsync(
        IReadOnlyList<ChatMessage> historico,
        ContextoFerramenta? contexto = null,
        IProgress<ProgressoGeracao>? progresso = null,
        CancellationToken cancellationToken = default)
    {
        List<ChatMessage> request = new List<ChatMessage>(historico.Count + 3)
        {
            new(ChatRole.System, contexto is null ? InstrucaoCriacao : InstrucaoEdicao),
        };

        if (contexto is not null)
        {
            request.Add(new(ChatRole.System, MontarContextoFerramenta(contexto.Atual)));
        }

        string? contextoExistentes = MontarContextoExistentes(contexto);
        if (contextoExistentes is not null)
        {
            request.Add(new(ChatRole.System, contextoExistentes));
        }

        string? contextoDiretorios = MontarContextoDiretorios();
        if (contextoDiretorios is not null)
        {
            request.Add(new(ChatRole.System, contextoDiretorios));
        }

        request.AddRange(historico);

        string bruto = (await LeituraStream.AcumularAsync(_client, request, progresso, cancellationToken)).Trim();

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

        DefinicaoFerramenta? definicao = dto.Pronto ? dto.ParaDefinicao() : null;
        RascunhoFerramenta? rascunho = definicao is null ? null : new RascunhoFerramenta(definicao, mensagem);

        return new RespostaGeracaoFerramenta(mensagem, rascunho);
    }

    private static string MontarContextoFerramenta(DefinicaoFerramenta atual) =>
        "Ferramenta atual, exatamente como está salva. É esta que você está revisando:\n\n" +
        JsonSerializer.Serialize(atual, OpcoesContexto);

    private string? MontarContextoExistentes(ContextoFerramenta? contexto)
    {
        if (_existentes is null)
        {
            return null;
        }

        List<ITool> outras = _existentes.Tools
            .Where(t => contexto is null || !t.Name.Equals(contexto.Atual.Nome, StringComparison.OrdinalIgnoreCase))
            .ToList();

        string? catalogo = CatalogoFerramentas.Descrever(outras);
        if (string.IsNullOrWhiteSpace(catalogo))
        {
            return null;
        }

        return "Ferramentas que você já expõe hoje. Entre parênteses vêm os parâmetros; os terminados " +
            "em '?' são opcionais.\n\n" + catalogo + "\n\n" +
            "Antes de propor uma nova, verifique se alguma destas já resolve o pedido — se resolver, diga " +
            "isso ao usuário em vez de duplicar. O nome proposto precisa ser diferente de todos os desta " +
            "lista: um nome repetido é descartado no registro e a ferramenta nunca fica disponível.";
    }

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

    private sealed class RascunhoDto : FerramentaJson
    {
        public string? Mensagem { get; set; }
        public bool Pronto { get; set; }
    }
}
