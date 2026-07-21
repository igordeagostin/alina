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

/// <summary>
/// Escuta o microfone de forma contínua e sinaliza quando a palavra de ativação
/// ("Alina") é dita. A detecção roda localmente, sem enviar áudio para a nuvem;
/// só depois de acionada é que o fluxo de voz normal grava e transcreve.
/// </summary>
public interface IDetectorPalavraAtivacao : IDisposable
{
    /// <summary>Disparado quando a palavra de ativação é reconhecida.</summary>
    event Action? PalavraDetectada;

    /// <summary>Disparado quando a escuta falha e é interrompida (ex.: microfone indisponível).</summary>
    event Action<Exception>? Falhou;

    /// <summary>Indica se há chave e modelo válidos para a detecção funcionar.</summary>
    bool Configurado { get; }

    /// <summary>Indica se a escuta está ligada (ainda que momentaneamente pausada).</summary>
    bool Ativo { get; }

    /// <summary>Liga a escuta contínua. Não faz nada se já estiver ativa ou não configurada.</summary>
    void Iniciar();

    /// <summary>Desliga a escuta e libera o microfone.</summary>
    void Parar();

    /// <summary>Suspende a captura temporariamente (libera o microfone) mantendo o estado ligado.</summary>
    void Pausar();

    /// <summary>Retoma a captura após um <see cref="Pausar"/>, se a escuta continua ligada.</summary>
    void Retomar();
}
