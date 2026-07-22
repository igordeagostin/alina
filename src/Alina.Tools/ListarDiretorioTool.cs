using System.ComponentModel;
using Alina.Core.IO;
using Alina.Core.Tools;
using Microsoft.Extensions.AI;

namespace Alina.Tools;

/// <summary>
/// Tool de listagem de diretórios (read-only, não exige confirmação).
/// Responsabilidade: descobrir a estrutura de pastas e projetos de um caminho
/// antes de agir, sem precisar recorrer ao terminal.
/// </summary>
public sealed class ListarDiretorioTool : ToolBase
{
    public ListarDiretorioTool(IConfirmationService confirmation) : base(confirmation) { }

    public override string Name => "listar_diretorio";

    public override string Description =>
        "Lista os subdiretórios (e opcionalmente os arquivos) sob um caminho do disco, em árvore " +
        "indentada. Use para descobrir a estrutura de pastas e os projetos disponíveis antes de agir.";

    public override AIFunction AsAIFunction() =>
        AIFunctionFactory.Create(Listar, Name, Description);

    [Description("Lista o conteúdo de um diretório, em árvore indentada.")]
    public string Listar(
        [Description("Caminho absoluto do diretório a listar.")] string path,
        [Description("Profundidade máxima de subpastas a percorrer (1 a 6; padrão 2).")] int profundidade = 2,
        [Description("Se true, inclui também os arquivos; se false, só as pastas.")] bool incluirArquivos = true)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "Erro: caminho do diretório não informado.";
        }

        if (!Directory.Exists(path))
        {
            return $"Erro: diretório não encontrado: {path}";
        }

        int prof = Math.Clamp(profundidade, 1, 6);
        string arvore = ArvoreDiretorios.Montar([path], prof, incluirArquivos);
        return arvore.Length == 0 ? "(diretório vazio)" : arvore;
    }
}
