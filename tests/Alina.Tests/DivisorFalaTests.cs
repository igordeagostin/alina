using Alina.Voice;

namespace Alina.Tests;

public sealed class DivisorFalaTests
{
    [Fact]
    public void Texto_vazio_nao_gera_blocos()
    {
        Assert.Empty(DivisorFala.Dividir("   "));
    }

    [Fact]
    public void Primeiro_bloco_sai_curto_para_comecar_a_falar_antes()
    {
        string resposta = string.Concat(Enumerable.Repeat("Achei o problema no build. ", 12));

        IReadOnlyList<string> blocos = DivisorFala.Dividir(resposta);

        Assert.True(blocos.Count > 2);
        Assert.True(blocos[0].Length <= 120, $"primeiro bloco longo demais: {blocos[0].Length}");
    }

    [Fact]
    public void Blocos_preservam_o_texto_original()
    {
        string resposta = "Rodei os testes. Passaram 42, falhou 1: o de permissão. Quer que eu investigue?";

        string junto = string.Join(" ", DivisorFala.Dividir(resposta));

        Assert.Equal(Normalizar(resposta), Normalizar(junto));
    }

    [Fact]
    public void Numero_decimal_nao_quebra_a_frase()
    {
        IReadOnlyList<string> blocos = DivisorFala.Dividir("A versão 10.1 saiu ontem.");

        Assert.Single(blocos);
    }

    [Fact]
    public void Quebra_de_linha_encerra_o_bloco()
    {
        string resposta = string.Concat(Enumerable.Repeat("Primeira linha bem comprida para estourar o limite\n", 4));

        Assert.True(DivisorFala.Dividir(resposta).Count > 1);
    }

    private static string Normalizar(string texto) => string.Join(' ', texto.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
