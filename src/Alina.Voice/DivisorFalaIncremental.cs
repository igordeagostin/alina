using System.Text;

namespace Alina.Voice;

/// <summary>
/// Versão incremental do <see cref="DivisorFala"/> para respostas em streaming: recebe o
/// texto em pedaços, do tamanho que vierem, e devolve cada frase assim que ela se completa.
/// É o que permite começar a sintetizar (e falar) a primeira frase enquanto o modelo ainda
/// gera o resto da resposta.
/// </summary>
public sealed class DivisorFalaIncremental
{
    private readonly StringBuilder _pendente = new StringBuilder();

    /// <summary>Acumula um pedaço e devolve as frases que fecharam com ele (pode ser nenhuma).</summary>
    public IReadOnlyList<string> Alimentar(string pedaco)
    {
        if (string.IsNullOrEmpty(pedaco))
        {
            return [];
        }

        _pendente.Append(pedaco);
        return ExtrairFrasesCompletas();
    }

    /// <summary>Encerra o stream: devolve o que restou, mesmo sem pontuação final.</summary>
    public IReadOnlyList<string> Concluir()
    {
        List<string> frases = new List<string>(ExtrairFrasesCompletas());

        string resto = _pendente.ToString().Trim();
        _pendente.Clear();
        if (resto.Length > 0)
        {
            frases.Add(resto);
        }

        return frases;
    }

    /// <summary>
    /// Um fim de frase só é confirmado quando o caractere seguinte já chegou e é um
    /// espaço — antes disso não dá para distinguir "fim de frase" de "1.5" ou de uma
    /// pontuação no meio do fluxo. A quebra de linha confirma sozinha.
    /// </summary>
    private IReadOnlyList<string> ExtrairFrasesCompletas()
    {
        if (_pendente.Length == 0)
        {
            return [];
        }

        List<string>? frases = null;
        string texto = _pendente.ToString();
        int inicio = 0;
        int i = 0;

        while (i < texto.Length)
        {
            char c = texto[i];
            bool fim = c == '\n'
                || (c is '.' or '!' or '?' or ':' or ';' && i + 1 < texto.Length && char.IsWhiteSpace(texto[i + 1]));

            if (!fim)
            {
                i++;
                continue;
            }

            int corte = i + 1;
            while (corte < texto.Length && char.IsWhiteSpace(texto[corte]))
            {
                corte++;
            }

            string frase = texto[inicio..corte].Trim();
            if (frase.Length > 0)
            {
                (frases ??= new List<string>()).Add(frase);
            }

            inicio = corte;
            i = corte;
        }

        if (frases is null)
        {
            return [];
        }

        _pendente.Clear();
        _pendente.Append(texto, inicio, texto.Length - inicio);
        return frases;
    }
}
