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
        FakeConfirmationService confirmation = new FakeConfirmationService(result: true);
        ToolRegistry registry = new ToolRegistry(new ITool[] { new FileReadTool(confirmation) });
        MemoryRetriever retriever = new MemoryRetriever(memory ?? new InMemoryMemoryStore());
        return new ChatOrchestrator(client, registry, store, new NullProfileStore(), retriever, new InMemoryHabilidadeStore());
    }

    [Fact]
    public async Task SendAsync_retorna_texto_e_persiste_conversa()
    {
        FakeChatClient client = new FakeChatClient((_, _) => new ChatResponse(new ChatMessage(ChatRole.Assistant, "olá, Igor")));
        InMemoryConversationStore store = new InMemoryConversationStore();
        ChatOrchestrator orchestrator = Build(client, store);

        string reply = await orchestrator.SendAsync("oi");

        Assert.Equal("olá, Igor", reply);
        Assert.Equal(1, store.SaveCount);
        Assert.Contains(orchestrator.Current.Messages, m => m.Role == ChatRole.User && m.Text == "oi");
        Assert.Contains(orchestrator.Current.Messages, m => m.Role == ChatRole.Assistant && m.Text == "olá, Igor");
    }

    [Fact]
    public async Task SendAsync_envia_system_prompt_e_tools_ao_LLM()
    {
        FakeChatClient client = new FakeChatClient((_, _) => new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));
        InMemoryConversationStore store = new InMemoryConversationStore();
        ChatOrchestrator orchestrator = Build(client, store);

        await orchestrator.SendAsync("faça algo");

        Assert.NotNull(client.LastMessages);
        Assert.Equal(ChatRole.System, client.LastMessages![0].Role);
        Assert.NotNull(client.LastOptions?.Tools);
        Assert.Single(client.LastOptions!.Tools!);
    }

    [Fact]
    public async Task SummarizeConversationAsync_resume_a_conversa_sem_tools()
    {
        FakeChatClient client = new FakeChatClient((_, _) => new ChatResponse(new ChatMessage(ChatRole.Assistant, "Resumo: usa .NET 10.")));
        InMemoryConversationStore store = new InMemoryConversationStore();
        ChatOrchestrator orchestrator = Build(client, store);

        await orchestrator.SendAsync("uso .NET 10 em tudo");
        string summary = await orchestrator.SummarizeConversationAsync();

        Assert.Equal("Resumo: usa .NET 10.", summary);
        // A síntese não deve oferecer tools ao modelo.
        Assert.True(client.LastOptions?.Tools is null || client.LastOptions.Tools.Count == 0);
    }

    [Fact]
    public async Task SummarizeConversationAsync_conversa_vazia_retorna_vazio()
    {
        FakeChatClient client = new FakeChatClient((_, _) => new ChatResponse(new ChatMessage(ChatRole.Assistant, "não deveria ser chamado")));
        InMemoryConversationStore store = new InMemoryConversationStore();
        ChatOrchestrator orchestrator = Build(client, store);

        string summary = await orchestrator.SummarizeConversationAsync();

        Assert.Equal(string.Empty, summary);
    }

    [Fact]
    public async Task StartNew_reinicia_a_conversa_atual()
    {
        FakeChatClient client = new FakeChatClient((_, _) => new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));
        InMemoryConversationStore store = new InMemoryConversationStore();
        ChatOrchestrator orchestrator = Build(client, store);

        await orchestrator.SendAsync("primeira");
        string firstId = orchestrator.Current.Id;

        orchestrator.StartNew();

        Assert.NotEqual(firstId, orchestrator.Current.Id);
        Assert.Empty(orchestrator.Current.Messages);
    }
}
