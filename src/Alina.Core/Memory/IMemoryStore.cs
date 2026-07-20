namespace Alina.Core.Memory;

/// <summary>
/// Persistência da memória permanente de fatos/preferências e procedimentos que a
/// Alina aprende. Diferente do <see cref="IProfileStore"/> (texto fixo só de
/// leitura), aqui a assistente adiciona, atualiza e remove itens dinamicamente.
/// O ranqueamento/relevância vive no <see cref="IMemoryRetriever"/>.
/// </summary>
public interface IMemoryStore
{
    Task<MemoryItem> AddAsync(string content, string? category = null, CancellationToken cancellationToken = default);

    /// <summary>Persiste um item já montado (com tipo/título/keywords/pin).</summary>
    Task<MemoryItem> AddAsync(MemoryItem item, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MemoryItem>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Substitui um item existente (pelo <see cref="MemoryItem.Id"/>). Usado para gravar
    /// o embedding calculado ou alternar o pin. Retorna <c>true</c> se o item existia.
    /// </summary>
    Task<bool> UpdateAsync(MemoryItem item, CancellationToken cancellationToken = default);

    /// <summary>Remove um item pelo id. Retorna <c>true</c> se algo foi removido.</summary>
    Task<bool> RemoveAsync(string id, CancellationToken cancellationToken = default);
}
