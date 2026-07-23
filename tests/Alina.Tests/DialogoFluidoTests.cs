using Alina.Voice;

namespace Alina.Tests;

/// <summary>
/// Cobre as decisões que tornam o diálogo fluido: o que conta como você retomando a
/// palavra sobre a fala da Alina e o que conta como um pedido para ela parar de verdade.
/// </summary>
public sealed class DialogoFluidoTests
{
    private static readonly string[] PalavrasParar = ["espera", "chega", "cancela", "esquece"];

    [Fact]
    public void Ruido_curto_sobre_a_fala_dela_nao_conta_como_interrupcao()
    {
        DetectorInicioFala detector = new DetectorInicioFala();

        Assert.False(detector.Alimentar(0.9f));
        Assert.False(detector.Alimentar(0.9f));
    }

    [Fact]
    public void Som_baixo_nunca_interrompe_por_mais_que_dure()
    {
        DetectorInicioFala detector = new DetectorInicioFala();

        for (int i = 0; i < 200; i++)
        {
            Assert.False(detector.Alimentar(0.05f));
        }
    }

    [Fact]
    public async Task Fala_alta_e_sustentada_confirma_a_retomada_da_palavra()
    {
        DetectorInicioFala detector = new DetectorInicioFala();

        detector.Alimentar(0.5f);
        await Task.Delay(320);

        Assert.True(detector.Alimentar(0.5f));
    }

    [Fact]
    public async Task Silencio_no_meio_zera_a_contagem_da_fala()
    {
        DetectorInicioFala detector = new DetectorInicioFala();

        detector.Alimentar(0.5f);
        await Task.Delay(320);
        detector.Alimentar(0.01f);

        Assert.False(detector.Alimentar(0.5f));
    }

    [Theory]
    [InlineData("chega")]
    [InlineData("Espera!")]
    [InlineData("cancela isso")]
    [InlineData("esquece, deixa pra lá")]
    public void Pedido_curto_e_explicito_para_o_trabalho(string fala) =>
        Assert.True(ComandoInterrupcao.EhPedidoDeParar(fala, PalavrasParar));

    [Theory]
    [InlineData("")]
    [InlineData("roda o build do projeto")]
    [InlineData("espera aí, na verdade eu queria que você rodasse os testes antes do build")]
    [InlineData("me lembra de cancelar a assinatura amanhã")]
    public void Fala_comum_nunca_e_confundida_com_pedido_de_parar(string fala) =>
        Assert.False(ComandoInterrupcao.EhPedidoDeParar(fala, PalavrasParar));
}
