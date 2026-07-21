using System.ComponentModel;
using System.Windows;
using Alina.App.Components;
using Alina.App.Services;
using Microsoft.AspNetCore.Components.WebView.Wpf;

namespace Alina.App;

public partial class MainWindow : Window
{
    private readonly ShellUiState _uiState;

    private const double LarguraCompacta = 380;
    private const double AlturaCompacta = 460;
    private const double LarguraDetalhada = 460;
    private const double AlturaDetalhada = 720;

    public MainWindow(IServiceProvider services)
    {
        InitializeComponent();

        _uiState = (ShellUiState)services.GetService(typeof(ShellUiState))!;
        _uiState.ModoAlterado += AoAlterarModo;
        AplicarModo();

        Blazor.Services = services;
        Blazor.RootComponents.Add(new RootComponent
        {
            Selector = "#app",
            ComponentType = typeof(Shell),
        });
    }

    private void AoAlterarModo() => Dispatcher.Invoke(AplicarModo);

    private void AplicarModo()
    {
        if (_uiState.Compacto)
        {
            Width = LarguraCompacta;
            Height = AlturaCompacta;
        }
        else
        {
            Width = LarguraDetalhada;
            Height = AlturaDetalhada;
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
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
        base.OnClosed(e);
    }
}
