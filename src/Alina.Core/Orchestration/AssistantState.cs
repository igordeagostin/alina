namespace Alina.Core.Orchestration;

/// <summary>
/// Estado atual da assistente, consumido pelos heads de UI para refletir o
/// orbe, o texto de status e as animações.
/// </summary>
public enum AssistantState
{
    /// <summary>Parada, aguardando comando.</summary>
    Idle,

    /// <summary>Captando áudio do microfone.</summary>
    Listening,

    /// <summary>LLM processando o pedido.</summary>
    Thinking,

    /// <summary>Rodando uma tool.</summary>
    Executing,

    /// <summary>Reproduzindo a resposta em voz.</summary>
    Speaking,
}
