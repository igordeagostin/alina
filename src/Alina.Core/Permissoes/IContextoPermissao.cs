namespace Alina.Core.Permissoes;

/// <summary>
/// Contexto compartilhado da execução corrente, usado para enriquecer os pedidos de permissão
/// com o diretório de trabalho (que o Claude Code nem sempre inclui no input da ferramenta).
/// A tool de delegação define o diretório antes de executar.
/// </summary>
public interface IContextoPermissao
{
    /// <summary>Diretório de trabalho da execução em andamento (ou <c>null</c>).</summary>
    string? DiretorioAtual { get; set; }
}
