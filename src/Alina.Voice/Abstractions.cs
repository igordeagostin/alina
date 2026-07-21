namespace Alina.Voice;

/// <summary>Converte áudio (WAV) em texto.</summary>
public interface ISpeechToText
{
    Task<string> TranscribeAsync(byte[] wavAudio, CancellationToken cancellationToken = default);
}

/// <summary>Converte texto em áudio falado (MP3).</summary>
public interface ITextToSpeech
{
    Task<byte[]> SynthesizeAsync(string text, CancellationToken cancellationToken = default);
}

/// <summary>Captura áudio do microfone.</summary>
public interface IAudioRecorder
{
    /// <summary>
    /// Grava do microfone até <paramref name="waitForStop"/> concluir (push-to-talk).
    /// Retorna o áudio no formato WAV (PCM 16 kHz mono). Se <paramref name="nivel"/>
    /// for informado, reporta a amplitude normalizada (0–1) a cada bloco capturado,
    /// para alimentar visualizações (waveform).
    /// </summary>
    Task<byte[]> RecordAsync(Func<CancellationToken, Task> waitForStop, IProgress<float>? nivel = null, CancellationToken cancellationToken = default);
}

/// <summary>Reproduz áudio.</summary>
public interface IAudioPlayer
{
    Task PlayMp3Async(byte[] mp3Audio, CancellationToken cancellationToken = default);
}
