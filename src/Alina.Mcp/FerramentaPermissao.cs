using System.ComponentModel;
using System.Text.Json;
using Alina.Core.Tools;
using ModelContextProtocol.Server;

namespace Alina.Mcp;

/// <summary>
/// Ferramenta MCP que o Claude Code chama (via <c>--permission-prompt-tool</c>) sempre que
/// precisa de permissão em modo headless. Encaminha o pedido ao <see cref="IConfirmationService"/>
/// — que na Alina pergunta ao usuário (voz/UI/console) — e devolve a decisão no formato esperado.
/// </summary>
[McpServerToolType]
internal sealed class FerramentaPermissao
{
    [McpServerTool(Name = "aprovar")]
    [Description("Decide se uma ação do Claude Code pode ser executada, perguntando ao usuário.")]
    public static async Task<string> AprovarAsync(
        IConfirmationService confirmacao,
        [Description("Nome da ferramenta que o Claude Code quer usar.")] string tool_name,
        [Description("Entrada (argumentos) da ferramenta.")] JsonElement input,
        CancellationToken cancellationToken)
    {
        var pedido = PermissaoPayload.DescreverPedido(tool_name, input);
        var autorizado = await confirmacao.ConfirmAsync("Permissão solicitada pelo Claude Code", pedido, cancellationToken);

        return autorizado
            ? PermissaoPayload.RespostaPermitir(input)
            : PermissaoPayload.RespostaNegar("O usuário não autorizou esta ação.");
    }
}
