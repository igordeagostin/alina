using System.Text.Json;
using Alina.Core.Tools;

namespace Alina.Tools.Plugins;

/// <summary>Resultado do carregamento de plugins.</summary>
public sealed record PluginLoadResult(IReadOnlyList<PluginTool> Tools, IReadOnlyList<string> Warnings);

/// <summary>
/// Carrega plugins declarativos de uma pasta. Cada arquivo <c>*.plugin.json</c>
/// vira uma <see cref="PluginTool"/>. Arquivos inválidos geram avisos, não exceções.
/// </summary>
public static class PluginLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static PluginLoadResult Load(string directory, IConfirmationService confirmation)
    {
        var tools = new List<PluginTool>();
        var warnings = new List<string>();

        if (!Directory.Exists(directory))
        {
            return new PluginLoadResult(tools, warnings);
        }

        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in Directory.EnumerateFiles(directory, "*.plugin.json", SearchOption.TopDirectoryOnly))
        {
            PluginManifest? manifest;
            try
            {
                manifest = JsonSerializer.Deserialize<PluginManifest>(File.ReadAllText(file), JsonOptions);
            }
            catch (JsonException ex)
            {
                warnings.Add($"{Path.GetFileName(file)}: JSON inválido — {ex.Message}");
                continue;
            }

            var validation = Validate(manifest, seenNames);
            if (validation is not null)
            {
                warnings.Add($"{Path.GetFileName(file)}: {validation}");
                continue;
            }

            seenNames.Add(manifest!.Name);
            tools.Add(new PluginTool(manifest, confirmation));
        }

        return new PluginLoadResult(tools, warnings);
    }

    private static string? Validate(PluginManifest? manifest, HashSet<string> seenNames)
    {
        if (manifest is null)
        {
            return "manifesto vazio.";
        }

        if (string.IsNullOrWhiteSpace(manifest.Name))
        {
            return "campo 'name' obrigatório.";
        }

        if (string.IsNullOrWhiteSpace(manifest.Command))
        {
            return "campo 'command' obrigatório.";
        }

        if (seenNames.Contains(manifest.Name))
        {
            return $"nome de tool duplicado '{manifest.Name}'.";
        }

        return null;
    }
}
