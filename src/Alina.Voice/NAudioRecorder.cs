using NAudio.Wave;

namespace Alina.Voice;

/// <summary>
/// Captura de microfone via NAudio (WaveIn). Grava em WAV PCM 16 kHz mono,
/// formato adequado para o Whisper. Reporta a amplitude de cada bloco para
/// alimentar a waveform.
/// </summary>
public sealed class NAudioRecorder : IAudioRecorder
{
    public async Task<byte[]> RecordAsync(Func<CancellationToken, Task> waitForStop, IProgress<float>? nivel = null, CancellationToken cancellationToken = default)
    {
        string tempPath = Path.Combine(Path.GetTempPath(), $"alina-rec-{Guid.NewGuid():n}.wav");
        TaskCompletionSource stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using WaveInEvent waveIn = new WaveInEvent { WaveFormat = new WaveFormat(16000, 16, 1), BufferMilliseconds = 30 };
        WaveFileWriter writer = new WaveFileWriter(tempPath, waveIn.WaveFormat);

        waveIn.DataAvailable += (_, e) =>
        {
            // ReSharper disable once AccessToDisposedClosure
            if (writer.CanWrite)
            {
                writer.Write(e.Buffer, 0, e.BytesRecorded);
            }

            if (nivel is not null)
            {
                nivel.Report(PicoNormalizado(e.Buffer, e.BytesRecorded));
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

    private static float PicoNormalizado(byte[] buffer, int bytes)
    {
        int pico = 0;
        for (int i = 0; i + 1 < bytes; i += 2)
        {
            short amostra = Math.Abs((short)(buffer[i] | (buffer[i + 1] << 8)));
            if (amostra > pico)
            {
                pico = amostra;
            }
        }

        return pico / 32768f;
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
