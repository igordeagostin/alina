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
        "- Cada argumento é passado literalmente ao processo (não há shell reinterpretando): para lógica de shell, " +
        "invoque explicitamente (ex.: comando 'powershell' com argumentos ['-NoProfile','-Command','...']).\n" +
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
        "recusa a chamada se não existir, em vez de executar às cegas), e \"Texto\" para o resto.";
}
