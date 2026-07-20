using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Alina.Core.Tools;
using Microsoft.Extensions.AI;

namespace Alina.Tools;

/// <summary>
/// Tool de terminal: executa comandos de shell. Operação crítica — exige
/// confirmação explícita do usuário (SIM/NÃO) antes de rodar.
/// Responsabilidade: rodar comandos, testes, builds.
/// </summary>
public sealed class TerminalTool : ToolBase
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(2);

    public TerminalTool(IConfirmationService confirmation) : base(confirmation) { }

    public override string Name => "executar_terminal";

    public override string Description =>
        "Executa um comando no terminal (PowerShell no Windows) e retorna a saída. Use para rodar testes, builds ou comandos do sistema. Exige confirmação do usuário.";

    public override bool RequiresConfirmation => true;

    public override AIFunction AsAIFunction() =>
        AIFunctionFactory.Create(RunAsync, Name, Description);

    [Description("Executa um comando de terminal e retorna a saída (stdout + stderr).")]
    public async Task<string> RunAsync(
        [Description("O comando de shell a ser executado.")] string command,
        [Description("Diretório de trabalho (opcional).")] string? workingDirectory = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return "Erro: comando vazio.";
        }

        var confirmed = await EnsureConfirmedAsync("Executar comando no terminal", command, cancellationToken);
        if (!confirmed)
        {
            return "Operação cancelada pelo usuário — o comando NÃO foi executado.";
        }

        var (fileName, arguments) = BuildShellInvocation(command);

        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
                ? Environment.CurrentDirectory
                : workingDirectory,
        };

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
            timeoutCts.CancelAfter(DefaultTimeout);

            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                return $"Erro: comando excedeu o tempo limite de {DefaultTimeout.TotalSeconds:0} segundos e foi encerrado.\n{output}";
            }

            var result = output.ToString().TrimEnd();
            return $"[exit code {process.ExitCode}]\n{result}";
        }
        catch (Exception ex)
        {
            return $"Erro ao executar o comando: {ex.Message}";
        }
    }

    private static (string FileName, string Arguments) BuildShellInvocation(string command)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Escapa aspas duplas para passar o comando como um único argumento ao PowerShell.
            var escaped = command.Replace("\"", "\\\"");
            return ("powershell.exe", $"-NoProfile -NonInteractive -Command \"{escaped}\"");
        }

        var bashEscaped = command.Replace("\"", "\\\"");
        return ("/bin/bash", $"-c \"{bashEscaped}\"");
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
