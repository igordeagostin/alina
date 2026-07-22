using System.Text.Json.Serialization;

namespace Alina.Core.Ferramentas;

/// <summary>
/// Descreve uma ferramenta declarativa: um comando externo que a Alina passa a
/// expor ao LLM como function-calling, sem precisar de código C# nem de novo build.
/// Persistida como um arquivo <c>*.tool.json</c> na pasta de dados do usuário.
/// </summary>
public sealed class DefinicaoFerramenta
{
    /// <summary>Nome da ferramenta exposta ao LLM (ex.: "deploy_diario"). Kebab/snake case.</summary>
    [JsonPropertyName("nome")]
    public string Nome { get; set; } = string.Empty;

    /// <summary>Descrição do que a ferramenta faz — ajuda o LLM a decidir quando usá-la.</summary>
    [JsonPropertyName("descricao")]
    public string Descricao { get; set; } = string.Empty;

    /// <summary>
    /// Se a execução exige confirmação (SIM/NÃO) do usuário. Padrão <c>true</c> por
    /// segurança: desligar é o que dispensa o SIM/NÃO, e deve ser uma escolha consciente.
    /// </summary>
    [JsonPropertyName("exigeConfirmacao")]
    public bool ExigeConfirmacao { get; set; } = true;

    /// <summary>Executável a ser chamado (ex.: "powershell", "git", "node", "cmd").</summary>
    [JsonPropertyName("comando")]
    public string Comando { get; set; } = string.Empty;

    /// <summary>
    /// Argumentos passados ao comando. Podem conter placeholders <c>{parametro}</c>
    /// substituídos pelos valores informados pelo LLM.
    /// </summary>
    [JsonPropertyName("argumentos")]
    public string[] Argumentos { get; set; } = Array.Empty<string>();

    /// <summary>Parâmetros que o LLM deve preencher ao chamar a ferramenta.</summary>
    [JsonPropertyName("parametros")]
    public List<ParametroFerramenta> Parametros { get; set; } = new();

    /// <summary>Diretório de trabalho (pode conter placeholders). Opcional.</summary>
    [JsonPropertyName("diretorioTrabalho")]
    public string? DiretorioTrabalho { get; set; }

    /// <summary>Tempo máximo de execução, em segundos.</summary>
    [JsonPropertyName("timeoutSegundos")]
    public int TimeoutSegundos { get; set; } = 300;

    public DefinicaoFerramenta Clonar() => new DefinicaoFerramenta
    {
        Nome = Nome,
        Descricao = Descricao,
        ExigeConfirmacao = ExigeConfirmacao,
        Comando = Comando,
        Argumentos = (string[])Argumentos.Clone(),
        Parametros = Parametros.Select(p => p.Clonar()).ToList(),
        DiretorioTrabalho = DiretorioTrabalho,
        TimeoutSegundos = TimeoutSegundos,
    };
}
