using System.ComponentModel;
using System.Text;
using Alina.Core.Orchestration;
using Alina.Core.Tools;
using Alina.Tools.ClaudeCode;
using Microsoft.Extensions.AI;

namespace Alina.Tools.Background;

/// <summary>
/// Dispara uma tarefa de desenvolvimento ao Claude Code em background (não-bloqueante):
/// retorna imediatamente com um id e a tarefa continua rodando enquanto o usuário
/// conversa. Exige confirmação (a execução é feita sem novas confirmações).
/// </summary>
public sealed class DelegateInBackgroundTool : ToolBase
{
    private readonly IBackgroundTaskManager _manager;
    private readonly ClaudeCodeTool _claudeCode;

    public DelegateInBackgroundTool(
        IConfirmationService confirmation, IBackgroundTaskManager manager, ClaudeCodeTool claudeCode)
        : base(confirmation)
    {
        _manager = manager;
        _claudeCode = claudeCode;
    }

    public override string Name => "delegar_em_background";

    public override string Description =>
        "Delega uma tarefa ao Claude Code em BACKGROUND (não bloqueia a conversa). Retorna um id na hora; " +
        "a tarefa roda em paralelo. Use para tarefas longas quando o usuário quer continuar conversando. " +
        "Exige confirmação.";

    public override bool RequiresConfirmation => true;

    public override AIFunction AsAIFunction() => AIFunctionFactory.Create(RunAsync, Name, Description);

    [Description("Inicia uma tarefa do Claude Code em background e retorna o id imediatamente.")]
    public async Task<string> RunAsync(
        [Description("A tarefa a ser executada pelo Claude Code, em linguagem natural.")] string task,
        [Description("Diretório do projeto (opcional).")] string? workingDirectory = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(task))
        {
            return "Erro: nenhuma tarefa informada.";
        }

        bool confirmed = await EnsureConfirmedAsync(
            "Iniciar tarefa do Claude Code em background", task, cancellationToken);
        if (!confirmed)
        {
            return "Operação cancelada pelo usuário — nada foi iniciado.";
        }

        string description = task.Length > 60 ? task[..60] + "…" : task;
        BackgroundTask backgroundTask = _manager.Start(description, ct => _claudeCode.ExecuteAsync(task, workingDirectory, ct));

        return $"Tarefa [{backgroundTask.Id}] iniciada em background. " +
               "Continue conversando; avisarei quando terminar (ou use listar_tarefas / o comando /tarefas).";
    }
}

/// <summary>Lista o status das tarefas em background.</summary>
public sealed class ListTasksTool : ToolBase
{
    private readonly IBackgroundTaskManager _manager;

    public ListTasksTool(IConfirmationService confirmation, IBackgroundTaskManager manager) : base(confirmation)
        => _manager = manager;

    public override string Name => "listar_tarefas";

    public override string Description => "Lista as tarefas em background e seus status (em execução, concluída, falhou).";

    public override AIFunction AsAIFunction() => AIFunctionFactory.Create(RunAsync, Name, Description);

    [Description("Retorna o status das tarefas em background.")]
    public Task<string> RunAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<BackgroundTask> tasks = _manager.GetAll();
        if (tasks.Count == 0)
        {
            return Task.FromResult("Nenhuma tarefa em background.");
        }

        StringBuilder sb = new StringBuilder();
        foreach (BackgroundTask task in tasks)
        {
            sb.AppendLine($"[{task.Id}] {task.Status}: {task.Description}");
        }

        return Task.FromResult(sb.ToString().TrimEnd());
    }
}
