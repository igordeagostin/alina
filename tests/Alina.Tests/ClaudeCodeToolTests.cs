using Alina.Tools.ClaudeCode;

namespace Alina.Tests;

public sealed class ClaudeCodeToolTests
{
    // Amostra real da saída de `claude -p --output-format json`.
    private const string SuccessJson =
        "{\"type\":\"result\",\"subtype\":\"success\",\"is_error\":false,\"result\":\"Criei o arquivo login.cs\"," +
        "\"num_turns\":3,\"duration_ms\":4200,\"session_id\":\"abc\",\"total_cost_usd\":0.0123,\"permission_denials\":[]}";

    [Fact]
    public void FormatResult_extrai_texto_e_rodape_de_sucesso()
    {
        var formatted = ClaudeCodeTool.FormatResult(SuccessJson, stderr: "", exitCode: 0);

        Assert.Contains("Criei o arquivo login.cs", formatted);
        Assert.Contains("3 turno(s)", formatted);
        Assert.Contains("$0.0123", formatted);
        Assert.DoesNotContain("erro", formatted, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FormatResult_sinaliza_erro_do_claude_code()
    {
        const string errorJson =
            "{\"type\":\"result\",\"subtype\":\"error_max_turns\",\"is_error\":true,\"result\":\"limite atingido\"}";

        var formatted = ClaudeCodeTool.FormatResult(errorJson, stderr: "", exitCode: 1);

        Assert.Contains("erro", formatted, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("limite atingido", formatted);
    }

    [Fact]
    public void FormatResult_sinaliza_permission_denials()
    {
        const string deniedJson =
            "{\"type\":\"result\",\"subtype\":\"success\",\"is_error\":false,\"result\":\"parcial\"," +
            "\"permission_denials\":[{\"tool\":\"Bash\"}]}";

        var formatted = ClaudeCodeTool.FormatResult(deniedJson, stderr: "", exitCode: 0);

        Assert.Contains("bloqueada", formatted, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FormatResult_devolve_bruto_quando_json_invalido()
    {
        var formatted = ClaudeCodeTool.FormatResult("saída não-json qualquer", stderr: "", exitCode: 2);

        Assert.Contains("saída não-json qualquer", formatted);
        Assert.Contains("exit 2", formatted);
    }

    [Fact]
    public async Task RunAsync_nao_executa_quando_confirmacao_negada()
    {
        var confirmation = new FakeConfirmationService(result: false);
        var tool = new ClaudeCodeTool(confirmation, new ClaudeCodeOptions());

        var result = await tool.RunAsync("qualquer tarefa");

        Assert.Equal(1, confirmation.Calls);
        Assert.Contains("cancelada", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_valida_tarefa_vazia()
    {
        var confirmation = new FakeConfirmationService(result: true);
        var tool = new ClaudeCodeTool(confirmation, new ClaudeCodeOptions());

        var result = await tool.RunAsync("   ");

        Assert.Equal(0, confirmation.Calls);
        Assert.Contains("nenhuma tarefa", result, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Please run /login to authenticate")]
    [InlineData("Invalid API key provided")]
    [InlineData("oauth token expired")]
    public void DiagnoseHint_detecta_problema_de_autenticacao(string texto)
    {
        var hint = ClaudeCodeTool.DiagnoseHint(texto);
        Assert.NotNull(hint);
        Assert.Contains("autentica", hint!, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("usage limit reached")]
    [InlineData("Error 429: rate limit")]
    public void DiagnoseHint_detecta_limite_de_uso(string texto)
    {
        var hint = ClaudeCodeTool.DiagnoseHint(texto);
        Assert.NotNull(hint);
        Assert.Contains("limite", hint!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DiagnoseHint_retorna_null_para_texto_neutro()
        => Assert.Null(ClaudeCodeTool.DiagnoseHint("tudo certo, arquivo criado"));

    [Fact]
    public void Tool_exige_confirmacao_e_tem_nome_esperado()
    {
        var tool = new ClaudeCodeTool(new FakeConfirmationService(result: true), new ClaudeCodeOptions());

        Assert.True(tool.RequiresConfirmation);
        Assert.Equal("delegar_claude_code", tool.Name);
    }

    // Amostra de linhas de `claude -p --output-format stream-json --verbose`.
    private static readonly string[] StreamLinhas =
    [
        "{\"type\":\"system\",\"subtype\":\"init\",\"session_id\":\"abc\",\"model\":\"claude\"}",
        "{\"type\":\"assistant\",\"message\":{\"content\":[{\"type\":\"text\",\"text\":\"Vou criar o arquivo.\"}]}}",
        "{\"type\":\"assistant\",\"message\":{\"content\":[{\"type\":\"tool_use\",\"name\":\"Write\",\"input\":{\"file_path\":\"login.cs\"}}]}}",
        "{\"type\":\"user\",\"message\":{\"content\":[{\"type\":\"tool_result\",\"content\":\"File created\"}]}}",
        "{\"type\":\"result\",\"subtype\":\"success\",\"is_error\":false,\"result\":\"Criei o arquivo login.cs\"," +
            "\"num_turns\":3,\"duration_ms\":4200,\"total_cost_usd\":0.0123,\"permission_denials\":[]}",
    ];

    [Fact]
    public void InterpretarStreaming_emite_eventos_na_ordem_e_resume_resultado()
    {
        var eventos = new List<EventoProgressoClaudeCode>();

        var resumo = ClaudeCodeTool.InterpretarStreaming(StreamLinhas, stderr: "", exitCode: 0, eventos.Add);

        Assert.Collection(eventos,
            e => Assert.Equal(TipoEventoClaudeCode.Inicio, e.Tipo),
            e =>
            {
                Assert.Equal(TipoEventoClaudeCode.Texto, e.Tipo);
                Assert.Contains("criar o arquivo", e.Texto, StringComparison.OrdinalIgnoreCase);
            },
            e =>
            {
                Assert.Equal(TipoEventoClaudeCode.Ferramenta, e.Tipo);
                Assert.Contains("Write", e.Texto);
                Assert.Contains("login.cs", e.Texto);
            },
            e => Assert.Equal(TipoEventoClaudeCode.ResultadoFerramenta, e.Tipo),
            e => Assert.Equal(TipoEventoClaudeCode.Fim, e.Tipo));

        Assert.Contains("Criei o arquivo login.cs", resumo);
        Assert.Contains("3 turno(s)", resumo);
        Assert.Contains("$0.0123", resumo);
    }

    [Fact]
    public void InterpretarStreaming_sinaliza_erro_e_permission_denials()
    {
        string[] linhas =
        [
            "{\"type\":\"result\",\"subtype\":\"error_max_turns\",\"is_error\":true,\"result\":\"parcial\"," +
                "\"permission_denials\":[{\"tool\":\"Bash\"},{\"tool\":\"Edit\"}]}",
        ];

        var resumo = ClaudeCodeTool.InterpretarStreaming(linhas, stderr: "", exitCode: 1, onEvento: null);

        Assert.Contains("erro", resumo, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2 ação", resumo);
        Assert.Contains("bloqueada", resumo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InterpretarStreaming_ignora_linhas_invalidas()
    {
        string[] linhas =
        [
            "não é json",
            "",
            "{\"type\":\"result\",\"subtype\":\"success\",\"is_error\":false,\"result\":\"ok\"}",
        ];

        var resumo = ClaudeCodeTool.InterpretarStreaming(linhas, stderr: "", exitCode: 0, onEvento: null);

        Assert.Contains("ok", resumo);
    }
}
