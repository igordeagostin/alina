namespace Alina.Core.Ferramentas;

/// <summary>
/// Resposta de um turno da criação conversacional de ferramenta. <see cref="Mensagem"/>
/// é o que a Alina diz no chat; <see cref="Rascunho"/> vem preenchido quando ela já tem
/// material suficiente para propor a ferramenta final.
/// </summary>
public sealed record RespostaGeracaoFerramenta(string Mensagem, RascunhoFerramenta? Rascunho)
{
    public bool TemRascunho => Rascunho is not null;
}
