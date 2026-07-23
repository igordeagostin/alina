using System.Windows.Threading;
using Alina.Core.Orchestration;
using Alina.Voice;

namespace Alina.App.Services;

/// <summary>
/// Liga a detecção de palavra de ativação ("Alina") ao fluxo de voz: com a assistente
/// ociosa, dizer o nome dela abre a conversa, como a hotkey e o orbe.
/// <para>
/// Só serve para acordá-la. Aberta a conversa, o microfone fica com a sessão de voz do
/// começo ao fim e o reconhecedor local sai de cena — a partir daí quem escuta é a
/// própria captura, então você fala quando quiser sem precisar chamar pelo nome, e nada
/// do que ela estiver fazendo é cancelado por isso.
/// </para>
/// </summary>
public sealed class GerenciadorPalavraAtivacao : IDisposable
{
    private readonly IDetectorPalavraAtivacao _detector;
    private readonly VoiceController _voz;
    private readonly ConversationUiState _log;

    private bool _ligado;

    public GerenciadorPalavraAtivacao(
        IDetectorPalavraAtivacao detector,
        VoiceController voz,
        ConversationUiState log)
    {
        _detector = detector;
        _voz = voz;
        _log = log;

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
        if (!_voz.EmConversa)
        {
            _detector.Pausar();
            _detector.Retomar();
        }
    }

    private void AoOcuparMicrofone() => _detector.Pausar();

    private void AoLiberarMicrofone() => RetomarSeOciosa();

    private void AoConcluir() => RetomarSeOciosa();

    private void RetomarSeOciosa()
    {
        if (_ligado && !_voz.EmConversa)
        {
            _detector.Retomar();
        }
    }

    private void AoDetectar() => NoDispatcher(() =>
    {
        if (!_voz.EmConversa)
        {
            _ = _voz.AlternarEscutaAsync();
        }
    });

    /// <summary>
    /// Fora da conversa não há o que interromper, e dentro dela o reconhecedor está parado.
    /// Os pedidos de parar durante a conversa são reconhecidos no texto transcrito, que é
    /// muito mais confiável que a gramática restrita do modelo local.
    /// </summary>
    private void AoDetectarInterrupcao() => NoDispatcher(() =>
    {
        if (_voz.PodeInterromper)
        {
            _voz.Interromper();
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
