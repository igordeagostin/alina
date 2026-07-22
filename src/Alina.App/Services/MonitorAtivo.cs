using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Alina.App.Services;

/// <summary>
/// Descobre em qual tela o usuário está trabalhando: a da janela em primeiro
/// plano, com a posição do cursor como desempate. Em multi-monitor é o que faz o
/// mini player nascer no monitor ativo, e não sempre no principal.
/// </summary>
internal static class MonitorAtivo
{
    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    /// <summary>Área útil (fora da barra de tarefas) do monitor ativo, em pixels físicos.</summary>
    public static Rectangle AreaUtil()
    {
        nint janelaAtiva = GetForegroundWindow();
        Screen tela = janelaAtiva != 0
            ? Screen.FromHandle(janelaAtiva)
            : Screen.FromPoint(Cursor.Position);

        return tela.WorkingArea;
    }
}
