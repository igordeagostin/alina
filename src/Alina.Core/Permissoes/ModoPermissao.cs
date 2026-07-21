namespace Alina.Core.Permissoes;

/// <summary>
/// Comportamento padrão da política quando nenhuma regra específica cobre o pedido.
/// </summary>
public enum ModoPermissao
{
    /// <summary>Pergunta ao usuário tudo que não for coberto por uma regra que libere.</summary>
    Perguntar,

    /// <summary>Libera leituras/consultas automaticamente; pergunta escritas e comandos.</summary>
    AutoLeitura,

    /// <summary>Libera edições de arquivo e leituras; pergunta comandos e rede.</summary>
    AceitarEdicoes,

    /// <summary>Faz tudo sem perguntar (equivalente a autonomia total).</summary>
    Autonomia,
}
