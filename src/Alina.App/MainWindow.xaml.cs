using System.ComponentModel;
using System.Windows;
using Size = System.Windows.Size;
using System.Windows.Media;
using System.Windows.Threading;
using Alina.App.Components;
using Alina.App.Services;
using Microsoft.AspNetCore.Components.WebView.Wpf;

namespace Alina.App;

public partial class MainWindow : Window
{
    private readonly ShellUiState _uiState;
    private readonly ConfiguracoesService _config;
    private readonly DispatcherTimer _memorizarTamanho;

    private const double LarguraCompacta = 380;
    private const double AlturaCompacta = 460;
    private const double LarguraDetalhada = 560;
    private const double AlturaDetalhada = 860;

    private Size _tamanhoAplicado;

    public MainWindow(IServiceProvider services)
    {
        InitializeComponent();

        _uiState = (ShellUiState)services.GetService(typeof(ShellUiState))!;
        _config = (ConfiguracoesService)services.GetService(typeof(ConfiguracoesService))!;
        _uiState.ModoAlterado += AoAlterarModo;
        _config.TamanhoJanelaRedefinido += AoRedefinirTamanho;

        _memorizarTamanho = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _memorizarTamanho.Tick += AoPararDeRedimensionar;

        AplicarModo();
        SizeChanged += AoRedimensionar;

        Blazor.Services = services;
        Blazor.RootComponents.Add(new RootComponent
        {
            Selector = "#app",
            ComponentType = typeof(Shell),
        });
    }

    private void AoAlterarModo() => Dispatcher.Invoke(AplicarModo);

    private void AoRedefinirTamanho() => Dispatcher.Invoke(AplicarModo);

    private void AplicarModo()
    {
        bool compacto = _uiState.Compacto;
        TamanhoJanela? memorizado = _config.Atual.LembrarTamanhoJanela
            ? compacto ? _config.Atual.JanelaCompacta : _config.Atual.JanelaDetalhada
            : null;

        Size alvo = CaberNaTela(
            memorizado?.Largura ?? (compacto ? LarguraCompacta : LarguraDetalhada),
            memorizado?.Altura ?? (compacto ? AlturaCompacta : AlturaDetalhada));

        _memorizarTamanho.Stop();
        _tamanhoAplicado = alvo;
        Width = alvo.Width;
        Height = alvo.Height;
    }

    private Size CaberNaTela(double largura, double altura)
    {
        System.Drawing.Rectangle area = MonitorAtivo.AreaUtil();
        DpiScale dpi = VisualTreeHelper.GetDpi(this);
        double maxLargura = Math.Max(MinWidth, area.Width / dpi.DpiScaleX);
        double maxAltura = Math.Max(MinHeight, area.Height / dpi.DpiScaleY);

        return new Size(
            Math.Clamp(largura, MinWidth, maxLargura),
            Math.Clamp(altura, MinHeight, maxAltura));
    }

    private void AoRedimensionar(object sender, SizeChangedEventArgs e)
    {
        if (WindowState != WindowState.Normal || !_config.Atual.LembrarTamanhoJanela)
        {
            return;
        }

        bool aplicadoPorNos = Math.Abs(ActualWidth - _tamanhoAplicado.Width) < 1
            && Math.Abs(ActualHeight - _tamanhoAplicado.Height) < 1;
        if (aplicadoPorNos)
        {
            return;
        }

        _memorizarTamanho.Stop();
        _memorizarTamanho.Start();
    }

    private void AoPararDeRedimensionar(object? sender, EventArgs e)
    {
        _memorizarTamanho.Stop();
        Memorizar();
    }

    private void Memorizar()
    {
        if (WindowState != WindowState.Normal)
        {
            return;
        }

        _tamanhoAplicado = new Size(ActualWidth, ActualHeight);
        _config.RegistrarTamanhoJanela(_uiState.Compacto, ActualWidth, ActualHeight);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_memorizarTamanho.IsEnabled)
        {
            _memorizarTamanho.Stop();
            Memorizar();
        }

        // Fechar no X apenas minimiza para a bandeja; sair de verdade é pelo menu.
        if (System.Windows.Application.Current is App { Encerrando: false })
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _uiState.ModoAlterado -= AoAlterarModo;
        _config.TamanhoJanelaRedefinido -= AoRedefinirTamanho;
        _memorizarTamanho.Tick -= AoPararDeRedimensionar;
        SizeChanged -= AoRedimensionar;
        base.OnClosed(e);
    }
}
