namespace Alina.Core.Ferramentas;

/// <summary>
/// Persistência das ferramentas declarativas criadas pelo usuário (ou pela própria
/// Alina), cada uma um arquivo <c>*.tool.json</c> na pasta de dados configurada.
/// É a mesma pasta que a UI de configurações governa, então mover a pasta de dados
/// leva as ferramentas junto.
/// </summary>
public interface IFerramentaStore
{
    /// <summary>Lista o índice leve de todas as ferramentas (nome + descrição + confirmação).</summary>
    Task<IReadOnlyList<FerramentaResumo>> ListarAsync(CancellationToken cancellationToken = default);

    /// <summary>Carrega a definição completa de uma ferramenta pelo nome. <c>null</c> se não existir.</summary>
    Task<DefinicaoFerramenta?> ObterAsync(string nome, CancellationToken cancellationToken = default);

    /// <summary>Cria ou atualiza a ferramenta (arquivo <c>.tool.json</c>).</summary>
    Task SalvarAsync(DefinicaoFerramenta definicao, CancellationToken cancellationToken = default);

    /// <summary>Remove uma ferramenta pelo nome. <c>true</c> se algo foi removido.</summary>
    Task<bool> RemoverAsync(string nome, CancellationToken cancellationToken = default);

    /// <summary>Indica se já existe uma ferramenta com esse nome.</summary>
    Task<bool> ExisteAsync(string nome, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lê, de forma síncrona, todas as definições válidas do disco. Usado pelo
    /// provider dinâmico para montar as tools a cada turno (hot-reload).
    /// </summary>
    IReadOnlyList<DefinicaoFerramenta> LerDefinicoes();
}
