using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Alina.Core.Tools;
using Microsoft.Extensions.AI;

namespace Alina.Tools;

/// <summary>
/// Abre uma pasta existente numa nova janela do VS Code. Operação segura (não roda
/// shell arbitrário, apenas invoca o executável do VS Code sobre um diretório que
/// precisa existir), por isso não exige confirmação.
/// </summary>
public sealed class AbrirNoVsCodeTool : ToolBase
{
    public AbrirNoVsCodeTool(IConfirmationService confirmation) : base(confirmation) { }

    public override string Name => "abrir_no_vscode";

    public override string Description =>
        "Abre uma pasta existente em uma NOVA janela do VS Code. Passe o caminho absoluto do projeto. " +
        "Use para atender pedidos como \"abra o projeto X\".";

    public override AIFunction AsAIFunction() =>
        AIFunctionFactory.Create(Abrir, Name, Description);

    [Description("Abre a pasta informada numa nova janela do VS Code.")]
    public string Abrir(
        [Description("Caminho absoluto da pasta do projeto a abrir.")] string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "Erro: caminho do projeto não informado.";
        }

        if (!Directory.Exists(path))
        {
            return $"Erro: pasta não encontrada: {path}";
        }

        (string executavel, string argumentos) = MontarInvocacao(path);

        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = executavel,
            Arguments = argumentos,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        try
        {
            Process.Start(psi);
            return $"VS Code aberto em: {path}";
        }
        catch (Exception ex)
        {
            return $"Erro ao abrir o VS Code ({executavel}): {ex.Message}. " +
                "Confirme se o comando 'code' está no PATH.";
        }
    }

    private static (string Executavel, string Argumentos) MontarInvocacao(string path)
    {
        string argumentos = $"-n \"{path}\"";

        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? ("cmd.exe", $"/c code {argumentos}")
            : ("code", argumentos);
    }
}
