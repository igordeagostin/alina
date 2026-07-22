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

    private static DefinicaoFerramenta ComCaminho(TipoParametroFerramenta tipo) => new()
    {
        Nome = "abrir",
        Descricao = "abre uma pasta",
        ExigeConfirmacao = false,
        Comando = OperatingSystem.IsWindows() ? "cmd" : "echo",
        Argumentos = OperatingSystem.IsWindows() ? ["/c", "echo", "{caminho}"] : ["{caminho}"],
        Parametros = [new ParametroFerramenta { Nome = "caminho", Descricao = "pasta", Tipo = tipo }],
    };

    [Fact]
    public async Task Diretorio_inexistente_nao_executa_e_orienta_a_localizar()
    {
        FakeConfirmationService confirmation = new FakeConfirmationService(result: true);
        AIFunction function = new FerramentaDeclarada(ComCaminho(TipoParametroFerramenta.Diretorio), confirmation).AsAIFunction();

        object? result = await function.InvokeAsync(new AIFunctionArguments { ["caminho"] = "diario api" });

        string texto = result?.ToString() ?? string.Empty;
        Assert.Contains("não existe", texto, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("localizar_projeto", texto);
        Assert.DoesNotContain("[exit", texto);
    }

    [Fact]
    public async Task Parametro_chamado_caminho_valida_mesmo_sem_tipo_declarado()
    {
        AIFunction function = new FerramentaDeclarada(ComCaminho(TipoParametroFerramenta.Automatico), new FakeConfirmationService(true)).AsAIFunction();

        object? result = await function.InvokeAsync(new AIFunctionArguments { ["caminho"] = "projeto que nao existe" });

        Assert.Contains("Erro", result?.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Diretorio_existente_executa_normalmente()
    {
        AIFunction function = new FerramentaDeclarada(ComCaminho(TipoParametroFerramenta.Diretorio), new FakeConfirmationService(true)).AsAIFunction();

        object? result = await function.InvokeAsync(new AIFunctionArguments { ["caminho"] = Path.GetTempPath() });

        Assert.Contains("[exit 0]", result?.ToString());
    }

    [Fact]
    public async Task Tipo_texto_explicito_desliga_a_validacao_de_caminho()
    {
        AIFunction function = new FerramentaDeclarada(ComCaminho(TipoParametroFerramenta.Texto), new FakeConfirmationService(true)).AsAIFunction();

        object? result = await function.InvokeAsync(new AIFunctionArguments { ["caminho"] = "pasta-que-nao-existe" });

        Assert.Contains("pasta-que-nao-existe", result?.ToString());
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

public sealed class GeradorFerramentaTests
{
    private static IReadOnlyList<ChatMessage> Historico(string texto)
        => new[] { new ChatMessage(ChatRole.User, texto) };

    private static DefinicaoFerramenta Existente() => new DefinicaoFerramenta
    {
        Nome = "deploy_diario",
        Descricao = "Publica o Diário",
        Comando = "powershell",
        Argumentos = ["-NoProfile", "-Command", "deploy.ps1"],
    };

    [Fact]
    public async Task ContinuarAsync_em_edicao_injeta_a_definicao_atual()
    {
        FakeChatClient client = new FakeChatClient((_, _) =>
            new ChatResponse(new ChatMessage(ChatRole.Assistant, "{\"mensagem\":\"ok\",\"pronto\":false}")));
        GeradorFerramenta gerador = new GeradorFerramenta(client);

        await gerador.ContinuarAsync(Historico("passa a rodar em homologação"), new ContextoFerramenta(Existente()));

        string contexto = string.Concat(
            client.LastMessages!.Where(m => m.Role == ChatRole.System).Select(m => m.Text));
        Assert.Contains("editar uma \"ferramenta\" que já existe", contexto);
        Assert.Contains("deploy_diario", contexto);
        Assert.Contains("deploy.ps1", contexto);
    }

    [Fact]
    public async Task ContinuarAsync_sem_contexto_trata_como_criacao()
    {
        FakeChatClient client = new FakeChatClient((_, _) =>
            new ChatResponse(new ChatMessage(ChatRole.Assistant, "{\"mensagem\":\"ok\",\"pronto\":false}")));
        GeradorFerramenta gerador = new GeradorFerramenta(client);

        await gerador.ContinuarAsync(Historico("quero automatizar o deploy"));

        string contexto = string.Concat(
            client.LastMessages!.Where(m => m.Role == ChatRole.System).Select(m => m.Text));
        Assert.Contains("criar uma nova \"ferramenta\"", contexto);
        Assert.DoesNotContain("Ferramenta atual", contexto);
    }

    [Fact]
    public async Task ContinuarAsync_com_pronto_retorna_definicao_completa()
    {
        string json = """
            {
              "mensagem": "Montei a ferramenta.",
              "pronto": true,
              "nome": "abrir_terminal",
              "descricao": "Abre o terminal numa pasta",
              "comando": "wt",
              "argumentos": ["-d", "{pasta}"],
              "exigeConfirmacao": false,
              "parametros": [{ "nome": "pasta", "descricao": "pasta alvo", "obrigatorio": true, "tipo": "Diretorio" }]
            }
            """;
        FakeChatClient client = new FakeChatClient((_, _) => new ChatResponse(new ChatMessage(ChatRole.Assistant, json)));
        GeradorFerramenta gerador = new GeradorFerramenta(client);

        RespostaGeracaoFerramenta resposta = await gerador.ContinuarAsync(Historico("abre o terminal numa pasta"));

        Assert.NotNull(resposta.Rascunho);
        Assert.Equal("abrir_terminal", resposta.Rascunho!.Definicao.Nome);
        Assert.False(resposta.Rascunho.Definicao.ExigeConfirmacao);
        Assert.Equal(TipoParametroFerramenta.Diretorio, resposta.Rascunho.Definicao.Parametros[0].Tipo);
    }
}
