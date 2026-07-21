using Alina.Mcp;
using Alina.Tools.ClaudeCode;

namespace Alina.Tests;

/// <summary>
/// Testes que executam o Claude Code CLI de verdade. Custam dinheiro e dependem
/// do ambiente, por isso ficam marcados com Skip. Remova o Skip para validar
/// manualmente a integração ponta a ponta.
/// </summary>
public sealed class ClaudeCodeIntegrationTests
{
    [Fact(Skip = "Integração real: executa o Claude Code (custa dinheiro). Rodar manualmente.")]
    public async Task Cria_arquivo_em_diretorio_temporario()
    {
        var dir = Path.Combine(Path.GetTempPath(), "alina-cc-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(dir);

        try
        {
            var tool = new ClaudeCodeTool(
                new FakeConfirmationService(result: true),
                new ClaudeCodeOptions { PermissionMode = "acceptEdits", MaxTurns = 5 });

            var result = await tool.RunAsync(
                "Crie um arquivo chamado hello.txt com exatamente o conteúdo: ola alina",
                dir);

            var file = Path.Combine(dir, "hello.txt");
            Assert.True(File.Exists(file), $"esperava hello.txt criado. Saída:\n{result}");
            Assert.Contains("ola alina", await File.ReadAllTextAsync(file), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact(Skip = "Integração real: streaming + permissão interativa via servidor MCP. Rodar manualmente.")]
    public async Task Permissao_interativa_pergunta_e_prossegue_com_streaming()
    {
        var dir = Path.Combine(Path.GetTempPath(), "alina-cc-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(dir);

        // Aprova automaticamente cada pedido de permissão que chegar ao servidor MCP.
        await using var servidor = new ServidorPermissaoMcp(new FakeConfirmationService(result: true));

        try
        {
            var tool = new ClaudeCodeTool(
                new FakeConfirmationService(result: true),
                new ClaudeCodeOptions { Streaming = true, PermissaoInterativa = true, MaxTurns = 8 },
                servidor);

            var eventos = new List<EventoProgressoClaudeCode>();
            tool.Progresso += eventos.Add;

            var result = await tool.RunAsync(
                "Crie um arquivo chamado hello.txt com exatamente o conteúdo: ola alina",
                dir);

            var file = Path.Combine(dir, "hello.txt");
            Assert.True(File.Exists(file), $"esperava hello.txt criado. Saída:\n{result}");
            Assert.NotEmpty(eventos);
            Assert.Contains(eventos, e => e.Tipo == TipoEventoClaudeCode.Ferramenta);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }
}
