using Alina.Core.Models;

namespace Alina.Core.Memory;

/// <summary>Persistência do histórico de conversas (memória de trabalho).</summary>
public interface IConversationStore
{
    Task SaveAsync(Conversation conversation, CancellationToken cancellationToken = default);

    Task<Conversation?> LoadAsync(string id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ConversationSummary>> ListAsync(CancellationToken cancellationToken = default);
}
