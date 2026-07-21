using Alina.Core.Memory;
using Alina.Core.Orchestration;
using Alina.Core.Tools;
using Alina.Infrastructure.Configuration;
using Alina.Infrastructure.Llm;
using Alina.Infrastructure.Memory;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Alina.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registra o núcleo da Alina: opções, LLM (IChatClient), memória, tools
    /// e orquestrador. A implementação de <see cref="IConfirmationService"/> deve
    /// ser registrada pela camada de UI (ex: console).
    /// </summary>
    public static IServiceCollection AddAlina(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<LlmOptions>(configuration.GetSection(LlmOptions.SectionName));
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));

        // Memória
        services.AddSingleton<IConversationStore>(sp =>
            new JsonConversationStore(sp.GetRequiredService<IOptions<StorageOptions>>().Value));
        services.AddSingleton<IProfileStore>(sp =>
            new FileProfileStore(sp.GetRequiredService<IOptions<StorageOptions>>().Value.ResolvePreferencesFile()));
        services.AddSingleton<IMemoryStore>(sp =>
            new JsonMemoryStore(sp.GetRequiredService<IOptions<StorageOptions>>().Value));

        // Recuperação seletiva de memória (índice leve + top-K semântico). O gerador de
        // embeddings é opcional: sem chave/desabilitado, o retriever cai para keyword.
        services.AddSingleton<IMemoryRetriever>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<LlmOptions>>().Value;
            var generator = LlmClientFactory.CreateEmbeddingGenerator(options);
            return new MemoryRetriever(sp.GetRequiredService<IMemoryStore>(), generator, options.EmbeddingModel);
        });

        // Status da assistente (orbe/estado) — os heads de UI assinam o evento;
        // o console simplesmente ignora.
        services.AddSingleton<IAssistantStatus, AssistantStatus>();

        // ToolRegistry agrega as ITool registradas pela camada de UI (composição).
        // Registre as tools concretas e o IConfirmationService no projeto de UI.
        services.AddSingleton<ToolRegistry>();

        // LLM
        services.AddSingleton<IChatClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<LlmOptions>>().Value;
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            return LlmClientFactory.Create(options, loggerFactory);
        });

        // Orquestrador
        services.AddSingleton<IOrchestrator, ChatOrchestrator>();

        return services;
    }
}
