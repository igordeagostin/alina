using System.Windows;
using Microsoft.Extensions.Hosting;

namespace Alina.App;

public partial class App : System.Windows.Application
{
    private IHost? _host;

    public IServiceProvider Services => _host?.Services
        ?? throw new InvalidOperationException("Host ainda não inicializado.");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = Composition.BuildHost();

        var window = new MainWindow(_host.Services);
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        base.OnExit(e);
    }
}
