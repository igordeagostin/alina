namespace Alina.Core.IO;

/// <summary>
/// Pastas de ruído (build, dependências, metadados de IDE) ignoradas em qualquer
/// varredura de disco — listagem de árvore ou busca de projeto.
/// </summary>
public static class PastasIgnoradas
{
    private static readonly HashSet<string> Nomes = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".vs", ".vscode", ".idea", "node_modules", "bin", "obj",
        "packages", "dist", "build", "target", ".next", ".nuget", ".gradle",
    };

    public static bool Contem(string nome) => Nomes.Contains(nome);
}
