namespace Alina.Core.Habilidades;

/// <summary>
/// Habilidade já existente sobre a qual a conversa acontece. Sem contexto, a Alina
/// entende que está criando uma habilidade nova; com contexto, ela recebe o documento
/// atual e passa a revisá-lo no <see cref="Modo"/> indicado.
/// </summary>
public sealed record ContextoHabilidade(Habilidade Atual, ModoConversaHabilidade Modo);
