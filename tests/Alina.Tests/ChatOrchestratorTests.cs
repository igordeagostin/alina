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
    public async Task Turno_interrompido_preserva_o_que_ja_foi_executado()
    {
        ClienteQueCaiNoMeio client = new ClienteQueCaiNoMeio();
        InMemoryConversationStore store = new InMemoryConversationStore();
        FakeConfirmationService confirmation = new FakeConfirmationService(result: true);
        ToolRegistry registry = new ToolRegistry(new ITool[] { new FileReadTool(confirmation) });
        MemoryRetriever retriever = new MemoryRetriever(new InMemoryMemoryStore());
        ChatOrchestrator orchestrator = new ChatOrchestrator(
            client, registry, store, new NullProfileStore(), retriever, new InMemoryHabilidadeStore());

        await Assert.ThrowsAsync<OperationCanceledException>(() => orchestrator.SendAsync("faça algo demorado"));

        Assert.Contains(orchestrator.Current.Messages, m => m.Text.Contains("Vou executar"));
        Assert.Contains(orchestrator.Current.Messages, m =>
            m.Contents.OfType<FunctionResultContent>().Any(r => r.CallId == "c1"));
        Assert.Contains(orchestrator.Current.Messages, m => m.Text.Contains("interrompido no meio"));
    }

    [Fact]
    public async Task Historico_grande_e_compactado_em_segundo_plano()
    {
        FakeChatClient client = new FakeChatClient((_, _) =>
            new ChatResponse(new ChatMessage(ChatRole.Assistant, "resumo denso da conversa")));
        InMemoryConversationStore store = new InMemoryConversationStore();
        ChatOrchestrator orchestrator = Build(client, store);

        string blocao = new string('x', 6000);
        for (int i = 0; i < 15; i++)
        {
            orchestrator.Current.Messages.Add(new ChatMessage(ChatRole.User, blocao));
            orchestrator.Current.Messages.Add(new ChatMessage(ChatRole.Assistant, blocao));
        }

        // Este turno agenda a compactação em segundo plano; um turno seguinte a aplica.
        await orchestrator.SendAsync("primeira");

        bool compactou = false;
        for (int tentativa = 0; tentativa < 50 && !compactou; tentativa++)
        {
            await Task.Delay(20);
            await orchestrator.SendAsync("seguinte");
            compactou = orchestrator.Current.Messages[0].Text.StartsWith("[resumo da conversa até aqui");
        }

        Assert.True(compactou);
        Assert.Contains("resumo denso da conversa", orchestrator.Current.Messages[0].Text);
    }

    /// <summary>Emite texto e uma chamada de ferramenta, e é cancelado antes de terminar o turno.</summary>
    private sealed class ClienteQueCaiNoMeio : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "não usado")));

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            yield return new ChatResponseUpdate(ChatRole.Assistant, "Vou executar a ferramenta.");
            yield return new ChatResponseUpdate(ChatRole.Assistant,
                [new FunctionCallContent("c1", "terminal", new Dictionary<string, object?>())]);
            throw new OperationCanceledException();
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
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
