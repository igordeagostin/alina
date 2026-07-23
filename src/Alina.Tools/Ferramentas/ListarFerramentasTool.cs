using System.ComponentModel;
using System.Text;
using Alina.Core.Ferramentas;
using Alina.Core.Tools;
using Microsoft.Extensions.AI;

namespace Alina.Tools.Ferramentas;

/// <summary>
/// Lista as ferramentas declarativas existentes — o subconjunto editável do que a
/// Alina pode chamar, que o índice do system prompt não distingue das tools nativas.
/// </summary>
public sealed class ListarFerramentasTool : ToolBase
{
    private readonly IFerramentaStore _ferramentas;

    public ListarFerramentasTool(IConfirmationService confirmation, IFerramentaStore ferramentas) : base(confirmation)
        => _ferramentas = ferramentas;

    public override string Name => "listar_ferramentas";

    public override string Description =>
        "Lista as ferramentas declarativas existentes (nome, descrição e se pedem confirmação) — são exatamente " +
        "as que você pode alterar com 'criar_ferramenta' ou apagar com 'esquecer_ferramenta'. Chame antes de " +
        "criar uma ferramenta nova, para não duplicar nem substituir sem querer uma que já existe.";

    public override AIFunction AsAIFunction() => AIFunctionFactory.Create(RunAsync, Name, Description);

    [Description("Lista as ferramentas declarativas existentes.")]
    public async Task<string> RunAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<FerramentaResumo> resumos = await _ferramentas.ListarAsync(cancellationToken);
        if (resumos.Count == 0)
        {
            return "Nenhuma ferramenta declarativa cadastrada.";
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("Ferramentas declarativas (editáveis):");
        foreach (FerramentaResumo resumo in resumos)
        {
            string confirmacao = resumo.ExigeConfirmacao ? "pede confirmação" : "roda sem confirmação";
            sb.AppendLine($"- {resumo.Nome} — {resumo.Descricao} [{confirmacao}]");
        }

        return sb.ToString().TrimEnd();
    }
}
