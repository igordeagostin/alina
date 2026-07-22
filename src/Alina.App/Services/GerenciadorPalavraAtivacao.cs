using System.Windows.Threading;
using Alina.Core.Orchestration;
using Alina.Voice;

namespace Alina.App.Services;

/// <summary>
/// Liga a detecção de palavra de ativação ("Alina") ao fluxo de voz. Quando a
/// palavra é reconhecida, dispara o mesmo <see cref="VoiceController.AlternarEscutaAsync"/>
/// da hotkey — desde que a assistente esteja ociosa. Enquanto um turno acontece
/// (gravação, fala ou execução), a escuta é pausada para liberar o microfone e
/// evitar reativações acidentais; ao concluir, é retomada.
/// </summary>
public sealed class GerenciadorPalavraAtivacao : IDisposable
{
    private readonly IDetectorPalavraAtivacao _detector;
    private readonly VoiceController _voz;
    private readonly IAssistantStatus _status;
    private readonly ConversationUiState _log;

    private bool _ligado;

    public GerenciadorPalavraAtivacao(
        IDetectorPalavraAtivacao detector,
        VoiceController voz,
        IAssistantStatus status,
        ConversationUiState log)
    {
        _detector = detector;
        _voz = voz;
        _status = status;
        _log = log;

        _detector.PalavraDetectada += AoDetectar;
        _detector.Falhou += AoFalhar;
        _voz.EscutaComecou += AoComecarEscuta;
        _voz.Concluido += AoConcluir;
    }

    /// <summary>Indica se há chave e modelo válidos para a palavra de ativação funcionar.</summary>
    public bool Disponivel => _detector.Configurado;

    /// <summary>Liga ou desliga a escuta pela palavra de ativação em tempo de execução.</summary>
    public void Definir(bool ligar)
    {
        _ligado = ligar && _detector.Configurado;

        if (_ligado)
        {
            _detector.Iniciar();
        }
        else
        {
            _detector.Parar();
        }
    }

    private void AoComecarEscuta() => _detector.Pausar();

    private void AoConcluir()
    {
        if (_ligado)
        {
            _detector.Retomar();
        }
    }

    private void AoDetectar()
    {
        Dispatcher? dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        dispatcher.InvokeAsync(() =>
        {
            if (_status.Current == AssistantState.Idle)
            {
                _ = _voz.AlternarEscutaAsync();
            }
        });
    }

    private void AoFalhar(Exception ex)
    {
        _ligado = false;
        _log.Adicionar("error", $"[palavra de ativação] {ex.Message}");
    }

    public void Dispose()
    {
        _detector.PalavraDetectada -= AoDetectar;
        _detector.Falhou -= AoFalhar;
        _voz.EscutaComecou -= AoComecarEscuta;
        _voz.Concluido -= AoConcluir;
        _detector.Parar();
    }
}
