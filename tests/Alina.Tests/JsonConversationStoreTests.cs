using Alina.Core.Models;
using Alina.Infrastructure.Memory;
using Microsoft.Extensions.AI;

namespace Alina.Tests;

public sealed class JsonConversationStoreTests : IDisposable
{
    private readonly string _tempDir;

    public JsonConversationStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "alina-tests", Guid.NewGuid().ToString("n"));
    }

    [Fact]
    public async Task SaveAsync_then_LoadAsync_preserva_mensagens_e_titulo()
    {
        var store = new JsonConversationStore(_tempDir);
        var conversation = new Conversation { Title = "Implementar login" };
        conversation.Messages.Add(new ChatMessage(ChatRole.User, "implemente o login"));
        conversation.Messages.Add(new ChatMessage(ChatRole.Assistant, "feito!"));

        await store.SaveAsync(conversation);
        var loaded = await store.LoadAsync(conversation.Id);

        Assert.NotNull(loaded);
        Assert.Equal("Implementar login", loaded!.Title);
        Assert.Equal(2, loaded.Messages.Count);
        Assert.Equal("implemente o login", loaded.Messages[0].Text);
        Assert.Equal(ChatRole.Assistant, loaded.Messages[1].Role);
    }

    [Fact]
    public async Task ListAsync_retorna_resumos_ordenados_por_atualizacao()
    {
        var store = new JsonConversationStore(_tempDir);

        var older = new Conversation { Title = "Antiga", UpdatedAt = DateTimeOffset.Now.AddHours(-1) };
        var newer = new Conversation { Title = "Recente", UpdatedAt = DateTimeOffset.Now };
        await store.SaveAsync(older);
        await store.SaveAsync(newer);

        var list = await store.ListAsync();

        Assert.Equal(2, list.Count);
        Assert.Equal("Recente", list[0].Title);
        Assert.Equal("Antiga", list[1].Title);
    }

    [Fact]
    public async Task LoadAsync_retorna_null_quando_nao_existe()
    {
        var store = new JsonConversationStore(_tempDir);
        var loaded = await store.LoadAsync("inexistente");
        Assert.Null(loaded);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}
