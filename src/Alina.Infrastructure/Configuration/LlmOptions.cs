namespace Alina.Infrastructure.Configuration;

public enum LlmProvider
{
    OpenAI,
    Anthropic,
}

/// <summary>Configuração do provedor de LLM (seção "Llm" do appsettings).</summary>
public sealed class LlmOptions
{
    public const string SectionName = "Llm";

    public LlmProvider Provider { get; set; } = LlmProvider.OpenAI;

    /// <summary>Chave de API. Configure via user-secrets, nunca commite.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Modelo a ser usado (ex: gpt-4o-mini).</summary>
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
}
