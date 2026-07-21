namespace Alina.Core.Orchestration;

/// <summary>
/// Publica o estado corrente da assistente para os heads de UI reagirem
/// (orbe, texto de status). Os heads chamam <see cref="Set"/> ao redor das
/// fases de voz e do LLM; <see cref="AssistantState.Executing"/> é marcado
/// automaticamente durante a execução de uma tool. Contrato agnóstico de UI:
/// o console simplesmente não assina o evento.
/// </summary>
public interface IAssistantStatus
{
    /// <summary>Estado corrente.</summary>
    AssistantState Current { get; }

    /// <summary>Disparado sempre que o estado muda.</summary>
    event EventHandler<AssistantState>? Changed;

    /// <summary>Define o estado corrente e notifica os assinantes.</summary>
    void Set(AssistantState state);
}
