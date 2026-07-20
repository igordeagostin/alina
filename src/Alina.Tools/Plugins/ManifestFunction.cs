using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Alina.Core.Tools;
using Microsoft.Extensions.AI;

namespace Alina.Tools.Plugins;

/// <summary>
/// <see cref="AIFunction"/> com schema dinâmico, gerada a partir de um
/// <see cref="PluginManifest"/>. Substitui os placeholders <c>{param}</c> nos
/// argumentos pelos valores do LLM, pede confirmação (se exigida) e executa o comando.
/// </summary>
internal sealed class ManifestFunction : AIFunction
{
    private readonly PluginManifest _manifest;
    private readonly IConfirmationService _confirmation;
    private readonly JsonElement _schema;

    public ManifestFunction(PluginManifest manifest, IConfirmationService confirmation)
    {
        _manifest = manifest;
        _confirmation = confirmation;
        _schema = BuildSchema(manifest);
    }

    public override string Name => _manifest.Name;

    public override string Description => _manifest.Description;

    public override JsonElement JsonSchema => _schema;

    protected override async ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in _manifest.Parameters)
        {
            arguments.TryGetValue(parameter.Name, out var raw);
            var value = ToStringValue(raw);

            if (parameter.Required && string.IsNullOrWhiteSpace(value))
            {
                return $"Erro: parâmetro obrigatório '{parameter.Name}' não informado.";
            }

            values[parameter.Name] = value;
        }

        var args = _manifest.Args.Select(a => Substitute(a, values)).ToArray();
        var workingDir = Substitute(_manifest.WorkingDirectory, values);

        if (_manifest.RequiresConfirmation)
        {
            var preview = $"{_manifest.Command} {string.Join(' ', args)}";
            var confirmed = await _confirmation.ConfirmAsync($"Executar plugin '{_manifest.Name}'", preview, cancellationToken);
            if (!confirmed)
            {
                return $"Operação cancelada pelo usuário — o plugin '{_manifest.Name}' NÃO foi executado.";
            }
        }

        return await RunProcessAsync(_manifest.Command, args, workingDir, _manifest.TimeoutSeconds, cancellationToken);
    }

    private static JsonElement BuildSchema(PluginManifest manifest)
    {
        var properties = new JsonObject();
        var required = new JsonArray();

        foreach (var parameter in manifest.Parameters)
        {
            properties[parameter.Name] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = parameter.Description,
            };

            if (parameter.Required)
            {
                required.Add(parameter.Name);
            }
        }

        var schema = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties,
        };

        if (required.Count > 0)
        {
            schema["required"] = required;
        }

        return JsonSerializer.SerializeToElement(schema);
    }

    private static string Substitute(string? template, IReadOnlyDictionary<string, string> values)
    {
        if (string.IsNullOrEmpty(template))
        {
            return string.Empty;
        }

        var result = template;
        foreach (var (key, value) in values)
        {
            result = result.Replace("{" + key + "}", value, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }

    private static string ToStringValue(object? raw) => raw switch
    {
        null => string.Empty,
        string s => s,
        JsonElement { ValueKind: JsonValueKind.String } e => e.GetString() ?? string.Empty,
        JsonElement e => e.ToString(),
        _ => raw.ToString() ?? string.Empty,
    };

    private static async Task<string> RunProcessAsync(
        string command, string[] args, string? workingDirectory, int timeoutSeconds, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = command,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            psi.WorkingDirectory = workingDirectory;
        }

        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        try
        {
            using var process = new Process { StartInfo = psi };
            var output = new StringBuilder();

            process.OutputDataReceived += (_, e) => { if (e.Data is not null) output.AppendLine(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data is not null) output.AppendLine(e.Data); };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                return $"Erro: o plugin excedeu {timeoutSeconds}s e foi encerrado.";
            }

            var text = output.ToString().TrimEnd();
            return $"[exit {process.ExitCode}]\n{text}";
        }
        catch (Exception ex)
        {
            return $"Erro ao executar o plugin: {ex.Message}. Verifique se '{command}' está no PATH.";
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // ignora
        }
    }
}
