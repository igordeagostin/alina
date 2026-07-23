using Alina.Voice;

namespace Alina.Tests;

/// <summary>
/// A Alina continua ouvindo enquanto fala, então o microfone entrega as duas vozes na mesma
/// gravação. Estes testes fixam o compromisso: tirar a voz dela de volta sem nunca engolir
/// a sua, nem quando ela falou muito mais que você.
/// </summary>
public sealed class FiltroEcoTests
{
    private const string FalaDela =
        "Coloquei o build do projeto para rodar em paralelo. Aviso assim que ele terminar.";

    [Fact]
    public void Gravacao_que_so_pegou_a_voz_dela_nao_vira_pedido() =>
        Assert.Empty(FiltroEco.RemoverEco("coloquei o build do projeto para rodar em paralelo", FalaDela));

    [Fact]
    public void Sua_fala_por_cima_da_dela_sobrevive_ao_eco_que_a_cerca()
    {
        string sobrou = FiltroEco.RemoverEco(
            "coloquei o build do projeto para rodar em paralelo sobe pra homologação também", FalaDela);

        Assert.Equal("sobe pra homologação também", sobrou);
    }

    [Fact]
    public void Pedido_curto_no_meio_de_muito_eco_nao_se_perde()
    {
        string sobrou = FiltroEco.RemoverEco(
            "coloquei o build do projeto para rodar em paralelo chega aviso assim que ele terminar", FalaDela);

        Assert.Equal("chega", sobrou);
    }

    [Fact]
    public void Pergunta_curta_com_as_mesmas_palavras_passa_inteira() =>
        Assert.Equal("e o build?", FiltroEco.RemoverEco("e o build?", FalaDela));

    [Fact]
    public void Fala_que_reaproveita_as_palavras_dela_noutra_ordem_passa_inteira()
    {
        const string sua = "roda o projeto em paralelo também, não só o build";

        Assert.Equal(sua, FiltroEco.RemoverEco(sua, FalaDela));
    }

    [Fact]
    public void Acentuacao_e_pontuacao_originais_sao_preservadas()
    {
        string sobrou = FiltroEco.RemoverEco("aviso assim que ele terminar não, começa já!", FalaDela);

        Assert.Equal("não, começa já!", sobrou);
    }

    [Fact]
    public void Resto_de_transcricao_sem_palavra_de_verdade_e_descartado() =>
        Assert.Empty(FiltroEco.RemoverEco("coloquei o build do projeto para rodar ã", FalaDela));

    [Fact]
    public void Sem_nada_sendo_falado_a_transcricao_passa_intacta() =>
        Assert.Equal("coloquei o build", FiltroEco.RemoverEco("coloquei o build", null));

    [Fact]
    public void Transcricao_vazia_nao_vira_pedido() =>
        Assert.Empty(FiltroEco.RemoverEco("   ", FalaDela));
}
