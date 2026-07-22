using System.ComponentModel;
using System.Text;
using Alina.Core.IO;
using Alina.Core.Permissoes;
using Alina.Core.Tools;
using Microsoft.Extensions.AI;

namespace Alina.Tools;

/// <summary>
/// Tool de busca de projeto pelo nome (read-only, não exige confirmação).
/// Responsabilidade: traduzir o nome que o usuário fala ("o diário API") no caminho
/// absoluto real, para nenhuma ferramenta receber caminho inventado.
/// </summary>
public sealed class LocalizarProjetoTool : ToolBase
{
    private readonly IPoliticaPermissao _politica;

    public LocalizarProjetoTool(IConfirmationService confirmation, IPoliticaPermissao politica)
        : base(confirmation) => _politica = politica;

    public override string Name => "localizar_projeto";

    public override string Description =>
        "Encontra o caminho absoluto de um projeto pelo nome, procurando nas pastas de projeto confiáveis " +
        "do usuário (tolera acento, espaço e hífen). Use SEMPRE que o usuário citar um projeto pelo nome e " +
        "você precisar do caminho — nunca invente nem deduza o caminho por conta própria.";

    public override AIFunction AsAIFunction() =>
        AIFunctionFactory.Create(Localizar, Name, Description);

    [Description("Procura pastas de projeto cujo nome case com o termo informado.")]
    public string Localizar(
        [Description("Nome ou parte do nome do projeto, como o usuário falou (ex.: \"diário api\").")] string nome,
        [Description("Profundidade máxima de subpastas a percorrer a partir de cada raiz (1 a 6; padrão 4).")] int profundidade = 4)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            return "Erro: nome do projeto não informado.";
        }

        IReadOnlyList<string> raizes = _politica.Opcoes.DiretoriosConfiaveis;
        if (raizes.Count == 0)
        {
            return "Nenhuma pasta de projeto confiável está configurada. Peça ao usuário o caminho absoluto do " +
                "projeto, ou que ele cadastre as pastas em Configurações → Permissões → diretórios confiáveis.";
        }

        IReadOnlyList<ProjetoEncontrado> candidatos =
            BuscaProjetos.Localizar(raizes, nome, Math.Clamp(profundidade, 1, 6));

        if (candidatos.Count == 0)
        {
            return $"Nenhum projeto encontrado para \"{nome}\" em: {string.Join("; ", raizes)}. " +
                "Use 'listar_diretorio' para inspecionar as pastas ou peça o caminho ao usuário.";
        }

        StringBuilder sb = new StringBuilder();
        sb.Append(candidatos.Count == 1
            ? "1 projeto encontrado:"
            : $"{candidatos.Count} projetos encontrados (o primeiro é o mais provável; se houver dúvida real, pergunte ao usuário):");

        foreach (ProjetoEncontrado projeto in candidatos)
        {
            sb.Append('\n').Append("- ").Append(projeto.Nome).Append(" — ").Append(projeto.Caminho);
            if (projeto.Marcador is not null)
            {
                sb.Append(" (contém ").Append(projeto.Marcador).Append(')');
            }
        }

        return sb.ToString();
    }
}
