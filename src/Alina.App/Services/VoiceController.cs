using System.Text;
using System.Threading.Channels;
using Alina.Core.Orchestration;
using Alina.Voice;
using Microsoft.Extensions.DependencyInjection;

namespace Alina.App.Services;

/// <summary>
/// Orquestra a interação por voz (ou texto) reaproveitando os componentes de
/// <c>Alina.Voice</c> e o <see cref="IOrchestrator"/>. É a fonte única do fluxo,
/// acionada pelo clique no orbe, pela hotkey global ou pela palavra de ativação.
/// <para>
/// A conversa não é feita de turnos que se esperam: aberta a sessão, dois laços rodam
/// juntos — um só ouve, o outro só responde. O microfone nunca fecha, nem enquanto ela
/// fala: o que você disser por cima é gravado e transcrito como qualquer outra fala, e a
/// voz dela sai depois no texto, pelo <see cref="FiltroEco"/>. Falar não cancela nada —
/// cala a voz dela e entra na fila. O que ela colocou para rodar em paralelo segue
/// rodando, e o resultado volta sozinho na primeira brecha da conversa. Só um pedido
/// explícito de parar (<see cref="VoiceOptions.PalavrasInterrupcao"/>) cancela trabalho.
/// </para>
/// </summary>
public sealed class VoiceController
{
    private readonly IServiceProvider _services;
    private readonly IAssistantStatus _status;
    private readonly ConversationUiState _log;

    private const string FrasePrevia = "Oi, eu sou a Alina. É assim que a minha voz vai soar.";

    /// <summary>Janela curta para juntar falas emendadas num pedido só, em vez de dois turnos.</summary>
    private static readonly TimeSpan JanelaCoalescencia = TimeSpan.FromMilliseconds(400);

    private const string NotaPedidoInterrompido =
        "O usuário mandou você parar antes de a resposta anterior sair; ele não ouviu nada. " +
        "Não retome o que foi cortado sem que ele peça. O próximo pedido dele é um comando " +
        "normal: atenda-o com suas ferramentas como faria em qualquer outro turno.";

    private IOrchestrator? _orchestrator;
    private IAudioRecorder? _recorder;
    private ISpeechToText? _stt;
    private ITextToSpeech? _tts;
    private IAudioPlayer? _player;
    private IBackgroundTaskManager? _tarefas;

    private Channel<Task<Fala>> _falas = Channel.CreateUnbounded<Task<Fala>>();
    private readonly Channel<BackgroundTask> _avisos = Channel.CreateUnbounded<BackgroundTask>();

    private readonly object _porta = new();

    private TaskCompletionSource? _pararGravacao;
    private CancellationTokenSource? _sessao;
    private CancellationTokenSource? _trabalho;
    private CancellationTokenSource? _fala;
    private volatile bool _emConversaVoz;
    private bool _atendendoTexto;

    /// <summary>
    /// Há áudio da Alina saindo pelos alto-falantes NESTE instante. É o sinal que o
    /// microfone usa para tratar o que capta como eco/barge-in — o estado visível não
    /// serve para isso, porque com a resposta em streaming ela fala enquanto pensa e
    /// executa ferramentas, e o estado mostra essas outras fases.
    /// </summary>
    private volatile bool _reproduzindoAudio;

    /// <summary>
    /// O que a Alina está dizendo (ou acabou de dizer) em voz alta. É a referência que
    /// permite reconhecer a própria voz dela quando ela volta pelo microfone. Fica de pé
    /// até a fala seguinte começar, porque o eco do último bloco ainda chega depois.
    /// </summary>
    private volatile string? _falaEmCurso;

    /// <summary>Amplitude do microfone (0–1) durante a gravação, para a waveform.</summary>
    public event Action<float>? NivelAudio;

    /// <summary>
    /// Disparado ao abrir a sessão de voz. O microfone fica tomado pela sessão inteira —
    /// quem mais o disputa (o reconhecedor da palavra de ativação) só volta ao encerrar,
    /// porque agora é o próprio detector de fala da captura que faz esse papel.
    /// </summary>
    public event Action? MicrofoneOcupado;

    /// <summary>Disparado ao devolver o microfone no fim da sessão.</summary>
    public event Action? MicrofoneLivre;

    /// <summary>Disparado ao encerrar a sessão de voz e voltar a ficar ocioso.</summary>
    public event Action? Concluido;

    public VoiceController(IServiceProvider services, IAssistantStatus status, ConversationUiState log)
    {
        _services = services;
        _status = status;
        _log = log;
    }

    /// <summary>Indica se há trabalho em andamento que um pedido explícito pode cancelar.</summary>
    public bool PodeInterromper =>
        _status.Current is AssistantState.Thinking or AssistantState.Executing or AssistantState.Speaking;

    /// <summary>Indica se a sessão de voz está aberta (ouvindo, pensando ou falando).</summary>
    public bool EmConversa => _emConversaVoz;

    /// <summary>
    /// Cancela o que está em andamento: o pedido ao LLM e a fala. Reservado ao pedido
    /// explícito de parar — a sessão em si continua aberta e ouvindo.
    /// </summary>
    public void Interromper()
    {
        _fala?.Cancel();
        _trabalho?.Cancel();
    }

    /// <summary>
    /// Abre a sessão de voz ou a encerra, se já estiver aberta. Não existe mais o gesto de
    /// "parar de gravar para processar": o fim de cada fala é detectado sozinho.
    /// </summary>
    public async Task AlternarEscutaAsync()
    {
        if (_emConversaVoz)
        {
            EncerrarSessao();
            return;
        }

        lock (_porta)
        {
            if (_emConversaVoz)
            {
                return;
            }

            _emConversaVoz = true;
        }

        await ConversarAsync();
    }

    private void EncerrarSessao()
    {
        _fala?.Cancel();
        _sessao?.Cancel();
        _pararGravacao?.TrySetResult();
    }

    private async Task ConversarAsync()
    {
        using CancellationTokenSource sessao = new CancellationTokenSource();
        _sessao = sessao;
        _falas = Channel.CreateUnbounded<Task<Fala>>();

        try
        {
            VoiceOptions opcoes = _services.GetRequiredService<VoiceOptions>();
            _recorder ??= _services.GetRequiredService<IAudioRecorder>();
            _stt ??= _services.GetRequiredService<ISpeechToText>();

            AssinarTarefas();
            MicrofoneOcupado?.Invoke();

            Task escuta = EscutarAsync(opcoes, sessao.Token);
            Task resposta = ResponderEnquantoHouverAsync(opcoes, sessao.Token);
            await Task.WhenAll(escuta, resposta);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _log.Adicionar("error", $"[erro no modo voz] {ex.Message}");
        }
        finally
        {
            lock (_porta)
            {
                _emConversaVoz = false;

                // Fila nova para o modo texto: transcrições que ficaram no meio do caminho
                // morrem com a sessão que o usuário mandou encerrar.
                _falas = Channel.CreateUnbounded<Task<Fala>>();
            }

            _sessao = null;
            DesassinarTarefas();
            _status.Set(AssistantState.Idle);
            MicrofoneLivre?.Invoke();
            Concluido?.Invoke();
        }
    }

    /// <summary>
    /// Só ouve. Nunca espera a Alina terminar de pensar ou de falar: assim que uma fala
    /// fecha, a transcrição é disparada em paralelo e o microfone já volta a gravar. As
    /// transcrições entram na fila como tarefas, e não como texto pronto, para que a ordem
    /// do que você disse seja preservada mesmo quando uma volta antes da outra.
    /// </summary>
    private async Task EscutarAsync(VoiceOptions opcoes, CancellationToken sessao)
    {
        bool primeira = true;

        while (!sessao.IsCancellationRequested)
        {
            Captura captura = await GravarAsync(opcoes, sessao);
            if (sessao.IsCancellationRequested)
            {
                break;
            }

            if (!captura.HouveFala)
            {
                if (primeira)
                {
                    _log.Adicionar("error", "(não entendi nada, tente de novo)");
                }

                if (!opcoes.ConversaContinua || PodeDormir())
                {
                    EncerrarSessao();
                    break;
                }

                primeira = false;
                continue;
            }

            primeira = false;
            _falas.Writer.TryWrite(TranscreverAsync(captura, sessao));
        }

        _falas.Writer.TryComplete();
    }

    /// <summary>
    /// Depois da janela de silêncio a Alina só dorme se de fato não houver nada acontecendo:
    /// com uma resposta saindo ou uma tarefa a comunicar, ela continua ouvindo.
    /// </summary>
    private bool PodeDormir() =>
        !PodeInterromper && _falas.Reader.Count == 0 && _avisos.Reader.Count == 0;

    /// <summary>
    /// Grava até o fim da fala — inclusive enquanto a Alina responde, que é quando o
    /// microfone ouve as duas vozes. Esse áudio não é jogado fora: ele é transcrito como
    /// qualquer outro e o eco da voz dela sai depois, no texto, comparando com o que ela
    /// estava dizendo. Confirmada a sua voz, a fala dela cala na hora e a gravação fecha
    /// para o que você disse ser atendido logo, sem esperar ela terminar.
    /// </summary>
    private async Task<Captura> GravarAsync(VoiceOptions opcoes, CancellationToken sessao)
    {
        MarcarEscutando();
        _pararGravacao = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        DetectorSilencio silencio = new DetectorSilencio(
            TimeSpan.FromSeconds(opcoes.SegundosSilencioParaEncerrar),
            TimeSpan.FromSeconds(opcoes.SegundosJanelaConversa));
        DetectorInicioFala inicio = new DetectorInicioFala();

        bool falavaAoGravar = false;

        ProgressoSincrono nivel = new ProgressoSincrono(v =>
        {
            NivelAudio?.Invoke(v);

            if (_reproduzindoAudio || _status.Current == AssistantState.Speaking)
            {
                falavaAoGravar = true;

                if (opcoes.InterromperPorVoz && inicio.Alimentar(v))
                {
                    _fala?.Cancel();
                    _pararGravacao?.TrySetResult();
                }

                return;
            }

            // Ela acabou de se calar: fecha aqui para transcrever já o que foi dito por cima,
            // em vez de deixar a gravação correr até a próxima janela de silêncio.
            if (falavaAoGravar)
            {
                _pararGravacao?.TrySetResult();
                return;
            }

            if (silencio.Alimentar(v))
            {
                _pararGravacao?.TrySetResult();
            }
        });

        using CancellationTokenRegistration parada = sessao.Register(() => _pararGravacao?.TrySetResult());

        byte[] wav = await _recorder!.RecordAsync(_ => _pararGravacao.Task, nivel);

        bool houveFala = (silencio.Falou || falavaAoGravar) && !sessao.IsCancellationRequested;

        return new Captura(wav, houveFala, falavaAoGravar ? _falaEmCurso : null);
    }

    private async Task<Fala> TranscreverAsync(Captura captura, CancellationToken sessao)
    {
        try
        {
            string texto = await _stt!.TranscribeAsync(captura.Wav, sessao);

            return new Fala(FiltroEco.RemoverEco(texto, captura.EcoDe), JaNoLog: false);
        }
        catch (OperationCanceledException)
        {
            return Fala.Vazia;
        }
        catch (Exception ex)
        {
            _log.Adicionar("error", $"[erro na transcrição] {ex.Message}");
            return Fala.Vazia;
        }
    }

    /// <summary>
    /// Só responde. Consome o que a escuta enfileirou, junta as falas emendadas num pedido
    /// só e, nas brechas em que não há nada a atender, entrega o resultado das tarefas que
    /// terminaram em paralelo.
    /// </summary>
    private async Task ResponderEnquantoHouverAsync(VoiceOptions opcoes, CancellationToken sessao)
    {
        while (!sessao.IsCancellationRequested)
        {
            Pedido? pedido = await ProximoPedidoAsync(sessao);
            if (pedido is null)
            {
                break;
            }

            if (pedido.ParaRegistrar.Length > 0)
            {
                _log.Adicionar("user", pedido.ParaRegistrar);
            }

            if (pedido.DoUsuario && ComandoInterrupcao.EhPedidoDeParar(pedido.Texto, opcoes.PalavrasInterrupcao))
            {
                Interromper();
                continue;
            }

            await ResponderAsync(pedido.Texto, comVoz: true, sessao);

            if (!opcoes.ConversaContinua)
            {
                EncerrarSessao();
                break;
            }
        }
    }

    private async Task<Pedido?> ProximoPedidoAsync(CancellationToken sessao)
    {
        while (!sessao.IsCancellationRequested)
        {
            Pedido? fala = await DrenarFalasAsync(sessao);
            if (fala is not null)
            {
                return fala;
            }

            if (_avisos.Reader.TryRead(out BackgroundTask? tarefa))
            {
                return new Pedido(MontarAvisoDeTarefa(tarefa), DoUsuario: false, ParaRegistrar: string.Empty);
            }

            if (!await EsperarMovimentoAsync(sessao))
            {
                return null;
            }
        }

        return null;
    }

    /// <summary>
    /// Junta num pedido único tudo que já foi transcrito, aguardando uma janela curta pelo
    /// resto da frase — quem fala pausa no meio do raciocínio, e duas metades da mesma ideia
    /// não deveriam virar dois pedidos. O que foi digitado já apareceu no chat na hora do
    /// envio e não volta ao log.
    /// </summary>
    private async Task<Pedido?> DrenarFalasAsync(CancellationToken sessao)
    {
        List<string> partes = new List<string>();
        List<string> aRegistrar = new List<string>();

        while (true)
        {
            while (_falas.Reader.TryRead(out Task<Fala>? pendente))
            {
                Fala fala = await pendente;
                string texto = fala.Texto.Trim();
                if (texto.Length == 0)
                {
                    continue;
                }

                partes.Add(texto);
                if (!fala.JaNoLog)
                {
                    aRegistrar.Add(texto);
                }
            }

            if (partes.Count == 0)
            {
                return null;
            }

            using CancellationTokenSource espera = CancellationTokenSource.CreateLinkedTokenSource(sessao);
            espera.CancelAfter(JanelaCoalescencia);

            try
            {
                if (!await _falas.Reader.WaitToReadAsync(espera.Token))
                {
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        return partes.Count == 0
            ? null
            : new Pedido(string.Join(" ", partes), DoUsuario: true, string.Join(" ", aRegistrar));
    }

    private async Task<bool> EsperarMovimentoAsync(CancellationToken sessao)
    {
        Task<bool> fala = _falas.Reader.WaitToReadAsync(sessao).AsTask();
        Task<bool> aviso = _avisos.Reader.WaitToReadAsync(sessao).AsTask();

        try
        {
            Task<bool> primeiro = await Task.WhenAny(fala, aviso);
            return await primeiro;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private static string MontarAvisoDeTarefa(BackgroundTask tarefa)
    {
        string desfecho = tarefa.Status switch
        {
            BackgroundTaskStatus.Completed => "terminou",
            BackgroundTaskStatus.Failed => "falhou",
            _ => "foi cancelada",
        };

        return $"[tarefa em paralelo] A tarefa \"{tarefa.Description}\" que você colocou para rodar {desfecho}. " +
               $"Resultado: {tarefa.Result}\n\n" +
               "Conte isso ao usuário agora, em uma ou duas frases, com naturalidade — como quem retoma um " +
               "assunto que ficou pendente. Não repita o pedido inteiro nem descreva o processo.";
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

    /// <summary>
    /// Envia um texto digitado. Havendo algo em andamento ele entra na mesma fila da fala —
    /// digitar não corta mais o que estiver rodando, só entra atrás na vez.
    /// </summary>
    public async Task EnviarTextoAsync(string texto)
    {
        texto = texto?.Trim() ?? string.Empty;
        if (texto.Length == 0)
        {
            return;
        }

        _log.Adicionar("user", texto);

        Task? atendimento = null;
        lock (_porta)
        {
            if (_emConversaVoz || _atendendoTexto)
            {
                _falas.Writer.TryWrite(Task.FromResult(new Fala(texto, JaNoLog: true)));
            }
            else
            {
                _atendendoTexto = true;
                atendimento = ResponderPorEscritoAsync(texto);
            }
        }

        if (atendimento is not null)
        {
            await atendimento;
        }
    }

    /// <summary>
    /// Atende o texto e tudo que entrar na fila enquanto ele é atendido — inclusive o
    /// resultado das tarefas paralelas. Só devolve o teclado quando não sobra nada, e a
    /// baixa do <c>_atendendoTexto</c> acontece junto da checagem da fila para que uma
    /// mensagem enviada nesse instante não fique órfã.
    /// </summary>
    private async Task ResponderPorEscritoAsync(string texto)
    {
        AssinarTarefas();

        try
        {
            string? proximo = texto;

            while (proximo is not null)
            {
                await ResponderAsync(proximo, comVoz: false, CancellationToken.None);

                lock (_porta)
                {
                    proximo = ProximoPedidoJaPronto();
                    if (proximo is null)
                    {
                        _atendendoTexto = false;
                    }
                }
            }
        }
        finally
        {
            lock (_porta)
            {
                _atendendoTexto = false;
            }

            DesassinarTarefas();
            _status.Set(AssistantState.Idle);
        }
    }

    private string? ProximoPedidoJaPronto()
    {
        while (_falas.Reader.TryRead(out Task<Fala>? pendente))
        {
            if (!pendente.IsCompletedSuccessfully)
            {
                continue;
            }

            string texto = pendente.Result.Texto.Trim();
            if (texto.Length > 0)
            {
                return texto;
            }
        }

        return _avisos.Reader.TryRead(out BackgroundTask? tarefa) ? MontarAvisoDeTarefa(tarefa) : null;
    }

    /// <summary>
    /// Roda um pedido pela mesma pilha do chat (mesmo orquestrador, mesmas ferramentas e
    /// confirmações) e devolve a resposta a quem chamou, em vez de falar e registrar no log.
    /// É como o treino de habilidades observa a Alina executando de verdade.
    /// </summary>
    public async Task<string> ExecutarParaTreinoAsync(string texto, CancellationToken cancellationToken = default)
    {
        texto = texto?.Trim() ?? string.Empty;
        if (texto.Length == 0 || _status.Current != AssistantState.Idle)
        {
            return string.Empty;
        }

        using CancellationTokenSource cancelamento = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _trabalho = cancelamento;

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
            _trabalho = null;
            _status.Set(AssistantState.Idle);
        }
    }

    /// <summary>
    /// Atende um pedido com a resposta em streaming: o texto aparece no chat enquanto o
    /// modelo o gera e, no modo voz, cada frase completa já vai para a síntese — a Alina
    /// começa a falar a primeira frase com o resto da resposta ainda saindo. Ser cortada
    /// (<see cref="_fala"/>) cala a voz e nada mais: a geração segue até o fim para o
    /// texto ficar completo no chat e no histórico.
    /// </summary>
    private async Task ResponderAsync(string texto, bool comVoz, CancellationToken sessao)
    {
        using CancellationTokenSource trabalho = CancellationTokenSource.CreateLinkedTokenSource(sessao);
        _trabalho = trabalho;

        bool falar = comVoz && _services.GetRequiredService<VoiceOptions>().Enabled;

        using CancellationTokenSource fala = CancellationTokenSource.CreateLinkedTokenSource(sessao);
        Channel<string> blocos = Channel.CreateUnbounded<string>();
        DivisorFalaIncremental divisor = new DivisorFalaIncremental();
        StringBuilder ouvido = new StringBuilder();
        StringBuilder acumulado = new StringBuilder();
        Task? reproducao = null;

        if (falar)
        {
            _fala = fala;
            reproducao = ReproduzirFalaAsync(blocos.Reader, ouvido, fala.Token);
        }

        ProgressoTexto progresso = new ProgressoTexto(pedaco =>
        {
            acumulado.Append(pedaco);
            string textoAtual = acumulado.ToString();
            _log.AtualizarParcial(textoAtual);

            if (falar)
            {
                _falaEmCurso = textoAtual;
                foreach (string frase in divisor.Alimentar(pedaco))
                {
                    blocos.Writer.TryWrite(frase);
                }
            }
        });

        try
        {
            _status.Set(AssistantState.Thinking);
            _orchestrator ??= _services.GetRequiredService<IOrchestrator>();

            string resposta;
            try
            {
                resposta = await _orchestrator.SendAsync(texto, progresso, trabalho.Token);
            }
            catch (OperationCanceledException)
            {
                _log.ConcluirParcial();
                if (sessao.IsCancellationRequested)
                {
                    return;
                }

                _log.Adicionar("error", "(interrompido)");
                _orchestrator.RegistrarNota(NotaPedidoInterrompido);
                return;
            }
            catch (Exception ex)
            {
                _log.ConcluirParcial();
                _log.Adicionar("error", $"[erro] {ex.Message}");
                return;
            }

            resposta = string.IsNullOrWhiteSpace(resposta) ? "(sem resposta)" : resposta;

            if (_log.ParcialAtiva)
            {
                _log.ConcluirParcial(resposta);
            }
            else
            {
                _log.Adicionar("bot", resposta);
            }

            if (falar)
            {
                foreach (string frase in divisor.Concluir())
                {
                    blocos.Writer.TryWrite(frase);
                }

                blocos.Writer.TryComplete();
                await reproducao!;

                if (fala.IsCancellationRequested && !sessao.IsCancellationRequested)
                {
                    _orchestrator.RegistrarNota(MontarNotaFalaCortada(ouvido.ToString().Trim()));
                }
            }
        }
        finally
        {
            blocos.Writer.TryComplete();
            fala.Cancel();
            if (reproducao is not null)
            {
                await reproducao;
            }

            _fala = null;
            _trabalho = null;
            MarcarOcioso();
        }
    }

    /// <summary>
    /// Consome as frases enfileiradas pelo streaming e as reproduz em ordem, sintetizando
    /// a próxima enquanto a atual toca. Só existe um trecho em reprodução por vez — é o
    /// que torna o barge-in instantâneo. Ser cortado aqui cala a voz e nada mais.
    /// </summary>
    private async Task ReproduzirFalaAsync(ChannelReader<string> blocos, StringBuilder ouvido, CancellationToken fala)
    {
        _tts ??= _services.GetRequiredService<ITextToSpeech>();
        _player ??= _services.GetRequiredService<IAudioPlayer>();

        // Token próprio: um erro na reprodução precisa soltar o produtor (que pode estar
        // aguardando vaga no canal) mesmo sem o pedido de calar a voz ter sido feito.
        using CancellationTokenSource interna = CancellationTokenSource.CreateLinkedTokenSource(fala);

        Channel<(string Bloco, Task<byte[]> Audio)> sinteses =
            Channel.CreateBounded<(string Bloco, Task<byte[]> Audio)>(1);

        Task producao = Task.Run(async () =>
        {
            try
            {
                await foreach (string bloco in blocos.ReadAllAsync(interna.Token))
                {
                    Task<byte[]> audio = _tts.SynthesizeAsync(bloco, interna.Token);
                    Descartar(audio);
                    await sinteses.Writer.WriteAsync((bloco, audio), interna.Token);
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                sinteses.Writer.TryComplete();
            }
        }, CancellationToken.None);

        try
        {
            await foreach ((string bloco, Task<byte[]> audio) in sinteses.Reader.ReadAllAsync(fala))
            {
                byte[] mp3 = await audio;

                _status.Set(AssistantState.Speaking);
                _reproduzindoAudio = true;
                try
                {
                    await _player.PlayMp3Async(mp3, fala);
                }
                finally
                {
                    _reproduzindoAudio = false;
                }

                if (fala.IsCancellationRequested)
                {
                    break;
                }

                ouvido.Append(bloco).Append(' ');
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _log.Adicionar("error", $"[erro na fala] {ex.Message}");
        }
        finally
        {
            interna.Cancel();
            await producao;
        }
    }

    private static string MontarNotaFalaCortada(string ouvido) =>
        ouvido.Length == 0
            ? "O usuário voltou a falar antes de você emitir som: ele não ouviu nada da resposta anterior. " +
              "Nada do que você está executando foi cancelado — siga em frente e atenda o que ele disser agora."
            : "O usuário falou por cima e você parou de falar na hora, como se deve. Da resposta anterior ele " +
              $"só chegou a ouvir: \"{ouvido}\". Não repita o que ele já ouviu nem recomece do zero: o que você " +
              "estava executando continua valendo.";

    private void MarcarOcioso() =>
        _status.Set(_emConversaVoz ? AssistantState.Listening : AssistantState.Idle);

    /// <summary>
    /// Marca a escuta sem atropelar o que a UI precisa mostrar: os dois laços rodam juntos,
    /// e a Alina está sempre ouvindo — o que interessa ao orbe é o que ela está fazendo.
    /// </summary>
    private void MarcarEscutando()
    {
        if (_status.Current is AssistantState.Idle or AssistantState.Listening)
        {
            _status.Set(AssistantState.Listening);
        }
    }

    private void AssinarTarefas()
    {
        _tarefas ??= _services.GetService<IBackgroundTaskManager>();
        if (_tarefas is not null)
        {
            _tarefas.TaskFinished -= AoTerminarTarefa;
            _tarefas.TaskFinished += AoTerminarTarefa;
        }
    }

    private void DesassinarTarefas()
    {
        if (_tarefas is not null)
        {
            _tarefas.TaskFinished -= AoTerminarTarefa;
        }
    }

    private void AoTerminarTarefa(object? remetente, BackgroundTask tarefa) =>
        _avisos.Writer.TryWrite(tarefa);

    private static void Descartar(Task<byte[]>? sintese) =>
        sintese?.ContinueWith(
            static t => _ = t.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

    /// <summary>
    /// Um trecho gravado. <paramref name="EcoDe"/> traz o que a Alina estava falando durante
    /// a gravação, quando havia — é contra esse texto que a voz dela é reconhecida e removida.
    /// </summary>
    private sealed record Captura(byte[] Wav, bool HouveFala, string? EcoDe);

    /// <summary>Uma entrada na fila. O que veio digitado já foi ao chat e não é registrado de novo.</summary>
    private sealed record Fala(string Texto, bool JaNoLog)
    {
        public static readonly Fala Vazia = new(string.Empty, JaNoLog: true);
    }

    private sealed record Pedido(string Texto, bool DoUsuario, string ParaRegistrar);

    /// <summary>Reporta o progresso de forma síncrona na thread de captura (baixa latência).</summary>
    private sealed class ProgressoSincrono : IProgress<float>
    {
        private readonly Action<float> _acao;

        public ProgressoSincrono(Action<float> acao) => _acao = acao;

        public void Report(float value) => _acao(value);
    }

    /// <summary>
    /// Entrega cada pedaço da resposta de forma síncrona e em ordem — um <see cref="Progress{T}"/>
    /// comum despacharia para outra thread e poderia embaralhar os pedaços.
    /// </summary>
    private sealed class ProgressoTexto : IProgress<string>
    {
        private readonly Action<string> _acao;

        public ProgressoTexto(Action<string> acao) => _acao = acao;

        public void Report(string value) => _acao(value);
    }
}
