using System.Drawing;
using System.Windows;
using System.Windows.Forms;

namespace Alina.App.Services;

/// <summary>
/// Ícone de bandeja da Alina: mostra/oculta a janela, alterna o modo de tela,
/// liga/desliga o autostart e encerra o app. O ícone é um orbe âmbar desenhado
/// em runtime, coerente com a identidade visual.
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
        using var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var brush = new SolidBrush(ColorTranslator.FromHtml("#DFA53A"));
            g.FillEllipse(brush, 4, 4, 24, 24);
        }

        var handle = bitmap.GetHicon();
        using var temp = Icon.FromHandle(handle);
        return (Icon)temp.Clone();
    }

    public void Dispose()
    {
        _notify.Visible = false;
        _notify.Dispose();
    }
}
