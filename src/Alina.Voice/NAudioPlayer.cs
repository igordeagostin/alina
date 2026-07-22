using NAudio.Wave;

namespace Alina.Voice;

/// <summary>Reprodução de áudio MP3 via NAudio (WaveOut + Mp3FileReader).</summary>
public sealed class NAudioPlayer : IAudioPlayer
{
    public async Task PlayMp3Async(byte[] mp3Audio, CancellationToken cancellationToken = default)
    {
        if (mp3Audio.Length == 0)
        {
            return;
        }

        using MemoryStream stream = new MemoryStream(mp3Audio);
        using Mp3FileReader reader = new Mp3FileReader(stream);
        using WaveOutEvent output = new WaveOutEvent();

        TaskCompletionSource finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        output.PlaybackStopped += (_, _) => finished.TrySetResult();

        output.Init(reader);
        output.Play();

        await using (cancellationToken.Register(() => output.Stop()))
        {
            await finished.Task;
        }
    }
}
