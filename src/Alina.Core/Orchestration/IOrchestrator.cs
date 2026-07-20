using Alina.Core.Models;

namespace Alina.Core.Orchestration;

/// <summary>
/// O "cérebro" da Alina: recebe o texto do usuário, coordena o LLM com as
/// tools disponíveis e mantém a conversa atual persistida.
/// </summary>
public interface IOrchestrator
{
    /// <summary>Conversa atualmente ativa (memória de trabalho).</summary>
    Conversation Current { get; }

    /// <summary>Inicia uma nova conversa em branco.</summary>
    void StartNew();

    /// <summary>Carrega uma conversa existente como a conversa ativa.</summary>
    Task<bool> ResumeAsync(string conversationId, CancellationToken cancellationToken = default);

    /// <summary>Envia uma mensagem do usuário e retorna a resposta final da assistente.</summary>
    Task<string> SendAsync(string userText, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gera um resumo conciso da conversa atual (via LLM, sem tools), para memorizá-la
    /// sob demanda. Retorna string vazia se não há nada a resumir.
    /// </summary>
    Task<string> SummarizeConversationAsync(CancellationToken cancellationToken = default);
}
