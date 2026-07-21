using Alina.Core.Permissoes;

namespace Alina.Console;

/// <summary>
/// Roteia a confirmação de permissão para voz ou console conforme o modo ativo, reutilizando o
/// mesmo estado de <see cref="ConfirmacaoRoteada.ModoVoz"/> das confirmações genéricas.
/// </summary>
public sealed class ConfirmacaoPermissaoRoteada : IConfirmacaoPermissao
{
    private readonly ConfirmacaoRoteada _estado;
    private readonly IConfirmacaoPermissao _console;
    private IConfirmacaoPermissao? _voz;

    public ConfirmacaoPermissaoRoteada(ConfirmacaoRoteada estado, IConfirmacaoPermissao console)
    {
        _estado = estado;
        _console = console;
    }

    /// <summary>Define a confirmação por voz (criada sob demanda pela camada de voz).</summary>
    public void DefinirVoz(IConfirmacaoPermissao voz) => _voz = voz;

    public Task<RespostaConfirmacaoPermissao> ConfirmarAsync(PedidoPermissao pedido, CancellationToken cancellationToken = default)
        => (_estado.ModoVoz && _voz is not null ? _voz : _console).ConfirmarAsync(pedido, cancellationToken);
}
