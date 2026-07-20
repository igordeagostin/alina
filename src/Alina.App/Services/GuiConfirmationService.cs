using System.Windows;
using Alina.Core.Tools;

namespace Alina.App.Services;

/// <summary>
/// Confirmação de segurança gráfica. Na Fase A usa um <see cref="MessageBox"/>
/// nativo do WPF (na thread de UI). Na Fase B vira um overlay dentro da própria
/// janela da Alina.
/// </summary>
public sealed class GuiConfirmationService : IConfirmationService
{
    public async Task<bool> ConfirmAsync(string action, string? details = null, CancellationToken cancellationToken = default)
    {
        var message = string.IsNullOrWhiteSpace(details)
            ? action
            : $"{action}\n\n{details}";

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            // Sem UI disponível: por segurança, nega.
            return false;
        }

        var result = await dispatcher.InvokeAsync(() =>
            MessageBox.Show(
                message,
                "Alina — confirmar ação",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No));

        return result == MessageBoxResult.Yes;
    }
}
