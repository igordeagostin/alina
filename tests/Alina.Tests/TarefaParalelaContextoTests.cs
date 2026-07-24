using Alina.Core.Models;
using Alina.Core.Orchestration;
using Alina.Tools.Background;
using Microsoft.Extensions.AI;

namespace Alina.Tests;

/// <summary>
/// Garante que uma tarefa paralela recebe o recorte do fim da conversa junto do pedido —
/// contexto implícito ("aquele arquivo", "o projeto que abrimos") não pode se perder.
/// </summary>
public sealed class TarefaParalelaContextoTests
{
    [Fact]
    public async Task Tarefa_paralela_recebe_recorte_da_conversa()
    {
        FakeOrquestrador executor = new FakeOrquestrador();
        FabricaOrquestrador fabrica = new FabricaOrquestrador(() => executor);
        using BackgroundTaskManager gerenciador = new BackgroundTaskManager();
        TaskCompletionSource terminou = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        gerenciador.TaskFinished += (_, _) => terminou.TrySetResult();

        FakeOrquestrador principal = new FakeOrquestrador();
        principal.Current.Messages.Add(new ChatMessage(ChatRole.User, "abre o projeto diário"));
        principal.Current.Messages.Add(new ChatMessage(ChatRole.Assistant, @"Aberto: D:\github\diario"));

        TarefaParalelaTool tool = new TarefaParalelaTool(
            new FakeConfirmationService(result: true), gerenciador, fabrica, () => principal);

        await tool.RunAsync("roda o build completo", "build");
        await terminou.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.NotNull(executor.UltimoPedido);
        Assert.Contains("Contexto recente da conversa", executor.UltimoPedido);
        Assert.Contains("abre o projeto diário", executor.UltimoPedido);
        Assert.Contains(@"D:\github\diario", executor.UltimoPedido);
        Assert.Contains("roda o build completo", executor.UltimoPedido);
    }

    [Fact]
    public async Task Sem_conversa_principal_o_pedido_vai_puro()
    {
        FakeOrquestrador executor = new FakeOrquestrador();
        FabricaOrquestrador fabrica = new FabricaOrquestrador(() => executor);
        using BackgroundTaskManager gerenciador = new BackgroundTaskManager();
        TaskCompletionSource terminou = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        gerenciador.TaskFinished += (_, _) => terminou.TrySetResult();

        TarefaParalelaTool tool = new TarefaParalelaTool(
            new FakeConfirmationService(result: true), gerenciador, fabrica, conversaPrincipal: null);

        await tool.RunAsync("roda o build completo", "build");
        await terminou.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal("roda o build completo", executor.UltimoPedido);
    }

    private sealed class FakeOrquestrador : IOrchestrator
    {
        public Conversation Current { get; } = new Conversation();

        public string? UltimoPedido { get; private set; }

        public void StartNew()
        {
        }

        public Task<bool> ResumeAsync(string conversationId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<string> SendAsync(string userText, CancellationToken cancellationToken = default)
        {
            UltimoPedido = userText;
            return Task.FromResult("ok");
        }

        public Task<string> SendAsync(string userText, IProgress<string>? progressoResposta, CancellationToken cancellationToken = default)
            => SendAsync(userText, cancellationToken);

        public void RegistrarNota(string nota)
        {
        }

        public Task<string> SummarizeConversationAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(string.Empty);
    }
}
