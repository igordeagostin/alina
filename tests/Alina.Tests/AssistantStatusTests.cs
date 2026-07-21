using Alina.Core.Orchestration;
using Alina.Core.Tools;
using Microsoft.Extensions.AI;

namespace Alina.Tests;

public sealed class AssistantStatusTests
{
    [Fact]
    public void Comeca_ocioso()
    {
        Assert.Equal(AssistantState.Idle, new AssistantStatus().Current);
    }

    [Fact]
    public void Set_altera_estado_e_dispara_evento()
    {
        var status = new AssistantStatus();
        var recebidos = new List<AssistantState>();
        status.Changed += (_, s) => recebidos.Add(s);

        status.Set(AssistantState.Thinking);

        Assert.Equal(AssistantState.Thinking, status.Current);
        Assert.Equal(new[] { AssistantState.Thinking }, recebidos);
    }

    [Fact]
    public void Set_para_o_mesmo_estado_nao_dispara_evento()
    {
        var status = new AssistantStatus();
        status.Set(AssistantState.Listening);

        var disparos = 0;
        status.Changed += (_, _) => disparos++;
        status.Set(AssistantState.Listening);

        Assert.Equal(0, disparos);
    }

    [Fact]
    public async Task Tool_marca_Executing_durante_a_execucao_e_restaura_o_estado()
    {
        var status = new AssistantStatus();
        AssistantState? duranteExecucao = null;

        var tool = new SondaTool(() => duranteExecucao = status.Current);
        var registry = new ToolRegistry(new ITool[] { tool }, status);

        status.Set(AssistantState.Thinking);
        var funcao = (AIFunction)registry.AsAIFunctions()[0];
        await funcao.InvokeAsync();

        Assert.Equal(AssistantState.Executing, duranteExecucao);
        Assert.Equal(AssistantState.Thinking, status.Current);
    }

    [Fact]
    public async Task Sem_status_a_tool_roda_sem_envolver()
    {
        var tool = new SondaTool(() => { });
        var registry = new ToolRegistry(new ITool[] { tool });

        var funcao = (AIFunction)registry.AsAIFunctions()[0];
        var resultado = await funcao.InvokeAsync();

        Assert.Equal("ok", resultado?.ToString());
    }

    private sealed class SondaTool : ITool
    {
        private readonly Action _aoExecutar;

        public SondaTool(Action aoExecutar) => _aoExecutar = aoExecutar;

        public string Name => "sonda";

        public string Description => "Tool de teste que registra o estado durante a execução.";

        public bool RequiresConfirmation => false;

        public AIFunction AsAIFunction() => AIFunctionFactory.Create(Executar, Name, Description);

        private string Executar()
        {
            _aoExecutar();
            return "ok";
        }
    }
}
