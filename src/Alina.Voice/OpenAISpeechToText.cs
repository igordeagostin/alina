using OpenAI;
using OpenAI.Audio;

namespace Alina.Voice;

/// <summary>Transcrição de áudio via OpenAI (Whisper / gpt-4o-transcribe).</summary>
public sealed class OpenAISpeechToText : ISpeechToText
{
    private readonly AudioClient _client;
    private readonly VoiceOptions _options;

    public OpenAISpeechToText(OpenAIClient client, VoiceOptions options)
    {
        _options = options;
        _client = client.GetAudioClient(options.SttModel);
    }

    public async Task<string> TranscribeAsync(byte[] wavAudio, CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream(wavAudio);

        var options = new AudioTranscriptionOptions();
        if (!string.IsNullOrWhiteSpace(_options.Language))
        {
            options.Language = _options.Language;
        }

        var result = await _client.TranscribeAudioAsync(stream, "audio.wav", options, cancellationToken);
        return result.Value.Text?.Trim() ?? string.Empty;
    }
}
