namespace Alina.Core.Habilidades;

/// <summary>
/// Rascunho de habilidade proposto pela Alina durante a criação conversacional,
/// antes de o usuário revisar e salvar.
/// </summary>
public sealed record RascunhoHabilidade(string Titulo, string Descricao, string Conteudo);
