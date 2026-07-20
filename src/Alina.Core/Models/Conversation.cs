using Microsoft.Extensions.AI;

namespace Alina.Core.Models;

/// <summary>
/// Representa uma conversa (memória de trabalho da sessão atual). As mensagens
/// reutilizam o tipo <see cref="ChatMessage"/> do Microsoft.Extensions.AI para
/// evitar recriar um modelo de mensagem próprio.
/// </summary>
public sealed class Conversation
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n");

    public string Title { get; set; } = "Nova conversa";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;

    /// <summary>
    /// Histórico de mensagens (usuário, assistente e chamadas/resultados de tools).
    /// A mensagem de sistema NÃO é persistida aqui — ela é montada a cada requisição.
    /// </summary>
    public List<ChatMessage> Messages { get; set; } = new();
}

/// <summary>Resumo leve de uma conversa, usado em listagens de histórico.</summary>
public sealed record ConversationSummary(string Id, string Title, DateTimeOffset UpdatedAt, int MessageCount);
