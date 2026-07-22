using System.Text.Json;
using Alina.Core.Tools;
using Alina.Tools.Plugins;
using Microsoft.Extensions.AI;

namespace Alina.Tests;

public sealed class PluginLoaderTests : IDisposable
{
    private readonly string _dir;

    public PluginLoaderTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "alina-plugins-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(_dir);
    }

    private void WritePlugin(string fileName, string json) => File.WriteAllText(Path.Combine(_dir, fileName), json);

    [Fact]
    public void Carrega_plugin_valido_como_tool()
    {
        WritePlugin("deploy.plugin.json", """
            {
              "name": "deploy_app",
              "description": "Faz deploy",
              "command": "pwsh",
              "args": ["-File", "deploy.ps1", "--env", "{ambiente}"],
              "parameters": [ { "name": "ambiente", "description": "prod ou staging" } ]
            }
            """);

        PluginLoadResult result = PluginLoader.Load(_dir, new FakeConfirmationService(true));

        Assert.Single(result.Tools);
        Assert.Empty(result.Warnings);
        Assert.Equal("deploy_app", result.Tools[0].Name);
        Assert.True(result.Tools[0].RequiresConfirmation); // default true
    }

    [Fact]
    public void Manifesto_invalido_vira_warning_nao_excecao()
    {
        WritePlugin("quebrado.plugin.json", "{ isso nao e json valido ");
        WritePlugin("sem_comando.plugin.json", """{ "name": "x", "description": "y" }""");

        PluginLoadResult result = PluginLoader.Load(_dir, new FakeConfirmationService(true));

        Assert.Empty(result.Tools);
        Assert.Equal(2, result.Warnings.Count);
    }

    [Fact]
    public void Nomes_duplicados_geram_warning()
    {
        string body = """{ "name": "dup", "description": "d", "command": "echo" }""";
        WritePlugin("a.plugin.json", body);
        WritePlugin("b.plugin.json", body);

        PluginLoadResult result = PluginLoader.Load(_dir, new FakeConfirmationService(true));

        Assert.Single(result.Tools);
        Assert.Contains(result.Warnings, w => w.Contains("duplicado"));
    }

    [Fact]
    public void Diretorio_inexistente_retorna_vazio()
    {
        PluginLoadResult result = PluginLoader.Load(Path.Combine(_dir, "nao-existe"), new FakeConfirmationService(true));
        Assert.Empty(result.Tools);
        Assert.Empty(result.Warnings);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }
}

public sealed class ManifestFunctionTests
{
    private static PluginManifest SampleManifest() => new()
    {
        Name = "eco",
        Description = "ecoa uma mensagem",
        RequiresConfirmation = true,
        Command = OperatingSystem.IsWindows() ? "cmd" : "echo",
        Args = OperatingSystem.IsWindows()
            ? new[] { "/c", "echo", "{mensagem}" }
            : new[] { "{mensagem}" },
        Parameters = new List<PluginParameter>
        {
            new() { Name = "mensagem", Description = "texto a ecoar", Required = true },
        },
    };

    [Fact]
    public void Schema_dinamico_reflete_parametros()
    {
        PluginTool tool = new PluginTool(SampleManifest(), new FakeConfirmationService(true));
        JsonElement schema = tool.AsAIFunction().JsonSchema;

        Assert.Equal(JsonValueKind.Object, schema.ValueKind);
        Assert.True(schema.GetProperty("properties").TryGetProperty("mensagem", out _));
        Assert.Contains("mensagem", schema.GetProperty("required").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public async Task Confirmacao_negada_nao_executa()
    {
        FakeConfirmationService confirmation = new FakeConfirmationService(result: false);
        AIFunction function = new PluginTool(SampleManifest(), confirmation).AsAIFunction();

        AIFunctionArguments args = new AIFunctionArguments { ["mensagem"] = "alina-plugin-ok" };
        object? result = await function.InvokeAsync(args);

        Assert.Equal(1, confirmation.Calls);
        Assert.Contains("cancelada", result?.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Confirmacao_aprovada_executa_e_substitui_placeholder()
    {
        FakeConfirmationService confirmation = new FakeConfirmationService(result: true);
        AIFunction function = new PluginTool(SampleManifest(), confirmation).AsAIFunction();

        AIFunctionArguments args = new AIFunctionArguments { ["mensagem"] = "alina-plugin-ok" };
        object? result = await function.InvokeAsync(args);

        Assert.Equal(1, confirmation.Calls);
        Assert.Contains("alina-plugin-ok", result?.ToString());
    }

    [Fact]
    public async Task Parametro_obrigatorio_ausente_retorna_erro()
    {
        AIFunction function = new PluginTool(SampleManifest(), new FakeConfirmationService(true)).AsAIFunction();

        object? result = await function.InvokeAsync(new AIFunctionArguments());

        Assert.Contains("obrigatório", result?.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
