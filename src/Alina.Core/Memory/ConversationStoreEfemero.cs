using Alina.Core.Models;

namespace Alina.Core.Memory;

/// <summary>
/// Store que descarta tudo: usado pelas tarefas paralelas, cujo raciocínio interno não
/// deve virar uma conversa no histórico do usuário. O que interessa dessas tarefas é só
/// o resultado, que volta pela conversa principal.
/// </summary>
public sealed class ConversationStoreEfemero : IConversationStore
{
    public static readonly ConversationStoreEfemero Instancia = new();

    private ConversationStoreEfemero()
    {
    }

    public Task SaveAsync(Conversation conversation, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<Conversation?> LoadAsync(string id, CancellationToken cancellationToken = default) =>
        Task.FromResult<Conversation?>(null);

    public Task<IReadOnlyList<ConversationSummary>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ConversationSummary>>([]);
}
