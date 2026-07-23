using System.Text.Json;
using Alina.Core.Ferramentas;
using Alina.Core.Habilidades;
using Alina.Core.Orchestration;
using Alina.Core.Tools;
using Alina.Infrastructure.Ferramentas;
using Alina.Tools.Ferramentas;
using Alina.Tools.Habilidades;
using Microsoft.Extensions.AI;

namespace Alina.Tests;

/// <summary>
/// Cobre o que a Alina consegue fazer sobre si mesma no meio de uma conversa:
/// ler, criar e corrigir as próprias ferramentas e habilidades sem passar pelas
/// telas de configuração.
/// </summary>
public sealed class FerramentasEmConversaTests : IDisposable
{
    private readonly string _dir;
    private readonly FileFerramentaStore _store;

    public FerramentasEmConversaTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "alina-conversa-" + Guid.NewGuid().ToString("n"));
        _store = new FileFerramentaStore(_dir);
    }

    private static DefinicaoFerramenta Deploy() => new()
    {
        Nome = "deploy_diario",
        Descricao = "Publica o Diário",
        Comando = "powershell",
        Argumentos = ["-NoProfile", "-File", "deploy.ps1", "{ambiente}"],
        Parametros = [new ParametroFerramenta { Nome = "ambiente", Descricao = "alvo", Obrigatorio = true }],
        ExigeConfirmacao = true,
    };

    [Fact]
    public async Task Obter_ferramenta_devolve_a_definicao_inteira_para_edicao()
    {
        await _store.SalvarAsync(Deploy());
        ObterFerramentaTool tool = new ObterFerramentaTool(new FakeConfirmationService(true), _store);

        string json = await tool.RunAsync("deploy_diario");
        DefinicaoFerramenta? lida = JsonSerializer.Deserialize<DefinicaoFerramenta>(json);

        Assert.NotNull(lida);
        Assert.Equal("powershell", lida!.Comando);
        Assert.Contains("deploy.ps1", lida.Argumentos);
        Assert.Equal("ambiente", Assert.Single(lida.Parametros).Nome);
        Assert.True(lida.ExigeConfirmacao);
    }

    [Fact]
    public async Task Obter_ferramenta_inexistente_orienta_a_listar()
    {
        ObterFerramentaTool tool = new ObterFerramentaTool(new FakeConfirmationService(true), _store);

        string resposta = await tool.RunAsync("nao_existe");

        Assert.Contains("listar_ferramentas", resposta);
    }

    [Fact]
    public async Task Listar_ferramentas_mostra_nome_descricao_e_confirmacao()
    {
        await _store.SalvarAsync(Deploy());
        ListarFerramentasTool tool = new ListarFerramentasTool(new FakeConfirmationService(true), _store);

        string resposta = await tool.RunAsync();

        Assert.Contains("deploy_diario", resposta);
        Assert.Contains("Publica o Diário", resposta);
        Assert.Contains("pede confirmação", resposta);
    }

    [Fact]
    public async Task Criar_ferramenta_com_nome_existente_atualiza_a_definicao()
    {
        await _store.SalvarAsync(Deploy());
        CriarFerramentaTool tool = new CriarFerramentaTool(new FakeConfirmationService(true), _store);

        string json = """
            {
              "nome": "deploy_diario",
              "descricao": "Publica o Diário",
              "comando": "powershell",
              "argumentos": ["-NoProfile", "-File", "deploy.ps1", "{ambiente}", "-Verbose"],
              "exigeConfirmacao": true,
              "parametros": [{ "nome": "ambiente", "descricao": "alvo", "obrigatorio": true }]
            }
            """;
        string resposta = await tool.RunAsync(json);

        Assert.Contains("atualizada", resposta);
        DefinicaoFerramenta? lida = await _store.ObterAsync("deploy_diario");
        Assert.Contains("-Verbose", lida!.Argumentos);
    }

    [Fact]
    public void Descricao_de_criar_ferramenta_ensina_as_regras_da_definicao()
    {
        CriarFerramentaTool tool = new CriarFerramentaTool(new FakeConfirmationService(true), _store);

        Assert.Contains("exigeConfirmacao", tool.Description);
        Assert.Contains("Diretorio", tool.Description);
        Assert.Contains("obter_ferramenta", tool.Description);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }
}

public sealed class HabilidadeEmConversaTests
{
    [Fact]
    public async Task Aprender_habilidade_existente_regrava_o_conteudo_revisado()
    {
        InMemoryHabilidadeStore store = new InMemoryHabilidadeStore();
        await store.SalvarAsync(new Habilidade { Nome = "Deploy do Diário", Descricao = "publica", Conteudo = "passo 1" });

        FakeConfirmationService confirmation = new FakeConfirmationService(true);
        AprenderHabilidadeTool tool = new AprenderHabilidadeTool(confirmation, store);

        await tool.RunAsync("Deploy do Diário", "publica", "passo 1\npasso 2 corrigido");

        Habilidade? atual = await store.ObterAsync("Deploy do Diário");
        Assert.Contains("passo 2 corrigido", atual!.Conteudo);
    }

    [Fact]
    public void Descricao_de_aprender_habilidade_cobre_a_edicao()
    {
        AprenderHabilidadeTool tool = new AprenderHabilidadeTool(new FakeConfirmationService(true), new InMemoryHabilidadeStore());

        Assert.Contains("usar_habilidade", tool.Description);
        Assert.Contains("EDITAR", tool.Description);
    }
}

public sealed class SystemPromptAprendizadoTests
{
    private static string Montar(IReadOnlyList<HabilidadeResumo>? habilidades = null)
        => SystemPromptBuilder.Build(Array.Empty<ITool>(), preferences: null, habilidades: habilidades);

    [Fact]
    public void Prompt_diz_que_ela_pode_aprender_na_propria_conversa()
    {
        string prompt = Montar();

        Assert.Contains("Aprender durante a conversa", prompt);
        Assert.Contains("obter_ferramenta", prompt);
        Assert.Contains("aprender_habilidade", prompt);
    }

    [Fact]
    public void Regra_de_correcao_so_aparece_quando_ha_habilidades()
    {
        Assert.DoesNotContain("Corrigir o que você já sabe", Montar());

        string comHabilidade = Montar([new HabilidadeResumo("deploy-diario", "publica o Diário")]);
        Assert.Contains("Corrigir o que você já sabe", comHabilidade);
        Assert.Contains("só grave se ele aceitar", comHabilidade);
    }
}
