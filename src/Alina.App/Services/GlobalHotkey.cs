using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace Alina.App.Services;

/// <summary>
/// Atalho global (system-wide) via <c>RegisterHotKey</c>. Dispara
/// <see cref="Pressionado"/> quando a combinação é acionada em qualquer app.
/// Observação: <c>RegisterHotKey</c> só sinaliza o pressionar — o push-to-talk
/// é por toque (alterna gravar/parar), não segurar.
/// </summary>
public sealed class GlobalHotkey : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const uint ModNoRepeat = 0x4000;
    private const int IdHotkey = 0x414C; // "AL"

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private IntPtr _handle;
    private HwndSource? _source;
    private bool _registrado;

    public event Action? Pressionado;

    public void Registrar(IntPtr windowHandle, ModifierKeys modificadores, Key tecla)
    {
        _handle = windowHandle;
        _source = HwndSource.FromHwnd(windowHandle);
        _source?.AddHook(WndProc);

        var vk = (uint)KeyInterop.VirtualKeyFromKey(tecla);
        _registrado = RegisterHotKey(_handle, IdHotkey, (uint)modificadores | ModNoRepeat, vk);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == IdHotkey)
        {
            Pressionado?.Invoke();
            handled = true;
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_registrado)
        {
            UnregisterHotKey(_handle, IdHotkey);
            _registrado = false;
        }

        _source?.RemoveHook(WndProc);
        _source = null;
    }
}
