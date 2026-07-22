using Alina.Core.Ferramentas;
using Alina.Core.Orchestration;
using Microsoft.Extensions.AI;

namespace Alina.Core.Tools;

/// <summary>
/// Registro central das tools disponíveis. Fornece as <see cref="AIFunction"/>
/// para o orquestrador montar o <c>ChatOptions.Tools</c>. Combina as tools
/// estáticas (registradas em C# na composição) com as ferramentas declarativas
/// vivas do <see cref="IFerramentaProvider"/>, resolvidas a cada consulta — é isso
/// que dá o hot-reload: uma ferramenta criada aparece já no turno seguinte.
/// Quando um <see cref="IAssistantStatus"/> está disponível, cada função é envolvida
/// para marcar <see cref="AssistantState.Executing"/> durante a execução.
/// </summary>
public sealed class ToolRegistry
{
    private readonly IReadOnlyList<ITool> _estaticas;
    private readonly IFerramentaProvider? _dinamicas;
    private readonly IAssistantStatus? _status;

    public ToolRegistry(IEnumerable<ITool> tools, IFerramentaProvider? dinamicas = null, IAssistantStatus? status = null)
    {
        _estaticas = tools.ToList();
        _dinamicas = dinamicas;
        _status = status;
    }

    /// <summary>Todas as tools atuais: estáticas + ferramentas declarativas (sem colisão de nome).</summary>
    public IReadOnlyList<ITool> Tools => Combinar();

    /// <summary>Todas as tools como <see cref="AITool"/> para o pipeline de function-calling.</summary>
    public IList<AITool> AsAIFunctions() =>
        Combinar().Select(t => (AITool)Envolver(t.AsAIFunction())).ToList();

    private IReadOnlyList<ITool> Combinar()
    {
        if (_dinamicas is null)
        {
            return _estaticas;
        }

        List<ITool> combinadas = new List<ITool>(_estaticas);
        HashSet<string> nomes = new HashSet<string>(_estaticas.Select(t => t.Name), StringComparer.OrdinalIgnoreCase);

        foreach (ITool ferramenta in _dinamicas.ObterFerramentas())
        {
            if (nomes.Add(ferramenta.Name))
            {
                combinadas.Add(ferramenta);
            }
        }

        return combinadas;
    }

    private AIFunction Envolver(AIFunction function) =>
        _status is null ? function : new StatusTrackingFunction(function, _status);

    public ITool? Find(string name) =>
        Combinar().FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
}
