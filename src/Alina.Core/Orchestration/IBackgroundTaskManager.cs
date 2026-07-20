namespace Alina.Core.Orchestration;

/// <summary>
/// Gerencia tarefas de longa duração executadas em background, permitindo que o
/// usuário continue conversando enquanto elas rodam.
/// </summary>
public interface IBackgroundTaskManager
{
    /// <summary>Dispara uma tarefa em background e retorna imediatamente.</summary>
    BackgroundTask Start(string description, Func<CancellationToken, Task<string>> work);

    IReadOnlyList<BackgroundTask> GetAll();

    BackgroundTask? Get(string id);

    /// <summary>Cancela uma tarefa em execução. Retorna <c>true</c> se havia o que cancelar.</summary>
    bool Cancel(string id);

    /// <summary>Disparado quando uma tarefa termina (sucesso, falha ou cancelamento).</summary>
    event EventHandler<BackgroundTask>? TaskFinished;
}
