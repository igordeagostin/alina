using Microsoft.Win32;

namespace Alina.App.Services;

/// <summary>
/// Iniciar com o Windows via a chave <c>Run</c> do usuário atual (sem exigir
/// privilégios de administrador). Inicia minimizado na bandeja (<c>--tray</c>).
/// </summary>
public static class Autostart
{
    private const string Chave = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Nome = "Alina";

    public static bool Ativo
    {
        get
        {
            using RegistryKey? chave = Registry.CurrentUser.OpenSubKey(Chave);
            return chave?.GetValue(Nome) is not null;
        }
    }

    public static void Definir(bool ativo)
    {
        using RegistryKey chave = Registry.CurrentUser.OpenSubKey(Chave, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(Chave);

        if (ativo)
        {
            string? caminho = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(caminho))
            {
                chave.SetValue(Nome, $"\"{caminho}\" --tray");
            }
        }
        else
        {
            chave.DeleteValue(Nome, throwOnMissingValue: false);
        }
    }
}
