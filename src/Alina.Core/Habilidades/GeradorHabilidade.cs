using System.Text.Json;
using Alina.Core.Ferramentas;
using Alina.Core.Geracao;
using Alina.Core.IO;
using Alina.Core.Permissoes;
using Alina.Core.Tools;
using Microsoft.Extensions.AI;

namespace Alina.Core.Habilidades;

/// <summary>
/// Implementação do gerador conversacional de habilidade. Conversa com o LLM sem
/// ferramentas (para ele não sair executando nada durante o planejamento) e pede
/// a resposta em JSON, separando a fala da Alina do rascunho proposto. Para que a
/// Alina "enxergue" os projetos sem executar nada, a árvore dos diretórios
/// confiáveis é injetada no contexto a cada turno. Quando há uma habilidade em
/// contexto, a mesma conversa vira edição ou treino do documento existente.
/// Junto do documento, ela pode propor as ferramentas que faltam para executá-lo —
/// assim uma habilidade nasce completa, sem o usuário ter de criar a ferramenta antes.
/// </summary>
public sealed class GeradorHabilidade : IGeradorHabilidade
{
    private const string PoliticaFerramentas =
        "Você também decide se esta habilidade precisa de FERRAMENTAS novas. Uma ferramenta é um comando " +
        "externo que você passa a poder chamar sozinha (function-calling); uma habilidade é só o documento " +
        "que orienta a execução. Antes de propor o documento, avalie:\n" +
        "- As ferramentas que você já tem (listadas adiante) cobrem os passos? Então NÃO proponha nada.\n" +
        "- Um passo é uma ação concreta, repetível e sempre igual (um comando, um script, uma chamada de CLI), " +
        "e nenhuma ferramenta atual a cobre? Proponha uma ferramenta para ele.\n" +
        "- O passo é exploratório, varia a cada execução ou é decisão sua na hora? Deixe no documento como " +
        "instrução e use 'terminal' — não vire ferramenta.\n" +
        "Proponha o mínimo: uma ferramenta por ação, e só as que a habilidade realmente usa. No documento, " +
        "mande chamar cada ferramenta proposta pelo nome, como faria com uma que já existe. Diga na " +
        "\"mensagem\" que ferramentas você está propondo e por quê — o usuário revisa e pode recusar cada uma.";

    private const string FormatoResposta =
        "Responda SEMPRE apenas com um objeto JSON (sem texto fora dele e sem cercas de código) com os campos:\n" +
        "- \"mensagem\": o que dizer ao usuário no chat (pergunta ou, ao propor, um resumo curto do que mudou e o convite para revisar). Nunca coloque o Markdown completo aqui.\n" +
        "- \"pronto\": true quando estiver propondo a habilidade; false enquanto ainda coleta informação.\n" +
        "- \"titulo\": título curto da habilidade (só quando pronto=true).\n" +
        "- \"descricao\": uma linha que descreve a habilidade, para um índice (só quando pronto=true).\n" +
        "- \"conteudo\": o documento completo em Markdown, bem estruturado (títulos, passos, blocos de código " +
        "quando fizer sentido), escrito como instruções para o seu \"eu futuro\" executar a tarefa (só quando pronto=true).\n" +
        "- \"ferramentas\": array das ferramentas novas que a habilidade exige (só quando pronto=true). Omita ou " +
        "deixe vazio quando as atuais bastarem. Cada item tem \"motivo\" (uma linha dizendo por que ela é " +
        "necessária) mais os campos:\n" +
        RegrasFerramenta.Campos + "\n\n" +
        RegrasFerramenta.Regras;

    private const string InstrucaoCriacao =
        "Você é a Alina ajudando o usuário a criar uma nova \"habilidade\": um documento em " +
        "Markdown que você mesma vai consultar depois, quando a tarefa for relevante. " +
        "Pense como no \"modo de planejamento\": converse em português do Brasil, de forma direta e sem " +
        "bajulação, e faça perguntas objetivas só quando faltar algo essencial (o que a habilidade faz, " +
        "quando aplicá-la, comandos, caminhos, convenções). Uma pergunta de cada vez.\n\n" +
        "Assim que tiver o suficiente, PROPONHA a habilidade em vez de seguir perguntando.\n\n" +
        PoliticaFerramentas + "\n\n" +
        FormatoResposta;

    private const string InstrucaoEdicao =
        "Você é a Alina ajudando o usuário a editar uma \"habilidade\" que já existe: um documento em " +
        "Markdown que você mesma consulta quando a tarefa for relevante. O documento atual vem logo a seguir.\n\n" +
        "Converse em português do Brasil, de forma direta e sem bajulação, e pergunte só o que for " +
        "essencial para aplicar a mudança. Uma pergunta de cada vez. Assim que entender o pedido, " +
        "PROPONHA a versão revisada em vez de seguir perguntando.\n\n" +
        "Devolva sempre o documento inteiro em \"conteudo\", preservando literalmente tudo o que o usuário " +
        "não pediu para mudar. Mantenha o título e a descrição atuais, a não ser que o pedido implique trocá-los.\n\n" +
        PoliticaFerramentas + "\n\n" +
        "Na edição, proponha ferramenta apenas para o que a mudança pedida trouxe de novo; não crie ferramenta " +
        "para passos que já funcionavam.\n\n" +
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
        PoliticaFerramentas + "\n\n" +
        "No treino, o teste é a evidência mais forte: se a execução falhou porque faltava uma ação chamável " +
        "(você acabou improvisando no terminal ou não conseguiu executar o passo), proponha a ferramenta que " +
        "estava faltando junto com a correção do documento.\n\n" +
        FormatoResposta;

    private static readonly JsonSerializerOptions OpcoesJson = new(JsonSerializerDefaults.Web);

    private readonly IChatClient _client;
    private readonly IPoliticaPermissao? _politica;
    private readonly ToolRegistry? _ferramentas;

    public GeradorHabilidade(
        IChatClient client,
        IPoliticaPermissao? politica = null,
        ToolRegistry? ferramentas = null)
    {
        _client = client;
        _politica = politica;
        _ferramentas = ferramentas;
    }

    public async Task<RespostaGeracao> ContinuarAsync(
        IReadOnlyList<ChatMessage> historico,
        ContextoHabilidade? contexto = null,
        IProgress<ProgressoGeracao>? progresso = null,
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

        string? contextoFerramentas = MontarContextoFerramentas();
        if (contextoFerramentas is not null)
        {
            request.Add(new(ChatRole.System, contextoFerramentas));
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
            ? new RascunhoHabilidade(
                dto.Titulo!.Trim(),
                dto.Descricao?.Trim() ?? string.Empty,
                dto.Conteudo!.Trim(),
                MontarPropostas(dto.Ferramentas))
            : null;

        return new RespostaGeracao(mensagem, rascunho);
    }

    private IReadOnlyList<FerramentaProposta> MontarPropostas(List<FerramentaJson>? ferramentas)
    {
        if (ferramentas is null || ferramentas.Count == 0)
        {
            return Array.Empty<FerramentaProposta>();
        }

        HashSet<string> existentes = new HashSet<string>(
            _ferramentas?.Tools.Select(t => t.Name) ?? Enumerable.Empty<string>(),
            StringComparer.OrdinalIgnoreCase);

        List<FerramentaProposta> propostas = new List<FerramentaProposta>();
        foreach (FerramentaJson item in ferramentas)
        {
            DefinicaoFerramenta? definicao = item.ParaDefinicao();
            if (definicao is null || propostas.Any(p => p.Definicao.Nome.Equals(definicao.Nome, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            propostas.Add(new FerramentaProposta(
                definicao,
                item.Motivo?.Trim() ?? string.Empty,
                existentes.Contains(definicao.Nome)));
        }

        return propostas;
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

    private string? MontarContextoFerramentas()
    {
        string? catalogo = _ferramentas is null ? null : CatalogoFerramentas.Descrever(_ferramentas.Tools);
        if (string.IsNullOrWhiteSpace(catalogo))
        {
            return null;
        }

        return "Ferramentas que você já tem e poderá chamar sozinha (function-calling) na hora de executar " +
            "esta habilidade. Entre parênteses vêm os parâmetros; os terminados em '?' são opcionais.\n\n" +
            catalogo + "\n\n" +
            "Ao escrever o documento, mande CHAMAR a ferramenta pelo nome, dizendo o que vai em cada " +
            "parâmetro, em vez de descrever o comando cru equivalente — a ferramenta já embute o comando, " +
            "o diretório e a confirmação, e o comando cru duplicado sai do ar assim que a ferramenta muda. " +
            "Use 'terminal' apenas para o que nenhuma ferramenta desta lista cobre. O documento só pode citar " +
            "ferramentas desta lista ou as que você propuser agora em \"ferramentas\" — nada além disso existe. " +
            "O nome de cada proposta precisa ser diferente dos daqui: repetir um nome substitui a ferramenta atual, " +
            "então só faça isso se a intenção for mesmo trocá-la.";
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
        public List<FerramentaJson>? Ferramentas { get; set; }
    }
}
