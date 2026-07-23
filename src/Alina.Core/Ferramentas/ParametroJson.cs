namespace Alina.Core.Ferramentas;

/// <summary>Forma crua de um parâmetro de ferramenta como o modelo o devolve no JSON.</summary>
internal sealed class ParametroJson
{
    public string? Nome { get; set; }
    public string? Descricao { get; set; }
    public bool Obrigatorio { get; set; } = true;
    public TipoParametroFerramenta Tipo { get; set; } = TipoParametroFerramenta.Automatico;
}
