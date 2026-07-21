namespace Alina.Core.Permissoes;

/// <summary>Abrangência da decisão que o usuário toma ao responder uma confirmação.</summary>
public enum EscopoPermissao
{
    /// <summary>Vale apenas para este pedido.</summary>
    UmaVez,

    /// <summary>Vale até o app ser fechado (regra não persistida).</summary>
    Sessao,

    /// <summary>Vale sempre, em qualquer diretório (regra persistida).</summary>
    Sempre,

    /// <summary>Vale sempre, mas só dentro do diretório de trabalho atual (regra persistida).</summary>
    SempreNesteDiretorio,
}
