using Alina.Core.Habilidades;
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
        List<ChatMessage> list = messages.ToList();
        LastMessages = list;
        LastOptions = options;
        return Task.FromResult(_handler(list, options));
    }

    /// <summary>Entrega a mesma resposta do modo normal, mas fatiada, como faria um modelo de verdade.</summary>
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ChatResponse resposta = await GetResponseAsync(messages, options, cancellationToken);
        string texto = resposta.Text ?? string.Empty;

        for (int i = 0; i < texto.Length; i += 7)
        {
            yield return new ChatResponseUpdate(ChatRole.Assistant, texto[i..Math.Min(i + 7, texto.Length)]);
        }
    }

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
        => Task.FromResult(_data.TryGetValue(id, out Conversation? c) ? c : null);

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
        int index = _items.FindIndex(i => i.Id == item.Id);
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

/// <summary>Habilidades em memória para testes.</summary>
public sealed class InMemoryHabilidadeStore : IHabilidadeStore
{
    private readonly Dictionary<string, Habilidade> _itens = new(StringComparer.OrdinalIgnoreCase);

    public Task<IReadOnlyList<HabilidadeResumo>> ListarAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<HabilidadeResumo>>(
            _itens.Values.Select(h => new HabilidadeResumo(h.Nome, h.Descricao)).ToList());

    public Task<Habilidade?> ObterAsync(string nome, CancellationToken cancellationToken = default)
        => Task.FromResult(_itens.TryGetValue(nome, out Habilidade? h) ? h : null);

    public Task SalvarAsync(Habilidade habilidade, CancellationToken cancellationToken = default)
    {
        _itens[habilidade.Nome] = habilidade;
        return Task.CompletedTask;
    }

    public Task<bool> RemoverAsync(string nome, CancellationToken cancellationToken = default)
        => Task.FromResult(_itens.Remove(nome));

    public Task<bool> ExisteAsync(string nome, CancellationToken cancellationToken = default)
        => Task.FromResult(_itens.ContainsKey(nome));
}
