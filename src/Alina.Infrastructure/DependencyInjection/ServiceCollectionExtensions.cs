using Alina.Core.Ferramentas;
using Alina.Core.Habilidades;
using Alina.Core.Memory;
using Alina.Core.Orchestration;
using Alina.Core.Permissoes;
using Alina.Core.Personalidade;
using Alina.Core.Tools;
using Alina.Infrastructure.Configuration;
using Alina.Infrastructure.Ferramentas;
using Alina.Infrastructure.Habilidades;
using Alina.Infrastructure.Llm;
using Alina.Infrastructure.Memory;
using Alina.Infrastructure.Permissoes;
using Alina.Infrastructure.Personalidade;
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

        // Personalidade: eixos de comportamento + orientações livres do usuário, relidos
        // a cada turno para valer sem reiniciar.
        services.AddSingleton<IPersonalidadeStore>(sp =>
            new FilePersonalidadeStore(sp.GetRequiredService<IOptions<StorageOptions>>().Value));

        // Habilidades: cada uma um arquivo Markdown numa pasta dedicada (índice no
        // system prompt, conteúdo completo sob demanda).
        services.AddSingleton<IHabilidadeStore>(sp =>
            new FileHabilidadeStore(sp.GetRequiredService<IOptions<StorageOptions>>().Value));

        // Ferramentas declarativas: cada uma um arquivo *.tool.json na pasta de dados do
        // usuário. O provider consulta o store a cada turno (hot-reload) e o ToolRegistry
        // combina essas ferramentas com as tools em C#.
        services.AddSingleton<IFerramentaStore>(sp =>
            new FileFerramentaStore(sp.GetRequiredService<IOptions<StorageOptions>>().Value));
        services.AddSingleton<IFerramentaProvider>(sp =>
            new FerramentaProvider(
                sp.GetRequiredService<IFerramentaStore>(),
                sp.GetRequiredService<IConfirmationService>(),
                sp.GetRequiredService<IOptions<StorageOptions>>().Value));

        // Política de permissões: decide o que liberar/perguntar antes de interromper o usuário.
        services.AddSingleton<IPoliticaPermissao>(sp =>
            new PoliticaPermissao(sp.GetRequiredService<IOptions<StorageOptions>>().Value));
        services.AddSingleton<IContextoPermissao, ContextoPermissao>();

        // Recuperação seletiva de memória (índice leve + top-K semântico). O gerador de
        // embeddings é opcional: sem chave/desabilitado, o retriever cai para keyword.
        services.AddSingleton<IMemoryRetriever>(sp =>
        {
            LlmOptions options = sp.GetRequiredService<IOptions<LlmOptions>>().Value;
            IEmbeddingGenerator<string, Embedding<float>>? generator = LlmClientFactory.CreateEmbeddingGenerator(options);
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
            LlmOptions options = sp.GetRequiredService<IOptions<LlmOptions>>().Value;
            ILoggerFactory loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            return LlmClientFactory.Create(options, loggerFactory);
        });

        // Opções de geração (temperatura) relidas por turno. Um head de UI pode
        // substituir este registro para ajustar em runtime.
        services.AddSingleton<IOpcoesGeracao, OpcoesGeracaoLlm>();

        // Orquestrador
        services.AddSingleton<IOrchestrator, ChatOrchestrator>();

        // Acesso tardio à conversa principal para as tools que herdam contexto (ex.:
        // tarefas paralelas). Uma Func evita o ciclo tools → orquestrador → tools:
        // a resolução só acontece na primeira execução, com o grafo já montado.
        services.AddSingleton<Func<IOrchestrator?>>(sp => () => sp.GetService<IOrchestrator>());

        // Orquestradores isolados para as tarefas paralelas: mesmas ferramentas (menos as
        // que abrem novas tarefas) e memória de trabalho descartável. A resolução tardia
        // do ToolRegistry é proposital — as tools que disparam paralelismo dependem desta
        // fábrica, e resolvê-la no construtor fecharia um ciclo.
        services.AddSingleton<IFabricaOrquestrador>(sp => new FabricaOrquestrador(() => new ChatOrchestrator(
            sp.GetRequiredService<IChatClient>(),
            sp.GetRequiredService<ToolRegistry>().SemFerramentas(
                NomesFerramentas.ExecutarEmParalelo, NomesFerramentas.DelegarEmBackground),
            ConversationStoreEfemero.Instancia,
            sp.GetRequiredService<IProfileStore>(),
            sp.GetRequiredService<IMemoryRetriever>(),
            sp.GetRequiredService<IHabilidadeStore>(),
            sp.GetService<IPoliticaPermissao>(),
            sp.GetService<IPersonalidadeStore>(),
            sp.GetService<IOpcoesGeracao>(),
            sp.GetService<ILogger<ChatOrchestrator>>())));

        return services;
    }
}
