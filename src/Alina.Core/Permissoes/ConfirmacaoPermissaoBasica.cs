using Alina.Core.Tools;

namespace Alina.Core.Permissoes;

/// <summary>
/// Adapta um <see cref="IConfirmationService"/> simples (sim/não) para a confirmação de
/// permissão: pergunta e devolve sempre escopo <see cref="EscopoPermissao.UmaVez"/>. Serve de
/// fallback onde a UI ainda não oferece as opções de "permitir sempre".
/// </summary>
public sealed class ConfirmacaoPermissaoBasica : IConfirmacaoPermissao
{
    private readonly IConfirmationService _confirmacao;

    public ConfirmacaoPermissaoBasica(IConfirmationService confirmacao) => _confirmacao = confirmacao;

    public async Task<RespostaConfirmacaoPermissao> ConfirmarAsync(PedidoPermissao pedido, CancellationToken cancellationToken = default)
    {
        bool autorizado = await _confirmacao.ConfirmAsync("Permissão solicitada pelo Claude Code", pedido.Descricao, cancellationToken);
        return autorizado ? RespostaConfirmacaoPermissao.PermitidaUmaVez : RespostaConfirmacaoPermissao.Negada;
    }
}
