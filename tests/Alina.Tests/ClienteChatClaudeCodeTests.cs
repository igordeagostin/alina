using Alina.Tools.ClaudeCode;
using Microsoft.Extensions.AI;

namespace Alina.Tests;

public sealed class ClienteChatClaudeCodeTests
{
    [Fact]
    public void MontarPrompt_separa_instrucoes_do_dialogo()
    {
        List<ChatMessage> historico =
        [
            new(ChatRole.System, "Você é a Alina treinando uma habilidade."),
            new(ChatRole.User, "abrir a api de revisão"),
            new(ChatRole.Assistant, "Não achei nenhum projeto com esse nome."),
            new(ChatRole.User, "o caminho certo é D:/github/tecsystem"),
        ];

        string prompt = ClienteChatClaudeCode.MontarPrompt(historico);

        Assert.Contains("<instrucoes>", prompt);
        Assert.Contains("Você é a Alina treinando uma habilidade.", prompt);
        Assert.Contains("Usuário: abrir a api de revisão", prompt);
        Assert.Contains("Você: Não achei nenhum projeto com esse nome.", prompt);

        // A instrução de sistema fica acima do diálogo, para o CLI lê-la primeiro.
        Assert.True(prompt.IndexOf("</instrucoes>", StringComparison.Ordinal)
                    < prompt.IndexOf("<conversa>", StringComparison.Ordinal));
    }

    [Fact]
    public void Interpretar_devolve_o_texto_da_resposta()
    {
        const string json =
            "{\"type\":\"result\",\"subtype\":\"success\",\"is_error\":false," +
            "\"result\":\"{\\\"mensagem\\\":\\\"pronto\\\"}\",\"num_turns\":1}";

        Assert.Equal("{\"mensagem\":\"pronto\"}", ClienteChatClaudeCode.Interpretar(json, erro: ""));
    }

    [Fact]
    public void Interpretar_sinaliza_erro_do_claude_code()
    {
        const string json =
            "{\"type\":\"result\",\"subtype\":\"error_max_turns\",\"is_error\":true,\"result\":\"limite atingido\"}";

        string texto = ClienteChatClaudeCode.Interpretar(json, erro: "");

        Assert.Contains("erro", texto, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("limite atingido", texto);
    }

    [Fact]
    public void Interpretar_usa_o_stderr_quando_nao_ha_saida()
    {
        string texto = ClienteChatClaudeCode.Interpretar(saida: "", erro: "command not found: claude");

        Assert.Contains("command not found: claude", texto);
    }
}
