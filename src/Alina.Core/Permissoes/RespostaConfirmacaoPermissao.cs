namespace Alina.Core.Permissoes;

/// <summary>
/// Resposta do usuário a uma confirmação de permissão: se autorizou e com qual abrangência.
/// O <see cref="Escopo"/> é ignorado quando <see cref="Permitido"/> e o escopo não pedem
/// persistência (ex.: <see cref="EscopoPermissao.UmaVez"/>).
/// </summary>
public sealed record RespostaConfirmacaoPermissao(bool Permitido, EscopoPermissao Escopo)
{
    /// <summary>Resposta de recusa pontual (não cria regra).</summary>
    public static RespostaConfirmacaoPermissao Negada { get; } = new(false, EscopoPermissao.UmaVez);

    /// <summary>Resposta de autorização pontual (não cria regra).</summary>
    public static RespostaConfirmacaoPermissao PermitidaUmaVez { get; } = new(true, EscopoPermissao.UmaVez);
}
