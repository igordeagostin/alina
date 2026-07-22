namespace Alina.Core.Ferramentas;

/// <summary>
/// Ferramenta já existente sobre a qual a conversa acontece. Sem contexto, a Alina
/// entende que está criando uma ferramenta nova; com contexto, ela recebe a definição
/// atual e passa a revisá-la.
/// </summary>
public sealed record ContextoFerramenta(DefinicaoFerramenta Atual);
