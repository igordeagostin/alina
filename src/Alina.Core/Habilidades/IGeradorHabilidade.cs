using Microsoft.Extensions.AI;

namespace Alina.Core.Habilidades;

/// <summary>
/// Conduz a criação de uma habilidade como uma conversa (estilo "modo plan"): a
/// partir do histórico de mensagens do usuário, a Alina faz perguntas quando
/// precisa e, quando tem o suficiente, propõe o documento Markdown da habilidade.
/// </summary>
public interface IGeradorHabilidade
{
    Task<RespostaGeracao> ContinuarAsync(
        IReadOnlyList<ChatMessage> historico, CancellationToken cancellationToken = default);
}
