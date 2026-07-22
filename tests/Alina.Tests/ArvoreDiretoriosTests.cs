using Alina.Core.IO;

namespace Alina.Tests;

public sealed class ArvoreDiretoriosTests
{
    private static string CriarArvore(out string raiz)
    {
        raiz = Path.Combine(Path.GetTempPath(), "alina-arv-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(raiz, "diario", "diario-api"));
        Directory.CreateDirectory(Path.Combine(raiz, "diario", "diario-web"));
        Directory.CreateDirectory(Path.Combine(raiz, "bin"));
        File.WriteAllText(Path.Combine(raiz, "README.md"), "x");
        return raiz;
    }

    [Fact]
    public void Montar_lista_subprojetos_respeitando_profundidade()
    {
        string raiz = CriarArvore(out string _);
        try
        {
            string arvore = ArvoreDiretorios.Montar([raiz], profundidadeMaxima: 3);

            Assert.Contains("diario/", arvore);
            Assert.Contains("diario-api/", arvore);
            Assert.Contains("diario-web/", arvore);
        }
        finally
        {
            Directory.Delete(raiz, recursive: true);
        }
    }

    [Fact]
    public void Montar_ignora_pastas_de_ruido()
    {
        string raiz = CriarArvore(out string _);
        try
        {
            string arvore = ArvoreDiretorios.Montar([raiz], profundidadeMaxima: 3);

            Assert.DoesNotContain("bin/", arvore);
        }
        finally
        {
            Directory.Delete(raiz, recursive: true);
        }
    }

    [Fact]
    public void Montar_inclui_arquivos_somente_quando_pedido()
    {
        string raiz = CriarArvore(out string _);
        try
        {
            Assert.DoesNotContain("README.md", ArvoreDiretorios.Montar([raiz], incluirArquivos: false));
            Assert.Contains("README.md", ArvoreDiretorios.Montar([raiz], incluirArquivos: true));
        }
        finally
        {
            Directory.Delete(raiz, recursive: true);
        }
    }

    [Fact]
    public void Montar_ignora_raizes_inexistentes()
    {
        string inexistente = Path.Combine(Path.GetTempPath(), "alina-nao-existe-" + Guid.NewGuid().ToString("N"));

        Assert.Equal(string.Empty, ArvoreDiretorios.Montar([inexistente]));
    }
}
