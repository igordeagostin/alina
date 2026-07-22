using Alina.Core.IO;

namespace Alina.Tests;

public sealed class BuscaProjetosTests : IDisposable
{
    private readonly string _raiz;

    public BuscaProjetosTests()
    {
        _raiz = Path.Combine(Path.GetTempPath(), "alina-busca-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_raiz, "diario", "diario-atualizacao", "src", "Atualizacao.API"));
        Directory.CreateDirectory(Path.Combine(_raiz, "school", "schoolmais-api"));
        Directory.CreateDirectory(Path.Combine(_raiz, "site", "node_modules", "diario-api"));
        File.WriteAllText(Path.Combine(_raiz, "diario", "diario-atualizacao", "src", "Atualizacao.API", "Atualizacao.API.csproj"), "x");
    }

    [Fact]
    public void Localiza_projeto_pelo_nome_falado_com_acento_e_espaco()
    {
        IReadOnlyList<ProjetoEncontrado> achados = BuscaProjetos.Localizar([_raiz], "diário api");

        Assert.NotEmpty(achados);
        Assert.Equal(Path.Combine(_raiz, "diario", "diario-atualizacao", "src", "Atualizacao.API"), achados[0].Caminho);
        Assert.Equal("Atualizacao.API.csproj", achados[0].Marcador);
    }

    [Fact]
    public void Nome_exato_da_pasta_vence_os_demais_candidatos()
    {
        IReadOnlyList<ProjetoEncontrado> achados = BuscaProjetos.Localizar([_raiz], "schoolmais api");

        Assert.Equal(Path.Combine(_raiz, "school", "schoolmais-api"), achados[0].Caminho);
    }

    [Fact]
    public void Ignora_pastas_de_ruido()
    {
        IReadOnlyList<ProjetoEncontrado> achados = BuscaProjetos.Localizar([_raiz], "diario api");

        Assert.DoesNotContain(achados, p => p.Caminho.Contains("node_modules", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Termo_sem_correspondencia_devolve_vazio()
        => Assert.Empty(BuscaProjetos.Localizar([_raiz], "financeiro"));

    [Fact]
    public void Raiz_inexistente_nao_quebra()
        => Assert.Empty(BuscaProjetos.Localizar([Path.Combine(_raiz, "nao-existe")], "diario"));

    public void Dispose()
    {
        if (Directory.Exists(_raiz))
        {
            Directory.Delete(_raiz, recursive: true);
        }
    }
}
