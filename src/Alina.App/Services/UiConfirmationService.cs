using Alina.Core.Tools;

namespace Alina.App.Services;

/// <summary>
/// Confirmação de segurança renderizada como overlay dentro da própria janela
/// da Alina (substitui o <c>MessageBox</c> nativo). Faz a ponte entre a chamada
/// da tool (em thread de background) e o shell Blazor: publica uma
/// <see cref="ConfirmationRequest"/> e aguarda o usuário responder no overlay.
/// </summary>
public sealed class UiConfirmationService : IConfirmationService
{
    /// <summary>Assinado pelo shell para exibir o overlay. Único assinante.</summary>
    public event Func<ConfirmationRequest, Task>? Requested;

    public async Task<bool> ConfirmAsync(string action, string? details = null, CancellationToken cancellationToken = default)
    {
        Func<ConfirmationRequest, Task>? handler = Requested;
        if (handler is null)
        {
            return false;
        }

        ConfirmationRequest request = new ConfirmationRequest(action, details);
        await using CancellationTokenRegistration _ = cancellationToken.Register(() => request.Responder(false));

        await handler(request);
        return await request.Resposta;
    }
}

/// <summary>Uma solicitação de confirmação pendente, completada pelo overlay.</summary>
public sealed class ConfirmationRequest
{
    private readonly TaskCompletionSource<bool> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public ConfirmationRequest(string action, string? details)
    {
        Action = action;
        Details = details;
    }

    public string Action { get; }

    public string? Details { get; }

    public Task<bool> Resposta => _tcs.Task;

    public void Responder(bool autorizado) => _tcs.TrySetResult(autorizado);
}
