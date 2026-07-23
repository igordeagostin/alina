using System.Globalization;
using System.Text;

namespace Alina.Voice;

/// <summary>
/// Descarta transcrições que não vieram de fala real. Diante de áudio sem voz
/// inteligível, os modelos de transcrição devolvem o próprio prompt de contexto ou
/// frases decoradas do treino — créditos de legenda ("Legendas pela comunidade da
/// Amara.org"), agradecimentos de vídeo, muitos deles em inglês. Sem este filtro
/// esse texto entra na conversa como se o usuário o tivesse dito, e a Alina responde
/// a uma fala que nunca existiu.
/// </summary>
public static class FiltroTranscricao
{
    /// <summary>Tamanho a partir do qual um trecho contido no prompt já indica eco.</summary>
    private const int MinimoEcoParcial = 20;

    /// <summary>Padrões já normalizados (minúsculas, sem acento e sem pontuação).</summary>
    private static readonly string[] PadroesAlucinacao =
    [
        "legendas pela comunidade",
        "amara org",
        "legendas por",
        "legendado por",
        "subtitles by",
        "subtitled by",
        "transcribed by",
        "thanks for watching",
        "thank you for watching",
        "please subscribe",
        "obrigado por assistir",
        "inscreva se no canal",
        "ate o proximo video",
    ];

    /// <summary>
    /// Indica se a transcrição deve ser jogada fora em vez de virar fala do usuário.
    /// </summary>
    /// <param name="texto">O que o modelo de transcrição devolveu.</param>
    /// <param name="promptContexto">A dica de vocabulário enviada ao modelo, se houver.</param>
    public static bool Descartavel(string? texto, string? promptContexto = null)
    {
        string normalizado = Normalizar(texto);
        if (normalizado.Length == 0)
        {
            return true;
        }

        foreach (string padrao in PadroesAlucinacao)
        {
            if (normalizado.Contains(padrao, StringComparison.Ordinal))
            {
                return true;
            }
        }

        string prompt = Normalizar(promptContexto);
        if (prompt.Length == 0)
        {
            return false;
        }

        return normalizado.Contains(prompt, StringComparison.Ordinal)
            || (normalizado.Length >= MinimoEcoParcial && prompt.Contains(normalizado, StringComparison.Ordinal));
    }

    /// <summary>Minúsculas, sem acentos e sem pontuação, para comparar fala com prompt.</summary>
    private static string Normalizar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return string.Empty;
        }

        string decomposto = valor.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        StringBuilder sb = new StringBuilder(decomposto.Length);
        bool espacoPendente = false;

        foreach (char c in decomposto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(c))
            {
                if (espacoPendente && sb.Length > 0)
                {
                    sb.Append(' ');
                }

                espacoPendente = false;
                sb.Append(c);
            }
            else
            {
                espacoPendente = true;
            }
        }

        return sb.ToString();
    }
}
