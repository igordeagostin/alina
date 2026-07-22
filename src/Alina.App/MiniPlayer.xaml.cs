using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Alina.App.Services;
using Alina.Core.Orchestration;

namespace Alina.App;

/// <summary>
/// Mini player flutuante da Alina: um cartão discreto num canto da tela que
/// sinaliza que ela ouviu o próprio nome e que há um diálogo em andamento,
/// mesmo com a janela principal oculta na bandeja. Nunca rouba o foco do que
/// o usuário está fazendo — clicar nele abre a janela completa.
/// </summary>
public partial class MiniPlayer : Window
{
    private const int GwlExstyle = -20;
    private const int WsExToolwindow = 0x00000080;
    private const int WsExNoactivate = 0x08000000;

    private const int SwpNosize = 0x0001;
    private const int SwpNozorder = 0x0004;
    private const int SwpNoactivate = 0x0010;

    private const double Margem = 4;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int ObterEstilo(nint hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int DefinirEstilo(nint hwnd, int index, int novo);

    [DllImport("user32.dll", EntryPoint = "SetWindowPos")]
    private static extern bool MoverJanela(nint hwnd, nint ordem, int x, int y, int largura, int altura, int sinalizadores);

    private readonly Action _aoAbrirJanela;
    private readonly Action _aoAlternarEscuta;
    private readonly Action _aoOcultar;
    private readonly DispatcherTimer _animacao;
    private readonly System.Windows.Shapes.Rectangle[] _barras;
    private readonly Storyboard _pulso;

    private CantoTela _canto = CantoTela.InferiorDireito;
    private AssistantState _estado = AssistantState.Idle;
    private double _fase;
    private volatile float _nivel;
    private bool _pulsando;
    private System.Drawing.Rectangle? _area;
    private (int X, int Y)? _ultimaPosicao;

    public MiniPlayer(Action aoAbrirJanela, Action aoAlternarEscuta, Action aoOcultar)
    {
        InitializeComponent();

        _aoAbrirJanela = aoAbrirJanela;
        _aoAlternarEscuta = aoAlternarEscuta;
        _aoOcultar = aoOcultar;

        _barras = [.. Barras.Children.OfType<System.Windows.Shapes.Rectangle>()];
        _pulso = (Storyboard)Resources["PulsoAura"];

        _animacao = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(60),
        };
        _animacao.Tick += AoAnimar;

        SizeChanged += (_, _) => Reposicionar();
        DpiChanged += (_, _) => Reposicionar();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        nint handle = new WindowInteropHelper(this).Handle;
        int estilo = ObterEstilo(handle, GwlExstyle);
        DefinirEstilo(handle, GwlExstyle, estilo | WsExToolwindow | WsExNoactivate);
    }

    /// <summary>Canto da área de trabalho onde o cartão é ancorado.</summary>
    public void DefinirCanto(CantoTela canto)
    {
        _canto = canto;
        Reposicionar();
    }

    /// <summary>Reflete o estado corrente da Alina no rótulo, no pulso e nas barras.</summary>
    public void Atualizar(AssistantState estado)
    {
        _estado = estado;

        Estado.Text = estado switch
        {
            AssistantState.Listening => "ouvindo…",
            AssistantState.Thinking => "pensando…",
            AssistantState.Executing => "executando…",
            AssistantState.Speaking => "falando…",
            _ => "à sua disposição",
        };

        BotaoMicrofone.Content = estado == AssistantState.Listening ? "⏹" : "🎙";
        BotaoMicrofone.IsEnabled = estado is AssistantState.Listening or AssistantState.Idle;
        BotaoMicrofone.ToolTip = estado == AssistantState.Listening
            ? "Encerrar a fala e processar"
            : "Falar com a Alina";

        DefinirPulso(estado != AssistantState.Idle);
    }

    /// <summary>Última fala ou resposta resumida; vazio esconde a linha de texto.</summary>
    public void DefinirTexto(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            Fala.Visibility = Visibility.Collapsed;
            Fala.Text = string.Empty;
            return;
        }

        Fala.Text = texto;
        Fala.Visibility = Visibility.Visible;
    }

    /// <summary>Amplitude do microfone (0–1), vinda da thread de captura.</summary>
    public void DefinirNivel(float nivel) => _nivel = Math.Max(_nivel, nivel);

    public void MostrarSuave()
    {
        BeginAnimation(OpacityProperty, null);

        if (Visibility != Visibility.Visible)
        {
            // Fixa o monitor no momento em que entra em cena: o da janela em uso.
            _area = MonitorAtivo.AreaUtil();
            _ultimaPosicao = null;

            Visibility = Visibility.Visible;
            Show();
            Reposicionar();
        }

        Topmost = true;
        _animacao.Start();
        BeginAnimation(OpacityProperty, new DoubleAnimation(Opacity, 1, TimeSpan.FromMilliseconds(180)));
    }

    public void EsconderSuave()
    {
        if (Visibility != Visibility.Visible)
        {
            return;
        }

        DoubleAnimation saida = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(260));
        saida.Completed += (_, _) =>
        {
            if (Opacity <= 0.01)
            {
                _animacao.Stop();
                DefinirPulso(false);
                Hide();
                Visibility = Visibility.Collapsed;
                _area = null;
            }
        };

        BeginAnimation(OpacityProperty, saida);
    }

    private void DefinirPulso(bool ligar)
    {
        if (ligar == _pulsando)
        {
            return;
        }

        _pulsando = ligar;

        if (ligar)
        {
            _pulso.Begin(this, true);
        }
        else
        {
            _pulso.Stop(this);
            Aura.Opacity = 0.18;
        }
    }

    /// <summary>
    /// Ancora o cartão no canto escolhido do monitor ativo. Como o app é
    /// per-monitor DPI aware, <c>Left</c>/<c>Top</c> em unidades independentes de
    /// dispositivo escorregam entre telas de escalas diferentes: a posição é
    /// calculada em pixels físicos e aplicada direto pelo <c>SetWindowPos</c>.
    /// </summary>
    private void Reposicionar()
    {
        if (PresentationSource.FromVisual(this) is not HwndSource origem || origem.CompositionTarget is null)
        {
            return;
        }

        System.Drawing.Rectangle area = _area ??= MonitorAtivo.AreaUtil();
        Matrix escala = origem.CompositionTarget.TransformToDevice;
        double largura = ActualWidth * escala.M11;
        double altura = ActualHeight * escala.M22;
        double margem = Margem * escala.M11;

        bool direita = _canto is CantoTela.InferiorDireito or CantoTela.SuperiorDireito;
        bool baixo = _canto is CantoTela.InferiorDireito or CantoTela.InferiorEsquerdo;

        int x = (int)Math.Round(direita ? area.Right - largura - margem : area.Left + margem);
        int y = (int)Math.Round(baixo ? area.Bottom - altura - margem : area.Top + margem);

        if (_ultimaPosicao == (x, y))
        {
            return;
        }

        _ultimaPosicao = (x, y);
        MoverJanela(origem.Handle, 0, x, y, 0, 0, SwpNosize | SwpNozorder | SwpNoactivate);
    }

    private void AoAnimar(object? sender, EventArgs e)
    {
        _fase += 0.34;

        for (int i = 0; i < _barras.Length; i++)
        {
            double onda = (Math.Sin(_fase + i * 0.85) + 1) / 2;
            double intensidade = _estado switch
            {
                AssistantState.Listening => Math.Clamp(_nivel * 3.2, 0.04, 1) * (0.5 + 0.5 * onda),
                AssistantState.Speaking => 0.25 + 0.6 * onda,
                AssistantState.Thinking or AssistantState.Executing => 0.12 + 0.24 * onda,
                _ => 0.04,
            };

            _barras[i].Height = 3 + (intensidade * 11);
        }

        _nivel *= 0.8f;
    }

    private void AoClicarNoCorpo(object sender, System.Windows.Input.MouseButtonEventArgs e) => _aoAbrirJanela();

    private void AoClicarMicrofone(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _aoAlternarEscuta();
    }

    private void AoClicarFechar(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _aoOcultar();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        _animacao.Stop();
        base.OnClosing(e);
    }
}
