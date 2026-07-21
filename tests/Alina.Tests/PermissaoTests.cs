using System.Text.Json;
using Alina.Mcp;
using Alina.Tools.ClaudeCode;

namespace Alina.Tests;

public sealed class PermissaoTests
{
    private static JsonElement Json(string texto) => JsonDocument.Parse(texto).RootElement;

    [Fact]
    public void DescreverPedido_usa_campo_relevante_do_input()
    {
        var pedido = PermissaoPayload.DescreverPedido("Bash", Json("{\"command\":\"git push origin main\"}"));

        Assert.Contains("Bash", pedido);
        Assert.Contains("git push origin main", pedido);
    }

    [Fact]
    public void DescreverPedido_sem_campos_conhecidos_ainda_cita_a_ferramenta()
    {
        var pedido = PermissaoPayload.DescreverPedido("Edit", Json("{\"foo\":1}"));
        Assert.Contains("Edit", pedido);
    }

    [Fact]
    public void RespostaPermitir_gera_behavior_allow_com_updatedInput()
    {
        var resposta = PermissaoPayload.RespostaPermitir(Json("{\"file_path\":\"a.cs\"}"));

        using var doc = JsonDocument.Parse(resposta);
        Assert.Equal("allow", doc.RootElement.GetProperty("behavior").GetString());
        Assert.Equal("a.cs", doc.RootElement.GetProperty("updatedInput").GetProperty("file_path").GetString());
    }

    [Fact]
    public void RespostaNegar_gera_behavior_deny_com_mensagem()
    {
        var resposta = PermissaoPayload.RespostaNegar("O usuário não autorizou.");

        using var doc = JsonDocument.Parse(resposta);
        Assert.Equal("deny", doc.RootElement.GetProperty("behavior").GetString());
        Assert.Contains("autoriz", doc.RootElement.GetProperty("message").GetString()!);
    }

    [Fact]
    public void MontarMcpConfig_gera_servidor_http_com_url()
    {
        var config = ClaudeCodeTool.MontarMcpConfig(
            new ClaudeCodeTool.ConfigPermissao("http://127.0.0.1:5123/mcp", "permissoes", "mcp__permissoes__aprovar"));

        using var doc = JsonDocument.Parse(config);
        var servidor = doc.RootElement.GetProperty("mcpServers").GetProperty("permissoes");
        Assert.Equal("http", servidor.GetProperty("type").GetString());
        Assert.Equal("http://127.0.0.1:5123/mcp", servidor.GetProperty("url").GetString());
    }

    [Fact]
    public async Task ServidorPermissao_inicia_em_localhost_e_expoe_url_mcp()
    {
        await using var servidor = new ServidorPermissaoMcp(new FakeConfirmationService(result: true), porta: 0);

        var url = await servidor.IniciarAsync();

        Assert.True(servidor.Ativo);
        Assert.StartsWith("http://127.0.0.1:", url);
        Assert.EndsWith("/mcp", url);
        Assert.Equal("mcp__permissoes__aprovar", servidor.NomeFerramenta);

        // Idempotente: chamar de novo devolve a mesma URL sem subir outro servidor.
        Assert.Equal(url, await servidor.IniciarAsync());
    }
}
