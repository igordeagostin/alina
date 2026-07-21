namespace Alina.Core.Permissoes;

/// <summary>
/// Pede ao usuário uma decisão de permissão com escopo (uma vez, sessão, sempre, sempre neste
/// diretório). É a versão enriquecida da confirmação, usada quando a política não resolve o
/// pedido automaticamente. As UIs implementam conforme sua capacidade; quando não suportam
/// escopo, devolvem <see cref="EscopoPermissao.UmaVez"/>.
/// </summary>
public interface IConfirmacaoPermissao
{
    Task<RespostaConfirmacaoPermissao> ConfirmarAsync(PedidoPermissao pedido, CancellationToken cancellationToken = default);
}
