using Alina.Core.Orchestration;
using Alina.Tools.Background;
using Alina.Tools.ClaudeCode;

namespace Alina.Tests;

public sealed class BackgroundTaskManagerTests
{
    private static Task<BackgroundTask> WhenFinished(BackgroundTaskManager manager)
    {
        var tcs = new TaskCompletionSource<BackgroundTask>(TaskCreationOptions.RunContinuationsAsynchronously);
        manager.TaskFinished += (_, t) => tcs.TrySetResult(t);
        return tcs.Task;
    }

    [Fact]
    public async Task Start_executa_e_marca_concluida()
    {
        using var manager = new BackgroundTaskManager();
        var finished = WhenFinished(manager);

        var task = manager.Start("tarefa teste", _ => Task.FromResult("resultado ok"));
        var result = await finished.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(task.Id, result.Id);
        Assert.Equal(BackgroundTaskStatus.Completed, result.Status);
        Assert.Equal("resultado ok", result.Result);
        Assert.NotNull(result.CompletedAt);
    }

    [Fact]
    public async Task Start_captura_excecao_como_falha()
    {
        using var manager = new BackgroundTaskManager();
        var finished = WhenFinished(manager);

        manager.Start("vai falhar", _ => throw new InvalidOperationException("boom"));
        var result = await finished.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(BackgroundTaskStatus.Failed, result.Status);
        Assert.Contains("boom", result.Result);
    }

    [Fact]
    public async Task Cancel_interrompe_tarefa_em_execucao()
    {
        using var manager = new BackgroundTaskManager();
        var finished = WhenFinished(manager);

        var task = manager.Start("longa", async ct =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return "nunca chega aqui";
        });

        Assert.True(manager.Cancel(task.Id));
        var result = await finished.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(BackgroundTaskStatus.Cancelled, result.Status);
    }

    [Fact]
    public void Cancel_de_id_inexistente_retorna_false()
    {
        using var manager = new BackgroundTaskManager();
        Assert.False(manager.Cancel("naoexiste"));
    }

    [Fact]
    public void Get_e_GetAll_refletem_a_tarefa()
    {
        using var manager = new BackgroundTaskManager();
        var task = manager.Start("x", _ => Task.FromResult("ok"));

        Assert.NotNull(manager.Get(task.Id));
        Assert.Contains(manager.GetAll(), t => t.Id == task.Id);
    }
}

public sealed class BackgroundToolsTests
{
    [Fact]
    public async Task DelegateInBackground_negado_nao_inicia_tarefa()
    {
        using var manager = new BackgroundTaskManager();
        var claude = new ClaudeCodeTool(new FakeConfirmationService(true), new ClaudeCodeOptions());
        var tool = new DelegateInBackgroundTool(new FakeConfirmationService(result: false), manager, claude);

        var result = await tool.RunAsync("faça algo demorado");

        Assert.Contains("cancelada", result, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(manager.GetAll());
    }

    [Fact]
    public async Task ListTasks_reporta_tarefas()
    {
        using var manager = new BackgroundTaskManager();
        manager.Start("minha tarefa", _ => Task.FromResult("ok"));
        var tool = new ListTasksTool(new FakeConfirmationService(true), manager);

        var result = await tool.RunAsync();

        Assert.Contains("minha tarefa", result);
    }

    [Fact]
    public void DelegateInBackground_exige_confirmacao()
    {
        using var manager = new BackgroundTaskManager();
        var claude = new ClaudeCodeTool(new FakeConfirmationService(true), new ClaudeCodeOptions());
        var tool = new DelegateInBackgroundTool(new FakeConfirmationService(true), manager, claude);

        Assert.True(tool.RequiresConfirmation);
        Assert.Equal("delegar_em_background", tool.Name);
    }
}
