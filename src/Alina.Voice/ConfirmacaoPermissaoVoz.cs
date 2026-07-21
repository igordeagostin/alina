using Alina.Core.Permissoes;

namespace Alina.Voice;

/// <summary>
/// Confirmação de permissão por voz, com escopo: a Alina fala o pedido e interpreta a resposta
/// como "não", "sim" (uma vez), "sempre" ou "sempre neste diretório". Se não compreender após as
/// tentativas, nega por segurança (ou delega ao <see cref="_fallback"/>).
/// </summary>
public sealed class ConfirmacaoPermissaoVoz : IConfirmacaoPermissao
{
    private static readonly string[] TermosSempre = ["sempre", "toda vez", "todas as vezes"];
    private static readonly string[] TermosDiretorio = ["diretorio", "projeto", "aqui", "nesta pasta", "pasta"];

    private readonly ITextToSpeech _tts;
    private readonly ISpeechToText _stt;
    private readonly IAudioRecorder _recorder;
    private readonly IAudioPlayer _player;
    private readonly VoiceOptions _options;
    private readonly IConfirmacaoPermissao? _fallback;

    public ConfirmacaoPermissaoVoz(
        ITextToSpeech tts,
        ISpeechToText stt,
        IAudioRecorder recorder,
        IAudioPlayer player,
        VoiceOptions options,
        IConfirmacaoPermissao? fallback = null)
    {
        _tts = tts;
        _stt = stt;
        _recorder = recorder;
        _player = player;
        _options = options;
        _fallback = fallback;
    }

    public async Task<RespostaConfirmacaoPermissao> ConfirmarAsync(PedidoPermissao pedido, CancellationToken cancellationToken = default)
    {
        var pergunta = $"{pedido.Descricao.TrimEnd('.')}. Autorizo? Diga sim, sempre, ou não.";
        await FalarAsync(pergunta, cancellationToken);

        var tentativas = Math.Max(1, _options.TentativasConfirmacaoVoz);
        for (var i = 0; i < tentativas; i++)
        {
            var texto = await CapturarAsync(cancellationToken);
            var decisao = Interpretar(texto, _options.PalavrasSim, _options.PalavrasNao);
            if (decisao is not null)
            {
                await FalarAsync(MensagemConfirmacao(decisao), cancellationToken);
                return decisao;
            }

            if (i < tentativas - 1)
            {
                await FalarAsync("Não entendi. Diga sim, sempre, ou não.", cancellationToken);
            }
        }

        if (_fallback is not null)
        {
            return await _fallback.ConfirmarAsync(pedido, cancellationToken);
        }

        await FalarAsync("Não consegui confirmar, então vou negar por segurança.", cancellationToken);
        return RespostaConfirmacaoPermissao.Negada;
    }

    /// <summary>
    /// Interpreta a resposta com escopo. Retorna <c>null</c> quando não compreende.
    /// "não" tem prioridade por segurança.
    /// </summary>
    internal static RespostaConfirmacaoPermissao? Interpretar(string? texto, IEnumerable<string> palavrasSim, IEnumerable<string> palavrasNao)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        var frase = TextoVoz.Normalizar(texto);
        if (frase.Trim().Length == 0)
        {
            return null;
        }

        if (TextoVoz.ContemAlgum(frase, palavrasNao))
        {
            return RespostaConfirmacaoPermissao.Negada;
        }

        var sempre = TextoVoz.ContemAlgum(frase, TermosSempre);
        var noDiretorio = TextoVoz.ContemAlgum(frase, TermosDiretorio);

        if (sempre)
        {
            return new RespostaConfirmacaoPermissao(true, noDiretorio ? EscopoPermissao.SempreNesteDiretorio : EscopoPermissao.Sempre);
        }

        if (TextoVoz.ContemAlgum(frase, palavrasSim))
        {
            return RespostaConfirmacaoPermissao.PermitidaUmaVez;
        }

        return null;
    }

    private static string MensagemConfirmacao(RespostaConfirmacaoPermissao decisao) => decisao switch
    {
        { Permitido: false } => "Ok, negado.",
        { Escopo: EscopoPermissao.Sempre } => "Certo, vou permitir sempre.",
        { Escopo: EscopoPermissao.SempreNesteDiretorio } => "Certo, sempre neste diretório.",
        _ => "Certo, autorizado.",
    };

    private async Task<string?> CapturarAsync(CancellationToken cancellationToken)
    {
        var janela = TimeSpan.FromSeconds(Math.Max(2, _options.SegundosRespostaConfirmacao));
        var audio = await _recorder.RecordAsync(ct => Task.Delay(janela, ct), nivel: null, cancellationToken);
        return await _stt.TranscribeAsync(audio, cancellationToken);
    }

    private async Task FalarAsync(string texto, CancellationToken cancellationToken)
    {
        var mp3 = await _tts.SynthesizeAsync(texto, cancellationToken);
        await _player.PlayMp3Async(mp3, cancellationToken);
    }
}
