namespace Alina.Core.Orchestration;

/// <summary>Implementação thread-safe de <see cref="IAssistantStatus"/>.</summary>
public sealed class AssistantStatus : IAssistantStatus
{
    private readonly object _gate = new();
    private AssistantState _current = AssistantState.Idle;

    public AssistantState Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    public event EventHandler<AssistantState>? Changed;

    public void Set(AssistantState state)
    {
        lock (_gate)
        {
            if (_current == state)
            {
                return;
            }

            _current = state;
        }

        Changed?.Invoke(this, state);
    }
}
