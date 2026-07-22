using System.ComponentModel;
using System.Text;
using Alina.Core.Habilidades;
using Alina.Core.Tools;
using Microsoft.Extensions.AI;

namespace Alina.Tools.Habilidades;

/// <summary>Ensina uma nova habilidade à Alina, gravando-a como arquivo Markdown.</summary>
public sealed class AprenderHabilidadeTool : ToolBase
{
    private readonly IHabilidadeStore _habilidades;

    public AprenderHabilidadeTool(IConfirmationService confirmation, IHabilidadeStore habilidades) : base(confirmation)
        => _habilidades = habilidades;

    public override string Name => "aprender_habilidade";

    public override string Description =>
        "Ensina uma nova habilidade à Alina, persistindo-a como um documento permanente. " +
        "Use quando o usuário pedir para você APRENDER uma habilidade, um jeito de fazer algo ou um " +
        "conhecimento nomeado e reutilizável. Se já existir uma habilidade com o mesmo nome, o usuário " +
        "será consultado antes de sobrescrever.";

    public override AIFunction AsAIFunction() => AIFunctionFactory.Create(RunAsync, Name, Description);

    [Description("Aprende (cria ou atualiza) uma habilidade permanente.")]
    public async Task<string> RunAsync(
        [Description("Título curto da habilidade (ex: 'Deploy da API do Diário').")] string titulo,
        [Description("Uma linha descrevendo a habilidade; aparece no índice do contexto.")] string descricao,
        [Description("As instruções completas da habilidade, em Markdown.")] string conteudo,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(titulo) || string.IsNullOrWhiteSpace(conteudo))
        {
            return "Erro: informe o título e o conteúdo da habilidade.";
        }

        Habilidade habilidade = new Habilidade
        {
            Nome = titulo,
            Descricao = descricao?.Trim() ?? string.Empty,
            Conteudo = conteudo,
        };

        if (await _habilidades.ExisteAsync(titulo, cancellationToken))
        {
            bool confirmado = await EnsureConfirmedAsync(
                "Sobrescrever habilidade existente",
                $"Já existe uma habilidade parecida com \"{titulo}\". Substituir?",
                cancellationToken);
            if (!confirmado)
            {
                return "Operação cancelada — a habilidade existente foi mantida.";
            }
        }

        await _habilidades.SalvarAsync(habilidade, cancellationToken);
        return $"Habilidade aprendida: {habilidade.Nome} — {habilidade.Descricao}";
    }

    public override bool RequiresConfirmation => false;
}

/// <summary>Carrega o conteúdo completo de uma habilidade aprendida, para aplicá-la.</summary>
public sealed class UsarHabilidadeTool : ToolBase
{
    private readonly IHabilidadeStore _habilidades;

    public UsarHabilidadeTool(IConfirmationService confirmation, IHabilidadeStore habilidades) : base(confirmation)
        => _habilidades = habilidades;

    public override string Name => "usar_habilidade";

    public override string Description =>
        "Carrega as instruções completas de uma habilidade aprendida (pelo nome que aparece no índice de " +
        "habilidades). Chame antes de executar uma tarefa coberta por uma habilidade.";

    public override AIFunction AsAIFunction() => AIFunctionFactory.Create(RunAsync, Name, Description);

    [Description("Carrega o conteúdo completo de uma habilidade pelo nome.")]
    public async Task<string> RunAsync(
        [Description("O nome da habilidade (como aparece no índice de habilidades).")] string nome,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            return "Erro: informe o nome da habilidade.";
        }

        Habilidade? habilidade = await _habilidades.ObterAsync(nome, cancellationToken);
        if (habilidade is null)
        {
            return $"Nenhuma habilidade chamada \"{nome}\" foi encontrada.";
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"Habilidade: {habilidade.Nome} — {habilidade.Descricao}");
        sb.AppendLine();
        sb.AppendLine(habilidade.Conteudo);
        return sb.ToString().TrimEnd();
    }
}

/// <summary>Remove uma habilidade aprendida.</summary>
public sealed class EsquecerHabilidadeTool : ToolBase
{
    private readonly IHabilidadeStore _habilidades;

    public EsquecerHabilidadeTool(IConfirmationService confirmation, IHabilidadeStore habilidades) : base(confirmation)
        => _habilidades = habilidades;

    public override string Name => "esquecer_habilidade";

    public override string Description =>
        "Remove uma habilidade aprendida pelo nome. Use quando o usuário pedir para esquecer/apagar uma habilidade.";

    public override AIFunction AsAIFunction() => AIFunctionFactory.Create(RunAsync, Name, Description);

    [Description("Esquece (remove) uma habilidade pelo nome.")]
    public async Task<string> RunAsync(
        [Description("O nome da habilidade a remover.")] string nome,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            return "Erro: informe o nome da habilidade a esquecer.";
        }

        bool removido = await _habilidades.RemoverAsync(nome, cancellationToken);
        return removido ? $"Habilidade \"{nome}\" esquecida." : $"Nenhuma habilidade chamada \"{nome}\" foi encontrada.";
    }
}
