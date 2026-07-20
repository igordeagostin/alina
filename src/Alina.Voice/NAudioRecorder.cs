using NAudio.Wave;

namespace Alina.Voice;

/// <summary>
/// Captura de microfone via NAudio (WaveIn). Grava em WAV PCM 16 kHz mono,
/// formato adequado para o Whisper.
/// </summary>
public sealed class NAudioRecorder : IAudioRecorder
{
    public async Task<byte[]> RecordAsync(Func<CancellationToken, Task> waitForStop, CancellationToken cancellationToken = default)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"alina-rec-{Guid.NewGuid():n}.wav");
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var waveIn = new WaveInEvent { WaveFormat = new WaveFormat(16000, 16, 1) };
        var writer = new WaveFileWriter(tempPath, waveIn.WaveFormat);

        waveIn.DataAvailable += (_, e) =>
        {
            // ReSharper disable once AccessToDisposedClosure
            if (writer.CanWrite)
            {
                writer.Write(e.Buffer, 0, e.BytesRecorded);
            }
        };

        waveIn.RecordingStopped += (_, _) =>
        {
            writer.Dispose();
            stopped.TrySetResult();
        };

        try
        {
            waveIn.StartRecording();
            await waitForStop(cancellationToken);
        }
        finally
        {
            waveIn.StopRecording();
        }

        await stopped.Task;

        try
        {
            return await File.ReadAllBytesAsync(tempPath, cancellationToken);
        }
        finally
        {
            TryDelete(tempPath);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // ignora
        }
    }
}
