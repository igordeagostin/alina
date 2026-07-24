using System.ComponentModel;
using System.Text;
using Alina.Core.Models;
using Alina.Core.Orchestration;
using Alina.Core.Tools;
using Microsoft.Extensions.AI;

namespace Alina.Tools.Background;

/// <summary>
/// Dispara um pedido para rodar em paralelo à conversa: retorna um id na hora e a
/// tarefa segue num orquestrador isolado, com as mesmas ferramentas. É o que permite
/// atender vários pedidos de uma vez sem travar o diálogo — a Alina responde na hora
/// e avisa o resultado de cada tarefa quando ele chega. A tarefa recebe junto um
/// recorte do fim da conversa: contexto implícito ("aquele arquivo", "o projeto que
/// abrimos") não se perde na tradução do pedido.
/// </summary>
public sealed class TarefaParalelaTool : ToolBase
{
    /// <summary>Quantas mensagens finais da conversa acompanham o pedido, como referência.</summary>
    private const int MensagensDeContexto = 10;

    /// <summary>Teto por mensagem ao montar o recorte, para o contexto não engolir o pedido.</summary>
    private const int CaracteresPorMensagem = 300;

    private readonly IBackgroundTaskManager _gerenciador;
    private readonly IFabricaOrquestrador _fabrica;
    private readonly Func<IOrchestrator?>? _conversaPrincipal;

    public TarefaParalelaTool(
        IConfirmationService confirmation,
        IBackgroundTaskManager gerenciador,
        IFabricaOrquestrador fabrica,
        Func<IOrchestrator?>? conversaPrincipal = null)
        : base(confirmation)
    {
        _gerenciador = gerenciador;
        _fabrica = fabrica;
        _conversaPrincipal = conversaPrincipal;
    }

    public override string Name => NomesFerramentas.ExecutarEmParalelo;

    public override string Description =>
        "Executa um pedido EM PARALELO à conversa e devolve um id imediatamente, sem bloquear o diálogo. " +
        "A tarefa roda com todas as suas ferramentas e você é avisada com o resultado assim que ela terminar. " +
        "Use sempre que atender o pedido levar mais que poucos segundos, e dispare UMA tarefa para CADA " +
        "item quando o usuário pedir várias coisas de uma vez — elas rodam simultaneamente. " +
        "Depois de chamar, comente em uma frase curta o que colocou para rodar e siga a conversa normalmente.";

    public override AIFunction AsAIFunction() => AIFunctionFactory.Create(RunAsync, Name, Description);

    [Description("Inicia um pedido em paralelo e retorna o id na hora.")]
    public Task<string> RunAsync(
        [Description("O pedido completo a executar, em linguagem natural, com todo o contexto necessário — " +
                     "quem executa não enxerga a conversa.")] string pedido,
        [Description("Rótulo curto da tarefa, para você e o usuário se referirem a ela.")] string rotulo,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pedido))
        {
            return Task.FromResult("Erro: nenhum pedido informado.");
        }

        string descricao = string.IsNullOrWhiteSpace(rotulo)
            ? (pedido.Length > 60 ? pedido[..60] + "…" : pedido)
            : rotulo.Trim();

        string pedidoCompleto = MontarPedidoComContexto(pedido);

        BackgroundTask tarefa = _gerenciador.Start(
            descricao, ct => _fabrica.CriarIsolado().SendAsync(pedidoCompleto, ct));

        return Task.FromResult(
            $"Tarefa [{tarefa.Id}] \"{descricao}\" rodando em paralelo. Siga a conversa normalmente: " +
            "o resultado chega até você sozinho quando terminar.");
    }

    private string MontarPedidoComContexto(string pedido)
    {
        string contexto = RecortarConversa();
        if (contexto.Length == 0)
        {
            return pedido;
        }

        return "Contexto recente da conversa com o usuário (apenas referência, não são novos pedidos):\n" +
               contexto +
               "\n\nSeu pedido, a executar agora:\n" + pedido;
    }

    private string RecortarConversa()
    {
        Conversation? conversa = _conversaPrincipal?.Invoke()?.Current;
        if (conversa is null || conversa.Messages.Count == 0)
        {
            return string.Empty;
        }

        StringBuilder sb = new StringBuilder();
        int inicio = Math.Max(0, conversa.Messages.Count - MensagensDeContexto);

        for (int i = inicio; i < conversa.Messages.Count; i++)
        {
            ChatMessage mensagem = conversa.Messages[i];
            if (mensagem.Role != ChatRole.User && mensagem.Role != ChatRole.Assistant)
            {
                continue;
            }

            string texto = mensagem.Text.Trim();
            if (texto.Length == 0)
            {
                continue;
            }

            if (texto.Length > CaracteresPorMensagem)
            {
                texto = texto[..CaracteresPorMensagem] + "…";
            }

            sb.Append(mensagem.Role == ChatRole.User ? "Usuário: " : "Alina: ").AppendLine(texto);
        }

        return sb.ToString().TrimEnd();
    }
}
