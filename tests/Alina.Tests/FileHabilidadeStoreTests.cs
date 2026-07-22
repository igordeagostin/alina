using Alina.Core.Habilidades;
using Alina.Infrastructure.Habilidades;

namespace Alina.Tests;

public sealed class FileHabilidadeStoreTests : IDisposable
{
    private readonly string _tempDir;

    public FileHabilidadeStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "alina-tests", Guid.NewGuid().ToString("n"));
    }

    [Fact]
    public async Task SalvarAsync_grava_md_com_frontmatter_e_slug()
    {
        FileHabilidadeStore store = new FileHabilidadeStore(_tempDir);

        await store.SalvarAsync(new Habilidade
        {
            Nome = "Deploy da API do Diário",
            Descricao = "Como subir a API do Diário",
            Conteudo = "1. dotnet publish\n2. copiar artefatos",
        });

        string arquivo = Path.Combine(_tempDir, "deploy-da-api-do-diario.md");
        Assert.True(File.Exists(arquivo));

        string texto = await File.ReadAllTextAsync(arquivo);
        Assert.Contains("nome: deploy-da-api-do-diario", texto);
        Assert.Contains("descricao: Como subir a API do Diário", texto);
        Assert.Contains("dotnet publish", texto);
    }

    [Fact]
    public async Task Salvar_Obter_preserva_conteudo_e_descricao()
    {
        FileHabilidadeStore store = new FileHabilidadeStore(_tempDir);

        await store.SalvarAsync(new Habilidade
        {
            Nome = "Rodar Testes",
            Descricao = "Como rodar a suíte",
            Conteudo = "dotnet test Alina.slnx",
        });

        Habilidade? recuperada = await store.ObterAsync("Rodar Testes");

        Assert.NotNull(recuperada);
        Assert.Equal("rodar-testes", recuperada!.Nome);
        Assert.Equal("Como rodar a suíte", recuperada.Descricao);
        Assert.Equal("dotnet test Alina.slnx", recuperada.Conteudo);
    }

    [Fact]
    public async Task ListarAsync_retorna_indice_ordenado()
    {
        FileHabilidadeStore store = new FileHabilidadeStore(_tempDir);
        await store.SalvarAsync(new Habilidade { Nome = "Zelda", Descricao = "z", Conteudo = "..." });
        await store.SalvarAsync(new Habilidade { Nome = "Alfa", Descricao = "a", Conteudo = "..." });

        IReadOnlyList<HabilidadeResumo> indice = await store.ListarAsync();

        Assert.Equal(2, indice.Count);
        Assert.Equal("alfa", indice[0].Nome);
        Assert.Equal("zelda", indice[1].Nome);
    }

    [Fact]
    public async Task ExisteAsync_reconhece_o_slug()
    {
        FileHabilidadeStore store = new FileHabilidadeStore(_tempDir);
        await store.SalvarAsync(new Habilidade { Nome = "Deploy da API", Descricao = "d", Conteudo = "..." });

        Assert.True(await store.ExisteAsync("Deploy da API"));
        Assert.True(await store.ExisteAsync("deploy-da-api"));
        Assert.False(await store.ExisteAsync("outra coisa"));
    }

    [Fact]
    public async Task RemoverAsync_apaga_a_habilidade()
    {
        FileHabilidadeStore store = new FileHabilidadeStore(_tempDir);
        await store.SalvarAsync(new Habilidade { Nome = "Temporária", Descricao = "t", Conteudo = "..." });

        Assert.True(await store.RemoverAsync("Temporária"));
        Assert.False(await store.RemoverAsync("Temporária"));
        Assert.Null(await store.ObterAsync("Temporária"));
    }

    [Fact]
    public async Task ObterAsync_retorna_null_quando_nao_existe()
    {
        FileHabilidadeStore store = new FileHabilidadeStore(_tempDir);
        Assert.Null(await store.ObterAsync("inexistente"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}
