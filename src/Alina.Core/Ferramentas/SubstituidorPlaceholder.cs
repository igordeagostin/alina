using System.Text.RegularExpressions;

namespace Alina.Core.Ferramentas;

/// <summary>
/// Troca os placeholders <c>{parametro}</c> de um argumento pelos valores informados pelo
/// LLM. O sufixo <c>:url</c> (<c>{termo:url}</c>) codifica o valor para uso dentro de uma
/// URL — sem ele, um termo de busca com <c>&amp;</c> ou <c>#</c> truncaria a query string
/// e a pesquisa chegaria mutilada ao site.
/// </summary>
internal static partial class SubstituidorPlaceholder
{
    public const string FormatoUrl = "url";

    [GeneratedRegex(@"\{(?<nome>[\p{L}0-9_]+)(?::(?<formato>[\p{L}0-9_]+))?\}", RegexOptions.CultureInvariant)]
    private static partial Regex Padrao { get; }

    public static string Aplicar(string? modelo, IReadOnlyDictionary<string, string> valores)
    {
        if (string.IsNullOrEmpty(modelo))
        {
            return string.Empty;
        }

        return Padrao.Replace(modelo, correspondencia =>
        {
            string nome = correspondencia.Groups["nome"].Value;
            if (!valores.TryGetValue(nome, out string? valor))
            {
                return correspondencia.Value;
            }

            Group formato = correspondencia.Groups["formato"];
            return formato.Success && formato.Value.Equals(FormatoUrl, StringComparison.OrdinalIgnoreCase)
                ? Uri.EscapeDataString(valor)
                : valor;
        });
    }

    /// <summary>Nomes dos parâmetros referenciados no modelo, independente do formato usado.</summary>
    public static IEnumerable<string> Referencias(string? modelo)
    {
        if (string.IsNullOrEmpty(modelo))
        {
            return Array.Empty<string>();
        }

        return Padrao.Matches(modelo).Select(m => m.Groups["nome"].Value);
    }
}
