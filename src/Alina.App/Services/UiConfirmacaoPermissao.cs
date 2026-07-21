using Alina.Core.Permissoes;

namespace Alina.App.Services;

/// <summary>
/// Confirmação de permissão renderizada como overlay na janela da Alina, com opções de escopo
/// (uma vez, sempre, sempre neste diretório). Faz a ponte entre a chamada em background (servidor
/// de permissão) e o shell Blazor.
/// </summary>
public sealed class UiConfirmacaoPermissao : IConfirmacaoPermissao
{
    /// <summary>Assinado pelo shell para exibir o overlay. Único assinante.</summary>
    public event Func<PedidoPermissaoUi, Task>? Requested;

    public async Task<RespostaConfirmacaoPermissao> ConfirmarAsync(PedidoPermissao pedido, CancellationToken cancellationToken = default)
    {
        var handler = Requested;
        if (handler is null)
        {
            return RespostaConfirmacaoPermissao.Negada;
        }

        var request = new PedidoPermissaoUi(pedido);
        await using var _ = cancellationToken.Register(() => request.Responder(RespostaConfirmacaoPermissao.Negada));

        await handler(request);
        return await request.Resposta;
    }
}

/// <summary>Um pedido de permissão pendente na UI, completado pelo overlay.</summary>
public sealed class PedidoPermissaoUi
{
    private readonly TaskCompletionSource<RespostaConfirmacaoPermissao> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public PedidoPermissaoUi(PedidoPermissao pedido) => Pedido = pedido;

    public PedidoPermissao Pedido { get; }

    public Task<RespostaConfirmacaoPermissao> Resposta => _tcs.Task;

    public void Responder(RespostaConfirmacaoPermissao resposta) => _tcs.TrySetResult(resposta);
}
