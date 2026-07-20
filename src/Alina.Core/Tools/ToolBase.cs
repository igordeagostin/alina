using Microsoft.Extensions.AI;

namespace Alina.Core.Tools;

/// <summary>
/// Base conveniente para tools. Centraliza o gating de confirmação: uma tool
/// com <see cref="RequiresConfirmation"/> só deve executar seu efeito após
/// <see cref="EnsureConfirmedAsync"/> retornar <c>true</c>.
/// </summary>
public abstract class ToolBase : ITool
{
    private readonly IConfirmationService _confirmation;

    protected ToolBase(IConfirmationService confirmation) => _confirmation = confirmation;

    public abstract string Name { get; }

    public abstract string Description { get; }

    public virtual bool RequiresConfirmation => false;

    public abstract AIFunction AsAIFunction();

    /// <summary>
    /// Garante a confirmação do usuário quando a tool exige. Retorna <c>true</c>
    /// se pode prosseguir (não exige confirmação, ou o usuário autorizou).
    /// </summary>
    protected Task<bool> EnsureConfirmedAsync(string action, string? details, CancellationToken cancellationToken)
        => RequiresConfirmation
            ? _confirmation.ConfirmAsync(action, details, cancellationToken)
            : Task.FromResult(true);
}
