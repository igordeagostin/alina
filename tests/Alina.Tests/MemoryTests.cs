using Alina.Core.Memory;
using Alina.Core.Orchestration;
using Alina.Core.Tools;
using Alina.Infrastructure.Memory;
using Alina.Tools.Memory;
using Microsoft.Extensions.AI;

namespace Alina.Tests;

public sealed class JsonMemoryStoreTests : IDisposable
{
    private readonly string _file;

    public JsonMemoryStoreTests()
        => _file = Path.Combine(Path.GetTempPath(), "alina-mem-" + Guid.NewGuid().ToString("n"), "memory.json");

    [Fact]
    public async Task Add_persiste_e_GetAll_recupera()
    {
        JsonMemoryStore store = new JsonMemoryStore(_file);
        MemoryItem item = await store.AddAsync("Sempre uso Clean Architecture", "convenção");

        // Nova instância lê do disco.
        JsonMemoryStore reloaded = new JsonMemoryStore(_file);
        IReadOnlyList<MemoryItem> all = await reloaded.GetAllAsync();

        Assert.Single(all);
        Assert.Equal("Sempre uso Clean Architecture", all[0].Content);
        Assert.Equal("convenção", all[0].Category);
        Assert.Equal(item.Id, all[0].Id);
    }

    [Fact]
    public async Task Remove_apaga_pelo_id()
    {
        JsonMemoryStore store = new JsonMemoryStore(_file);
        MemoryItem a = await store.AddAsync("fato A");
        await store.AddAsync("fato B");

        bool removed = await store.RemoveAsync(a.Id);
        IReadOnlyList<MemoryItem> all = await store.GetAllAsync();

        Assert.True(removed);
        Assert.Single(all);
        Assert.Equal("fato B", all[0].Content);
    }

    [Fact]
    public async Task Remove_id_inexistente_retorna_false()
    {
        JsonMemoryStore store = new JsonMemoryStore(_file);
        Assert.False(await store.RemoveAsync("naoexiste"));
    }

    public void Dispose()
    {
        string? dir = Path.GetDirectoryName(_file);
        if (dir is not null && Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}

public sealed class MemoryToolsTests
{
    [Fact]
    public async Task RememberTool_salva_e_retorna_id()
    {
        InMemoryMemoryStore memory = new InMemoryMemoryStore();
        RememberTool tool = new RememberTool(new FakeConfirmationService(true), memory);

        string result = await tool.RunAsync("Prefiro respostas curtas", "preferência");

        Assert.Contains("Memorizado", result);
        IReadOnlyList<MemoryItem> all = await memory.GetAllAsync();
        Assert.Single(all);
        Assert.Equal("Prefiro respostas curtas", all[0].Content);
    }

    [Fact]
    public async Task ForgetTool_remove_item_existente()
    {
        InMemoryMemoryStore memory = new InMemoryMemoryStore();
        MemoryItem item = await memory.AddAsync("algo temporário");
        ForgetTool tool = new ForgetTool(new FakeConfirmationService(true), memory);

        string result = await tool.RunAsync(item.Id);

        Assert.Contains("esquecida", result, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await memory.GetAllAsync());
    }

    [Fact]
    public async Task RecallTool_lista_memorias()
    {
        InMemoryMemoryStore memory = new InMemoryMemoryStore();
        await memory.AddAsync("fato importante", "projeto");
        RecallTool tool = new RecallTool(new FakeConfirmationService(true), memory);

        string result = await tool.RunAsync();

        Assert.Contains("fato importante", result);
        Assert.Contains("projeto", result);
    }
}

public sealed class SystemPromptMemoryTests
{
    [Fact]
    public void Build_detalha_memorias_relevantes_com_id()
    {
        List<MemoryItem> detailed = new List<MemoryItem>
        {
            new() { Id = "abc12345", Content = "Usa .NET 10", Category = "stack" },
        };

        string prompt = SystemPromptBuilder.Build(Array.Empty<ITool>(), preferences: null, memoryIndex: null, detailedMemories: detailed);

        Assert.Contains("Usa .NET 10", prompt);
        Assert.Contains("abc12345", prompt);
    }

    [Fact]
    public void Build_indice_mostra_titulo_mas_nao_o_conteudo_completo()
    {
        List<MemoryIndexEntry> index = new List<MemoryIndexEntry>
        {
            new("id111111", MemoryKind.Fact, "stack", "Preferência de framework"),
        };

        string prompt = SystemPromptBuilder.Build(Array.Empty<ITool>(), preferences: null, memoryIndex: index, detailedMemories: null);

        Assert.Contains("id111111", prompt);
        Assert.Contains("Preferência de framework", prompt);
        Assert.Contains("recuperar_memoria", prompt);
    }
}

/// <summary>Gerador de embeddings determinístico para testes (sem rede).</summary>
public sealed class FakeEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    private readonly Func<string, float[]> _map;

    public FakeEmbeddingGenerator(Func<string, float[]> map) => _map = map;

    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default)
    {
        GeneratedEmbeddings<Embedding<float>> result = new GeneratedEmbeddings<Embedding<float>>(
            values.Select(v => new Embedding<float>(_map(v))).ToList());
        return Task.FromResult(result);
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}

public sealed class MemoryRetrieverTests
{
    [Fact]
    public async Task GetIndex_nao_expoe_conteudo_completo()
    {
        InMemoryMemoryStore store = new InMemoryMemoryStore();
        await store.AddAsync(new MemoryItem { Title = "Deploy do X", Content = "passo a passo secreto", Kind = MemoryKind.Procedure });
        MemoryRetriever retriever = new MemoryRetriever(store);

        IReadOnlyList<MemoryIndexEntry> index = await retriever.GetIndexAsync();

        Assert.Single(index);
        Assert.Equal("Deploy do X", index[0].Title);
        Assert.Equal(MemoryKind.Procedure, index[0].Kind);
    }

    [Fact]
    public async Task Search_com_embeddings_ordena_por_similaridade()
    {
        InMemoryMemoryStore store = new InMemoryMemoryStore();
        MemoryItem git = await store.AddAsync(new MemoryItem { Content = "git", Keywords = { "git" } });
        MemoryItem deploy = await store.AddAsync(new MemoryItem { Content = "deploy", Keywords = { "deploy" } });

        // Vetores determinísticos: "git*" perto de (1,0), "deploy*" perto de (0,1).
        FakeEmbeddingGenerator generator = new FakeEmbeddingGenerator(text =>
            text.Contains("git", StringComparison.OrdinalIgnoreCase) ? new[] { 1f, 0f } : new[] { 0f, 1f });
        MemoryRetriever retriever = new MemoryRetriever(store, generator, "fake-model");

        IReadOnlyList<MemoryItem> results = await retriever.SearchAsync("como faço git status", topK: 1);

        Assert.Single(results);
        Assert.Equal(git.Id, results[0].Id);
        Assert.NotEqual(deploy.Id, results[0].Id);
    }

    [Fact]
    public async Task Search_sem_generator_usa_fallback_keyword()
    {
        InMemoryMemoryStore store = new InMemoryMemoryStore();
        await store.AddAsync(new MemoryItem { Content = "Sempre uso Clean Architecture", Keywords = { "arquitetura" } });
        MemoryItem alvo = await store.AddAsync(new MemoryItem { Content = "O deploy do projeto usa Docker", Keywords = { "deploy", "docker" } });
        MemoryRetriever retriever = new MemoryRetriever(store); // sem embeddings

        IReadOnlyList<MemoryItem> results = await retriever.SearchAsync("como fazer o deploy com docker", topK: 1);

        Assert.Single(results);
        Assert.Equal(alvo.Id, results[0].Id);
    }

    [Fact]
    public async Task GetPinned_retorna_apenas_fixadas_e_Search_ignora_fixadas()
    {
        InMemoryMemoryStore store = new InMemoryMemoryStore();
        MemoryItem pin = await store.AddAsync(new MemoryItem { Content = "regra fixa", Pinned = true, Keywords = { "regra" } });
        await store.AddAsync(new MemoryItem { Content = "regra comum", Keywords = { "regra" } });
        MemoryRetriever retriever = new MemoryRetriever(store);

        IReadOnlyList<MemoryItem> pinned = await retriever.GetPinnedAsync();
        IReadOnlyList<MemoryItem> search = await retriever.SearchAsync("regra", topK: 5);

        Assert.Single(pinned);
        Assert.Equal(pin.Id, pinned[0].Id);
        Assert.DoesNotContain(search, i => i.Id == pin.Id);
    }
}

public sealed class ProceduralMemoryToolTests
{
    [Fact]
    public async Task Memorizar_e_recuperar_procedimento_por_id()
    {
        InMemoryMemoryStore store = new InMemoryMemoryStore();
        FakeConfirmationService confirmation = new FakeConfirmationService(true);
        RememberProcedureTool remember = new RememberProcedureTool(confirmation, store);
        MemoryRetriever retriever = new MemoryRetriever(store);
        RetrieveMemoryTool retrieve = new RetrieveMemoryTool(confirmation, retriever);

        string saved = await remember.RunAsync("deploy do X", "1. build\n2. push", "deploy,x", "deploy");
        string id = saved.Split('[', ']')[1];

        string recovered = await retrieve.RunAsync(query: null, ids: id);

        Assert.Contains("deploy do X", recovered);
        Assert.Contains("1. build", recovered);
    }
}
