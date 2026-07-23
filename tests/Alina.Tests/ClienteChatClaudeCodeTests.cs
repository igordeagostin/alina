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
    public void LeitorFluxo_emite_cada_pedaco_e_devolve_o_texto_inteiro()
    {
        List<string> pedacos = [];
        ClienteChatClaudeCode.LeitorFluxo leitor = new(pedacos.Add);

        leitor.Processar(Delta("{\"mensa"));
        leitor.Processar(Delta("gem\":\"pronto\"}"));
        leitor.Processar("{\"type\":\"result\",\"subtype\":\"success\",\"is_error\":false," +
                         "\"result\":\"{\\\"mensagem\\\":\\\"pronto\\\"}\",\"num_turns\":1}");

        Assert.Equal(["{\"mensa", "gem\":\"pronto\"}"], pedacos);
        Assert.Equal("{\"mensagem\":\"pronto\"}", leitor.Concluir(erro: ""));
    }

    [Fact]
    public void LeitorFluxo_cai_no_resultado_final_quando_nao_ha_deltas()
    {
        ClienteChatClaudeCode.LeitorFluxo leitor = new();

        leitor.Processar("{\"type\":\"result\",\"subtype\":\"success\",\"is_error\":false,\"result\":\"pronto\"}");

        Assert.Equal("pronto", leitor.Concluir(erro: ""));
    }

    [Fact]
    public void LeitorFluxo_sinaliza_erro_do_claude_code()
    {
        ClienteChatClaudeCode.LeitorFluxo leitor = new();

        leitor.Processar("{\"type\":\"result\",\"subtype\":\"error_max_turns\",\"is_error\":true," +
                         "\"result\":\"limite atingido\"}");
        string texto = leitor.Concluir(erro: "");

        Assert.Contains("erro", texto, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("limite atingido", texto);
    }

    [Fact]
    public void LeitorFluxo_usa_o_stderr_quando_nao_ha_saida()
    {
        ClienteChatClaudeCode.LeitorFluxo leitor = new();

        Assert.Contains("command not found: claude", leitor.Concluir(erro: "command not found: claude"));
    }

    [Fact]
    public void LeitorFluxo_ignora_linhas_que_nao_sao_texto()
    {
        List<string> pedacos = [];
        ClienteChatClaudeCode.LeitorFluxo leitor = new(pedacos.Add);

        leitor.Processar("{\"type\":\"system\",\"subtype\":\"init\"}");
        leitor.Processar("linha solta que não é json");
        leitor.Processar("{\"type\":\"stream_event\",\"event\":{\"type\":\"message_stop\"}}");

        Assert.Empty(pedacos);
    }

    private static string Delta(string texto) =>
        "{\"type\":\"stream_event\",\"event\":{\"type\":\"content_block_delta\"," +
        $"\"delta\":{{\"type\":\"text_delta\",\"text\":{System.Text.Json.JsonSerializer.Serialize(texto)}}}}}}}";
}
