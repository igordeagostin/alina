using Alina.Tools;

namespace Alina.Tests;

public sealed class ConfirmationGatingTests
{
    [Fact]
    public async Task Terminal_nao_executa_quando_confirmacao_negada()
    {
        var confirmation = new FakeConfirmationService(result: false);
        var tool = new TerminalTool(confirmation);

        // Comando que criaria um arquivo — não deve rodar.
        var marker = Path.Combine(Path.GetTempPath(), $"alina-nao-deve-existir-{Guid.NewGuid():n}.txt");
        var result = await tool.RunAsync($"Set-Content -Path '{marker}' -Value x");

        Assert.Equal(1, confirmation.Calls);
        Assert.Contains("cancelada", result, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(marker));
    }

    [Fact]
    public async Task Terminal_executa_quando_confirmacao_aprovada()
    {
        var confirmation = new FakeConfirmationService(result: true);
        var tool = new TerminalTool(confirmation);

        var result = await tool.RunAsync("echo alina-ok");

        Assert.Equal(1, confirmation.Calls);
        Assert.Contains("alina-ok", result, StringComparison.OrdinalIgnoreCase);
    }
}
