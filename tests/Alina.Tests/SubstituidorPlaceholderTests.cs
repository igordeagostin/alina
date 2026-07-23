using Alina.Core.Ferramentas;

namespace Alina.Tests;

public sealed class SubstituidorPlaceholderTests
{
    private static Dictionary<string, string> Valores(string chave, string valor)
        => new(StringComparer.OrdinalIgnoreCase) { [chave] = valor };

    [Fact]
    public void Placeholder_simples_substitui_literalmente()
    {
        string resultado = SubstituidorPlaceholder.Aplicar("echo {mensagem}", Valores("mensagem", "olá & tchau"));

        Assert.Equal("echo olá & tchau", resultado);
    }

    [Fact]
    public void Sufixo_url_codifica_o_valor()
    {
        string resultado = SubstituidorPlaceholder.Aplicar(
            "https://www.google.com/search?q={termo:url}",
            Valores("termo", "C# async & await"));

        Assert.Equal("https://www.google.com/search?q=C%23%20async%20%26%20await", resultado);
    }

    [Fact]
    public void Sufixo_url_codifica_acentos()
    {
        string resultado = SubstituidorPlaceholder.Aplicar("?q={termo:url}", Valores("termo", "programação"));

        Assert.DoesNotContain("ç", resultado);
        Assert.StartsWith("?q=programa", resultado);
    }

    [Fact]
    public void Placeholder_sem_valor_correspondente_fica_intacto()
    {
        string resultado = SubstituidorPlaceholder.Aplicar("{a} {b}", Valores("a", "1"));

        Assert.Equal("1 {b}", resultado);
    }

    [Fact]
    public void Nome_do_parametro_ignora_maiusculas()
    {
        string resultado = SubstituidorPlaceholder.Aplicar("{Termo:URL}", Valores("termo", "a b"));

        Assert.Equal("a%20b", resultado);
    }

    [Fact]
    public void Formato_desconhecido_nao_codifica()
    {
        string resultado = SubstituidorPlaceholder.Aplicar("{termo:base64}", Valores("termo", "a b"));

        Assert.Equal("a b", resultado);
    }

    [Fact]
    public void Referencias_lista_os_nomes_independente_do_formato()
    {
        string[] nomes = SubstituidorPlaceholder.Referencias("{a}/{b:url}/{a}").ToArray();

        Assert.Equal(["a", "b", "a"], nomes);
    }

    [Fact]
    public void Modelo_vazio_vira_string_vazia()
    {
        Assert.Equal(string.Empty, SubstituidorPlaceholder.Aplicar(null, Valores("a", "1")));
    }
}
