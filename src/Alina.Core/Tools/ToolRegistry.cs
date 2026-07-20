using Microsoft.Extensions.AI;

namespace Alina.Core.Tools;

/// <summary>
/// Registro central das tools disponíveis. Fornece as <see cref="AIFunction"/>
/// para o orquestrador montar o <c>ChatOptions.Tools</c>.
/// </summary>
public sealed class ToolRegistry
{
    private readonly IReadOnlyList<ITool> _tools;

    public ToolRegistry(IEnumerable<ITool> tools) => _tools = tools.ToList();

    public IReadOnlyList<ITool> Tools => _tools;

    /// <summary>Todas as tools como <see cref="AITool"/> para o pipeline de function-calling.</summary>
    public IList<AITool> AsAIFunctions() => _tools.Select(t => (AITool)t.AsAIFunction()).ToList();

    public ITool? Find(string name) =>
        _tools.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
}
