using System.Diagnostics;

namespace Alina.Voice;

/// <summary>
/// Detector simples de fim de fala (VAD) baseado na amplitude dos blocos de áudio.
/// Depois que o usuário começa a falar, se o volume permanecer abaixo do limiar
/// pelo tempo de silêncio configurado, sinaliza que a gravação pode encerrar — assim
/// o turno é processado sem precisar de clique. Um teto de duração total e uma espera
/// inicial por fala garantem que a gravação nunca fique presa.
/// </summary>
public sealed class DetectorSilencio
{
    private const float LimiarSilencio = 0.03f;
    private static readonly TimeSpan DuracaoMaxima = TimeSpan.FromSeconds(60);

    private readonly TimeSpan _silencioParaEncerrar;
    private readonly TimeSpan _esperaInicialPorFala;
    private readonly Stopwatch _relogio = Stopwatch.StartNew();

    private bool _falou;
    private TimeSpan? _silencioDesde;

    /// <summary>Indica se chegou a haver fala antes do encerramento (falso = só silêncio).</summary>
    public bool Falou => _falou;

    public DetectorSilencio(TimeSpan silencioParaEncerrar, TimeSpan esperaInicialPorFala)
    {
        _silencioParaEncerrar = silencioParaEncerrar;
        _esperaInicialPorFala = esperaInicialPorFala;
    }

    /// <summary>Alimenta um novo nível de áudio (0–1). Retorna true quando a gravação deve encerrar.</summary>
    public bool Alimentar(float nivel)
    {
        TimeSpan agora = _relogio.Elapsed;

        if (agora >= DuracaoMaxima)
        {
            return true;
        }

        if (nivel >= LimiarSilencio)
        {
            _falou = true;
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
        return agora - _silencioDesde.Value >= _silencioParaEncerrar;
    }
}
