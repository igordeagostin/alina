using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Alina.Core.Tools;
using Microsoft.Extensions.AI;

namespace Alina.Tools.ClaudeCode;

/// <summary>
/// Tool que delega uma tarefa de codificação ao Claude Code, executando-o em
/// modo headless (<c>claude -p --output-format json</c>) como subprocesso.
/// É o "braço" da Alina para escrever/alterar código, rodar testes, etc.
/// Operação crítica — exige confirmação do usuário.
/// </summary>
public sealed class ClaudeCodeTool : ToolBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly ClaudeCodeOptions _options;

    public ClaudeCodeTool(IConfirmationService confirmation, ClaudeCodeOptions options)
        : base(confirmation)
        => _options = options;

    public override string Name => "delegar_claude_code";

    public override string Description =>
        "Delega uma tarefa de desenvolvimento ao Claude Code (agente que lê e altera arquivos, " +
        "escreve código, roda testes e comandos no projeto). Informe a tarefa em linguagem natural " +
        "e, se necessário, o diretório do projeto. Exige confirmação do usuário.";

    public override bool RequiresConfirmation => true;

    public override AIFunction AsAIFunction() => AIFunctionFactory.Create(RunAsync, Name, Description);

    [Description("Executa uma tarefa de desenvolvimento no Claude Code e retorna o resumo do resultado.")]
    public async Task<string> RunAsync(
        [Description("A tarefa a ser executada pelo Claude Code, em linguagem natural.")] string task,
        [Description("Diretório do projeto onde a tarefa será executada (opcional).")] string? workingDirectory = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(task))
        {
            return "Erro: nenhuma tarefa informada para o Claude Code.";
        }

        var directory = ResolveWorkingDirectory(workingDirectory);
        if (directory is not null && !Directory.Exists(directory))
        {
            return $"Erro: diretório de trabalho não encontrado: {directory}";
        }

        var confirmed = await EnsureConfirmedAsync(
            "Delegar tarefa ao Claude Code (pode alterar arquivos e rodar comandos)",
            $"{task}\n   (dir: {directory ?? Environment.CurrentDirectory})",
            cancellationToken);

        if (!confirmed)
        {
            return "Operação cancelada pelo usuário — o Claude Code NÃO foi executado.";
        }

        return await ExecuteAsync(task, workingDirectory, cancellationToken);
    }

    /// <summary>
    /// Executa o Claude Code SEM pedir confirmação. Use quando a confirmação já foi
    /// obtida (ex: disparo em background). Retorna o resumo formatado do resultado.
    /// </summary>
    public async Task<string> ExecuteAsync(string task, string? workingDirectory, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(task))
        {
            return "Erro: nenhuma tarefa informada para o Claude Code.";
        }

        var directory = ResolveWorkingDirectory(workingDirectory);
        if (directory is not null && !Directory.Exists(directory))
        {
            return $"Erro: diretório de trabalho não encontrado: {directory}";
        }

        var psi = BuildStartInfo(directory);

        try
        {
            using var process = new Process { StartInfo = psi };
            var stdout = new StringBuilder();
            var stderr = new StringBuilder();

            process.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Passa a tarefa via stdin (evita problemas de escaping em prompts longos).
            await process.StandardInput.WriteAsync(task);
            process.StandardInput.Close();

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                return $"Erro: o Claude Code excedeu o tempo limite de {_options.TimeoutSeconds}s e foi encerrado.";
            }

            return FormatResult(stdout.ToString(), stderr.ToString(), process.ExitCode);
        }
        catch (Exception ex)
        {
            return $"Erro ao executar o Claude Code: {ex.Message}. " +
                   $"Verifique se o executável '{_options.Executable}' está no PATH ou configure 'ClaudeCode:Executable'.";
        }
    }

    private string? ResolveWorkingDirectory(string? requested)
    {
        if (!string.IsNullOrWhiteSpace(requested))
        {
            return requested;
        }

        return string.IsNullOrWhiteSpace(_options.DefaultWorkingDirectory)
            ? null
            : _options.DefaultWorkingDirectory;
    }

    private ProcessStartInfo BuildStartInfo(string? workingDirectory)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _options.Executable,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            psi.WorkingDirectory = workingDirectory;
        }

        psi.ArgumentList.Add("-p");
        psi.ArgumentList.Add("--output-format");
        psi.ArgumentList.Add("json");

        if (_options.SkipPermissions)
        {
            psi.ArgumentList.Add("--dangerously-skip-permissions");
        }
        else
        {
            psi.ArgumentList.Add("--permission-mode");
            psi.ArgumentList.Add(_options.PermissionMode);
        }

        if (_options.MaxTurns > 0)
        {
            psi.ArgumentList.Add("--max-turns");
            psi.ArgumentList.Add(_options.MaxTurns.ToString());
        }

        foreach (var arg in _options.ExtraArgs)
        {
            psi.ArgumentList.Add(arg);
        }

        return psi;
    }

    internal static string FormatResult(string stdout, string stderr, int exitCode)
    {
        ClaudeCodeResult? parsed = TryParse(stdout);

        if (parsed is null)
        {
            var raw = string.Join("\n", new[] { stdout, stderr }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim();
            var hint = DiagnoseHint(raw);
            var message = $"[Claude Code exit {exitCode}] Não foi possível interpretar a saída JSON.\n{raw}";
            return hint is null ? message : $"{message}\n\n{hint}";
        }

        var sb = new StringBuilder();

        if (parsed.IsError)
        {
            sb.AppendLine($"⚠ Claude Code retornou erro (subtype: {parsed.Subtype ?? "?"}).");
        }

        var body = string.IsNullOrWhiteSpace(parsed.Result) ? "(sem texto de resultado)" : parsed.Result!.Trim();
        sb.AppendLine(body);

        if (parsed.IsError)
        {
            var hint = DiagnoseHint($"{body}\n{stderr}\n{parsed.Subtype}");
            if (hint is not null)
            {
                sb.AppendLine($"\n{hint}");
            }

            if (!string.IsNullOrWhiteSpace(stderr))
            {
                sb.AppendLine($"\n(stderr: {stderr.Trim()})");
            }
        }

        if (parsed.PermissionDenials is { Count: > 0 })
        {
            sb.AppendLine($"\nⓘ {parsed.PermissionDenials.Count} ação(ões) foram bloqueadas por permissão.");
        }

        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var footer = new List<string>();
        if (parsed.NumTurns is { } turns) footer.Add($"{turns} turno(s)");
        if (parsed.DurationMs is { } ms) footer.Add(string.Create(inv, $"{ms / 1000.0:0.0}s"));
        if (parsed.TotalCostUsd is { } cost) footer.Add(string.Create(inv, $"${cost:0.0000}"));
        if (footer.Count > 0)
        {
            sb.AppendLine($"\n— {string.Join(" · ", footer)}");
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>Detecta causas comuns de falha e devolve uma dica acionável (ou null).</summary>
    internal static string? DiagnoseHint(string text)
    {
        var t = (text ?? string.Empty).ToLowerInvariant();

        if (t.Contains("login") || t.Contains("authenticat") || t.Contains("unauthor") ||
            t.Contains("api key") || t.Contains("api_key") || t.Contains("oauth") || t.Contains("credential"))
        {
            return "Dica: parece autenticação do Claude Code. No terminal, rode `claude` e faça login (`/login`), " +
                   "ou defina a variável de ambiente ANTHROPIC_API_KEY. Depois tente de novo.";
        }

        if (t.Contains("usage limit") || t.Contains("rate limit") || t.Contains("quota") ||
            t.Contains("credit balance") || t.Contains("overloaded") || t.Contains("429"))
        {
            return "Dica: limite de uso/cota atingido ou serviço sobrecarregado. Aguarde alguns minutos e tente " +
                   "novamente, ou verifique os limites da sua assinatura.";
        }

        return null;
    }

    private static ClaudeCodeResult? TryParse(string stdout)
    {
        if (string.IsNullOrWhiteSpace(stdout))
        {
            return null;
        }

        // A saída pode conter linhas extras; pega a última linha que é um objeto JSON.
        var line = stdout
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault(l => l.StartsWith('{') && l.EndsWith('}'));

        if (line is null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ClaudeCodeResult>(line, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
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
            // ignora falhas ao encerrar o processo.
        }
    }
}
