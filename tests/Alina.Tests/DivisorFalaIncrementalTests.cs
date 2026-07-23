using Alina.Voice;

namespace Alina.Tests;

/// <summary>
/// Cobre o corte de frases sobre a resposta em streaming: cada frase deve sair assim
/// que fecha — nem antes (pedaço solto não é frase) nem só no fim da resposta.
/// </summary>
public sealed class DivisorFalaIncrementalTests
{
    [Fact]
    public void Frase_sai_assim_que_fecha()
    {
        DivisorFalaIncremental divisor = new DivisorFalaIncremental();

        Assert.Empty(divisor.Alimentar("Chrome fe"));
        Assert.Empty(divisor.Alimentar("chado"));

        IReadOnlyList<string> frases = divisor.Alimentar(". Se tinha aba");

        Assert.Equal(["Chrome fechado."], frases);
    }

    [Fact]
    public void Pontuacao_no_fim_do_pedaco_espera_o_proximo_caractere()
    {
        DivisorFalaIncremental divisor = new DivisorFalaIncremental();

        Assert.Empty(divisor.Alimentar("Feito."));

        Assert.Equal(["Feito."], divisor.Alimentar(" E agora"));
    }

    [Fact]
    public void Numero_decimal_nao_quebra_a_frase()
    {
        DivisorFalaIncremental divisor = new DivisorFalaIncremental();

        Assert.Empty(divisor.Alimentar("A versão 10."));
        Assert.Empty(divisor.Alimentar("1 saiu"));

        Assert.Equal(["A versão 10.1 saiu ontem."], divisor.Alimentar(" ontem. "));
    }

    [Fact]
    public void Quebra_de_linha_fecha_a_frase_sozinha()
    {
        DivisorFalaIncremental divisor = new DivisorFalaIncremental();

        IReadOnlyList<string> frases = divisor.Alimentar("Primeira linha\nSegunda");

        Assert.Equal(["Primeira linha"], frases);
        Assert.Equal(["Segunda"], divisor.Concluir());
    }

    [Fact]
    public void Concluir_devolve_o_resto_sem_pontuacao_final()
    {
        DivisorFalaIncremental divisor = new DivisorFalaIncremental();

        divisor.Alimentar("Tá na mão. Precisa de mais alguma coisa");

        Assert.Equal(["Precisa de mais alguma coisa"], divisor.Concluir());
    }

    [Fact]
    public void Concluir_sem_nada_pendente_devolve_vazio()
    {
        DivisorFalaIncremental divisor = new DivisorFalaIncremental();

        Assert.Equal(["Pronto."], divisor.Alimentar("Pronto. "));

        Assert.Empty(divisor.Concluir());
    }

    [Fact]
    public void Varias_frases_num_pedaco_so_saem_separadas()
    {
        DivisorFalaIncremental divisor = new DivisorFalaIncremental();

        IReadOnlyList<string> frases = divisor.Alimentar("Fechei o Chrome. Mais alguma coisa? Estou à disposição. ");

        Assert.Equal(["Fechei o Chrome.", "Mais alguma coisa?", "Estou à disposição."], frases);
    }

    [Fact]
    public void Texto_inteiro_e_preservado_na_juncao()
    {
        DivisorFalaIncremental divisor = new DivisorFalaIncremental();
        string resposta = "Navegador aberto no Google. Tá na mão! Quer que eu pesquise algo? Me diz.";
        List<string> frases = new List<string>();

        for (int i = 0; i < resposta.Length; i += 5)
        {
            frases.AddRange(divisor.Alimentar(resposta[i..Math.Min(i + 5, resposta.Length)]));
        }

        frases.AddRange(divisor.Concluir());

        Assert.Equal(resposta, string.Join(" ", frases));
    }
}
