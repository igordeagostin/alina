namespace Alina.Core.Memory;

/// <summary>
/// Um fato/preferência ou procedimento aprendido pela Alina, persistido entre
/// sessões. Só o índice (id/tipo/categoria/título) é sempre visível no system prompt;
/// o conteúdo completo entra por recuperação seletiva (memória inteligente).
/// </summary>
public sealed class MemoryItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n")[..8];

    /// <summary>Tipo da memória (fato ou procedimento).</summary>
    public MemoryKind Kind { get; set; } = MemoryKind.Fact;

    /// <summary>
    /// Título curto usado no índice (poucos tokens). Para procedimentos, é o nome/gatilho.
    /// Se vazio, cai para um trecho do <see cref="Content"/>.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>O conteúdo completo. Para procedimentos, os passos.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Categoria opcional (ex: "preferência", "projeto", "convenção").</summary>
    public string? Category { get; set; }

    /// <summary>Palavras-chave/gatilhos, usadas no ranqueamento por fallback (sem embedding).</summary>
    public List<string> Keywords { get; set; } = new();

    /// <summary>Quando <c>true</c>, é núcleo: sempre carregado no prompt.</summary>
    public bool Pinned { get; set; }

    /// <summary>Vetor de embedding cacheado (calculado sob demanda). Nulo até a primeira busca.</summary>
    public float[]? Embedding { get; set; }

    /// <summary>Modelo que gerou o <see cref="Embedding"/>, para invalidá-lo se o modelo mudar.</summary>
    public string? EmbeddingModel { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;

    /// <summary>Título efetivo para o índice: <see cref="Title"/> ou um trecho do conteúdo.</summary>
    public string DisplayTitle()
    {
        if (!string.IsNullOrWhiteSpace(Title))
        {
            return Title!.Trim();
        }

        var content = Content.Trim();
        return content.Length > 80 ? content[..80] + "…" : content;
    }
}
