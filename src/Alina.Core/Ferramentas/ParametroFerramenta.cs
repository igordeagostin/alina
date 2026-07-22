using System.Text.Json.Serialization;

namespace Alina.Core.Ferramentas;

/// <summary>Um parâmetro de ferramenta que o LLM preenche ao invocá-la.</summary>
public sealed class ParametroFerramenta
{
    [JsonPropertyName("nome")]
    public string Nome { get; set; } = string.Empty;

    [JsonPropertyName("descricao")]
    public string Descricao { get; set; } = string.Empty;

    [JsonPropertyName("obrigatorio")]
    public bool Obrigatorio { get; set; } = true;

    public ParametroFerramenta Clonar() => new ParametroFerramenta
    {
        Nome = Nome,
        Descricao = Descricao,
        Obrigatorio = Obrigatorio,
    };
}
