namespace Alina.Core.Memory;

/// <summary>Uma linha do índice leve de memórias (sem o conteúdo completo).</summary>
/// <param name="Id">Id do item.</param>
/// <param name="Kind">Tipo (fato ou procedimento).</param>
/// <param name="Category">Categoria opcional.</param>
/// <param name="Title">Título curto para exibição no prompt.</param>
public readonly record struct MemoryIndexEntry(string Id, MemoryKind Kind, string? Category, string Title);

/// <summary>
/// Recupera memórias de forma seletiva para não despejar tudo no system prompt a
/// cada comando: um índice leve fica sempre visível e só o top-K relevante (ou o que
/// for pedido) traz o conteúdo completo. O ranqueamento usa embeddings quando há um
/// gerador disponível e cai para sobreposição de palavras-chave caso contrário.
/// </summary>
public interface IMemoryRetriever
{
    /// <summary>Índice leve de tudo que existe (para o modelo saber o que pode recuperar).</summary>
    Task<IReadOnlyList<MemoryIndexEntry>> GetIndexAsync(CancellationToken cancellationToken = default);

    /// <summary>Itens fixados (núcleo sempre carregado), com conteúdo completo.</summary>
    Task<IReadOnlyList<MemoryItem>> GetPinnedAsync(CancellationToken cancellationToken = default);

    /// <summary>As <paramref name="topK"/> memórias mais relevantes para <paramref name="query"/>.</summary>
    Task<IReadOnlyList<MemoryItem>> SearchAsync(string query, int topK, CancellationToken cancellationToken = default);

    /// <summary>Recupera itens completos por id (para a tool de recuperação sob demanda).</summary>
    Task<IReadOnlyList<MemoryItem>> GetByIdsAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default);
}
