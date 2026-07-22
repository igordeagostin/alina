using Microsoft.Extensions.AI;

namespace Alina.Core.Ferramentas;

/// <summary>
/// Conduz a criação de uma ferramenta como uma conversa (estilo "modo plan"): a partir
/// do histórico de mensagens, a Alina pergunta o que falta e, quando tem o suficiente,
/// propõe a <see cref="DefinicaoFerramenta"/> — comando, argumentos e parâmetros.
/// </summary>
public interface IGeradorFerramenta
{
    Task<RespostaGeracaoFerramenta> ContinuarAsync(
        IReadOnlyList<ChatMessage> historico, CancellationToken cancellationToken = default);
}
