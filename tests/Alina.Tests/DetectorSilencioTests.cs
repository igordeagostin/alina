using Alina.Voice;

namespace Alina.Tests;

/// <summary>
/// Cobre o encerramento adaptativo da gravação: comando curto encerra com menos
/// silêncio que fala longa, que costuma ter pausas de raciocínio no meio.
/// </summary>
public sealed class DetectorSilencioTests
{
    private const int MilissegundosPorBloco = 30;

    [Fact]
    public void Comando_curto_encerra_com_menos_silencio()
    {
        Detector detector = new Detector(silencioSegundos: 1.5);

        detector.Alimentar(nivel: 0.5f, porMilissegundos: 1500);

        Assert.False(detector.Alimentar(nivel: 0.01f, porMilissegundos: 900));
        Assert.True(detector.Alimentar(nivel: 0.01f, porMilissegundos: 210));
    }

    [Fact]
    public void Fala_longa_exige_o_silencio_configurado_inteiro()
    {
        Detector detector = new Detector(silencioSegundos: 1.5);

        detector.Alimentar(nivel: 0.5f, porMilissegundos: 5000);

        Assert.False(detector.Alimentar(nivel: 0.01f, porMilissegundos: 1400));
        Assert.True(detector.Alimentar(nivel: 0.01f, porMilissegundos: 210));
    }

    [Fact]
    public void Voltar_a_falar_zera_a_contagem_de_silencio()
    {
        Detector detector = new Detector(silencioSegundos: 1.5);

        detector.Alimentar(nivel: 0.5f, porMilissegundos: 600);
        detector.Alimentar(nivel: 0.01f, porMilissegundos: 600);
        detector.Alimentar(nivel: 0.5f, porMilissegundos: 300);

        Assert.False(detector.Alimentar(nivel: 0.01f, porMilissegundos: 600));
    }

    [Fact]
    public void So_silencio_encerra_na_espera_inicial_sem_marcar_fala()
    {
        Detector detector = new Detector(silencioSegundos: 1.5, esperaInicialSegundos: 2);

        Assert.False(detector.Alimentar(nivel: 0.01f, porMilissegundos: 1900));
        Assert.True(detector.Alimentar(nivel: 0.01f, porMilissegundos: 210));
        Assert.False(detector.Falou);
    }

    /// <summary>Alimenta o detector em blocos de 30 ms num relógio controlado, sem esperas reais.</summary>
    private sealed class Detector
    {
        private readonly DetectorSilencio _detector;
        private TimeSpan _agora;

        public Detector(double silencioSegundos, double esperaInicialSegundos = 10)
        {
            _detector = new DetectorSilencio(
                TimeSpan.FromSeconds(silencioSegundos),
                TimeSpan.FromSeconds(esperaInicialSegundos),
                () => _agora);
        }

        public bool Falou => _detector.Falou;

        public bool Alimentar(float nivel, int porMilissegundos)
        {
            bool encerrou = false;

            for (int decorrido = 0; decorrido < porMilissegundos; decorrido += MilissegundosPorBloco)
            {
                encerrou |= _detector.Alimentar(nivel);
                _agora += TimeSpan.FromMilliseconds(MilissegundosPorBloco);
            }

            return encerrou;
        }
    }
}
