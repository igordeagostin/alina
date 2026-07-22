using System.ComponentModel;
using Alina.Core.Tools;
using Microsoft.Extensions.AI;

namespace Alina.Tools.Git;

/// <summary>Base das tools de Git read-only (não exigem confirmação).</summary>
public abstract class GitReadToolBase : ToolBase
{
    protected GitOptions Options { get; }

    protected GitReadToolBase(IConfirmationService confirmation, GitOptions options) : base(confirmation)
        => Options = options;
}

/// <summary>Mostra o status do repositório (branch atual e arquivos modificados).</summary>
public sealed class GitStatusTool : GitReadToolBase
{
    public GitStatusTool(IConfirmationService confirmation, GitOptions options) : base(confirmation, options) { }

    public override string Name => "git_status";

    public override string Description =>
        "Mostra o status do repositório Git: branch atual e arquivos modificados/staged/não rastreados.";

    public override AIFunction AsAIFunction() => AIFunctionFactory.Create(RunAsync, Name, Description);

    [Description("Retorna o status do repositório Git.")]
    public async Task<string> RunAsync(
        [Description("Caminho do repositório (opcional).")] string? repositoryPath = null,
        CancellationToken cancellationToken = default)
    {
        GitCommandResult result = await GitCommandRunner.RunAsync(Options, repositoryPath, cancellationToken, "status", "--short", "--branch");
        return result.ToToolResult();
    }
}

/// <summary>Mostra as diferenças (diff) do repositório.</summary>
public sealed class GitDiffTool : GitReadToolBase
{
    public GitDiffTool(IConfirmationService confirmation, GitOptions options) : base(confirmation, options) { }

    public override string Name => "git_diff";

    public override string Description =>
        "Mostra o diff do repositório Git. Use staged=true para ver o que já está no stage (git diff --cached).";

    public override AIFunction AsAIFunction() => AIFunctionFactory.Create(RunAsync, Name, Description);

    [Description("Retorna o diff das mudanças do repositório.")]
    public async Task<string> RunAsync(
        [Description("Ver o diff do que está staged (git diff --cached).")] bool staged = false,
        [Description("Caminho do repositório (opcional).")] string? repositoryPath = null,
        CancellationToken cancellationToken = default)
    {
        string[] args = staged ? new[] { "diff", "--cached" } : new[] { "diff" };
        GitCommandResult result = await GitCommandRunner.RunAsync(Options, repositoryPath, cancellationToken, args);
        return result.ToToolResult();
    }
}

/// <summary>Mostra o histórico de commits recentes.</summary>
public sealed class GitLogTool : GitReadToolBase
{
    public GitLogTool(IConfirmationService confirmation, GitOptions options) : base(confirmation, options) { }

    public override string Name => "git_log";

    public override string Description =>
        "Mostra os commits recentes do repositório (hash curto, autor, data e mensagem). Útil para gerar changelogs.";

    public override AIFunction AsAIFunction() => AIFunctionFactory.Create(RunAsync, Name, Description);

    [Description("Retorna os últimos commits do repositório.")]
    public async Task<string> RunAsync(
        [Description("Quantidade de commits a listar (default 15).")] int count = 15,
        [Description("Caminho do repositório (opcional).")] string? repositoryPath = null,
        CancellationToken cancellationToken = default)
    {
        int limit = count is > 0 and <= 200 ? count : 15;
        GitCommandResult result = await GitCommandRunner.RunAsync(
            Options, repositoryPath, cancellationToken,
            "log", $"-{limit}", "--pretty=format:%h %ad %an: %s", "--date=short");
        return result.ToToolResult();
    }
}
