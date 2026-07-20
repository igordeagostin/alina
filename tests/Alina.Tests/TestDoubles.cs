using Alina.Core.Memory;
using Alina.Core.Models;
using Alina.Core.Tools;
using Microsoft.Extensions.AI;

namespace Alina.Tests;

/// <summary>Confirmação fake: sempre retorna o valor configurado e conta chamadas.</summary>
public sealed class FakeConfirmationService : IConfirmationService
{
    private readonly bool _result;

    public FakeConfirmationService(bool result) => _result = result;

    public int Calls { get; private set; }

    public Task<bool> ConfirmAsync(string action, string? details = null, CancellationToken cancellationToken = default)
    {
        Calls++;
        return Task.FromResult(_result);
    }
}

/// <summary>IChatClient fake que devolve uma resposta canônica e captura a última requisição.</summary>
public sealed class FakeChatClient : IChatClient
{
    private readonly Func<IReadOnlyList<ChatMessage>, ChatOptions?, ChatResponse> _handler;

    public FakeChatClient(Func<IReadOnlyList<ChatMessage>, ChatOptions?, ChatResponse> handler) => _handler = handler;

    public ChatOptions? LastOptions { get; private set; }

    public IReadOnlyList<ChatMessage>? LastMessages { get; private set; }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var list = messages.ToList();
        LastMessages = list;
        LastOptions = options;
        return Task.FromResult(_handler(list, options));
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}

/// <summary>Store em memória para testes do orquestrador.</summary>
public sealed class InMemoryConversationStore : IConversationStore
{
    private readonly Dictionary<string, Conversation> _data = new();

    public int SaveCount { get; private set; }

    public Task SaveAsync(Conversation conversation, CancellationToken cancellationToken = default)
    {
        SaveCount++;
        _data[conversation.Id] = conversation;
        return Task.CompletedTask;
    }

    public Task<Conversation?> LoadAsync(string id, CancellationToken cancellationToken = default)
        => Task.FromResult(_data.TryGetValue(id, out var c) ? c : null);

    public Task<IReadOnlyList<ConversationSummary>> ListAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ConversationSummary>>(
            _data.Values.Select(c => new ConversationSummary(c.Id, c.Title, c.UpdatedAt, c.Messages.Count)).ToList());
}

/// <summary>Perfil vazio (sem preferências permanentes).</summary>
public sealed class NullProfileStore : IProfileStore
{
    public Task<string?> GetPreferencesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<string?>(null);
}

/// <summary>Memória em memória para testes.</summary>
public sealed class InMemoryMemoryStore : IMemoryStore
{
    private readonly List<MemoryItem> _items = new();

    public Task<MemoryItem> AddAsync(string content, string? category = null, CancellationToken cancellationToken = default)
        => AddAsync(new MemoryItem { Content = content, Category = category }, cancellationToken);

    public Task<MemoryItem> AddAsync(MemoryItem item, CancellationToken cancellationToken = default)
    {
        _items.Add(item);
        return Task.FromResult(item);
    }

    public Task<IReadOnlyList<MemoryItem>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<MemoryItem>>(_items.ToList());

    public Task<bool> UpdateAsync(MemoryItem item, CancellationToken cancellationToken = default)
    {
        var index = _items.FindIndex(i => i.Id == item.Id);
        if (index < 0)
        {
            return Task.FromResult(false);
        }

        _items[index] = item;
        return Task.FromResult(true);
    }

    public Task<bool> RemoveAsync(string id, CancellationToken cancellationToken = default)
        => Task.FromResult(_items.RemoveAll(i => i.Id == id) > 0);
}
