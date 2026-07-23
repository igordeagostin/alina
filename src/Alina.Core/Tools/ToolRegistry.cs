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
    private readonly IReadOnlySet<string> _ocultas;

    public ToolRegistry(IEnumerable<ITool> tools, IFerramentaProvider? dinamicas = null, IAssistantStatus? status = null)
        : this(tools.ToList(), dinamicas, status, new HashSet<string>(StringComparer.OrdinalIgnoreCase))
    {
    }

    private ToolRegistry(
        IReadOnlyList<ITool> estaticas,
        IFerramentaProvider? dinamicas,
        IAssistantStatus? status,
        IReadOnlySet<string> ocultas)
    {
        _estaticas = estaticas;
        _dinamicas = dinamicas;
        _status = status;
        _ocultas = ocultas;
    }

    /// <summary>Todas as tools atuais: estáticas + ferramentas declarativas (sem colisão de nome).</summary>
    public IReadOnlyList<ITool> Tools => Combinar();

    /// <summary>
    /// Uma visão deste registro sem as ferramentas informadas, compartilhando as mesmas
    /// instâncias. É o que impede uma tarefa paralela de abrir outra tarefa paralela em
    /// cascata sem fim.
    /// </summary>
    public ToolRegistry SemFerramentas(params string[] nomes) =>
        new(_estaticas, _dinamicas, _status, new HashSet<string>(_ocultas.Concat(nomes), StringComparer.OrdinalIgnoreCase));

    /// <summary>Todas as tools como <see cref="AITool"/> para o pipeline de function-calling.</summary>
    public IList<AITool> AsAIFunctions() =>
        Combinar().Select(t => (AITool)Envolver(t.AsAIFunction())).ToList();

    private IReadOnlyList<ITool> Combinar()
    {
        List<ITool> combinadas = _estaticas.Where(t => !_ocultas.Contains(t.Name)).ToList();

        if (_dinamicas is null)
        {
            return combinadas;
        }

        HashSet<string> nomes = new HashSet<string>(combinadas.Select(t => t.Name), StringComparer.OrdinalIgnoreCase);

        foreach (ITool ferramenta in _dinamicas.ObterFerramentas())
        {
            if (!_ocultas.Contains(ferramenta.Name) && nomes.Add(ferramenta.Name))
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
