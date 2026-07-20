namespace Alina.Core.Memory;

/// <summary>
/// Memória permanente do usuário: preferências pessoais, convenções de código,
/// padrões arquiteturais. Injetada no system prompt do orquestrador.
/// </summary>
public interface IProfileStore
{
    /// <summary>
    /// Retorna as preferências/convenções em texto livre (Markdown), ou
    /// <c>null</c> se ainda não houver perfil configurado.
    /// </summary>
    Task<string?> GetPreferencesAsync(CancellationToken cancellationToken = default);
}
