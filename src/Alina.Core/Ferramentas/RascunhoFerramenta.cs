namespace Alina.Core.Ferramentas;

/// <summary>
/// Rascunho de ferramenta proposto pela Alina durante a criação conversacional.
/// Carrega a <see cref="Definicao"/> completa (pronta para salvar) e um
/// <see cref="Resumo"/> curto do que ela faz, para revisão humana.
/// </summary>
public sealed record RascunhoFerramenta(DefinicaoFerramenta Definicao, string Resumo);
