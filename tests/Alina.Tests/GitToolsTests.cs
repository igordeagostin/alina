using Alina.Tools.Git;

namespace Alina.Tests;

/// <summary>
/// Testes de integração das tools de Git usando um repositório temporário real.
/// O git é gratuito e rápido, então rodam normalmente (sem Skip).
/// </summary>
public sealed class GitToolsTests : IDisposable
{
    private readonly string _repo;
    private readonly GitOptions _options = new();

    public GitToolsTests()
    {
        _repo = Path.Combine(Path.GetTempPath(), "alina-git-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(_repo);
    }

    private async Task InitRepoAsync()
    {
        await GitCommandRunner.RunAsync(_options, _repo, default, "init", "-b", "main");
        await GitCommandRunner.RunAsync(_options, _repo, default, "config", "user.email", "test@alina.dev");
        await GitCommandRunner.RunAsync(_options, _repo, default, "config", "user.name", "Alina Test");
        await GitCommandRunner.RunAsync(_options, _repo, default, "config", "commit.gpgsign", "false");
    }

    private void WriteFile(string name, string content) => File.WriteAllText(Path.Combine(_repo, name), content);

    [Fact]
    public async Task Status_mostra_arquivo_nao_rastreado()
    {
        await InitRepoAsync();
        WriteFile("a.txt", "conteudo");

        var status = await new GitStatusTool(new FakeConfirmationService(true), _options).RunAsync(_repo);

        Assert.Contains("a.txt", status);
    }

    [Fact]
    public async Task Commit_negado_nao_cria_commit()
    {
        await InitRepoAsync();
        WriteFile("a.txt", "conteudo");

        var confirmation = new FakeConfirmationService(result: false);
        var commit = await new GitCommitTool(confirmation, _options).RunAsync("nao deve acontecer", repositoryPath: _repo);

        Assert.Equal(1, confirmation.Calls);
        Assert.Contains("cancelada", commit, StringComparison.OrdinalIgnoreCase);

        var log = await new GitLogTool(new FakeConfirmationService(true), _options).RunAsync(repositoryPath: _repo);
        Assert.DoesNotContain("nao deve acontecer", log);
    }

    [Fact]
    public async Task Commit_aprovado_cria_commit_e_aparece_no_log()
    {
        await InitRepoAsync();
        WriteFile("a.txt", "conteudo");

        var commit = await new GitCommitTool(new FakeConfirmationService(true), _options)
            .RunAsync("primeiro commit da alina", repositoryPath: _repo);
        Assert.DoesNotContain("git falhou", commit);

        var log = await new GitLogTool(new FakeConfirmationService(true), _options).RunAsync(repositoryPath: _repo);
        Assert.Contains("primeiro commit da alina", log);
    }

    [Fact]
    public async Task Branch_cria_e_troca_para_nova_branch()
    {
        await InitRepoAsync();
        WriteFile("a.txt", "conteudo");
        await new GitCommitTool(new FakeConfirmationService(true), _options).RunAsync("base", repositoryPath: _repo);

        var branch = await new GitBranchTool(new FakeConfirmationService(true), _options)
            .RunAsync("feature/login", create: true, repositoryPath: _repo);
        Assert.DoesNotContain("git falhou", branch);

        var status = await new GitStatusTool(new FakeConfirmationService(true), _options).RunAsync(_repo);
        Assert.Contains("feature/login", status);
    }

    [Fact]
    public void GitCommit_exige_confirmacao_status_nao()
    {
        var confirmation = new FakeConfirmationService(true);
        Assert.True(new GitCommitTool(confirmation, _options).RequiresConfirmation);
        Assert.False(new GitStatusTool(confirmation, _options).RequiresConfirmation);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_repo))
            {
                // Remove atributo read-only de arquivos do .git antes de apagar.
                foreach (var file in Directory.EnumerateFiles(_repo, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }

                Directory.Delete(_repo, recursive: true);
            }
        }
        catch
        {
            // limpeza best-effort
        }
    }
}
