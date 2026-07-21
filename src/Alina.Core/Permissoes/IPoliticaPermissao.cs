namespace Alina.Core.Permissoes;

/// <summary>
/// Decide, antes de interromper o usuário, se um pedido de permissão do Claude Code pode ser
/// liberado, negado ou precisa ser perguntado — com base nas opções e nas regras aprendidas.
/// Também registra novas regras quando o usuário escolhe "permitir/negar sempre".
/// </summary>
public interface IPoliticaPermissao
{
    /// <summary>Opções estáticas correntes (modo padrão, diretórios confiáveis, allowlists…).</summary>
    PoliticaPermissaoOptions Opcoes { get; }

    /// <summary>Regras aprendidas/persistidas correntes (somente leitura).</summary>
    IReadOnlyList<RegraPermissao> Regras { get; }

    /// <summary>Avalia um pedido e devolve a decisão automática (ou <see cref="DecisaoPermissao.Perguntar"/>).</summary>
    DecisaoPermissao Avaliar(PedidoPermissao pedido);

    /// <summary>
    /// Registra a decisão do usuário conforme o escopo (cria/persiste regra quando aplicável).
    /// Escopos <see cref="EscopoPermissao.UmaVez"/> não geram regra.
    /// </summary>
    void Aprender(PedidoPermissao pedido, RespostaConfirmacaoPermissao resposta);

    /// <summary>Substitui as opções e persiste.</summary>
    void AtualizarOpcoes(PoliticaPermissaoOptions opcoes);

    /// <summary>Remove uma regra aprendida pelo id e persiste.</summary>
    void RemoverRegra(string id);
}
