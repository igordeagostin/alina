using Microsoft.Extensions.AI;

namespace Alina.Core.Memory;

/// <summary>
/// Implementação de <see cref="IMemoryRetriever"/> sobre um <see cref="IMemoryStore"/>.
/// Ranqueia por similaridade de cosseno usando um <see cref="IEmbeddingGenerator{TInput,TEmbedding}"/>
/// (opcional); sem gerador — ou em caso de falha de rede — cai para pontuação por
/// sobreposição de palavras-chave. Embeddings dos itens são calculados uma vez e
/// cacheados de volta no store.
/// </summary>
public sealed class MemoryRetriever : IMemoryRetriever
{
    /// <summary>
    /// Teto de espera pelo embedding. A busca semântica roda no caminho crítico de cada
    /// turno; se o provedor demorar mais que isto, vale mais cair para keyword na hora
    /// do que segurar a resposta.
    /// </summary>
    private static readonly TimeSpan TempoMaximoEmbedding = TimeSpan.FromMilliseconds(800);

    private readonly IMemoryStore _store;
    private readonly IEmbeddingGenerator<string, Embedding<float>>? _embeddings;
    private readonly string _embeddingModel;

    public MemoryRetriever(
        IMemoryStore store,
        IEmbeddingGenerator<string, Embedding<float>>? embeddings = null,
        string? embeddingModel = null)
    {
        _store = store;
        _embeddings = embeddings;
        _embeddingModel = string.IsNullOrWhiteSpace(embeddingModel) ? "unknown" : embeddingModel!;
    }

    public async Task<IReadOnlyList<MemoryIndexEntry>> GetIndexAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<MemoryItem> items = await _store.GetAllAsync(cancellationToken);
        return items
            .Select(i => new MemoryIndexEntry(i.Id, i.Kind, i.Category, i.DisplayTitle()))
            .ToList();
    }

    public async Task<IReadOnlyList<MemoryItem>> GetPinnedAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<MemoryItem> items = await _store.GetAllAsync(cancellationToken);
        return items.Where(i => i.Pinned).ToList();
    }

    public async Task<IReadOnlyList<MemoryItem>> GetByIdsAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default)
    {
        HashSet<string> wanted = new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase);
        IReadOnlyList<MemoryItem> items = await _store.GetAllAsync(cancellationToken);
        return items.Where(i => wanted.Contains(i.Id)).ToList();
    }

    public async Task<IReadOnlyList<MemoryItem>> SearchAsync(string query, int topK, CancellationToken cancellationToken = default)
    {
        if (topK <= 0 || string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<MemoryItem>();
        }

        IReadOnlyList<MemoryItem> items = await _store.GetAllAsync(cancellationToken);
        // Itens fixados já entram sempre; não gaste ranqueamento com eles.
        List<MemoryItem> candidates = items.Where(i => !i.Pinned).ToList();
        if (candidates.Count == 0)
        {
            return Array.Empty<MemoryItem>();
        }

        float[]? queryVector = await TryEmbedAsync(query, cancellationToken);
        if (queryVector is not null)
        {
            List<(MemoryItem Item, float Score)> scored = new List<(MemoryItem Item, float Score)>(candidates.Count);
            foreach (MemoryItem? item in candidates)
            {
                float[]? vector = await EnsureEmbeddingAsync(item, cancellationToken);
                if (vector is not null)
                {
                    scored.Add((item, Cosine(queryVector, vector)));
                }
            }

            if (scored.Count > 0)
            {
                return scored.OrderByDescending(s => s.Score).Take(topK).Select(s => s.Item).ToList();
            }
        }

        // Fallback: pontuação por sobreposição de tokens (keywords/categoria/conteúdo).
        return RankByKeywords(candidates, query, topK);
    }

    private async Task<float[]?> EnsureEmbeddingAsync(MemoryItem item, CancellationToken cancellationToken)
    {
        if (item.Embedding is { Length: > 0 } && string.Equals(item.EmbeddingModel, _embeddingModel, StringComparison.Ordinal))
        {
            return item.Embedding;
        }

        string text = EmbeddingText(item);
        float[]? vector = await TryEmbedAsync(text, cancellationToken);
        if (vector is null)
        {
            return null;
        }

        item.Embedding = vector;
        item.EmbeddingModel = _embeddingModel;
        await _store.UpdateAsync(item, cancellationToken);
        return vector;
    }

    private async Task<float[]?> TryEmbedAsync(string text, CancellationToken cancellationToken)
    {
        if (_embeddings is null || string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            using CancellationTokenSource limite = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            limite.CancelAfter(TempoMaximoEmbedding);

            GeneratedEmbeddings<Embedding<float>> embeddings = await _embeddings.GenerateAsync(new[] { text }, cancellationToken: limite.Token);
            return embeddings.Count > 0 ? embeddings[0].Vector.ToArray() : null;
        }
        catch
        {
            // Sem rede, provider lento ou com erro: cai para o fallback por keyword.
            return null;
        }
    }

    private static string EmbeddingText(MemoryItem item)
    {
        string keywords = item.Keywords.Count > 0 ? " " + string.Join(" ", item.Keywords) : string.Empty;
        string title = string.IsNullOrWhiteSpace(item.Title) ? string.Empty : item.Title + ". ";
        return (title + item.Content + keywords).Trim();
    }

    private static IReadOnlyList<MemoryItem> RankByKeywords(IReadOnlyList<MemoryItem> candidates, string query, int topK)
    {
        HashSet<string> queryTokens = Tokenize(query);
        if (queryTokens.Count == 0)
        {
            return Array.Empty<MemoryItem>();
        }

        List<(MemoryItem Item, int Score)> scored = new List<(MemoryItem Item, int Score)>(candidates.Count);
        foreach (MemoryItem item in candidates)
        {
            HashSet<string> haystack = Tokenize($"{item.Title} {item.Content} {item.Category} {string.Join(' ', item.Keywords)}");
            int score = queryTokens.Count(haystack.Contains);
            if (score > 0)
            {
                scored.Add((item, score));
            }
        }

        return scored.OrderByDescending(s => s.Score).Take(topK).Select(s => s.Item).ToList();
    }

    private static HashSet<string> Tokenize(string text)
    {
        HashSet<string> tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(text))
        {
            return tokens;
        }

        foreach (string raw in text.Split(
            new[] { ' ', '\t', '\n', '\r', '.', ',', ';', ':', '!', '?', '(', ')', '[', ']', '{', '}', '/', '\\', '"', '\'', '-' },
            StringSplitOptions.RemoveEmptyEntries))
        {
            if (raw.Length >= 3)
            {
                tokens.Add(raw);
            }
        }

        return tokens;
    }

    private static float Cosine(float[] a, float[] b)
    {
        int length = Math.Min(a.Length, b.Length);
        double dot = 0, na = 0, nb = 0;
        for (int i = 0; i < length; i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }

        if (na == 0 || nb == 0)
        {
            return 0f;
        }

        return (float)(dot / (Math.Sqrt(na) * Math.Sqrt(nb)));
    }
}