namespace Alina.Core.Habilidades;

/// <summary>
/// Persistência das habilidades ensinadas à Alina, cada uma um arquivo Markdown
/// numa pasta dedicada. O índice (nome + descrição) é sempre injetado no system
/// prompt; o conteúdo completo entra sob demanda via <see cref="ObterAsync"/>.
/// </summary>
public interface IHabilidadeStore
{
    /// <summary>Lista o índice leve de todas as habilidades (nome + descrição).</summary>
    Task<IReadOnlyList<HabilidadeResumo>> ListarAsync(CancellationToken cancellationToken = default);

    /// <summary>Carrega uma habilidade completa pelo nome. Retorna <c>null</c> se não existir.</summary>
    Task<Habilidade?> ObterAsync(string nome, CancellationToken cancellationToken = default);

    /// <summary>Cria ou atualiza a habilidade (arquivo <c>.md</c>).</summary>
    Task SalvarAsync(Habilidade habilidade, CancellationToken cancellationToken = default);

    /// <summary>Remove uma habilidade pelo nome. Retorna <c>true</c> se algo foi removido.</summary>
    Task<bool> RemoverAsync(string nome, CancellationToken cancellationToken = default);

    /// <summary>Indica se já existe uma habilidade com esse nome.</summary>
    Task<bool> ExisteAsync(string nome, CancellationToken cancellationToken = default);
}
