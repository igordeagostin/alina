using System.ComponentModel;
using System.Text.Json;
using Alina.Core.Permissoes;
using ModelContextProtocol.Server;

namespace Alina.Mcp;

/// <summary>
/// Ferramenta MCP que o Claude Code chama (via <c>--permission-prompt-tool</c>) sempre que
/// precisa de permissão em modo headless. Primeiro consulta a <see cref="IPoliticaPermissao"/>
/// (que pode liberar/negar automaticamente, evitando interromper o usuário); só quando a política
/// deixa em aberto é que pergunta — e, então, aprende a decisão conforme o escopo escolhido.
/// </summary>
[McpServerToolType]
internal sealed class FerramentaPermissao
{
    [McpServerTool(Name = "aprovar")]
    [Description("Decide se uma ação do Claude Code pode ser executada, conforme a política e o usuário.")]
    public static async Task<string> AprovarAsync(
        IPoliticaPermissao politica,
        IConfirmacaoPermissao confirmacao,
        IContextoPermissao contexto,
        [Description("Nome da ferramenta que o Claude Code quer usar.")] string tool_name,
        [Description("Entrada (argumentos) da ferramenta.")] JsonElement input,
        CancellationToken cancellationToken)
    {
        PedidoPermissao pedido = MapeadorPedidoPermissao.Mapear(tool_name, input, contexto.DiretorioAtual);

        DecisaoPermissao decisao = politica.Avaliar(pedido);
        switch (decisao)
        {
            case DecisaoPermissao.Permitir:
                return PermissaoPayload.RespostaPermitir(input);
            case DecisaoPermissao.Negar:
                return PermissaoPayload.RespostaNegar("Bloqueado por uma regra de permissão.");
        }

        RespostaConfirmacaoPermissao resposta = await confirmacao.ConfirmarAsync(pedido, cancellationToken);
        politica.Aprender(pedido, resposta);

        return resposta.Permitido
            ? PermissaoPayload.RespostaPermitir(input)
            : PermissaoPayload.RespostaNegar("O usuário não autorizou esta ação.");
    }
}
