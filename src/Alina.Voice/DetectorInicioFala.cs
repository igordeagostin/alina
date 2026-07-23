using System.Diagnostics;

namespace Alina.Voice;

/// <summary>
/// Detecta que você tomou a palavra enquanto a Alina fala, para calar a voz dela na hora
/// sem cancelar nada do que ela esteja executando.
/// <para>
/// Um limiar fixo não serve: de fone, o microfone só ouve você e qualquer sussurro deveria
/// valer; em caixas de som, ele ouve a Alina alto e nada abaixo disso pode valer. Então o
/// limiar se calibra sozinho pelo que se ouve nos primeiros instantes da fala dela — que é
/// justamente o eco a ignorar. E o tempo de fala é acumulado, não exigido contínuo: fala
/// humana tem vales entre as sílabas, e exigir som ininterrupto simplesmente nunca dispara.
/// </para>
/// </summary>
public sealed class DetectorInicioFala
{
    /// <summary>Piso absoluto: abaixo disso é ruído de sala, nunca fala.</summary>
    private const float PisoMinimo = 0.02f;

    /// <summary>Quanto a sua voz precisa se destacar do que já se ouvia para contar.</summary>
    private const float FatorAcimaDoPiso = 1.8f;

    /// <summary>Trecho inicial usado só para medir o eco da voz dela, sem decidir nada.</summary>
    private static readonly TimeSpan Calibragem = TimeSpan.FromMilliseconds(400);

    /// <summary>Tempo de voz somado que confirma que quem está falando é você.</summary>
    private static readonly TimeSpan FalaAcumulada = TimeSpan.FromMilliseconds(220);

    /// <summary>Silêncio tolerado dentro de uma fala antes de zerar a contagem.</summary>
    private static readonly TimeSpan ToleranciaVale = TimeSpan.FromMilliseconds(180);

    private readonly Func<TimeSpan> _relogio;

    private float _piso = PisoMinimo;
    private TimeSpan _acumulado;
    private TimeSpan? _anterior;
    private TimeSpan? _quietoDesde;

    public DetectorInicioFala()
    {
        Stopwatch cronometro = Stopwatch.StartNew();
        _relogio = () => cronometro.Elapsed;
    }

    /// <summary>Sobrecarga com relógio próprio: tudo aqui é decidido por tempo decorrido.</summary>
    public DetectorInicioFala(Func<TimeSpan> relogio) => _relogio = relogio;

    /// <summary>Alimenta um nível de áudio (0–1). Retorna true quando a palavra passou a ser sua.</summary>
    public bool Alimentar(float nivel)
    {
        TimeSpan agora = _relogio();
        TimeSpan intervalo = _anterior is null ? TimeSpan.Zero : agora - _anterior.Value;
        _anterior = agora;

        if (agora < Calibragem)
        {
            _piso = Math.Max(_piso, nivel);
            return false;
        }

        float limiar = Math.Max(PisoMinimo, _piso * FatorAcimaDoPiso);

        if (nivel >= limiar)
        {
            _quietoDesde = null;
            _acumulado += intervalo;
            return _acumulado >= FalaAcumulada;
        }

        // Fora da fala, o piso acompanha o eco: sobe na hora se ele aumentar e cede devagar
        // quando ele diminui, para a calibragem não ficar presa a um estalo do começo.
        _piso = Math.Max(_piso * 0.995f, nivel);

        _quietoDesde ??= agora;
        if (agora - _quietoDesde.Value >= ToleranciaVale)
        {
            _acumulado = TimeSpan.Zero;
        }

        return false;
    }

    public void Reiniciar()
    {
        _acumulado = TimeSpan.Zero;
        _quietoDesde = null;
    }
}
