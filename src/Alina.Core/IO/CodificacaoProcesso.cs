using System.Diagnostics;
using System.Text;

namespace Alina.Core.IO;

/// <summary>
/// Força UTF-8 nos canais redirecionados de um subprocesso. Sem isso, num app sem
/// console anexado (WPF) o .NET decodifica a saída com a codepage ANSI/OEM do
/// sistema, enquanto as ferramentas de linha de comando emitem UTF-8 — e a
/// acentuação chega corrompida ("usuário" vira "usuÃ¡rio").
/// </summary>
public static class CodificacaoProcesso
{
    private static readonly UTF8Encoding SemBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    public static ProcessStartInfo AplicarUtf8(this ProcessStartInfo psi)
    {
        if (psi.RedirectStandardOutput)
        {
            psi.StandardOutputEncoding = SemBom;
        }

        if (psi.RedirectStandardError)
        {
            psi.StandardErrorEncoding = SemBom;
        }

        if (psi.RedirectStandardInput)
        {
            psi.StandardInputEncoding = SemBom;
        }

        return psi;
    }
}
