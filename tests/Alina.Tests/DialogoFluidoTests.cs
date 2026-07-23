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
    public void De_fone_de_ouvido_basta_falar_para_tomar_a_palavra()
    {
        Microfone microfone = new Microfone();

        microfone.Ouvir(nivel: 0.01f, porMilissegundos: 600);

        Assert.True(microfone.Ouvir(nivel: 0.3f, porMilissegundos: 240));
    }

    [Fact]
    public void Estalo_curto_nao_toma_a_palavra()
    {
        Microfone microfone = new Microfone();

        microfone.Ouvir(nivel: 0.01f, porMilissegundos: 600);

        Assert.False(microfone.Ouvir(nivel: 0.9f, porMilissegundos: 120));
    }

    [Fact]
    public void Em_caixa_de_som_o_eco_da_propria_voz_dela_nao_toma_a_palavra()
    {
        Microfone microfone = new Microfone();

        Assert.False(microfone.Ouvir(nivel: 0.3f, porMilissegundos: 3000));
    }

    [Fact]
    public void Em_caixa_de_som_a_voz_que_se_destaca_do_eco_toma_a_palavra()
    {
        Microfone microfone = new Microfone();

        microfone.Ouvir(nivel: 0.3f, porMilissegundos: 600);

        Assert.True(microfone.Ouvir(nivel: 0.85f, porMilissegundos: 240));
    }

    [Fact]
    public void Vale_entre_silabas_nao_zera_a_contagem()
    {
        Microfone microfone = new Microfone();
        microfone.Ouvir(nivel: 0.01f, porMilissegundos: 600);

        microfone.Ouvir(nivel: 0.3f, porMilissegundos: 120);
        microfone.Ouvir(nivel: 0.01f, porMilissegundos: 90);

        Assert.True(microfone.Ouvir(nivel: 0.3f, porMilissegundos: 120));
    }

    [Fact]
    public void Pausa_longa_zera_a_contagem()
    {
        Microfone microfone = new Microfone();
        microfone.Ouvir(nivel: 0.01f, porMilissegundos: 600);

        microfone.Ouvir(nivel: 0.3f, porMilissegundos: 150);
        microfone.Ouvir(nivel: 0.01f, porMilissegundos: 400);

        Assert.False(microfone.Ouvir(nivel: 0.3f, porMilissegundos: 150));
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

    /// <summary>
    /// Alimenta o detector com blocos de 30 ms — o mesmo tamanho que a captura entrega —
    /// num relógio controlado, para os limites de tempo serem verificados sem esperas reais.
    /// </summary>
    private sealed class Microfone
    {
        private const int MilissegundosPorBloco = 30;

        private readonly DetectorInicioFala _detector;
        private TimeSpan _agora;

        public Microfone() => _detector = new DetectorInicioFala(() => _agora);

        /// <returns><c>true</c> se o detector confirmou a fala em algum bloco do trecho.</returns>
        public bool Ouvir(float nivel, int porMilissegundos)
        {
            bool confirmou = false;

            for (int decorrido = 0; decorrido < porMilissegundos; decorrido += MilissegundosPorBloco)
            {
                confirmou |= _detector.Alimentar(nivel);
                _agora += TimeSpan.FromMilliseconds(MilissegundosPorBloco);
            }

            return confirmou;
        }
    }
}
