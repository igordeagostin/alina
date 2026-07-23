using System.ClientModel;
using OpenAI;
using OpenAI.Audio;

namespace Alina.Voice;

/// <summary>Transcrição de áudio via OpenAI (Whisper / gpt-4o-transcribe).</summary>
public sealed class OpenAISpeechToText : ISpeechToText
{
    private readonly OpenAIClient _client;
    private readonly VoiceOptions _options;

    public OpenAISpeechToText(OpenAIClient client, VoiceOptions options)
    {
        _options = options;
        _client = client;
    }

    public async Task<string> TranscribeAsync(byte[] wavAudio, CancellationToken cancellationToken = default)
    {
        AudioClient audio = _client.GetAudioClient(_options.SttModel);
        using MemoryStream stream = new MemoryStream(wavAudio);

        AudioTranscriptionOptions options = new AudioTranscriptionOptions();
        if (!string.IsNullOrWhiteSpace(_options.Language))
        {
            options.Language = _options.Language;
        }

        if (!string.IsNullOrWhiteSpace(_options.PromptTranscricao))
        {
            options.Prompt = _options.PromptTranscricao;
        }

        ClientResult<AudioTranscription> result = await audio.TranscribeAudioAsync(stream, "audio.wav", options, cancellationToken);
        string texto = result.Value.Text?.Trim() ?? string.Empty;

        return FiltroTranscricao.Descartavel(texto, _options.PromptTranscricao) ? string.Empty : texto;
    }
}
