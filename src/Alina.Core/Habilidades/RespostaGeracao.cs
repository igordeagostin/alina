namespace Alina.Core.Habilidades;

/// <summary>
/// Resposta de um turno da criação conversacional de habilidade. <see cref="Mensagem"/>
/// é o que a Alina diz no chat (pergunta de esclarecimento ou comentário);
/// <see cref="Rascunho"/> vem preenchido quando ela já tem material suficiente para
/// propor o documento final.
/// </summary>
public sealed record RespostaGeracao(string Mensagem, RascunhoHabilidade? Rascunho)
{
    public bool TemRascunho => Rascunho is not null;
}
