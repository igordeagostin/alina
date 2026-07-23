namespace Alina.Core.Orchestration;

/// <summary>
/// Cria orquestradores isolados para as tarefas que rodam em paralelo à conversa.
/// Cada um tem a própria memória de trabalho e não escreve no histórico da conversa
/// principal — é o que permite a Alina tocar várias frentes ao mesmo tempo sem que
/// uma embaralhe as mensagens da outra.
/// </summary>
public interface IFabricaOrquestrador
{
    IOrchestrator CriarIsolado();
}

/// <summary>Fábrica que delega a criação a uma função montada na composição.</summary>
public sealed class FabricaOrquestrador : IFabricaOrquestrador
{
    private readonly Func<IOrchestrator> _criar;

    public FabricaOrquestrador(Func<IOrchestrator> criar) => _criar = criar;

    public IOrchestrator CriarIsolado() => _criar();
}
