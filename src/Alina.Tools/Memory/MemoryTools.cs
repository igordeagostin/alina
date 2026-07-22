using System.ComponentModel;
using System.Text;
using Alina.Core.Memory;
using Alina.Core.Tools;
using Microsoft.Extensions.AI;

namespace Alina.Tools.Memory;

/// <summary>Salva um fato/preferência na memória permanente da Alina.</summary>
public sealed class RememberTool : ToolBase
{
    private readonly IMemoryStore _memory;

    public RememberTool(IConfirmationService confirmation, IMemoryStore memory) : base(confirmation)
        => _memory = memory;

    public override string Name => "lembrar";

    public override string Description =>
        "Salva um fato ou preferência importante do usuário na memória permanente (persiste entre conversas). " +
        "Use quando o usuário compartilhar preferências, convenções, decisões ou informações duradouras.";

    public override AIFunction AsAIFunction() => AIFunctionFactory.Create(RunAsync, Name, Description);

    [Description("Memoriza um fato/preferência de forma permanente.")]
    public async Task<string> RunAsync(
        [Description("O fato ou preferência a memorizar. Seja conciso e objetivo.")] string content,
        [Description("Categoria opcional (ex: preferência, convenção, projeto).")] string? category = null,
        [Description("Palavras-chave separadas por vírgula, para facilitar recuperação futura (opcional).")] string? keywords = null,
        [Description("Se true, é uma memória essencial que deve estar sempre no contexto (fixada). Use com parcimônia.")] bool pinned = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return "Erro: nada para memorizar (conteúdo vazio).";
        }

        MemoryItem item = new MemoryItem
        {
            Kind = MemoryKind.Fact,
            Content = content,
            Category = category,
            Keywords = MemoryToolHelpers.ParseKeywords(keywords),
            Pinned = pinned,
        };

        await _memory.AddAsync(item, cancellationToken);
        string pin = pinned ? " (fixada)" : string.Empty;
        return $"Memorizado [{item.Id}]{pin}: {item.Content}";
    }
}

/// <summary>Salva um procedimento (passos para repetir um comando sem reexplorar).</summary>
public sealed class RememberProcedureTool : ToolBase
{
    private readonly IMemoryStore _memory;

    public RememberProcedureTool(IConfirmationService confirmation, IMemoryStore memory) : base(confirmation)
        => _memory = memory;

    public override string Name => "memorizar_procedimento";

    public override string Description =>
        "Salva um procedimento resolvido (nome + passos) para repetir um comando no futuro sem reexplorar. " +
        "Use após descobrir COMO fazer algo repetível (ex: como fazer deploy do projeto X, como rodar os testes).";

    public override AIFunction AsAIFunction() => AIFunctionFactory.Create(RunAsync, Name, Description);

    [Description("Memoriza um procedimento repetível de forma permanente.")]
    public async Task<string> RunAsync(
        [Description("Nome/gatilho curto do procedimento (ex: 'deploy do projeto X').")] string name,
        [Description("Os passos do procedimento, em ordem.")] string steps,
        [Description("Palavras-chave/gatilhos separados por vírgula que devem disparar este procedimento (opcional).")] string? triggers = null,
        [Description("Categoria opcional (ex: deploy, testes, git).")] string? category = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(steps))
        {
            return "Erro: informe o nome e os passos do procedimento.";
        }

        MemoryItem item = new MemoryItem
        {
            Kind = MemoryKind.Procedure,
            Title = name.Trim(),
            Content = steps,
            Category = category,
            Keywords = MemoryToolHelpers.ParseKeywords(triggers),
        };

        await _memory.AddAsync(item, cancellationToken);
        return $"Procedimento memorizado [{item.Id}]: {item.DisplayTitle()}";
    }
}

/// <summary>Recupera o conteúdo completo de memórias sob demanda (por consulta, id ou categoria).</summary>
public sealed class RetrieveMemoryTool : ToolBase
{
    private readonly IMemoryRetriever _retriever;

    public RetrieveMemoryTool(IConfirmationService confirmation, IMemoryRetriever retriever) : base(confirmation)
        => _retriever = retriever;

    public override string Name => "recuperar_memoria";

    public override string Description =>
        "Recupera o conteúdo completo de memórias que aparecem no índice mas não estão detalhadas no contexto. " +
        "Passe uma consulta (busca semântica) OU ids específicos separados por vírgula.";

    public override AIFunction AsAIFunction() => AIFunctionFactory.Create(RunAsync, Name, Description);

    [Description("Busca o conteúdo completo de memórias relevantes.")]
    public async Task<string> RunAsync(
        [Description("Consulta em linguagem natural para busca semântica (opcional se passar ids).")] string? query = null,
        [Description("Ids específicos separados por vírgula (opcional se passar query).")] string? ids = null,
        [Description("Quantas memórias retornar na busca por consulta (padrão 5).")] int topK = 5,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<MemoryItem> items;
        if (!string.IsNullOrWhiteSpace(ids))
        {
            List<string> idList = MemoryToolHelpers.ParseKeywords(ids);
            items = await _retriever.GetByIdsAsync(idList, cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(query))
        {
            items = await _retriever.SearchAsync(query, topK <= 0 ? 5 : topK, cancellationToken);
        }
        else
        {
            return "Erro: informe uma consulta ou ids para recuperar.";
        }

        if (items.Count == 0)
        {
            return "Nenhuma memória encontrada.";
        }

        StringBuilder sb = new StringBuilder();
        foreach (MemoryItem item in items)
        {
            string kind = item.Kind == MemoryKind.Procedure ? "procedimento" : "fato";
            string category = string.IsNullOrWhiteSpace(item.Category) ? string.Empty : $" ({item.Category})";
            sb.AppendLine($"[{item.Id}] {kind}{category} — {item.DisplayTitle()}");
            sb.AppendLine(item.Content);
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }
}

internal static class MemoryToolHelpers
{
    public static List<string> ParseKeywords(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
        {
            return new List<string>();
        }

        return csv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }
}

/// <summary>Lista os fatos/preferências já memorizados.</summary>
public sealed class RecallTool : ToolBase
{
    private readonly IMemoryStore _memory;

    public RecallTool(IConfirmationService confirmation, IMemoryStore memory) : base(confirmation)
        => _memory = memory;

    public override string Name => "listar_memorias";

    public override string Description =>
        "Lista tudo o que já foi memorizado sobre o usuário/projetos, com seus ids (para eventual remoção).";

    public override AIFunction AsAIFunction() => AIFunctionFactory.Create(RunAsync, Name, Description);

    [Description("Retorna a lista de memórias salvas.")]
    public async Task<string> RunAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<MemoryItem> items = await _memory.GetAllAsync(cancellationToken);
        if (items.Count == 0)
        {
            return "Nenhuma memória salva ainda.";
        }

        StringBuilder sb = new StringBuilder();
        foreach (MemoryItem item in items)
        {
            string category = string.IsNullOrWhiteSpace(item.Category) ? string.Empty : $" ({item.Category})";
            string kind = item.Kind == MemoryKind.Procedure ? "⚙ " : string.Empty;
            string pin = item.Pinned ? "📌 " : string.Empty;
            sb.AppendLine($"[{item.Id}] {pin}{kind}{category} {item.DisplayTitle()}");
        }

        return sb.ToString().TrimEnd();
    }
}

/// <summary>Remove um fato/preferência da memória permanente.</summary>
public sealed class ForgetTool : ToolBase
{
    private readonly IMemoryStore _memory;

    public ForgetTool(IConfirmationService confirmation, IMemoryStore memory) : base(confirmation)
        => _memory = memory;

    public override string Name => "esquecer";

    public override string Description =>
        "Remove um item da memória permanente pelo seu id (obtido em listar_memorias ou no contexto).";

    public override AIFunction AsAIFunction() => AIFunctionFactory.Create(RunAsync, Name, Description);

    [Description("Esquece (remove) uma memória pelo id.")]
    public async Task<string> RunAsync(
        [Description("O id da memória a remover.")] string id,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return "Erro: informe o id da memória a esquecer.";
        }

        bool removed = await _memory.RemoveAsync(id, cancellationToken);
        return removed ? $"Memória [{id}] esquecida." : $"Nenhuma memória com id [{id}] foi encontrada.";
    }
}
