using System.ClientModel;
using Anthropic;
using Alina.Infrastructure.Configuration;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI;

namespace Alina.Infrastructure.Llm;

/// <summary>
/// Cria o <see cref="IChatClient"/> abstraído via Microsoft.Extensions.AI,
/// com pipeline de invocação de funções e logging. O provider é escolhido por
/// configuração (OpenAI ou Anthropic/Claude).
/// </summary>
public static class LlmClientFactory
{
    public static IChatClient Create(LlmOptions options, ILoggerFactory loggerFactory)
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new InvalidOperationException(
                "Chave de API do LLM não configurada. Defina-a via user-secrets: " +
                "dotnet user-secrets set \"Llm:ApiKey\" \"<sua-chave>\".");
        }

        IChatClient inner = options.Provider switch
        {
            LlmProvider.OpenAI => CreateOpenAI(options),
            LlmProvider.Anthropic => CreateAnthropic(options),
            _ => throw new ArgumentOutOfRangeException(nameof(options), options.Provider, "Provider desconhecido."),
        };

        return inner
            .AsBuilder()
            .UseFunctionInvocation(loggerFactory)
            .UseLogging(loggerFactory)
            .Build();
    }

    private static IChatClient CreateAnthropic(LlmOptions options)
    {
        var client = string.IsNullOrWhiteSpace(options.Endpoint)
            ? new AnthropicClient { ApiKey = options.ApiKey! }
            : new AnthropicClient { ApiKey = options.ApiKey!, BaseUrl = options.Endpoint! };

        return client.AsIChatClient(options.Model);
    }

    /// <summary>
    /// Cria o gerador de embeddings para a memória semântica, ou <c>null</c> se
    /// desabilitado/sem chave — nesse caso a recuperação usa o fallback por keyword.
    /// </summary>
    public static IEmbeddingGenerator<string, Embedding<float>>? CreateEmbeddingGenerator(LlmOptions options)
    {
        if (!options.EmbeddingsEnabled
            || string.IsNullOrWhiteSpace(options.ApiKey)
            || string.IsNullOrWhiteSpace(options.EmbeddingModel)
            || options.Provider != LlmProvider.OpenAI)
        {
            return null;
        }

        return CreateOpenAIClient(options).GetEmbeddingClient(options.EmbeddingModel).AsIEmbeddingGenerator();
    }

    private static IChatClient CreateOpenAI(LlmOptions options)
        => CreateOpenAIClient(options).GetChatClient(options.Model).AsIChatClient();

    private static OpenAIClient CreateOpenAIClient(LlmOptions options)
    {
        var credential = new ApiKeyCredential(options.ApiKey!);

        return string.IsNullOrWhiteSpace(options.Endpoint)
            ? new OpenAIClient(credential)
            : new OpenAIClient(credential, new OpenAIClientOptions { Endpoint = new Uri(options.Endpoint!) });
    }
}
