using Alina.Core.Tools;
using Alina.Tools;

namespace Alina.Tests;

public sealed class ToolRegistryTests
{
    [Fact]
    public void Registra_e_expoe_tools_como_AIFunctions()
    {
        FakeConfirmationService confirmation = new FakeConfirmationService(result: true);
        ITool[] tools = new ITool[] { new TerminalTool(confirmation), new FileReadTool(confirmation) };

        ToolRegistry registry = new ToolRegistry(tools);

        Assert.Equal(2, registry.Tools.Count);
        Assert.Equal(2, registry.AsAIFunctions().Count);
    }

    [Fact]
    public void Find_localiza_tool_por_nome_ignorando_caixa()
    {
        FakeConfirmationService confirmation = new FakeConfirmationService(result: true);
        ToolRegistry registry = new ToolRegistry(new ITool[] { new TerminalTool(confirmation), new FileReadTool(confirmation) });

        Assert.NotNull(registry.Find("EXECUTAR_TERMINAL"));
        Assert.NotNull(registry.Find("ler_arquivo"));
        Assert.Null(registry.Find("inexistente"));
    }

    [Fact]
    public void Terminal_exige_confirmacao_e_leitura_nao()
    {
        FakeConfirmationService confirmation = new FakeConfirmationService(result: true);

        Assert.True(new TerminalTool(confirmation).RequiresConfirmation);
        Assert.False(new FileReadTool(confirmation).RequiresConfirmation);
    }
}
