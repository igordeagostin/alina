namespace Alina.Core.Tools;

/// <summary>
/// Abstração de confirmação de segurança. Implementada pela camada de UI
/// (console, desktop, voz), mantém as tools agnósticas de interface e
/// centraliza a política "Deseja realmente executar? (SIM/NÃO)".
/// </summary>
public interface IConfirmationService
{
    /// <summary>
    /// Pede confirmação ao usuário para executar uma ação crítica.
    /// Retorna <c>true</c> se autorizado, <c>false</c> caso contrário.
    /// </summary>
    /// <param name="action">Descrição da ação a ser confirmada.</param>
    /// <param name="details">Detalhe adicional (ex: o comando exato a executar).</param>
    Task<bool> ConfirmAsync(string action, string? details = null, CancellationToken cancellationToken = default);
}
