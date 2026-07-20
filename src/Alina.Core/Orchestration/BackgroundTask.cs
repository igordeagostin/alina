namespace Alina.Core.Orchestration;

public enum BackgroundTaskStatus
{
    Running,
    Completed,
    Failed,
    Cancelled,
}

/// <summary>Uma tarefa de longa duração executada em background (ex: delegação ao Claude Code).</summary>
public sealed class BackgroundTask
{
    public string Id { get; init; } = Guid.NewGuid().ToString("n")[..6];

    public required string Description { get; init; }

    public BackgroundTaskStatus Status { get; internal set; } = BackgroundTaskStatus.Running;

    /// <summary>Saída/resultado (preenchido quando a tarefa termina).</summary>
    public string? Result { get; internal set; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;

    public DateTimeOffset? CompletedAt { get; internal set; }

    public bool IsFinished => Status != BackgroundTaskStatus.Running;
}
