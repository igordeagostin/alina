namespace Alina.Core.Permissoes;

/// <summary>Resultado da avaliação de um pedido de permissão pela política.</summary>
public enum DecisaoPermissao
{
    /// <summary>Autorizado automaticamente, sem perguntar.</summary>
    Permitir,

    /// <summary>Negado automaticamente, sem perguntar.</summary>
    Negar,

    /// <summary>Indefinido pela política: deve-se perguntar ao usuário.</summary>
    Perguntar,
}
