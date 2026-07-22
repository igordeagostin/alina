using System.Globalization;
using System.Text;

namespace Alina.Voice;

/// <summary>Utilidades de normalização de texto falado (minúsculas, sem acento/pontuação).</summary>
internal static class TextoVoz
{
    public static string Normalizar(string texto)
    {
        StringBuilder sb = new StringBuilder(texto.Length);
        foreach (char c in texto.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c))
            {
                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(' ');
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    public static bool Contem(string frase, string termo)
    {
        string alvo = Normalizar(termo).Trim();
        return alvo.Length > 0 && frase.Contains(alvo, StringComparison.Ordinal);
    }

    public static bool ContemAlgum(string frase, IEnumerable<string> termos)
        => termos.Any(t => Contem(frase, t));
}
