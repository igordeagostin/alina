using OpenAI;
using OpenAI.Audio;

namespace Alina.Voice;

/// <summary>Síntese de fala via OpenAI (gpt-4o-mini-tts / tts-1).</summary>
public sealed class OpenAITextToSpeech : ITextToSpeech
{
    private readonly AudioClient _client;
    private readonly VoiceOptions _options;

    public OpenAITextToSpeech(OpenAIClient client, VoiceOptions options)
    {
        _options = options;
        _client = client.GetAudioClient(options.TtsModel);
    }

    public async Task<byte[]> SynthesizeAsync(string text, CancellationToken cancellationToken = default)
    {
        var options = new SpeechGenerationOptions
        {
            ResponseFormat = GeneratedSpeechFormat.Mp3,
            SpeedRatio = _options.Speed,
        };

        var voice = new GeneratedSpeechVoice(_options.Voice);

        var result = await _client.GenerateSpeechAsync(text, voice, options, cancellationToken);
        return result.Value.ToArray();
    }
}
