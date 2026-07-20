namespace Alina.Tools.Git;

/// <summary>Configuração das ferramentas de Git (seção "Git" da configuração).</summary>
public sealed class GitOptions
{
    public const string SectionName = "Git";

    /// <summary>Executável do git (default "git", resolvido pelo PATH).</summary>
    public string Executable { get; set; } = "git";

    /// <summary>Repositório padrão quando o LLM não informar um caminho.</summary>
    public string? DefaultRepository { get; set; }

    /// <summary>Tempo máximo de execução de um comando git, em segundos.</summary>
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>Limite de caracteres da saída devolvida ao LLM (evita floods de diff/log).</summary>
    public int MaxOutputChars { get; set; } = 20_000;
}
