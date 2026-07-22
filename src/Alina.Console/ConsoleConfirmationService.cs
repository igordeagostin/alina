using Alina.Core.Tools;

namespace Alina.Console;

/// <summary>
/// Implementação de confirmação no console: mostra a ação e pergunta SIM/NÃO.
/// </summary>
public sealed class ConsoleConfirmationService : IConfirmationService
{
    public Task<bool> ConfirmAsync(string action, string? details = null, CancellationToken cancellationToken = default)
    {
        ConsoleColor previous = System.Console.ForegroundColor;
        System.Console.ForegroundColor = ConsoleColor.Yellow;
        System.Console.WriteLine();
        System.Console.WriteLine($"⚠  {action}");
        if (!string.IsNullOrWhiteSpace(details))
        {
            System.Console.WriteLine($"   {details}");
        }
        System.Console.Write("Deseja realmente executar? (SIM/NÃO): ");
        System.Console.ForegroundColor = previous;

        string? answer = System.Console.ReadLine()?.Trim();
        bool confirmed = answer is not null &&
            (answer.Equals("SIM", StringComparison.OrdinalIgnoreCase) ||
             answer.Equals("S", StringComparison.OrdinalIgnoreCase) ||
             answer.Equals("Y", StringComparison.OrdinalIgnoreCase) ||
             answer.Equals("YES", StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(confirmed);
    }
}
