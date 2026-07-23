using System.Text;

namespace Alina.Voice;

/// <summary>
/// Separa o que você disse do que é a voz da Alina voltando pelo microfone.
/// <para>
/// Para conversar de verdade ela precisa continuar ouvindo enquanto fala, e aí o microfone
/// entrega as duas vozes na mesma gravação. Descartá-la inteira é o caminho fácil e custa
/// caro: some tudo que você disser por cima dela. Como o texto que ela está falando é
/// conhecido, dá para limpar — e o que denuncia o eco não é repetir palavras dela, é
/// repetir a ordem delas: o eco devolve trechos literais, enquanto você usaria as mesmas
/// palavras noutro arranjo. Por isso a limpeza remove sequências contíguas, não palavras
/// soltas, e o que sobra é você.
/// </para>
/// <para>
/// Na dúvida, ouvir. Engolir sua fala quebra a conversa; deixar passar um resto de eco só
/// rende uma resposta desnecessária.
/// </para>
/// </summary>
public static class FiltroEco
{
    /// <summary>Tamanho a partir do qual uma sequência idêntica denuncia eco, e não coincidência.</summary>
    private const int MinimoPalavrasDoTrecho = 3;

    /// <summary>Abaixo disto o que sobrou é resto de transcrição, não uma fala sua.</summary>
    private const int MinimoLetrasParaValer = 3;

    /// <summary>
    /// Devolve o que a transcrição tem de seu, sem os trechos que são a
    /// <paramref name="falaDela"/> voltando pelo microfone. String vazia significa que só
    /// havia eco.
    /// </summary>
    public static string RemoverEco(string? transcricao, string? falaDela)
    {
        if (string.IsNullOrWhiteSpace(transcricao))
        {
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(falaDela))
        {
            return transcricao.Trim();
        }

        string[] originais = transcricao.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string[] ouvidas = originais.Select(Normalizar).ToArray();
        string[] dela = falaDela.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(Normalizar).ToArray();

        bool[] eco = new bool[ouvidas.Length];

        while (true)
        {
            (int inicio, int tamanho) = MaiorTrechoComum(ouvidas, dela, eco);
            if (tamanho < MinimoPalavrasDoTrecho)
            {
                break;
            }

            for (int i = inicio; i < inicio + tamanho; i++)
            {
                eco[i] = true;
            }
        }

        return Sobrou(originais, ouvidas, eco);
    }

    private static string Sobrou(string[] originais, string[] ouvidas, bool[] eco)
    {
        StringBuilder sb = new StringBuilder();
        int letras = 0;

        for (int i = 0; i < originais.Length; i++)
        {
            if (eco[i])
            {
                continue;
            }

            letras = Math.Max(letras, ouvidas[i].Length);

            if (sb.Length > 0)
            {
                sb.Append(' ');
            }

            sb.Append(originais[i]);
        }

        return letras >= MinimoLetrasParaValer ? sb.ToString() : string.Empty;
    }

    /// <summary>Maior sequência de palavras presente nas duas falas, ignorando o que já saiu como eco.</summary>
    private static (int Inicio, int Tamanho) MaiorTrechoComum(string[] ouvidas, string[] dela, bool[] eco)
    {
        int[] anterior = new int[dela.Length + 1];
        int[] atual = new int[dela.Length + 1];
        int melhorFim = 0;
        int melhor = 0;

        for (int i = 1; i <= ouvidas.Length; i++)
        {
            for (int j = 1; j <= dela.Length; j++)
            {
                bool casa = !eco[i - 1]
                    && ouvidas[i - 1].Length > 0
                    && ouvidas[i - 1] == dela[j - 1];

                atual[j] = casa ? anterior[j - 1] + 1 : 0;

                if (atual[j] > melhor)
                {
                    melhor = atual[j];
                    melhorFim = i;
                }
            }

            (anterior, atual) = (atual, anterior);
            Array.Clear(atual);
        }

        return (melhorFim - melhor, melhor);
    }

    private static string Normalizar(string palavra) => TextoVoz.Normalizar(palavra).Trim();
}
