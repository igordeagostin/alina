using System.Diagnostics;

namespace Alina.Voice;

/// <summary>
/// Detecta o começo de uma fala do usuário enquanto a Alina responde, para calar a
/// voz dela na hora (barge-in) sem cancelar o que ela está executando. Usa um limiar
/// mais alto que o <see cref="DetectorSilencio"/> e exige som sustentado por alguns
/// blocos: em caixas de som o microfone capta a própria voz da Alina, e sem essas duas
/// travas ela se calaria sozinha a cada frase.
/// </summary>
public sealed class DetectorInicioFala
{
    private const float LimiarFala = 0.12f;
    private static readonly TimeSpan FalaSustentada = TimeSpan.FromMilliseconds(280);

    private readonly Stopwatch _relogio = Stopwatch.StartNew();

    private TimeSpan? _falandoDesde;

    /// <summary>Alimenta um nível de áudio (0–1). Retorna true quando há fala confirmada.</summary>
    public bool Alimentar(float nivel)
    {
        if (nivel < LimiarFala)
        {
            _falandoDesde = null;
            return false;
        }

        TimeSpan agora = _relogio.Elapsed;
        _falandoDesde ??= agora;
        return agora - _falandoDesde.Value >= FalaSustentada;
    }

    public void Reiniciar() => _falandoDesde = null;
}
