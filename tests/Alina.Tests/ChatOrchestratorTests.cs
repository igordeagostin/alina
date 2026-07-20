using Alina.Core.Memory;
using Alina.Core.Orchestration;
using Alina.Core.Tools;
using Alina.Tools;
using Microsoft.Extensions.AI;

namespace Alina.Tests;

public sealed class ChatOrchestratorTests
{
    private static ChatOrchestrator Build(FakeChatClient client, InMemoryConversationStore store, IMemoryStore? memory = null)
    {
        var confirmation = new FakeConfirmationService(result: true);
        var registry = new ToolRegistry(new ITool[] { new FileReadTool(confirmation) });
        var retriever = new MemoryRetriever(memory ?? new InMemoryMemoryStore());
        return new ChatOrchestrator(client, registry, store, new NullProfileStore(), retriever);
    }

    [Fact]
    public async Task SendAsync_retorna_texto_e_persiste_conversa()
    {
        var client = new FakeChatClient((_, _) => new ChatResponse(new ChatMessage(ChatRole.Assistant, "olá, Igor")));
        var store = new InMemoryConversationStore();
        var orchestrator = Build(client, store);

        var reply = await orchestrator.SendAsync("oi");

        Assert.Equal("olá, Igor", reply);
        Assert.Equal(1, store.SaveCount);
        Assert.Contains(orchestrator.Current.Messages, m => m.Role == ChatRole.User && m.Text == "oi");
        Assert.Contains(orchestrator.Current.Messages, m => m.Role == ChatRole.Assistant && m.Text == "olá, Igor");
    }

    [Fact]
    public async Task SendAsync_envia_system_prompt_e_tools_ao_LLM()
    {
        var client = new FakeChatClient((_, _) => new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));
        var store = new InMemoryConversationStore();
        var orchestrator = Build(client, store);

        await orchestrator.SendAsync("faça algo");

        Assert.NotNull(client.LastMessages);
        Assert.Equal(ChatRole.System, client.LastMessages![0].Role);
        Assert.NotNull(client.LastOptions?.Tools);
        Assert.Single(client.LastOptions!.Tools!);
    }

    [Fact]
    public async Task SummarizeConversationAsync_resume_a_conversa_sem_tools()
    {
        var client = new FakeChatClient((_, _) => new ChatResponse(new ChatMessage(ChatRole.Assistant, "Resumo: usa .NET 10.")));
        var store = new InMemoryConversationStore();
        var orchestrator = Build(client, store);

        await orchestrator.SendAsync("uso .NET 10 em tudo");
        var summary = await orchestrator.SummarizeConversationAsync();

        Assert.Equal("Resumo: usa .NET 10.", summary);
        // A síntese não deve oferecer tools ao modelo.
        Assert.True(client.LastOptions?.Tools is null || client.LastOptions.Tools.Count == 0);
    }

    [Fact]
    public async Task SummarizeConversationAsync_conversa_vazia_retorna_vazio()
    {
        var client = new FakeChatClient((_, _) => new ChatResponse(new ChatMessage(ChatRole.Assistant, "não deveria ser chamado")));
        var store = new InMemoryConversationStore();
        var orchestrator = Build(client, store);

        var summary = await orchestrator.SummarizeConversationAsync();

        Assert.Equal(string.Empty, summary);
    }

    [Fact]
    public async Task StartNew_reinicia_a_conversa_atual()
    {
        var client = new FakeChatClient((_, _) => new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));
        var store = new InMemoryConversationStore();
        var orchestrator = Build(client, store);

        await orchestrator.SendAsync("primeira");
        var firstId = orchestrator.Current.Id;

        orchestrator.StartNew();

        Assert.NotEqual(firstId, orchestrator.Current.Id);
        Assert.Empty(orchestrator.Current.Messages);
    }
}
