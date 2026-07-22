namespace Alina.Infrastructure.Configuration;

public enum LlmProvider
{
    OpenAI,
    Anthropic,

    /// <summary>
    /// CLI do Claude Code (assinatura do usuário), sem chave de API. Não passa pelo
    /// <c>LlmClientFactory</c> — quem monta o cliente é a camada de UI, que conhece a tool.
    /// </summary>
    ClaudeCode,
}

/// <summary>Configuração do provedor de LLM (seção "Llm" do appsettings).</summary>
public sealed class LlmOptions
{
    public const string SectionName = "Llm";

    public LlmProvider Provider { get; set; } = LlmProvider.OpenAI;

    /// <summary>Chave de API. Configure via user-secrets, nunca commite.</summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Modelo a ser usado. Depende do <see cref="Provider"/>: para OpenAI, ex.
    /// "gpt-4o" ou "gpt-4.1"; para Anthropic, ex. "claude-opus-4-8" ou "claude-sonnet-5".
    /// </summary>
    public string Model { get; set; } = "gpt-4o-mini";

    /// <summary>Endpoint opcional (para APIs compatíveis com OpenAI).</summary>
    public string? Endpoint { get; set; }

    /// <summary>
    /// Quando <c>true</c>, gera embeddings das memórias para recuperação semântica.
    /// Se desabilitado (ou sem gerador disponível), a recuperação cai para keyword.
    /// </summary>
    public bool EmbeddingsEnabled { get; set; } = true;

    /// <summary>Modelo de embedding (reutiliza <see cref="ApiKey"/>/<see cref="Endpoint"/>).</summary>
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";

    /// <summary>
    /// Chave usada só para embeddings. Existe porque a memória semântica é sempre da
    /// OpenAI, mesmo quando o <see cref="Provider"/> do chat é outro. Vazia = usa a
    /// <see cref="ApiKey"/> quando o provedor já é OpenAI.
    /// </summary>
    public string? EmbeddingApiKey { get; set; }
}
