namespace Alina.Core.Ferramentas;

/// <summary>
/// Texto que ensina o modelo a montar uma <see cref="DefinicaoFerramenta"/> válida.
/// Fica em um lugar só porque tanto o gerador de ferramenta quanto o de habilidade
/// (que pode propor ferramentas junto com o documento) dependem das mesmas regras —
/// e duas cópias divergem na primeira mudança.
/// </summary>
public static class RegrasFerramenta
{
    public const string Regras =
        "Regras da definição de uma ferramenta:\n" +
        "- Os argumentos usam placeholders {parametro} que serão substituídos pelos valores informados na hora.\n" +
        "- Cada placeholder usado deve ter um parâmetro correspondente na lista.\n" +
        "- Placeholder dentro de uma URL deve usar o sufixo :url — {termo:url} — para o valor ser codificado. " +
        "Sem ele, um termo com espaço, acento, '&' ou '#' chega truncado ao site.\n" +
        "- Cada argumento é passado literalmente ao processo (não há shell reinterpretando): para lógica de shell, " +
        "invoque explicitamente (ex.: comando 'powershell' com argumentos ['-NoProfile','-Command','...']).\n" +
        "- Nunca interpole um {placeholder} dentro de um script passado a '-Command': o valor vem do modelo e " +
        "viraria injeção de comando. Passe o valor como argumento separado (ex.: '-File','script.ps1','{valor}').\n" +
        "- O processo é aguardado até terminar e MORTO ao estourar o timeout, junto com toda a sua árvore. " +
        "Para abrir um programa que fica aberto (navegador, editor, player), use um lançador que retorna na hora — " +
        "comando 'cmd' com argumentos ['/c','start','','{url}'] — em vez de chamar o executável direto, " +
        "senão o timeout fecha o programa na cara do usuário.\n" +
        "- 'exigeConfirmacao' deve ser true para qualquer coisa destrutiva ou com efeito colateral relevante; " +
        "só use false para ações seguras e idempotentes (o que dispensa o SIM/NÃO).\n" +
        "- Nunca invente executável, CLI, endpoint ou chave: se a ferramenta depender de algo que você não sabe " +
        "que existe na máquina do usuário (um CLI instalado, um token, uma URL), pergunte antes de propor.";

    public const string Campos =
        "- \"nome\": identificador snake_case da ferramenta (ex.: \"deploy_diario\").\n" +
        "- \"descricao\": uma linha dizendo o que a ferramenta faz (ajuda você a decidir quando usá-la).\n" +
        "- \"comando\": o executável a chamar (ex.: \"powershell\", \"git\", \"node\", \"cmd\").\n" +
        "- \"argumentos\": array de strings com os argumentos (podendo conter {placeholders}).\n" +
        "- \"exigeConfirmacao\": booleano.\n" +
        "- \"parametros\": array de objetos { \"nome\", \"descricao\", \"obrigatorio\", \"tipo\" } — um por placeholder. " +
        "O \"tipo\" é \"Diretorio\" ou \"Arquivo\" quando o valor é um caminho que precisa existir no disco (a ferramenta " +
        "recusa a chamada se não existir, em vez de executar às cegas), \"Url\" quando o valor é um endereço " +
        "http/https inteiro (recusa outros esquemas e metacaracteres de shell), e \"Texto\" para o resto.";
}
