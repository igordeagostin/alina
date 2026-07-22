using System.Text;

namespace Alina.Voice;

/// <summary>
/// Quebra a resposta em blocos curtos para a síntese de fala. Falar por blocos
/// deixa a conversa fluida em dois sentidos: a primeira frase sai muito antes
/// (não espera sintetizar o texto inteiro) e a interrupção corta quase na hora,
/// já que nunca há mais que um bloco em reprodução.
/// </summary>
public static class DivisorFala
{
    private const int TamanhoPrimeiroBloco = 90;
    private const int TamanhoBloco = 220;

    public static IReadOnlyList<string> Dividir(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return [];
        }

        List<string> blocos = new List<string>();
        StringBuilder atual = new StringBuilder();

        foreach (string frase in SepararFrases(texto))
        {
            int limite = blocos.Count == 0 ? TamanhoPrimeiroBloco : TamanhoBloco;

            if (atual.Length > 0 && atual.Length + frase.Length > limite)
            {
                blocos.Add(atual.ToString().Trim());
                atual.Clear();
            }

            atual.Append(frase);
        }

        if (atual.Length > 0)
        {
            blocos.Add(atual.ToString().Trim());
        }

        return blocos.Where(b => b.Length > 0).ToArray();
    }

    private static IEnumerable<string> SepararFrases(string texto)
    {
        int inicio = 0;

        for (int i = 0; i < texto.Length; i++)
        {
            if (!EhFimDeFrase(texto, i))
            {
                continue;
            }

            int fim = i + 1;
            while (fim < texto.Length && char.IsWhiteSpace(texto[fim]))
            {
                fim++;
            }

            yield return texto[inicio..fim];
            inicio = fim;
            i = fim - 1;
        }

        if (inicio < texto.Length)
        {
            yield return texto[inicio..];
        }
    }

    private static bool EhFimDeFrase(string texto, int i)
    {
        char c = texto[i];

        if (c is '\n')
        {
            return true;
        }

        if (c is not ('.' or '!' or '?' or ':' or ';'))
        {
            return false;
        }

        bool proximoSeparado = i + 1 >= texto.Length || char.IsWhiteSpace(texto[i + 1]);
        bool numeroOuSigla = c == '.' && i > 0 && i + 1 < texto.Length
            && char.IsDigit(texto[i - 1]) && char.IsDigit(texto[i + 1]);

        return proximoSeparado && !numeroOuSigla;
    }
}
