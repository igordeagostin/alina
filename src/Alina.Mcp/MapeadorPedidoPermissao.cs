using System.Text.Json;
using Alina.Core.Permissoes;

namespace Alina.Mcp;

/// <summary>
/// Converte a chamada de ferramenta que o Claude Code quer autorizar (nome + input) num
/// <see cref="PedidoPermissao"/> normalizado, extraindo comando e caminho dos campos usuais.
/// </summary>
internal static class MapeadorPedidoPermissao
{
    public static PedidoPermissao Mapear(string toolName, JsonElement input, string? diretorioAtual)
    {
        string ferramenta = string.IsNullOrWhiteSpace(toolName) ? "ferramenta" : toolName;
        string? comando = LerString(input, "command");
        string? caminho = LerString(input, "file_path")
                      ?? LerString(input, "path")
                      ?? LerString(input, "notebook_path");

        return new PedidoPermissao
        {
            Ferramenta = ferramenta,
            Comando = comando,
            Caminho = caminho,
            DiretorioTrabalho = diretorioAtual,
            Descricao = PermissaoPayload.DescreverPedido(ferramenta, input),
        };
    }

    private static string? LerString(JsonElement elemento, string propriedade)
        => elemento.ValueKind == JsonValueKind.Object &&
           elemento.TryGetProperty(propriedade, out JsonElement v) &&
           v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;
}
