using System.Globalization;
using System.Text;

namespace Alina.Core.IO;

/// <summary>
/// Encontra pastas de projeto pelo nome falado pelo usuário ("diário API") dentro das
/// raízes confiáveis, tolerando acento, espaço, hífen e ponto. Existe para que a Alina
/// nunca precise adivinhar um caminho: ela pergunta ao disco antes de agir.
/// </summary>
public static class BuscaProjetos
{
    private const int MaxDiretoriosVisitados = 20_000;

    private static readonly string[] MarcadoresDeProjeto =
        [".git", "*.slnx", "*.sln", "*.csproj", "package.json", "pom.xml", "go.mod", "Cargo.toml", "pyproject.toml"];

    public static IReadOnlyList<ProjetoEncontrado> Localizar(
        IEnumerable<string> raizes,
        string termo,
        int profundidadeMaxima = 4,
        int maxResultados = 8)
    {
        string[] tokens = Tokenizar(termo);
        if (tokens.Length == 0)
        {
            return [];
        }

        List<ProjetoEncontrado> encontrados = new List<ProjetoEncontrado>();
        int restante = MaxDiretoriosVisitados;

        foreach (string raiz in raizes)
        {
            if (string.IsNullOrWhiteSpace(raiz) || !Directory.Exists(raiz))
            {
                continue;
            }

            restante = Percorrer(Path.GetFullPath(raiz), Path.GetFullPath(raiz), 1, profundidadeMaxima, tokens, encontrados, restante);
        }

        return encontrados
            .OrderByDescending(p => p.Pontuacao)
            .ThenBy(p => p.Caminho.Length)
            .ThenBy(p => p.Caminho, StringComparer.OrdinalIgnoreCase)
            .Take(maxResultados)
            .ToList();
    }

    private static int Percorrer(
        string raiz, string diretorio, int nivel, int profundidadeMaxima,
        string[] tokens, List<ProjetoEncontrado> encontrados, int restante)
    {
        if (nivel > profundidadeMaxima || restante <= 0)
        {
            return restante;
        }

        foreach (string sub in Enumerar(diretorio))
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

            restante--;

            string? marcador = MarcadorDe(sub);
            int pontuacao = Pontuar(nome, Path.GetRelativePath(raiz, sub), tokens, nivel, marcador is not null);
            if (pontuacao > 0)
            {
                encontrados.Add(new ProjetoEncontrado(nome, sub, pontuacao, marcador));
            }

            restante = Percorrer(raiz, sub, nivel + 1, profundidadeMaxima, tokens, encontrados, restante);
        }

        return restante;
    }

    private static int Pontuar(string nome, string caminhoRelativo, string[] tokens, int nivel, bool ehProjeto)
    {
        string nomeNormalizado = Normalizar(nome);
        string caminhoNormalizado = Normalizar(caminhoRelativo);
        string termo = string.Join(' ', tokens);

        int noNome = tokens.Count(t => Contem(nomeNormalizado, t));
        if (noNome == 0)
        {
            return 0;
        }

        int noCaminho = tokens.Count(t => Contem(caminhoNormalizado, t));
        if (noCaminho < tokens.Length)
        {
            return 0;
        }

        int pontuacao = 20 + (noNome * 15);

        if (nomeNormalizado == termo)
        {
            pontuacao += 100;
        }
        else if (noNome == tokens.Length)
        {
            pontuacao += 40;
        }

        if (ehProjeto)
        {
            pontuacao += 25;
        }

        return Math.Max(1, pontuacao - (nivel * 2));
    }

    private static bool Contem(string texto, string token)
    {
        foreach (string palavra in texto.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (palavra.StartsWith(token, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return token.Length >= 3 && texto.Contains(token, StringComparison.Ordinal);
    }

    private static string? MarcadorDe(string diretorio)
    {
        foreach (string padrao in MarcadoresDeProjeto)
        {
            try
            {
                if (!padrao.Contains('*'))
                {
                    string caminho = Path.Combine(diretorio, padrao);
                    if (File.Exists(caminho) || Directory.Exists(caminho))
                    {
                        return padrao;
                    }

                    continue;
                }

                string? achado = Directory.EnumerateFiles(diretorio, padrao, SearchOption.TopDirectoryOnly).FirstOrDefault();
                if (achado is not null)
                {
                    return Path.GetFileName(achado);
                }
            }
            catch (Exception e) when (e is UnauthorizedAccessException or IOException)
            {
                return null;
            }
        }

        return null;
    }

    private static IEnumerable<string> Enumerar(string diretorio)
    {
        try
        {
            return Directory.EnumerateDirectories(diretorio).OrderBy(c => c, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception e) when (e is UnauthorizedAccessException or IOException)
        {
            return [];
        }
    }

    private static string[] Tokenizar(string termo)
        => Normalizar(termo).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>Minúsculas, sem acento, com separadores (-, _, ., /, \) virando espaço.</summary>
    private static string Normalizar(string valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return string.Empty;
        }

        StringBuilder sb = new StringBuilder(valor.Length);
        foreach (char c in valor.ToLowerInvariant().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            sb.Append(char.IsLetterOrDigit(c) ? c : ' ');
        }

        return string.Join(' ', sb.ToString().Normalize(NormalizationForm.FormC)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
