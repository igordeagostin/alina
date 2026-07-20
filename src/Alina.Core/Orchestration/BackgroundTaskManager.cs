using System.Collections.Concurrent;

namespace Alina.Core.Orchestration;

/// <summary>Implementação in-memory e thread-safe do gerenciador de tarefas em background.</summary>
public sealed class BackgroundTaskManager : IBackgroundTaskManager, IDisposable
{
    private readonly ConcurrentDictionary<string, TrackedTask> _tasks = new();

    public event EventHandler<BackgroundTask>? TaskFinished;

    public BackgroundTask Start(string description, Func<CancellationToken, Task<string>> work)
    {
        var task = new BackgroundTask { Description = description };
        var cts = new CancellationTokenSource();
        _tasks[task.Id] = new TrackedTask(task, cts);

        // Fire-and-forget: roda em background e atualiza o estado ao terminar.
        _ = Task.Run(async () =>
        {
            try
            {
                var result = await work(cts.Token);
                task.Result = result;
                task.Status = cts.IsCancellationRequested ? BackgroundTaskStatus.Cancelled : BackgroundTaskStatus.Completed;
            }
            catch (OperationCanceledException)
            {
                task.Status = BackgroundTaskStatus.Cancelled;
                task.Result = "Tarefa cancelada.";
            }
            catch (Exception ex)
            {
                task.Status = BackgroundTaskStatus.Failed;
                task.Result = ex.Message;
            }
            finally
            {
                task.CompletedAt = DateTimeOffset.Now;
                cts.Dispose();
                TaskFinished?.Invoke(this, task);
            }
        });

        return task;
    }

    public IReadOnlyList<BackgroundTask> GetAll() =>
        _tasks.Values.Select(t => t.Task).OrderByDescending(t => t.CreatedAt).ToList();

    public BackgroundTask? Get(string id) =>
        _tasks.TryGetValue(id, out var tracked) ? tracked.Task : null;

    public bool Cancel(string id)
    {
        if (!_tasks.TryGetValue(id, out var tracked) || tracked.Task.IsFinished)
        {
            return false;
        }

        tracked.Cts.Cancel();
        return true;
    }

    public void Dispose()
    {
        foreach (var tracked in _tasks.Values)
        {
            try
            {
                tracked.Cts.Cancel();
                tracked.Cts.Dispose();
            }
            catch
            {
                // ignora
            }
        }
    }

    private sealed record TrackedTask(BackgroundTask Task, CancellationTokenSource Cts);
}
