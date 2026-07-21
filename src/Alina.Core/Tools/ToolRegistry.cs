using Alina.Core.Orchestration;
using Microsoft.Extensions.AI;

namespace Alina.Core.Tools;

/// <summary>
/// Registro central das tools disponíveis. Fornece as <see cref="AIFunction"/>
/// para o orquestrador montar o <c>ChatOptions.Tools</c>. Quando um
/// <see cref="IAssistantStatus"/> está disponível, cada função é envolvida para
/// marcar <see cref="AssistantState.Executing"/> durante a execução.
/// </summary>
public sealed class ToolRegistry
{
    private readonly IReadOnlyList<ITool> _tools;
    private readonly IAssistantStatus? _status;

    public ToolRegistry(IEnumerable<ITool> tools, IAssistantStatus? status = null)
    {
        _tools = tools.ToList();
        _status = status;
    }

    public IReadOnlyList<ITool> Tools => _tools;

    /// <summary>Todas as tools como <see cref="AITool"/> para o pipeline de function-calling.</summary>
    public IList<AITool> AsAIFunctions() =>
        _tools.Select(t => (AITool)Envolver(t.AsAIFunction())).ToList();

    private AIFunction Envolver(AIFunction function) =>
        _status is null ? function : new StatusTrackingFunction(function, _status);

    public ITool? Find(string name) =>
        _tools.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
}
