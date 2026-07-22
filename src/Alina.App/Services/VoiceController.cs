using System.Text;
using Alina.Core.Orchestration;
using Alina.Voice;
using Microsoft.Extensions.DependencyInjection;

namespace Alina.App.Services;

/// <summary>
/// Orquestra a interação por voz (ou texto) reaproveitando os componentes de
/// <c>Alina.Voice</c> e o <see cref="IOrchestrator"/>. É a fonte única do fluxo,
/// acionada pelo clique no orbe, pela hotkey global ou pela palavra de ativação.
/// Em conversa contínua, <see cref="AlternarEscutaAsync"/> inicia um ciclo de
/// turnos: a cada resposta, a Alina volta a ouvir sozinha e só "dorme" quando você
/// fica em silêncio pela janela configurada. Chamada durante a escuta, encerra o
/// turno atual e manda processar na hora; chamada enquanto a Alina pensa ou fala,
/// corta o turno (<see cref="Interromper"/>) e devolve a palavra a você.
/// </summary>
public sealed class VoiceController
{
    private readonly IServiceProvider _services;
    private readonly IAssistantStatus _status;
    private readonly ConversationUiState _log;

    private const string FrasePrevia = "Oi, eu sou a Alina. É assim que a minha voz vai soar.";

    private const string NotaPedidoInterrompido =
        "O usuário interrompeu você antes de a resposta anterior sair; ele não ouviu nada. " +
        "Aguarde a nova orientação e não retome o que foi cortado sem que ele peça.";

    private IOrchestrator? _orchestrator;
    private IAudioRecorder? _recorder;
    private ISpeechToText? _stt;
    private ITextToSpeech? _tts;
    private IAudioPlayer? _player;
    private TaskCompletionSource? _pararGravacao;
    private CancellationTokenSource? _cancelamentoTurno;
    private Task _turnoAtual = Task.CompletedTask;
    private volatile bool _retomarEscutaAposInterromper = true;
    private volatile bool _emConversaVoz;
    private volatile bool _descartarGravacao;

    /// <summary>Amplitude do microfone (0–1) durante a gravação, para a waveform.</summary>
    public event Action<float>? NivelAudio;

    /// <summary>Disparado ao tomar o microfone para gravar — libera quem mais o estiver usando.</summary>
    public event Action? MicrofoneOcupado;

    /// <summary>
    /// Disparado ao devolver o microfone ainda dentro do turno (a Alina passa a pensar
    /// ou falar). É o que permite escutar uma interrupção enquanto ela responde.
    /// </summary>
    public event Action? MicrofoneLivre;

    /// <summary>Disparado ao encerrar um turno de voz e voltar a ficar ocioso.</summary>
    public event Action? Concluido;

    public VoiceController(IServiceProvider services, IAssistantStatus status, ConversationUiState log)
    {
        _services = services;
        _status = status;
        _log = log;
    }

    /// <summary>Indica se há um turno em andamento que pode ser cortado (pensando, executando ou falando).</summary>
    public bool PodeInterromper =>
        _status.Current is AssistantState.Thinking or AssistantState.Executing or AssistantState.Speaking;

    /// <summary>
    /// Corta o turno em andamento: cancela o pedido ao LLM e cala a fala na hora.
    /// Com <paramref name="retomarEscuta"/>, a conversa por voz volta a ouvir logo em
    /// seguida — é o que se espera de quem interrompeu justamente para falar.
    /// </summary>
    public void Interromper(bool retomarEscuta = true)
    {
        if (!PodeInterromper)
        {
            return;
        }

        _retomarEscutaAposInterromper = retomarEscuta;
        _cancelamentoTurno?.Cancel();
    }

    public async Task AlternarEscutaAsync()
    {
        if (_status.Current == AssistantState.Listening)
        {
            _pararGravacao?.TrySetResult();
            return;
        }

        if (PodeInterromper)
        {
            bool emConversa = _emConversaVoz;
            Interromper(retomarEscuta: emConversa);

            if (emConversa)
            {
                return;
            }

            await AguardarTurnoAsync();
        }

        if (_status.Current != AssistantState.Idle)
        {
            return;
        }

        _turnoAtual = ConversarAsync();
        await _turnoAtual;
    }

    private async Task ConversarAsync()
    {
        VoiceOptions opcoes = _services.GetRequiredService<VoiceOptions>();
        _recorder ??= _services.GetRequiredService<IAudioRecorder>();
        _stt ??= _services.GetRequiredService<ISpeechToText>();

        _emConversaVoz = true;
        bool houveInteracao = false;

        try
        {
            while (true)
            {
                string texto = await CapturarFalaAsync(opcoes);
                if (_descartarGravacao)
                {
                    break;
                }

                if (string.IsNullOrWhiteSpace(texto))
                {
                    if (!houveInteracao)
                    {
                        _log.Adicionar("error", "(não entendi nada, tente de novo)");
                    }

                    break;
                }

                _log.Adicionar("user", texto);
                bool interrompida = await ResponderAsync(texto, comVoz: true);
                houveInteracao = true;

                if (interrompida)
                {
                    if (!_retomarEscutaAposInterromper)
                    {
                        break;
                    }

                    continue;
                }

                if (!opcoes.ConversaContinua)
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _log.Adicionar("error", $"[erro no modo voz] {ex.Message}");
        }
        finally
        {
            _emConversaVoz = false;
            _status.Set(AssistantState.Idle);
            Concluido?.Invoke();
        }
    }

    private async Task<string> CapturarFalaAsync(VoiceOptions opcoes)
    {
        _status.Set(AssistantState.Listening);
        _pararGravacao = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        DetectorSilencio silencio = new DetectorSilencio(
            TimeSpan.FromSeconds(opcoes.SegundosSilencioParaEncerrar),
            TimeSpan.FromSeconds(opcoes.SegundosJanelaConversa));
        ProgressoSincrono nivel = new ProgressoSincrono(v =>
        {
            NivelAudio?.Invoke(v);
            if (silencio.Alimentar(v))
            {
                _pararGravacao?.TrySetResult();
            }
        });

        MicrofoneOcupado?.Invoke();
        byte[] wav = await _recorder!.RecordAsync(_ => _pararGravacao.Task, nivel);

        if (_descartarGravacao)
        {
            MicrofoneLivre?.Invoke();
            return string.Empty;
        }

        _status.Set(AssistantState.Thinking);
        try
        {
            return await _stt!.TranscribeAsync(wav);
        }
        finally
        {
            MicrofoneLivre?.Invoke();
        }
    }

    /// <summary>
    /// Reproduz uma frase curta com a <paramref name="voz"/> e a <paramref name="velocidade"/>
    /// informadas, para o usuário conferir a voz antes de salvar. Sobrepõe temporariamente as
    /// opções vivas e as restaura ao final. Só toca quando a Alina está ociosa.
    /// </summary>
    public async Task PrevisualizarVozAsync(string voz, double velocidade)
    {
        if (_status.Current != AssistantState.Idle || string.IsNullOrWhiteSpace(voz))
        {
            return;
        }

        VoiceOptions opcoes = _services.GetRequiredService<VoiceOptions>();
        string vozAnterior = opcoes.Voice;
        float velocidadeAnterior = opcoes.Speed;

        try
        {
            _status.Set(AssistantState.Speaking);
            _tts ??= _services.GetRequiredService<ITextToSpeech>();
            _player ??= _services.GetRequiredService<IAudioPlayer>();

            opcoes.Voice = voz;
            opcoes.Speed = (float)velocidade;

            byte[] mp3 = await _tts.SynthesizeAsync(FrasePrevia);
            await _player.PlayMp3Async(mp3);
        }
        catch (Exception ex)
        {
            _log.Adicionar("error", $"[erro na prévia de voz] {ex.Message}");
        }
        finally
        {
            opcoes.Voice = vozAnterior;
            opcoes.Speed = velocidadeAnterior;
            _status.Set(AssistantState.Idle);
        }
    }

    public async Task EnviarTextoAsync(string texto)
    {
        texto = texto?.Trim() ?? string.Empty;
        if (texto.Length == 0)
        {
            return;
        }

        if (PodeInterromper)
        {
            Interromper(retomarEscuta: false);
            await AguardarTurnoAsync();
        }
        else if (_status.Current == AssistantState.Listening)
        {
            _descartarGravacao = true;
            _pararGravacao?.TrySetResult();
            await AguardarTurnoAsync();
            _descartarGravacao = false;
        }

        _log.Adicionar("user", texto);
        _turnoAtual = ResponderAsync(texto, comVoz: false);
        await _turnoAtual;
    }

    /// <summary>
    /// Roda um pedido pela mesma pilha do chat (mesmo orquestrador, mesmas ferramentas e
    /// confirmações) e devolve a resposta a quem chamou, em vez de falar e registrar no log.
    /// É como o treino de habilidades observa a Alina executando de verdade; o turno continua
    /// cortável por <see cref="Interromper"/>.
    /// </summary>
    public async Task<string> ExecutarParaTreinoAsync(string texto, CancellationToken cancellationToken = default)
    {
        texto = texto?.Trim() ?? string.Empty;
        if (texto.Length == 0 || _status.Current != AssistantState.Idle)
        {
            return string.Empty;
        }

        using CancellationTokenSource cancelamento = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _cancelamentoTurno = cancelamento;
        _retomarEscutaAposInterromper = false;

        try
        {
            _status.Set(AssistantState.Thinking);
            _orchestrator ??= _services.GetRequiredService<IOrchestrator>();

            string resposta = await _orchestrator.SendAsync(texto, cancelamento.Token);
            return string.IsNullOrWhiteSpace(resposta) ? "(sem resposta)" : resposta;
        }
        catch (OperationCanceledException)
        {
            return "(interrompido)";
        }
        finally
        {
            _cancelamentoTurno = null;
            _status.Set(AssistantState.Idle);
        }
    }

    /// <returns><c>true</c> se o turno foi cortado pelo usuário antes de terminar.</returns>
    private async Task<bool> ResponderAsync(string texto, bool comVoz)
    {
        using CancellationTokenSource cancelamento = new CancellationTokenSource();
        _cancelamentoTurno = cancelamento;
        _retomarEscutaAposInterromper = true;

        try
        {
            _status.Set(AssistantState.Thinking);
            _orchestrator ??= _services.GetRequiredService<IOrchestrator>();

            string resposta;
            try
            {
                resposta = await _orchestrator.SendAsync(texto, cancelamento.Token);
            }
            catch (OperationCanceledException)
            {
                _log.Adicionar("error", "(interrompido)");
                _orchestrator.RegistrarNota(NotaPedidoInterrompido);
                return true;
            }
            catch (Exception ex)
            {
                _log.Adicionar("error", $"[erro] {ex.Message}");
                return false;
            }

            resposta = string.IsNullOrWhiteSpace(resposta) ? "(sem resposta)" : resposta;
            _log.Adicionar("bot", resposta);

            if (!comVoz || !_services.GetRequiredService<VoiceOptions>().Enabled)
            {
                return false;
            }

            return await FalarAsync(resposta, cancelamento.Token);
        }
        finally
        {
            _cancelamentoTurno = null;
            _status.Set(AssistantState.Idle);
        }
    }

    /// <summary>
    /// Fala a resposta em blocos curtos, sintetizando o próximo enquanto o atual toca.
    /// Além de começar a falar bem antes, é o que torna a interrupção instantânea: só
    /// existe um bloco em reprodução por vez.
    /// </summary>
    /// <returns><c>true</c> se a fala foi cortada no meio.</returns>
    private async Task<bool> FalarAsync(string resposta, CancellationToken cancelamento)
    {
        IReadOnlyList<string> blocos = DivisorFala.Dividir(resposta);
        if (blocos.Count == 0)
        {
            return false;
        }

        _status.Set(AssistantState.Speaking);
        _tts ??= _services.GetRequiredService<ITextToSpeech>();
        _player ??= _services.GetRequiredService<IAudioPlayer>();

        StringBuilder ouvido = new StringBuilder();
        Task<byte[]>? proximo = null;

        try
        {
            for (int i = 0; i < blocos.Count; i++)
            {
                Task<byte[]> atual = proximo ?? _tts.SynthesizeAsync(blocos[i], cancelamento);
                byte[] audio = await atual;

                proximo = i + 1 < blocos.Count
                    ? _tts.SynthesizeAsync(blocos[i + 1], cancelamento)
                    : null;

                await _player.PlayMp3Async(audio, cancelamento);
                if (cancelamento.IsCancellationRequested)
                {
                    break;
                }

                ouvido.Append(blocos[i]).Append(' ');
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _log.Adicionar("error", $"[erro na fala] {ex.Message}");
            return false;
        }
        finally
        {
            Descartar(proximo);
        }

        if (!cancelamento.IsCancellationRequested)
        {
            return false;
        }

        _log.Adicionar("error", "(interrompido)");
        _orchestrator!.RegistrarNota(MontarNotaFalaCortada(ouvido.ToString().Trim()));
        return true;
    }

    private static string MontarNotaFalaCortada(string ouvido) =>
        ouvido.Length == 0
            ? NotaPedidoInterrompido
            : "O usuário cortou a sua fala no meio. Da resposta anterior ele só chegou a ouvir: " +
              $"\"{ouvido}\". Leve isso em conta e não repita o que ele já ouviu.";

    private async Task AguardarTurnoAsync()
    {
        Task turno = _turnoAtual;
        if (turno.IsCompleted)
        {
            return;
        }

        await Task.WhenAny(turno, Task.Delay(TimeSpan.FromSeconds(5)));
    }

    private static void Descartar(Task<byte[]>? sintese) =>
        sintese?.ContinueWith(
            static t => _ = t.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

    /// <summary>Reporta o progresso de forma síncrona na thread de captura (baixa latência).</summary>
    private sealed class ProgressoSincrono : IProgress<float>
    {
        private readonly Action<float> _acao;

        public ProgressoSincrono(Action<float> acao) => _acao = acao;

        public void Report(float value) => _acao(value);
    }
}
