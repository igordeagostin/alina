using System.IO;
using Microsoft.Win32;

namespace Alina.App.Services;

/// <summary>
/// Abre o diálogo nativo do Windows para escolher uma pasta. Garante execução na thread de UI
/// (STA), já que os handlers do Blazor podem rodar fora dela.
/// </summary>
public sealed class SeletorPasta
{
    /// <summary>Mostra o seletor e devolve a pasta escolhida, ou <c>null</c> se cancelado.</summary>
    public string? Escolher(string? diretorioInicial = null)
    {
        string? resultado = null;

        void Abrir()
        {
            var dialog = new OpenFolderDialog { Title = "Escolher pasta do projeto" };
            if (!string.IsNullOrWhiteSpace(diretorioInicial) && Directory.Exists(diretorioInicial))
            {
                dialog.InitialDirectory = diretorioInicial;
            }

            if (dialog.ShowDialog() == true)
            {
                resultado = dialog.FolderName;
            }
        }

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(Abrir);
        }
        else
        {
            Abrir();
        }

        return resultado;
    }
}
