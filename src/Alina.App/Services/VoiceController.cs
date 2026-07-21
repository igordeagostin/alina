using Alina.Core.Orchestration;
using Alina.Voice;
using Microsoft.Extensions.DependencyInjection;

namespace Alina.App.Services;

/// <summary>
/// Orquestra um turno completo de interação (voz ou texto) reaproveitando os
/// componentes de <c>Alina.Voice</c> e o <see cref="IOrchestrator"/>. É a fonte
/// única do fluxo, acionada tanto pelo clique no orbe quanto pela hotkey global.
/// Push-to-talk por toque: <see cref="AlternarEscutaAsync"/> inicia a gravação e,
/// chamada de novo, a encerra e processa.
/// </summary>
public sealed class VoiceController
{
    private readonly IServiceProvider _services;
    private readonly IAssistantStatus _status;
    private readonly ConversationUiState _log;

    private IOrchestrator? _orchestrator;
    private IAudioRecorder? _recorder;
    private ISpeechToText? _stt;
    private ITextToSpeech? _tts;
    private IAudioPlayer? _player;
    private TaskCompletionSource? _pararGravacao;

    /// <summary>Amplitude do microfone (0–1) durante a gravação, para a waveform.</summary>
    public event Action<float>? NivelAudio;

    /// <summary>Disparado ao começar a gravar a voz — usado para liberar o microfone da palavra de ativação.</summary>
    public event Action? EscutaComecou;

    /// <summary>Disparado ao encerrar um turno de voz e voltar a ficar ocioso.</summary>
    public event Action? Concluido;

    public VoiceController(IServiceProvider services, IAssistantStatus status, ConversationUiState log)
    {
        _services = services;
        _status = status;
        _log = log;
    }

    public async Task AlternarEscutaAsync()
    {
        if (_status.Current == AssistantState.Listening)
        {
            _pararGravacao?.TrySetResult();
            return;
        }

        if (_status.Current != AssistantState.Idle)
        {
            return;
        }

        try
        {
            _recorder ??= _services.GetRequiredService<IAudioRecorder>();
            _stt ??= _services.GetRequiredService<ISpeechToText>();

            _status.Set(AssistantState.Listening);
            EscutaComecou?.Invoke();
            _pararGravacao = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            var nivel = new ProgressoSincrono(v => NivelAudio?.Invoke(v));
            var wav = await _recorder.RecordAsync(_ => _pararGravacao.Task, nivel);

            _status.Set(AssistantState.Thinking);
            var texto = await _stt.TranscribeAsync(wav);
            if (string.IsNullOrWhiteSpace(texto))
            {
                _log.Adicionar("error", "(não entendi nada, tente de novo)");
                _status.Set(AssistantState.Idle);
                return;
            }

            _log.Adicionar("user", texto);
            await ResponderAsync(texto, comVoz: true);
        }
        catch (Exception ex)
        {
            _log.Adicionar("error", $"[erro no modo voz] {ex.Message}");
            _status.Set(AssistantState.Idle);
        }
        finally
        {
            Concluido?.Invoke();
        }
    }

    public async Task EnviarTextoAsync(string texto)
    {
        texto = texto?.Trim() ?? string.Empty;
        if (texto.Length == 0 || _status.Current is AssistantState.Thinking or AssistantState.Executing or AssistantState.Speaking)
        {
            return;
        }

        _log.Adicionar("user", texto);
        await ResponderAsync(texto, comVoz: false);
    }

    private async Task ResponderAsync(string texto, bool comVoz)
    {
        _status.Set(AssistantState.Thinking);

        string resposta;
        try
        {
            _orchestrator ??= _services.GetRequiredService<IOrchestrator>();
            resposta = await _orchestrator.SendAsync(texto);
            resposta = string.IsNullOrWhiteSpace(resposta) ? "(sem resposta)" : resposta;
            _log.Adicionar("bot", resposta);
        }
        catch (Exception ex)
        {
            _log.Adicionar("error", $"[erro] {ex.Message}");
            _status.Set(AssistantState.Idle);
            return;
        }

        if (comVoz && _services.GetRequiredService<VoiceOptions>().Enabled)
        {
            try
            {
                _status.Set(AssistantState.Speaking);
                _tts ??= _services.GetRequiredService<ITextToSpeech>();
                _player ??= _services.GetRequiredService<IAudioPlayer>();

                var mp3 = await _tts.SynthesizeAsync(resposta);
                await _player.PlayMp3Async(mp3);
            }
            catch (Exception ex)
            {
                _log.Adicionar("error", $"[erro na fala] {ex.Message}");
            }
        }

        _status.Set(AssistantState.Idle);
    }

    /// <summary>Reporta o progresso de forma síncrona na thread de captura (baixa latência).</summary>
    private sealed class ProgressoSincrono : IProgress<float>
    {
        private readonly Action<float> _acao;

        public ProgressoSincrono(Action<float> acao) => _acao = acao;

        public void Report(float value) => _acao(value);
    }
}
