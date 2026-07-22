using System.Windows.Threading;
using Alina.Core.Orchestration;
using Alina.Voice;

namespace Alina.App.Services;

/// <summary>
/// Liga a detecção de palavra de ativação ("Alina") ao fluxo de voz. Com a
/// assistente ociosa, a palavra dispara o mesmo <see cref="VoiceController.AlternarEscutaAsync"/>
/// da hotkey. O microfone só é liberado durante a gravação do que você diz; enquanto
/// a Alina pensa ou fala, a escuta continua ligada — é assim que chamá-la pelo nome
/// (ou dizer uma das <see cref="VoiceOptions.PalavrasInterrupcao"/>) corta a resposta
/// no meio e devolve a palavra a você.
/// </summary>
public sealed class GerenciadorPalavraAtivacao : IDisposable
{
    private readonly IDetectorPalavraAtivacao _detector;
    private readonly VoiceController _voz;
    private readonly IAssistantStatus _status;
    private readonly ConversationUiState _log;
    private readonly VoiceOptions _opcoes;

    private bool _ligado;

    public GerenciadorPalavraAtivacao(
        IDetectorPalavraAtivacao detector,
        VoiceController voz,
        IAssistantStatus status,
        ConversationUiState log,
        VoiceOptions opcoes)
    {
        _detector = detector;
        _voz = voz;
        _status = status;
        _log = log;
        _opcoes = opcoes;

        _detector.PalavraDetectada += AoDetectar;
        _detector.InterrupcaoDetectada += AoDetectarInterrupcao;
        _detector.Falhou += AoFalhar;
        _voz.MicrofoneOcupado += AoOcuparMicrofone;
        _voz.MicrofoneLivre += AoLiberarMicrofone;
        _voz.Concluido += AoConcluir;
    }

    /// <summary>Indica se há chave e modelo válidos para a palavra de ativação funcionar.</summary>
    public bool Disponivel => _detector.Configurado;

    /// <summary>Liga ou desliga a escuta pela palavra de ativação em tempo de execução.</summary>
    public void Definir(bool ligar)
    {
        _ligado = ligar && _detector.Configurado;

        if (!_ligado)
        {
            _detector.Parar();
            return;
        }

        _detector.Iniciar();
        RecarregarGramatica();
    }

    /// <summary>
    /// A gramática restrita do reconhecedor é montada ao abrir a captura, então
    /// mudanças nas palavras só valem depois de reabri-la.
    /// </summary>
    private void RecarregarGramatica()
    {
        if (_status.Current != AssistantState.Listening)
        {
            _detector.Pausar();
            _detector.Retomar();
        }
    }

    private void AoOcuparMicrofone() => _detector.Pausar();

    private void AoLiberarMicrofone()
    {
        if (_ligado && _opcoes.InterromperPorVoz)
        {
            _detector.Retomar();
        }
    }

    private void AoConcluir()
    {
        if (_ligado)
        {
            _detector.Retomar();
        }
    }

    private void AoDetectar() => NoDispatcher(() =>
    {
        if (_status.Current == AssistantState.Idle || _opcoes.InterromperPorVoz)
        {
            _ = _voz.AlternarEscutaAsync();
        }
    });

    private void AoDetectarInterrupcao() => NoDispatcher(() =>
    {
        if (_opcoes.InterromperPorVoz && _voz.PodeInterromper)
        {
            _ = _voz.AlternarEscutaAsync();
        }
    });

    private static void NoDispatcher(Action acao)
    {
        Dispatcher? dispatcher = System.Windows.Application.Current?.Dispatcher;
        dispatcher?.InvokeAsync(acao);
    }

    private void AoFalhar(Exception ex)
    {
        _ligado = false;
        _log.Adicionar("error", $"[palavra de ativação] {ex.Message}");
    }

    public void Dispose()
    {
        _detector.PalavraDetectada -= AoDetectar;
        _detector.InterrupcaoDetectada -= AoDetectarInterrupcao;
        _detector.Falhou -= AoFalhar;
        _voz.MicrofoneOcupado -= AoOcuparMicrofone;
        _voz.MicrofoneLivre -= AoLiberarMicrofone;
        _voz.Concluido -= AoConcluir;
        _detector.Parar();
    }
}
