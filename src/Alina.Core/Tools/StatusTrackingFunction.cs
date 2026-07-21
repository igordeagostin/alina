using Alina.Core.Orchestration;
using Microsoft.Extensions.AI;

namespace Alina.Core.Tools;

/// <summary>
/// Envolve uma <see cref="AIFunction"/> marcando <see cref="AssistantState.Executing"/>
/// enquanto a tool roda e restaurando o estado anterior ao terminar. Preserva o
/// schema e os metadados da função original por delegação.
/// </summary>
internal sealed class StatusTrackingFunction : DelegatingAIFunction
{
    private readonly IAssistantStatus _status;

    public StatusTrackingFunction(AIFunction inner, IAssistantStatus status) : base(inner)
        => _status = status;

    protected override async ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var previous = _status.Current;
        _status.Set(AssistantState.Executing);
        try
        {
            return await base.InvokeCoreAsync(arguments, cancellationToken);
        }
        finally
        {
            _status.Set(previous);
        }
    }
}
