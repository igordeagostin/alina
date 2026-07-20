using System.Text.Json;
using System.Text.Json.Serialization;

namespace Alina.Tools.ClaudeCode;

/// <summary>
/// Mapeia o JSON retornado por <c>claude -p --output-format json</c>
/// (apenas os campos de interesse).
/// </summary>
internal sealed class ClaudeCodeResult
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("subtype")]
    public string? Subtype { get; set; }

    [JsonPropertyName("is_error")]
    public bool IsError { get; set; }

    [JsonPropertyName("result")]
    public string? Result { get; set; }

    [JsonPropertyName("total_cost_usd")]
    public double? TotalCostUsd { get; set; }

    [JsonPropertyName("num_turns")]
    public int? NumTurns { get; set; }

    [JsonPropertyName("duration_ms")]
    public long? DurationMs { get; set; }

    [JsonPropertyName("session_id")]
    public string? SessionId { get; set; }

    [JsonPropertyName("permission_denials")]
    public List<JsonElement>? PermissionDenials { get; set; }
}
