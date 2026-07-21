using System.Drawing;
using System.Windows;
using System.Windows.Forms;

namespace Alina.App.Services;

/// <summary>
/// Ícone de bandeja da Alina: mostra/oculta a janela, alterna o modo de tela,
/// liga/desliga o autostart e encerra o app. Usa a logo oficial da Alina
/// (Assets/alina.ico), coerente com a identidade visual.
/// </summary>
public sealed class TrayIconController : IDisposable
{
    private readonly Window _janela;
    private readonly ShellUiState _uiState;
    private readonly Action _aoSair;
    private readonly NotifyIcon _notify;
    private readonly ToolStripMenuItem _itemAutostart;

    public TrayIconController(Window janela, ShellUiState uiState, Action aoSair)
    {
        _janela = janela;
        _uiState = uiState;
        _aoSair = aoSair;

        var menu = new ContextMenuStrip();
        menu.Items.Add("Mostrar / ocultar", null, (_, _) => AlternarJanela());
        menu.Items.Add("Alternar modo de tela", null, (_, _) => _uiState.Alternar());

        _itemAutostart = new ToolStripMenuItem("Iniciar com o Windows", null, (_, _) => AlternarAutostart())
        {
            Checked = Autostart.Ativo,
        };
        menu.Items.Add(_itemAutostart);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Sair", null, (_, _) => _aoSair());

        _notify = new NotifyIcon
        {
            Icon = CriarIcone(),
            Text = "Alina",
            Visible = true,
            ContextMenuStrip = menu,
        };
        _notify.DoubleClick += (_, _) => MostrarJanela();
    }

    private void AlternarJanela()
    {
        if (_janela.IsVisible)
        {
            _janela.Hide();
        }
        else
        {
            MostrarJanela();
        }
    }

    private void MostrarJanela()
    {
        _janela.Show();
        _janela.WindowState = WindowState.Normal;
        _janela.Activate();
    }

    private void AlternarAutostart()
    {
        Autostart.Definir(!Autostart.Ativo);
        _itemAutostart.Checked = Autostart.Ativo;
    }

    private static Icon CriarIcone()
    {
        var uri = new Uri("pack://application:,,,/Assets/alina.ico");
        using var stream = System.Windows.Application.GetResourceStream(uri)!.Stream;
        return new Icon(stream, new System.Drawing.Size(32, 32));
    }

    public void Dispose()
    {
        _notify.Visible = false;
        _notify.Dispose();
    }
}
