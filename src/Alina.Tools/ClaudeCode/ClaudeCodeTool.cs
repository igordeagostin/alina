using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Alina.Core.Tools;
using Microsoft.Extensions.AI;

namespace Alina.Tools.ClaudeCode;

/// <summary>
/// Tool que delega uma tarefa de codificação ao Claude Code, executando-o como
/// subprocesso. Em modo streaming (<c>claude -p --output-format stream-json</c>) acompanha
/// a execução evento a evento e reporta o progresso via <see cref="Progresso"/>; caso
/// contrário usa o modo <c>--output-format json</c> (resultado apenas no fim).
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
    private readonly IServidorPermissao? _servidorPermissao;

    public ClaudeCodeTool(IConfirmationService confirmation, ClaudeCodeOptions options, IServidorPermissao? servidorPermissao = null)
        : base(confirmation)
    {
        _options = options;
        _servidorPermissao = servidorPermissao;
    }

    public override string Name => "delegar_claude_code";

    public override string Description =>
        "Delega uma tarefa de desenvolvimento ao Claude Code (agente que lê e altera arquivos, " +
        "escreve código, roda testes e comandos no projeto). Informe a tarefa em linguagem natural " +
        "e, se necessário, o diretório do projeto. Exige confirmação do usuário.";

    public override bool RequiresConfirmation => true;

    /// <summary>
    /// Disparado a cada evento de progresso durante a execução em streaming. Os heads de UI
    /// (e a voz) assinam para acompanhar a tarefa em tempo real.
    /// </summary>
    public event Action<EventoProgressoClaudeCode>? Progresso;

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

        var permissao = await ResolverPermissaoAsync(cancellationToken);
        var psi = BuildStartInfo(directory, permissao);

        try
        {
            using var process = new Process { StartInfo = psi };
            process.Start();

            // Passa a tarefa via stdin (evita problemas de escaping em prompts longos).
            await process.StandardInput.WriteAsync(task);
            process.StandardInput.Close();

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

            return _options.Streaming
                ? await LerStreamingAsync(process, timeoutCts.Token)
                : await LerJsonUnicoAsync(process, timeoutCts.Token);
        }
        catch (Exception ex)
        {
            return $"Erro ao executar o Claude Code: {ex.Message}. " +
                   $"Verifique se o executável '{_options.Executable}' está no PATH ou configure 'ClaudeCode:Executable'.";
        }
    }

    private async Task<string> LerStreamingAsync(Process process, CancellationToken cancellationToken)
    {
        var stderr = new StringBuilder();
        var stderrTask = Task.Run(async () =>
        {
            string? linha;
            while ((linha = await process.StandardError.ReadLineAsync(CancellationToken.None)) is not null)
            {
                stderr.AppendLine(linha);
            }
        });

        var acumulado = new ResultadoAcumulado();
        void OnEvento(EventoProgressoClaudeCode e) => Progresso?.Invoke(e);

        try
        {
            string? linha;
            while ((linha = await process.StandardOutput.ReadLineAsync(cancellationToken)) is not null)
            {
                ProcessarEventoStreaming(linha, acumulado, OnEvento);
            }

            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            return $"Erro: o Claude Code excedeu o tempo limite de {_options.TimeoutSeconds}s e foi encerrado.";
        }

        await stderrTask;
        return MontarResumo(acumulado, stderr.ToString(), process.ExitCode);
    }

    /// <summary>
    /// Interpreta uma sequência de linhas de <c>--output-format stream-json</c>, emitindo os
    /// eventos de progresso e devolvendo o resumo final. Extraído para permitir teste sem
    /// subprocesso.
    /// </summary>
    internal static string InterpretarStreaming(
        IEnumerable<string> linhas,
        string stderr,
        int exitCode,
        Action<EventoProgressoClaudeCode>? onEvento)
    {
        var acumulado = new ResultadoAcumulado();
        foreach (var linha in linhas)
        {
            ProcessarEventoStreaming(linha, acumulado, onEvento);
        }

        return MontarResumo(acumulado, stderr, exitCode);
    }

    private async Task<string> LerJsonUnicoAsync(Process process, CancellationToken cancellationToken)
    {
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            return $"Erro: o Claude Code excedeu o tempo limite de {_options.TimeoutSeconds}s e foi encerrado.";
        }

        return FormatResult(await stdoutTask, await stderrTask, process.ExitCode);
    }

    private static void ProcessarEventoStreaming(string linha, ResultadoAcumulado acumulado, Action<EventoProgressoClaudeCode>? onEvento)
    {
        if (string.IsNullOrWhiteSpace(linha))
        {
            return;
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(linha);
        }
        catch (JsonException)
        {
            return;
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("type", out var tipoEl) ||
                tipoEl.ValueKind != JsonValueKind.String)
            {
                return;
            }

            switch (tipoEl.GetString())
            {
                case "system":
                    if (LerString(root, "subtype") == "init")
                    {
                        Emitir(onEvento, TipoEventoClaudeCode.Inicio, "Claude Code iniciado.");
                    }
                    break;

                case "assistant":
                    ProcessarConteudoAssistente(root, onEvento);
                    break;

                case "user":
                    ProcessarResultadoFerramenta(root, onEvento);
                    break;

                case "result":
                    acumulado.Subtype = LerString(root, "subtype");
                    acumulado.IsError = root.TryGetProperty("is_error", out var errEl) &&
                                        errEl.ValueKind is JsonValueKind.True;
                    acumulado.Result = LerString(root, "result");
                    if (root.TryGetProperty("total_cost_usd", out var custoEl) && custoEl.ValueKind is JsonValueKind.Number)
                    {
                        acumulado.Cost = custoEl.GetDouble();
                    }
                    if (root.TryGetProperty("num_turns", out var turnosEl) && turnosEl.ValueKind is JsonValueKind.Number)
                    {
                        acumulado.Turns = turnosEl.GetInt32();
                    }
                    if (root.TryGetProperty("duration_ms", out var durEl) && durEl.ValueKind is JsonValueKind.Number)
                    {
                        acumulado.DurationMs = durEl.GetInt64();
                    }
                    if (root.TryGetProperty("permission_denials", out var denEl) && denEl.ValueKind is JsonValueKind.Array)
                    {
                        acumulado.PermissionDenials = denEl.GetArrayLength();
                    }
                    Emitir(onEvento, TipoEventoClaudeCode.Fim, acumulado.Result ?? "(sem texto de resultado)");
                    break;
            }
        }
    }

    private static void ProcessarConteudoAssistente(JsonElement root, Action<EventoProgressoClaudeCode>? onEvento)
    {
        if (!root.TryGetProperty("message", out var msg) ||
            !msg.TryGetProperty("content", out var conteudo) ||
            conteudo.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var bloco in conteudo.EnumerateArray())
        {
            switch (LerString(bloco, "type"))
            {
                case "text":
                    var texto = LerString(bloco, "text");
                    if (!string.IsNullOrWhiteSpace(texto))
                    {
                        Emitir(onEvento, TipoEventoClaudeCode.Texto, texto!.Trim());
                    }
                    break;

                case "tool_use":
                    Emitir(onEvento, TipoEventoClaudeCode.Ferramenta, DescreverFerramenta(bloco));
                    break;
            }
        }
    }

    private static void ProcessarResultadoFerramenta(JsonElement root, Action<EventoProgressoClaudeCode>? onEvento)
    {
        if (!root.TryGetProperty("message", out var msg) ||
            !msg.TryGetProperty("content", out var conteudo) ||
            conteudo.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var bloco in conteudo.EnumerateArray())
        {
            if (LerString(bloco, "type") != "tool_result")
            {
                continue;
            }

            var preview = ExtrairTextoResultado(bloco);
            if (!string.IsNullOrWhiteSpace(preview))
            {
                Emitir(onEvento, TipoEventoClaudeCode.ResultadoFerramenta, Resumir(preview!, 200));
            }
        }
    }

    private static string DescreverFerramenta(JsonElement toolUse)
    {
        var nome = LerString(toolUse, "name") ?? "ferramenta";
        if (!toolUse.TryGetProperty("input", out var input) || input.ValueKind != JsonValueKind.Object)
        {
            return $"Usando {nome}…";
        }

        // Campos mais comuns que ajudam a descrever a ação sem despejar o input inteiro.
        foreach (var chave in new[] { "command", "file_path", "path", "pattern", "url", "description" })
        {
            var valor = LerString(input, chave);
            if (!string.IsNullOrWhiteSpace(valor))
            {
                return $"Usando {nome}: {Resumir(valor!, 160)}";
            }
        }

        return $"Usando {nome}…";
    }

    private static string ExtrairTextoResultado(JsonElement toolResult)
    {
        if (!toolResult.TryGetProperty("content", out var conteudo))
        {
            return string.Empty;
        }

        if (conteudo.ValueKind == JsonValueKind.String)
        {
            return conteudo.GetString() ?? string.Empty;
        }

        if (conteudo.ValueKind == JsonValueKind.Array)
        {
            var sb = new StringBuilder();
            foreach (var bloco in conteudo.EnumerateArray())
            {
                var texto = LerString(bloco, "text");
                if (!string.IsNullOrWhiteSpace(texto))
                {
                    sb.Append(texto);
                }
            }
            return sb.ToString();
        }

        return string.Empty;
    }

    private static void Emitir(Action<EventoProgressoClaudeCode>? onEvento, TipoEventoClaudeCode tipo, string texto)
        => onEvento?.Invoke(new EventoProgressoClaudeCode(tipo, texto));

    private static string? LerString(JsonElement elemento, string propriedade)
        => elemento.ValueKind == JsonValueKind.Object &&
           elemento.TryGetProperty(propriedade, out var v) &&
           v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    private static string Resumir(string texto, int limite)
    {
        var t = texto.Replace('\n', ' ').Replace('\r', ' ').Trim();
        return t.Length <= limite ? t : t[..limite] + "…";
    }

    private static string MontarResumo(ResultadoAcumulado acumulado, string stderr, int exitCode)
    {
        var sb = new StringBuilder();

        if (acumulado.Result is null && string.IsNullOrWhiteSpace(stderr) && acumulado.Turns is null)
        {
            var raw = stderr.Trim();
            var hint0 = DiagnoseHint(raw);
            var msg = $"[Claude Code exit {exitCode}] Não foi possível interpretar a saída do streaming.";
            return hint0 is null ? msg : $"{msg}\n\n{hint0}";
        }

        if (acumulado.IsError)
        {
            sb.AppendLine($"⚠ Claude Code retornou erro (subtype: {acumulado.Subtype ?? "?"}).");
        }

        var body = string.IsNullOrWhiteSpace(acumulado.Result) ? "(sem texto de resultado)" : acumulado.Result!.Trim();
        sb.AppendLine(body);

        if (acumulado.IsError)
        {
            var hint = DiagnoseHint($"{body}\n{stderr}\n{acumulado.Subtype}");
            if (hint is not null)
            {
                sb.AppendLine($"\n{hint}");
            }

            if (!string.IsNullOrWhiteSpace(stderr))
            {
                sb.AppendLine($"\n(stderr: {stderr.Trim()})");
            }
        }

        if (acumulado.PermissionDenials > 0)
        {
            sb.AppendLine($"\nⓘ {acumulado.PermissionDenials} ação(ões) foram bloqueadas por permissão.");
        }

        AppendRodape(sb, acumulado.Turns, acumulado.DurationMs, acumulado.Cost);
        return sb.ToString().TrimEnd();
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

    private async Task<ConfigPermissao?> ResolverPermissaoAsync(CancellationToken cancellationToken)
    {
        if (!_options.PermissaoInterativa || _servidorPermissao is null)
        {
            return null;
        }

        var url = await _servidorPermissao.IniciarAsync(cancellationToken);
        return new ConfigPermissao(url, _servidorPermissao.NomeServidor, _servidorPermissao.NomeFerramenta);
    }

    private ProcessStartInfo BuildStartInfo(string? workingDirectory, ConfigPermissao? permissao)
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
        psi.ArgumentList.Add(_options.Streaming ? "stream-json" : "json");

        if (_options.Streaming)
        {
            psi.ArgumentList.Add("--verbose");
        }

        if (permissao is not null)
        {
            // Permissão interativa: cada ação passa pela ferramenta MCP da Alina, que pergunta
            // ao usuário. Não pré-aprova nada (deixa o modo padrão), para tudo ser perguntado.
            psi.ArgumentList.Add("--mcp-config");
            psi.ArgumentList.Add(MontarMcpConfig(permissao));
            psi.ArgumentList.Add("--permission-prompt-tool");
            psi.ArgumentList.Add(permissao.NomeFerramenta);
        }
        else if (_options.SkipPermissions)
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

    internal static string MontarMcpConfig(ConfigPermissao permissao)
    {
        var config = new
        {
            mcpServers = new Dictionary<string, object>
            {
                [permissao.NomeServidor] = new { type = "http", url = permissao.Url },
            },
        };
        return JsonSerializer.Serialize(config);
    }

    internal sealed record ConfigPermissao(string Url, string NomeServidor, string NomeFerramenta);

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

        AppendRodape(sb, parsed.NumTurns, parsed.DurationMs, parsed.TotalCostUsd);
        return sb.ToString().TrimEnd();
    }

    private static void AppendRodape(StringBuilder sb, int? turns, long? durationMs, double? cost)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var footer = new List<string>();
        if (turns is { } t) footer.Add($"{t} turno(s)");
        if (durationMs is { } ms) footer.Add(string.Create(inv, $"{ms / 1000.0:0.0}s"));
        if (cost is { } c) footer.Add(string.Create(inv, $"${c:0.0000}"));
        if (footer.Count > 0)
        {
            sb.AppendLine($"\n— {string.Join(" · ", footer)}");
        }
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

    internal sealed class ResultadoAcumulado
    {
        public string? Result { get; set; }
        public bool IsError { get; set; }
        public string? Subtype { get; set; }
        public double? Cost { get; set; }
        public int? Turns { get; set; }
        public long? DurationMs { get; set; }
        public int PermissionDenials { get; set; }
    }
}
