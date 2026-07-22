using Microsoft.Extensions.AI;

namespace Alina.Core.Ferramentas;

/// <summary>
/// Conduz a criação e a edição de uma ferramenta como uma conversa (estilo "modo plan"):
/// a partir do histórico de mensagens, a Alina pergunta o que falta e, quando tem o
/// suficiente, propõe a <see cref="DefinicaoFerramenta"/> — comando, argumentos e parâmetros.
/// </summary>
public interface IGeradorFerramenta
{
    /// <summary>
    /// Avança um turno da conversa. Sem <paramref name="contexto"/>, a ferramenta é criada
    /// do zero; com contexto, a Alina recebe a definição atual e propõe a versão revisada.
    /// </summary>
    Task<RespostaGeracaoFerramenta> ContinuarAsync(
        IReadOnlyList<ChatMessage> historico,
        ContextoFerramenta? contexto = null,
        CancellationToken cancellationToken = default);
}
