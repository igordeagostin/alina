using System.ComponentModel;
using Alina.Core.Ferramentas;
using Alina.Core.Tools;
using Microsoft.Extensions.AI;

namespace Alina.Tools.Ferramentas;

/// <summary>Remove uma ferramenta declarativa pelo nome.</summary>
public sealed class EsquecerFerramentaTool : ToolBase
{
    private readonly IFerramentaStore _ferramentas;

    public EsquecerFerramentaTool(IConfirmationService confirmation, IFerramentaStore ferramentas) : base(confirmation)
        => _ferramentas = ferramentas;

    public override string Name => "esquecer_ferramenta";

    public override string Description =>
        "Remove uma ferramenta declarativa pelo nome. Use quando o usuário pedir para apagar/esquecer uma ferramenta.";

    public override AIFunction AsAIFunction() => AIFunctionFactory.Create(RunAsync, Name, Description);

    [Description("Esquece (remove) uma ferramenta declarativa pelo nome.")]
    public async Task<string> RunAsync(
        [Description("O nome da ferramenta a remover.")] string nome,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            return "Erro: informe o nome da ferramenta a esquecer.";
        }

        bool removido = await _ferramentas.RemoverAsync(nome, cancellationToken);
        return removido ? $"Ferramenta '{nome}' removida." : $"Nenhuma ferramenta chamada '{nome}' foi encontrada.";
    }
}
