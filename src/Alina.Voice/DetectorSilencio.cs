using System.Diagnostics;

namespace Alina.Voice;

/// <summary>
/// Detector simples de fim de fala (VAD) baseado na amplitude dos blocos de áudio.
/// Depois que o usuário começa a falar, se o volume permanecer abaixo do limiar
/// pelo tempo de silêncio, sinaliza que a gravação pode encerrar — assim o turno é
/// processado sem precisar de clique. O tempo de silêncio é adaptativo: comandos
/// curtos ("fecha o navegador") encerram mais cedo que falas longas, que costumam
/// ter pausas de raciocínio no meio. Um teto de duração total e uma espera inicial
/// por fala garantem que a gravação nunca fique presa.
/// </summary>
public sealed class DetectorSilencio
{
    private const float LimiarSilencio = 0.03f;
    private static readonly TimeSpan DuracaoMaxima = TimeSpan.FromSeconds(60);

    /// <summary>Fala até esta duração conta como comando curto e encerra com menos silêncio.</summary>
    private static readonly TimeSpan FalaCurta = TimeSpan.FromSeconds(3);

    /// <summary>Fração do silêncio configurado exigida após um comando curto.</summary>
    private const double FatorFalaCurta = 0.65;

    private readonly TimeSpan _silencioParaEncerrar;
    private readonly TimeSpan _esperaInicialPorFala;
    private readonly Func<TimeSpan> _relogio;

    private bool _falou;
    private TimeSpan? _primeiraFala;
    private TimeSpan _ultimaFala;
    private TimeSpan? _silencioDesde;

    /// <summary>Indica se chegou a haver fala antes do encerramento (falso = só silêncio).</summary>
    public bool Falou => _falou;

    public DetectorSilencio(TimeSpan silencioParaEncerrar, TimeSpan esperaInicialPorFala, Func<TimeSpan>? relogio = null)
    {
        _silencioParaEncerrar = silencioParaEncerrar;
        _esperaInicialPorFala = esperaInicialPorFala;

        if (relogio is null)
        {
            Stopwatch cronometro = Stopwatch.StartNew();
            relogio = () => cronometro.Elapsed;
        }

        _relogio = relogio;
    }

    /// <summary>Alimenta um novo nível de áudio (0–1). Retorna true quando a gravação deve encerrar.</summary>
    public bool Alimentar(float nivel)
    {
        TimeSpan agora = _relogio();

        if (agora >= DuracaoMaxima)
        {
            return true;
        }

        if (nivel >= LimiarSilencio)
        {
            _falou = true;
            _primeiraFala ??= agora;
            _ultimaFala = agora;
            _silencioDesde = null;
            return false;
        }

        if (!_falou)
        {
            return agora >= _esperaInicialPorFala;
        }

        if (_silencioParaEncerrar <= TimeSpan.Zero)
        {
            return false;
        }

        _silencioDesde ??= agora;
        return agora - _silencioDesde.Value >= SilencioNecessario();
    }

    private TimeSpan SilencioNecessario()
    {
        TimeSpan duracaoFala = _ultimaFala - (_primeiraFala ?? _ultimaFala);
        return duracaoFala <= FalaCurta
            ? TimeSpan.FromTicks((long)(_silencioParaEncerrar.Ticks * FatorFalaCurta))
            : _silencioParaEncerrar;
    }
}
