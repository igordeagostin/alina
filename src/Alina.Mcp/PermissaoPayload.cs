using System.Text.Json;
using System.Text.Json.Nodes;

namespace Alina.Mcp;

/// <summary>
/// Formata os pedidos e respostas do protocolo <c>--permission-prompt-tool</c> do Claude Code.
/// A resposta deve ser um JSON com <c>behavior</c> "allow" (com <c>updatedInput</c>) ou "deny"
/// (com <c>message</c>). Isolado da infraestrutura para permitir teste direto.
/// </summary>
internal static class PermissaoPayload
{
    private static readonly string[] CamposDescritivos =
        ["command", "file_path", "path", "pattern", "url", "description", "prompt"];

    /// <summary>Monta uma descrição legível do que o Claude Code quer fazer.</summary>
    public static string DescreverPedido(string toolName, JsonElement input)
    {
        var nome = string.IsNullOrWhiteSpace(toolName) ? "ferramenta" : toolName;

        if (input.ValueKind == JsonValueKind.Object)
        {
            foreach (var campo in CamposDescritivos)
            {
                if (input.TryGetProperty(campo, out var v) && v.ValueKind == JsonValueKind.String)
                {
                    var valor = v.GetString();
                    if (!string.IsNullOrWhiteSpace(valor))
                    {
                        return $"O Claude Code quer usar {nome}: {Resumir(valor!, 200)}";
                    }
                }
            }
        }

        return $"O Claude Code quer usar {nome}.";
    }

    /// <summary>Resposta de autorização, devolvendo o input (inalterado) como <c>updatedInput</c>.</summary>
    public static string RespostaPermitir(JsonElement input)
    {
        var updated = input.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
            ? new JsonObject()
            : JsonNode.Parse(input.GetRawText());

        var obj = new JsonObject
        {
            ["behavior"] = "allow",
            ["updatedInput"] = updated,
        };
        return obj.ToJsonString();
    }

    /// <summary>Resposta de negação, com o motivo exibido ao agente.</summary>
    public static string RespostaNegar(string motivo)
    {
        var obj = new JsonObject
        {
            ["behavior"] = "deny",
            ["message"] = string.IsNullOrWhiteSpace(motivo) ? "Negado pelo usuário." : motivo,
        };
        return obj.ToJsonString();
    }

    private static string Resumir(string texto, int limite)
    {
        var t = texto.Replace('\n', ' ').Replace('\r', ' ').Trim();
        return t.Length <= limite ? t : t[..limite] + "…";
    }
}
