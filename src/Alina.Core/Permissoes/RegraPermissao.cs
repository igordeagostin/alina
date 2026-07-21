namespace Alina.Core.Permissoes;

/// <summary>
/// Uma regra de permissão aprendida ou configurada. Casa um pedido quando a ferramenta bate
/// (ou é curinga) e os prefixos de comando/caminho e o diretório, quando informados, também batem.
/// </summary>
public sealed record RegraPermissao
{
    public string Id { get; init; } = Guid.NewGuid().ToString("n");

    /// <summary>Ferramenta que a regra cobre; <c>*</c> cobre qualquer ferramenta.</summary>
    public required string Ferramenta { get; init; }

    /// <summary>Se definido, casa quando o comando do pedido começa com este prefixo.</summary>
    public string? PrefixoComando { get; init; }

    /// <summary>Se definido, casa quando o caminho do pedido está sob este diretório/prefixo.</summary>
    public string? PrefixoCaminho { get; init; }

    /// <summary>Se definido, restringe a regra a pedidos cujo diretório de trabalho está sob ele.</summary>
    public string? Diretorio { get; init; }

    /// <summary><c>true</c> autoriza; <c>false</c> nega.</summary>
    public required bool Permitir { get; init; }

    /// <summary>Descrição amigável exibida na tela de permissões.</summary>
    public string? Rotulo { get; init; }

    public DateTimeOffset CriadaEm { get; init; } = DateTimeOffset.Now;
}
