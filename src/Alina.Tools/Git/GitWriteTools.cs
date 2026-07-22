using System.ComponentModel;
using Alina.Core.Tools;
using Microsoft.Extensions.AI;

namespace Alina.Tools.Git;

/// <summary>Cria um commit (stage + commit). Exige confirmação do usuário.</summary>
public sealed class GitCommitTool : ToolBase
{
    private readonly GitOptions _options;

    public GitCommitTool(IConfirmationService confirmation, GitOptions options) : base(confirmation)
        => _options = options;

    public override string Name => "git_commit";

    public override string Description =>
        "Cria um commit no repositório Git. Por padrão adiciona todas as mudanças (git add -A) e commita com a " +
        "mensagem informada. Exige confirmação do usuário.";

    public override bool RequiresConfirmation => true;

    public override AIFunction AsAIFunction() => AIFunctionFactory.Create(RunAsync, Name, Description);

    [Description("Faz stage das mudanças e cria um commit com a mensagem informada.")]
    public async Task<string> RunAsync(
        [Description("Mensagem do commit.")] string message,
        [Description("Adicionar todas as mudanças antes do commit (git add -A). Default true.")] bool stageAll = true,
        [Description("Caminho do repositório (opcional).")] string? repositoryPath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "Erro: mensagem de commit vazia.";
        }

        bool confirmed = await EnsureConfirmedAsync("Criar commit no Git", $"\"{message}\"" + (stageAll ? " (git add -A)" : ""), cancellationToken);
        if (!confirmed)
        {
            return "Operação cancelada pelo usuário — nenhum commit foi criado.";
        }

        if (stageAll)
        {
            GitCommandResult stage = await GitCommandRunner.RunAsync(_options, repositoryPath, cancellationToken, "add", "-A");
            if (!stage.Success)
            {
                return stage.ToToolResult();
            }
        }

        GitCommandResult commit = await GitCommandRunner.RunAsync(_options, repositoryPath, cancellationToken, "commit", "-m", message);
        return commit.ToToolResult();
    }
}

/// <summary>Cria e/ou troca de branch.</summary>
public sealed class GitBranchTool : ToolBase
{
    private readonly GitOptions _options;

    public GitBranchTool(IConfirmationService confirmation, GitOptions options) : base(confirmation)
        => _options = options;

    public override string Name => "git_branch";

    public override string Description =>
        "Cria e/ou troca de branch no repositório Git. Use create=true para criar uma nova branch.";

    public override AIFunction AsAIFunction() => AIFunctionFactory.Create(RunAsync, Name, Description);

    [Description("Cria e/ou faz checkout de uma branch.")]
    public async Task<string> RunAsync(
        [Description("Nome da branch.")] string name,
        [Description("Criar a branch (git switch -c). Se false, apenas faz checkout de uma existente.")] bool create = true,
        [Description("Caminho do repositório (opcional).")] string? repositoryPath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Erro: nome da branch vazio.";
        }

        string[] args = create ? new[] { "switch", "-c", name } : new[] { "switch", name };
        GitCommandResult result = await GitCommandRunner.RunAsync(_options, repositoryPath, cancellationToken, args);
        return result.ToToolResult();
    }
}
