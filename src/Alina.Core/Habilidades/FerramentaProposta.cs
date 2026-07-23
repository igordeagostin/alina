using Alina.Core.Ferramentas;

namespace Alina.Core.Habilidades;

/// <summary>
/// Ferramenta que a Alina julgou necessária para a habilidade funcionar e propôs
/// junto com o documento. <see cref="Motivo"/> é a justificativa curta que ela dá
/// ao usuário; <see cref="SubstituiExistente"/> avisa que já há uma ferramenta com
/// esse nome, e salvar a proposta trocaria a atual.
/// </summary>
public sealed record FerramentaProposta(DefinicaoFerramenta Definicao, string Motivo, bool SubstituiExistente);
