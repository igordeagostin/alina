using System.ComponentModel;
using System.Text.Encodings.Web;
using System.Text.Json;
using Alina.Core.Ferramentas;
using Alina.Core.Tools;
using Microsoft.Extensions.AI;

namespace Alina.Tools.Ferramentas;

/// <summary>
/// Devolve o JSON completo de uma ferramenta declarativa. Sem isso, editar uma
/// ferramenta em conversa significa reescrever de memória comando, argumentos e
/// parâmetros — e o que a Alina não lembrar some na regravação.
/// </summary>
public sealed class ObterFerramentaTool : ToolBase
{
    private static readonly JsonSerializerOptions OpcoesJson = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly IFerramentaStore _ferramentas;

    public ObterFerramentaTool(IConfirmationService confirmation, IFerramentaStore ferramentas) : base(confirmation)
        => _ferramentas = ferramentas;

    public override string Name => "obter_ferramenta";

    public override string Description =>
        "Lê a definição completa (JSON) de uma ferramenta declarativa: comando, argumentos, parâmetros, " +
        "diretório de trabalho e confirmação. Chame ANTES de alterar uma ferramenta existente e use o JSON " +
        "que voltar como base da chamada a 'criar_ferramenta', preservando os campos que não mudam. " +
        "Só enxerga as ferramentas declarativas — as tools nativas não têm definição editável.";

    public override AIFunction AsAIFunction() => AIFunctionFactory.Create(RunAsync, Name, Description);

    [Description("Lê o JSON de definição de uma ferramenta declarativa pelo nome.")]
    public async Task<string> RunAsync(
        [Description("O nome da ferramenta (snake_case, como aparece em 'listar_ferramentas').")] string nome,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            return "Erro: informe o nome da ferramenta.";
        }

        DefinicaoFerramenta? definicao = await _ferramentas.ObterAsync(nome, cancellationToken);
        if (definicao is null)
        {
            return $"Nenhuma ferramenta declarativa chamada '{nome}' foi encontrada. " +
                "Chame 'listar_ferramentas' para ver as que existem.";
        }

        return JsonSerializer.Serialize(definicao, OpcoesJson);
    }
}
