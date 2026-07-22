using System.Text.Json;
using Alina.Core.Ferramentas;
using Alina.Infrastructure.Ferramentas;
using Microsoft.Extensions.AI;

namespace Alina.Tests;

public sealed class FuncaoFerramentaTests
{
    private static DefinicaoFerramenta Exemplo() => new()
    {
        Nome = "eco",
        Descricao = "ecoa uma mensagem",
        ExigeConfirmacao = true,
        Comando = OperatingSystem.IsWindows() ? "cmd" : "echo",
        Argumentos = OperatingSystem.IsWindows()
            ? ["/c", "echo", "{mensagem}"]
            : ["{mensagem}"],
        Parametros =
        [
            new ParametroFerramenta { Nome = "mensagem", Descricao = "texto a ecoar", Obrigatorio = true },
        ],
    };

    [Fact]
    public void Schema_dinamico_reflete_parametros()
    {
        FerramentaDeclarada tool = new FerramentaDeclarada(Exemplo(), new FakeConfirmationService(true));
        JsonElement schema = tool.AsAIFunction().JsonSchema;

        Assert.Equal(JsonValueKind.Object, schema.ValueKind);
        Assert.True(schema.GetProperty("properties").TryGetProperty("mensagem", out _));
        Assert.Contains("mensagem", schema.GetProperty("required").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public async Task Confirmacao_negada_nao_executa()
    {
        FakeConfirmationService confirmation = new FakeConfirmationService(result: false);
        AIFunction function = new FerramentaDeclarada(Exemplo(), confirmation).AsAIFunction();

        AIFunctionArguments args = new AIFunctionArguments { ["mensagem"] = "alina-ferr-ok" };
        object? result = await function.InvokeAsync(args);

        Assert.Equal(1, confirmation.Calls);
        Assert.Contains("cancelada", result?.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Confirmacao_aprovada_executa_e_substitui_placeholder()
    {
        FakeConfirmationService confirmation = new FakeConfirmationService(result: true);
        AIFunction function = new FerramentaDeclarada(Exemplo(), confirmation).AsAIFunction();

        AIFunctionArguments args = new AIFunctionArguments { ["mensagem"] = "alina-ferr-ok" };
        object? result = await function.InvokeAsync(args);

        Assert.Equal(1, confirmation.Calls);
        Assert.Contains("alina-ferr-ok", result?.ToString());
    }

    [Fact]
    public async Task Sem_confirmacao_nao_pergunta()
    {
        DefinicaoFerramenta definicao = Exemplo();
        definicao.ExigeConfirmacao = false;
        FakeConfirmationService confirmation = new FakeConfirmationService(result: true);
        AIFunction function = new FerramentaDeclarada(definicao, confirmation).AsAIFunction();

        object? result = await function.InvokeAsync(new AIFunctionArguments { ["mensagem"] = "sem-perguntar" });

        Assert.Equal(0, confirmation.Calls);
        Assert.Contains("sem-perguntar", result?.ToString());
    }

    [Fact]
    public async Task Parametro_obrigatorio_ausente_retorna_erro()
    {
        AIFunction function = new FerramentaDeclarada(Exemplo(), new FakeConfirmationService(true)).AsAIFunction();

        object? result = await function.InvokeAsync(new AIFunctionArguments());

        Assert.Contains("obrigatório", result?.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class FileFerramentaStoreTests : IDisposable
{
    private readonly string _dir;

    public FileFerramentaStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "alina-ferramentas-" + Guid.NewGuid().ToString("n"));
    }

    [Fact]
    public void Semeia_ferramentas_padrao_em_pasta_nova()
    {
        FileFerramentaStore store = new FileFerramentaStore(_dir);

        IReadOnlyList<DefinicaoFerramenta> definicoes = store.LerDefinicoes();

        Assert.Contains(definicoes, d => d.Nome == "abrir_no_vscode");
        Assert.Contains(definicoes, d => d.Nome == "saudar");
    }

    [Fact]
    public async Task Salvar_lista_obter_e_remover_roundtrip()
    {
        FileFerramentaStore store = new FileFerramentaStore(_dir);

        DefinicaoFerramenta definicao = new DefinicaoFerramenta
        {
            Nome = "Minha Ferramenta",
            Descricao = "faz algo",
            Comando = "echo",
            Argumentos = ["oi"],
            ExigeConfirmacao = false,
        };

        await store.SalvarAsync(definicao);

        Assert.Equal("minha_ferramenta", definicao.Nome);

        DefinicaoFerramenta? lida = await store.ObterAsync("minha_ferramenta");
        Assert.NotNull(lida);
        Assert.Equal("faz algo", lida!.Descricao);
        Assert.False(lida.ExigeConfirmacao);

        Assert.True(await store.RemoverAsync("minha_ferramenta"));
        Assert.Null(await store.ObterAsync("minha_ferramenta"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }
}

public sealed class FerramentaProviderTests : IDisposable
{
    private readonly string _dir;
    private readonly Alina.Infrastructure.Configuration.StorageOptions _options;

    public FerramentaProviderTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "alina-ferr-prov-" + Guid.NewGuid().ToString("n"));
        _options = new Alina.Infrastructure.Configuration.StorageOptions { DataDirectory = _dir };
    }

    [Fact]
    public async Task Reflete_ferramenta_criada_sem_reiniciar()
    {
        FileFerramentaStore store = new FileFerramentaStore(_options);
        FerramentaProvider provider = new FerramentaProvider(store, new FakeConfirmationService(true), _options);

        int inicial = provider.ObterFerramentas().Count;

        await store.SalvarAsync(new DefinicaoFerramenta
        {
            Nome = "nova_ferramenta",
            Descricao = "d",
            Comando = "echo",
            Argumentos = ["x"],
        });

        IReadOnlyList<Alina.Core.Tools.ITool> depois = provider.ObterFerramentas();
        Assert.Equal(inicial + 1, depois.Count);
        Assert.Contains(depois, t => t.Name == "nova_ferramenta");
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }
}
