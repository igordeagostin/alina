using System.ClientModel;
using Alina.Voice;
using OpenAI;

namespace Alina.Tests;

/// <summary>
/// Integração real com a OpenAI (TTS + STT). Custa alguns centavos e exige a
/// variável de ambiente OPENAI_API_KEY. Marcado com Skip — rodar manualmente.
/// </summary>
public sealed class VoiceIntegrationTests
{
    [Fact(Skip = "Integração real: chama OpenAI TTS+STT (custa). Rodar manualmente com OPENAI_API_KEY setada.")]
    public async Task Tts_gera_audio_e_stt_transcreve_de_volta()
    {
        var key = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        Assert.False(string.IsNullOrWhiteSpace(key), "defina OPENAI_API_KEY para rodar este teste");

        var client = new OpenAIClient(new ApiKeyCredential(key!));
        var options = new VoiceOptions { Voice = "nova", TtsModel = "gpt-4o-mini-tts", SttModel = "whisper-1", Language = "pt" };

        var tts = new OpenAITextToSpeech(client, options);
        var stt = new OpenAISpeechToText(client, options);

        // TTS: gera MP3 a partir de um texto conhecido.
        var mp3 = await tts.SynthesizeAsync("A Alina está funcionando por voz.");
        Assert.True(mp3.Length > 1000, "esperava um MP3 não-trivial");

        // Converte o MP3 em WAV (o STT recebe WAV) e transcreve de volta.
        var wav = Mp3ToWav(mp3);
        var texto = await stt.TranscribeAsync(wav);

        Assert.Contains("alina", texto, StringComparison.OrdinalIgnoreCase);
    }

    private static byte[] Mp3ToWav(byte[] mp3)
    {
        using var input = new MemoryStream(mp3);
        using var reader = new NAudio.Wave.Mp3FileReader(input);
        using var output = new MemoryStream();
        NAudio.Wave.WaveFileWriter.WriteWavFileToStream(output, reader);
        return output.ToArray();
    }
}
