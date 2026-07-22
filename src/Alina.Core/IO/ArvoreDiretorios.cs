using System.Text;

namespace Alina.Core.IO;

/// <summary>
/// Monta uma listagem textual (árvore indentada) de diretórios a partir de uma ou
/// mais raízes, até uma profundidade máxima. Serve para dar à Alina uma visão dos
/// projetos disponíveis sem precisar executar comandos, respeitando um teto de
/// entradas para não estourar o contexto. Pastas de ruído (bin, obj, .git,
/// node_modules…) são ignoradas.
/// </summary>
public static class ArvoreDiretorios
{
    public static string Montar(
        IEnumerable<string> raizes,
        int profundidadeMaxima = 2,
        bool incluirArquivos = false,
        int maxEntradas = 500)
    {
        StringBuilder sb = new StringBuilder();
        int restante = maxEntradas;

        foreach (string raiz in raizes)
        {
            if (string.IsNullOrWhiteSpace(raiz) || !Directory.Exists(raiz))
            {
                continue;
            }

            sb.Append(raiz).Append('\n');
            restante = Percorrer(sb, raiz, 1, profundidadeMaxima, incluirArquivos, restante);
        }

        return sb.ToString().TrimEnd();
    }

    private static int Percorrer(
        StringBuilder sb, string diretorio, int nivel, int profundidadeMaxima,
        bool incluirArquivos, int restante)
    {
        if (nivel > profundidadeMaxima || restante <= 0)
        {
            return restante;
        }

        string indentacao = new string(' ', nivel * 2);

        if (incluirArquivos)
        {
            foreach (string arquivo in Enumerar(Directory.EnumerateFiles, diretorio))
            {
                if (restante <= 0)
                {
                    break;
                }

                sb.Append(indentacao).Append(Path.GetFileName(arquivo)).Append('\n');
                restante--;
            }
        }

        foreach (string sub in Enumerar(Directory.EnumerateDirectories, diretorio))
        {
            if (restante <= 0)
            {
                break;
            }

            string nome = Path.GetFileName(sub);
            if (PastasIgnoradas.Contem(nome))
            {
                continue;
            }

            sb.Append(indentacao).Append(nome).Append('/').Append('\n');
            restante--;
            restante = Percorrer(sb, sub, nivel + 1, profundidadeMaxima, incluirArquivos, restante);
        }

        return restante;
    }

    private static IEnumerable<string> Enumerar(Func<string, IEnumerable<string>> fonte, string diretorio)
    {
        try
        {
            return fonte(diretorio).OrderBy(caminho => caminho, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception e) when (e is UnauthorizedAccessException or IOException)
        {
            return [];
        }
    }
}
