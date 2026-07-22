using System.ComponentModel;
using Alina.Core.Tools;
using Microsoft.Extensions.AI;

namespace Alina.Tools;

/// <summary>
/// Tool de leitura de arquivos (read-only, não exige confirmação).
/// Responsabilidade: ler o conteúdo de um arquivo do projeto para análise.
/// </summary>
public sealed class FileReadTool : ToolBase
{
    private const int MaxBytes = 200_000;

    public FileReadTool(IConfirmationService confirmation) : base(confirmation) { }

    public override string Name => "ler_arquivo";

    public override string Description =>
        "Lê e retorna o conteúdo de um arquivo de texto do disco. Use para analisar código-fonte, configs ou documentação.";

    public override AIFunction AsAIFunction() =>
        AIFunctionFactory.Create(ReadAsync, Name, Description);

    [Description("Lê o conteúdo de um arquivo de texto e o retorna.")]
    public async Task<string> ReadAsync(
        [Description("Caminho absoluto ou relativo do arquivo a ser lido.")] string path,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "Erro: caminho do arquivo não informado.";
        }

        if (!File.Exists(path))
        {
            return $"Erro: arquivo não encontrado: {path}";
        }

        FileInfo info = new FileInfo(path);
        if (info.Length > MaxBytes)
        {
            return $"Erro: arquivo muito grande ({info.Length} bytes, limite {MaxBytes}). Leia um trecho menor.";
        }

        string content = await File.ReadAllTextAsync(path, cancellationToken);
        return content;
    }
}
