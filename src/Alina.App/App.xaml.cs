using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Alina.App.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Alina.App;

public partial class App : System.Windows.Application
{
    private IHost? _host;
    private MainWindow? _window;
    private GlobalHotkey? _hotkey;
    private TrayIconController? _tray;

    public IServiceProvider Services => _host?.Services
        ?? throw new InvalidOperationException("Host ainda não inicializado.");

    /// <summary>Indica que o app está encerrando de fato (não apenas minimizando para a bandeja).</summary>
    public bool Encerrando { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Não encerra ao esconder a janela — a Alina fica viva na bandeja.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _host = Composition.BuildHost();
        var services = _host.Services;

        _window = new MainWindow(services);

        // Garante o HWND mesmo iniciando oculto (necessário para a hotkey global).
        var handle = new WindowInteropHelper(_window).EnsureHandle();

        _hotkey = new GlobalHotkey();
        _hotkey.Pressionado += AoPressionarHotkey;
        _hotkey.Registrar(handle, ModifierKeys.Control, Key.Space);

        _tray = new TrayIconController(_window, services.GetRequiredService<ShellUiState>(), Encerrar);

        // Iniciada com o Windows (--tray) começa apenas na bandeja.
        if (!e.Args.Contains("--tray"))
        {
            _window.Show();
        }
    }

    private void AoPressionarHotkey()
    {
        var voz = Services.GetRequiredService<VoiceController>();
        Dispatcher.InvokeAsync(() => _ = voz.AlternarEscutaAsync());
    }

    private void Encerrar()
    {
        Encerrando = true;
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkey?.Dispose();
        _tray?.Dispose();
        _host?.Dispose();
        base.OnExit(e);
    }
}
