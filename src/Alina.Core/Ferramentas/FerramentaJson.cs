using System.Text.Json.Serialization;

namespace Alina.Core.Ferramentas;

/// <summary>
/// Forma crua de uma ferramenta como o modelo a devolve no JSON. Serve tanto ao
/// gerador de ferramenta (que a traz na raiz da resposta) quanto ao de habilidade
/// (que a traz dentro de "ferramentas"), por isso vive aqui e não em um deles.
/// </summary>
internal class FerramentaJson
{
    public string? Nome { get; set; }
    public string? Descricao { get; set; }
    public string? Comando { get; set; }
    public string[]? Argumentos { get; set; }
    public string? Motivo { get; set; }

    [JsonPropertyName("exigeConfirmacao")]
    public bool ExigeConfirmacao { get; set; } = true;

    public List<ParametroJson>? Parametros { get; set; }

    /// <summary>
    /// Converte para a definição persistível. Devolve <c>null</c> quando falta o
    /// mínimo (nome e comando), para o rascunho nunca carregar algo que o registro
    /// descartaria depois em silêncio.
    /// </summary>
    public DefinicaoFerramenta? ParaDefinicao()
    {
        if (string.IsNullOrWhiteSpace(Nome) || string.IsNullOrWhiteSpace(Comando))
        {
            return null;
        }

        return new DefinicaoFerramenta
        {
            Nome = Nome!.Trim(),
            Descricao = Descricao?.Trim() ?? string.Empty,
            Comando = Comando!.Trim(),
            Argumentos = Argumentos ?? Array.Empty<string>(),
            ExigeConfirmacao = ExigeConfirmacao,
            Parametros = (Parametros ?? new List<ParametroJson>())
                .Where(p => !string.IsNullOrWhiteSpace(p.Nome))
                .Select(p => new ParametroFerramenta
                {
                    Nome = p.Nome!.Trim(),
                    Descricao = p.Descricao?.Trim() ?? string.Empty,
                    Obrigatorio = p.Obrigatorio,
                    Tipo = p.Tipo,
                })
                .ToList(),
        };
    }
}
