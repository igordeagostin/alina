using System.Globalization;
using System.Text;
using Alina.Core.Tools;

namespace Alina.Voice;

/// <summary>
/// Confirmação de segurança por voz: a Alina fala a ação e pergunta "posso prosseguir?",
/// captura a resposta do microfone, transcreve e interpreta sim/não. Se não compreender
/// após as tentativas configuradas, nega por segurança (ou delega ao <see cref="_fallback"/>,
/// quando fornecido). Mantém a política agnóstica de UI de <see cref="IConfirmationService"/>.
/// </summary>
public sealed class VozConfirmationService : IConfirmationService
{
    private readonly ITextToSpeech _tts;
    private readonly ISpeechToText _stt;
    private readonly IAudioRecorder _recorder;
    private readonly IAudioPlayer _player;
    private readonly VoiceOptions _options;
    private readonly IConfirmationService? _fallback;

    public VozConfirmationService(
        ITextToSpeech tts,
        ISpeechToText stt,
        IAudioRecorder recorder,
        IAudioPlayer player,
        VoiceOptions options,
        IConfirmationService? fallback = null)
    {
        _tts = tts;
        _stt = stt;
        _recorder = recorder;
        _player = player;
        _options = options;
        _fallback = fallback;
    }

    /// <summary>Disparado com o texto que a Alina fala, para os heads de UI exibirem.</summary>
    public event Action<string>? Falou;

    public async Task<bool> ConfirmAsync(string action, string? details = null, CancellationToken cancellationToken = default)
    {
        string pergunta = MontarPergunta(action, details);
        await FalarAsync(pergunta, cancellationToken);

        int tentativas = Math.Max(1, _options.TentativasConfirmacaoVoz);
        for (int i = 0; i < tentativas; i++)
        {
            string? texto = await CapturarRespostaAsync(cancellationToken);
            bool? decisao = InterpretarResposta(texto, _options.PalavrasSim, _options.PalavrasNao);

            if (decisao is true)
            {
                await FalarAsync("Certo, prosseguindo.", cancellationToken);
                return true;
            }

            if (decisao is false)
            {
                await FalarAsync("Ok, cancelado.", cancellationToken);
                return false;
            }

            if (i < tentativas - 1)
            {
                await FalarAsync("Não entendi. Diga sim ou não.", cancellationToken);
            }
        }

        if (_fallback is not null)
        {
            return await _fallback.ConfirmAsync(action, details, cancellationToken);
        }

        await FalarAsync("Não consegui confirmar, então vou cancelar por segurança.", cancellationToken);
        return false;
    }

    private async Task<string?> CapturarRespostaAsync(CancellationToken cancellationToken)
    {
        TimeSpan janela = TimeSpan.FromSeconds(Math.Max(2, _options.SegundosRespostaConfirmacao));
        byte[] audio = await _recorder.RecordAsync(
            ct => Task.Delay(janela, ct),
            nivel: null,
            cancellationToken);

        return await _stt.TranscribeAsync(audio, cancellationToken);
    }

    private async Task FalarAsync(string texto, CancellationToken cancellationToken)
    {
        Falou?.Invoke(texto);
        byte[] mp3 = await _tts.SynthesizeAsync(texto, cancellationToken);
        await _player.PlayMp3Async(mp3, cancellationToken);
    }

    private static string MontarPergunta(string action, string? details)
    {
        StringBuilder sb = new StringBuilder(action.TrimEnd('.', ' '));
        sb.Append('.');
        if (!string.IsNullOrWhiteSpace(details))
        {
            sb.Append(' ').Append(details!.Trim().TrimEnd('.', ' ')).Append('.');
        }
        sb.Append(" Posso prosseguir? Diga sim ou não.");
        return sb.ToString();
    }

    /// <summary>
    /// Interpreta a resposta transcrita: <c>true</c> (sim), <c>false</c> (não) ou <c>null</c>
    /// (não compreendida). Prioriza "não" por segurança quando ambos aparecem.
    /// </summary>
    internal static bool? InterpretarResposta(string? texto, IEnumerable<string> palavrasSim, IEnumerable<string> palavrasNao)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        List<string> tokens = Tokenizar(texto);
        if (tokens.Count == 0)
        {
            return null;
        }

        HashSet<string> conjunto = new HashSet<string>(tokens);
        string frase = string.Join(' ', tokens);

        if (ContemAlgum(conjunto, frase, palavrasNao))
        {
            return false;
        }

        if (ContemAlgum(conjunto, frase, palavrasSim))
        {
            return true;
        }

        return null;
    }

    private static bool ContemAlgum(HashSet<string> tokens, string frase, IEnumerable<string> termos)
    {
        foreach (string termo in termos)
        {
            string alvo = Normalizar(termo);
            if (string.IsNullOrEmpty(alvo))
            {
                continue;
            }

            // Termo composto (ex.: "melhor nao") casa como substring da frase normalizada;
            // termo simples casa por token exato para evitar falsos positivos.
            bool casa = alvo.Contains(' ') ? frase.Contains(alvo, StringComparison.Ordinal) : tokens.Contains(alvo);
            if (casa)
            {
                return true;
            }
        }

        return false;
    }

    private static List<string> Tokenizar(string texto)
    {
        string normalizado = Normalizar(texto);
        return normalizado
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    private static string Normalizar(string texto)
    {
        StringBuilder semAcento = new StringBuilder(texto.Length);
        foreach (char c in texto.Normalize(NormalizationForm.FormD))
        {
            UnicodeCategory categoria = CharUnicodeInfo.GetUnicodeCategory(c);
            if (categoria == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c))
            {
                semAcento.Append(char.ToLowerInvariant(c));
            }
            else
            {
                semAcento.Append(' ');
            }
        }

        return semAcento.ToString().Normalize(NormalizationForm.FormC);
    }
}
