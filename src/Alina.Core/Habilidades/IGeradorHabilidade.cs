using Microsoft.Extensions.AI;

namespace Alina.Core.Habilidades;

/// <summary>
/// Conduz a criação, a edição e o treino de uma habilidade como uma conversa (estilo
/// "modo plan"): a partir do histórico de mensagens do usuário, a Alina faz perguntas
/// quando precisa e, quando tem o suficiente, propõe o documento Markdown da habilidade.
/// </summary>
public interface IGeradorHabilidade
{
    /// <summary>
    /// Avança um turno da conversa. Sem <paramref name="contexto"/>, a habilidade é
    /// criada do zero; com contexto, a Alina recebe o documento atual e propõe a versão
    /// revisada.
    /// </summary>
    Task<RespostaGeracao> ContinuarAsync(
        IReadOnlyList<ChatMessage> historico,
        ContextoHabilidade? contexto = null,
        CancellationToken cancellationToken = default);
}
