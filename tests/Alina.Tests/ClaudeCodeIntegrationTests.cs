using Alina.Core.Permissoes;
using Alina.Infrastructure.Permissoes;
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
        string dir = Path.Combine(Path.GetTempPath(), "alina-cc-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(dir);

        try
        {
            ClaudeCodeTool tool = new ClaudeCodeTool(
                new FakeConfirmationService(result: true),
                new ClaudeCodeOptions { PermissionMode = "acceptEdits", MaxTurns = 5 });

            string result = await tool.RunAsync(
                "Crie um arquivo chamado hello.txt com exatamente o conteúdo: ola alina",
                dir);

            string file = Path.Combine(dir, "hello.txt");
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
        string dir = Path.Combine(Path.GetTempPath(), "alina-cc-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(dir);

        // Aprova automaticamente cada pedido de permissão que chegar ao servidor MCP.
        ContextoPermissao contexto = new ContextoPermissao();
        PoliticaPermissao politica = new PoliticaPermissao(Path.Combine(dir, "permissoes.json"));
        ConfirmacaoPermissaoBasica confirmacao = new ConfirmacaoPermissaoBasica(new FakeConfirmationService(result: true));
        await using ServidorPermissaoMcp servidor = new ServidorPermissaoMcp(politica, confirmacao, contexto);

        try
        {
            ClaudeCodeTool tool = new ClaudeCodeTool(
                new FakeConfirmationService(result: true),
                new ClaudeCodeOptions { Streaming = true, PermissaoInterativa = true, MaxTurns = 8 },
                servidor,
                contexto);

            List<EventoProgressoClaudeCode> eventos = new List<EventoProgressoClaudeCode>();
            tool.Progresso += eventos.Add;

            string result = await tool.RunAsync(
                "Crie um arquivo chamado hello.txt com exatamente o conteúdo: ola alina",
                dir);

            string file = Path.Combine(dir, "hello.txt");
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
