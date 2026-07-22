using System.Windows;
using System.Windows.Threading;
using Alina.Core.Orchestration;

namespace Alina.App.Services;

/// <summary>
/// Decide quando o <see cref="MiniPlayer"/> aparece. A regra é a da bandeja: só
/// entra em cena quando a janela principal está oculta — iniciada com o Windows
/// (<c>--tray</c>) ou fechada no X — e a Alina foi acionada pela palavra de
/// ativação ou pela hotkey global. Ao terminar o diálogo, ele espera alguns
/// segundos e some sozinho.
/// </summary>
public sealed class MiniPlayerController : IDisposable
{
    private readonly IAssistantStatus _status;
    private readonly ConversationUiState _log;
    private readonly VoiceController _voz;
    private readonly DispatcherTimer _ocultarDepois;

    private Window? _janelaPrincipal;
    private MiniPlayer? _player;
    private ConfiguracoesApp _config = new();
    private bool _dispensadoNoTurno;

    public MiniPlayerController(IAssistantStatus status, ConversationUiState log, VoiceController voz)
    {
        _status = status;
        _log = log;
        _voz = voz;

        _ocultarDepois = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(_config.SegundosMiniPlayerVisivel),
        };
        _ocultarDepois.Tick += AoExpirar;

        _status.Changed += AoMudarEstado;
        _log.Alterado += AoMudarLog;
        _voz.NivelAudio += AoNivelAudio;
    }

    /// <summary>Liga o controlador à janela principal, cuja visibilidade define a regra de exibição.</summary>
    public void Vincular(Window janelaPrincipal)
    {
        _janelaPrincipal = janelaPrincipal;
        _janelaPrincipal.IsVisibleChanged += AoMudarVisibilidade;
        Avaliar();
    }

    /// <summary>Aplica as preferências do usuário (ligar/desligar, canto, tempo visível).</summary>
    public void Aplicar(ConfiguracoesApp config)
    {
        _config = config;
        _ocultarDepois.Interval = TimeSpan.FromSeconds(Math.Max(1, config.SegundosMiniPlayerVisivel));

        Executar(() =>
        {
            _player?.DefinirCanto(config.CantoMiniPlayer);
            if (!config.MiniPlayerMostraTexto)
            {
                _player?.DefinirTexto(null);
            }

            Avaliar();
        });
    }

    private bool JanelaOculta => _janelaPrincipal is null || !_janelaPrincipal.IsVisible;

    private void AoMudarEstado(object? sender, AssistantState estado) => Executar(() =>
    {
        if (estado != AssistantState.Idle)
        {
            _dispensadoNoTurno = false;
        }

        _player?.Atualizar(estado);
        Avaliar();
    });

    private void AoMudarLog() => Executar(() =>
    {
        if (_player is null || !_config.MiniPlayerMostraTexto)
        {
            return;
        }

        ChatEntry? ultima = _log.Snapshot().LastOrDefault();
        _player.DefinirTexto(ultima?.Texto);
    });

    private void AoNivelAudio(float nivel) => _player?.DefinirNivel(nivel);

    private void AoMudarVisibilidade(object? sender, DependencyPropertyChangedEventArgs e) => Avaliar();

    private void Avaliar()
    {
        bool ativo = _config.MostrarMiniPlayer
            && JanelaOculta
            && !_dispensadoNoTurno
            && _status.Current != AssistantState.Idle;

        if (ativo)
        {
            _ocultarDepois.Stop();
            Garantir().MostrarSuave();
            return;
        }

        if (_player is null)
        {
            return;
        }

        bool encerrando = _config.MostrarMiniPlayer
            && JanelaOculta
            && !_dispensadoNoTurno
            && _status.Current == AssistantState.Idle;

        if (encerrando)
        {
            _ocultarDepois.Stop();
            _ocultarDepois.Start();
            return;
        }

        _ocultarDepois.Stop();
        _player.EsconderSuave();
    }

    private void AoExpirar(object? sender, EventArgs e)
    {
        _ocultarDepois.Stop();
        _player?.EsconderSuave();
    }

    private MiniPlayer Garantir()
    {
        if (_player is not null)
        {
            return _player;
        }

        _player = new MiniPlayer(AbrirJanelaPrincipal, AlternarEscuta, Dispensar);
        _player.DefinirCanto(_config.CantoMiniPlayer);
        _player.Atualizar(_status.Current);

        if (_config.MiniPlayerMostraTexto)
        {
            _player.DefinirTexto(_log.Snapshot().LastOrDefault()?.Texto);
        }

        return _player;
    }

    private void AbrirJanelaPrincipal()
    {
        if (_janelaPrincipal is null)
        {
            return;
        }

        _janelaPrincipal.Show();
        _janelaPrincipal.WindowState = WindowState.Normal;
        _janelaPrincipal.Activate();
    }

    private void AlternarEscuta() => _ = _voz.AlternarEscutaAsync();

    private void Dispensar()
    {
        _dispensadoNoTurno = true;
        _ocultarDepois.Stop();
        _player?.EsconderSuave();
    }

    private static void Executar(Action acao)
    {
        Dispatcher? dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        if (dispatcher.CheckAccess())
        {
            acao();
            return;
        }

        dispatcher.InvokeAsync(acao);
    }

    public void Dispose()
    {
        _status.Changed -= AoMudarEstado;
        _log.Alterado -= AoMudarLog;
        _voz.NivelAudio -= AoNivelAudio;

        if (_janelaPrincipal is not null)
        {
            _janelaPrincipal.IsVisibleChanged -= AoMudarVisibilidade;
        }

        _ocultarDepois.Stop();
        _player?.Close();
    }
}
