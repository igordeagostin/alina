using System.Diagnostics;
using System.Text;

namespace Alina.Tools.Git;

/// <summary>Executa comandos <c>git</c> como subprocesso e devolve a saída combinada.</summary>
internal static class GitCommandRunner
{
    public static async Task<GitCommandResult> RunAsync(
        GitOptions options,
        string? workingDirectory,
        CancellationToken cancellationToken,
        params string[] arguments)
    {
        var directory = string.IsNullOrWhiteSpace(workingDirectory)
            ? (string.IsNullOrWhiteSpace(options.DefaultRepository) ? Environment.CurrentDirectory : options.DefaultRepository!)
            : workingDirectory;

        if (!Directory.Exists(directory))
        {
            return new GitCommandResult(-1, $"Erro: diretório do repositório não encontrado: {directory}");
        }

        var psi = new ProcessStartInfo
        {
            FileName = options.Executable,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = directory,
        };

        foreach (var arg in arguments)
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
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));

            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                return new GitCommandResult(-1, $"Erro: comando git excedeu {options.TimeoutSeconds}s.");
            }

            var text = Truncate(output.ToString().TrimEnd(), options.MaxOutputChars);
            return new GitCommandResult(process.ExitCode, text);
        }
        catch (Exception ex)
        {
            return new GitCommandResult(-1,
                $"Erro ao executar o git: {ex.Message}. Verifique se '{options.Executable}' está no PATH.");
        }
    }

    private static string Truncate(string text, int max)
    {
        if (max <= 0 || text.Length <= max)
        {
            return text;
        }

        return text[..max] + $"\n… (saída truncada em {max} caracteres)";
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

internal readonly record struct GitCommandResult(int ExitCode, string Output)
{
    public bool Success => ExitCode == 0;

    /// <summary>Formata para devolver ao LLM, sinalizando falhas.</summary>
    public string ToToolResult()
    {
        if (Success)
        {
            return string.IsNullOrWhiteSpace(Output) ? "(ok, sem saída)" : Output;
        }

        return $"[git falhou, exit {ExitCode}]\n{Output}";
    }
}
