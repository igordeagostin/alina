namespace Alina.Core.Habilidades;

/// <summary>
/// Rascunho de habilidade proposto pela Alina durante a criação conversacional,
/// antes de o usuário revisar e salvar. <see cref="Ferramentas"/> traz as ferramentas
/// que ela concluiu que precisam existir para o documento ser executável — vazia
/// quando as ferramentas atuais já dão conta.
/// </summary>
public sealed record RascunhoHabilidade(
    string Titulo,
    string Descricao,
    string Conteudo,
    IReadOnlyList<FerramentaProposta> Ferramentas)
{
    public RascunhoHabilidade(string titulo, string descricao, string conteudo)
        : this(titulo, descricao, conteudo, Array.Empty<FerramentaProposta>())
    {
    }
}
