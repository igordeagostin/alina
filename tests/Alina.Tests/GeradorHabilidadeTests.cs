using Alina.Core.Habilidades;
using Alina.Core.Permissoes;
using Microsoft.Extensions.AI;

namespace Alina.Tests;

public sealed class GeradorHabilidadeTests
{
    private static IReadOnlyList<ChatMessage> Historico(string texto)
        => new[] { new ChatMessage(ChatRole.User, texto) };

    private sealed class FakePolitica : IPoliticaPermissao
    {
        public FakePolitica(params string[] diretoriosConfiaveis)
            => Opcoes = new PoliticaPermissaoOptions { DiretoriosConfiaveis = [.. diretoriosConfiaveis] };

        public PoliticaPermissaoOptions Opcoes { get; }
        public IReadOnlyList<RegraPermissao> Regras => [];
        public DecisaoPermissao Avaliar(PedidoPermissao pedido) => DecisaoPermissao.Perguntar;
        public void Aprender(PedidoPermissao pedido, RespostaConfirmacaoPermissao resposta) { }
        public void AtualizarOpcoes(PoliticaPermissaoOptions opcoes) { }
        public void RemoverRegra(string id) { }
    }

    [Fact]
    public async Task ContinuarAsync_injeta_arvore_dos_diretorios_confiaveis()
    {
        string raiz = Path.Combine(Path.GetTempPath(), "alina-hab-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(raiz, "diario", "diario-api"));
        try
        {
            FakeChatClient client = new FakeChatClient((_, _) =>
                new ChatResponse(new ChatMessage(ChatRole.Assistant, "{\"mensagem\":\"ok\",\"pronto\":false}")));
            GeradorHabilidade gerador = new GeradorHabilidade(client, new FakePolitica(raiz));

            await gerador.ContinuarAsync(Historico("abra o projeto diario api"));

            string contexto = string.Concat(
                client.LastMessages!.Where(m => m.Role == ChatRole.System).Select(m => m.Text));
            Assert.Contains(raiz, contexto);
            Assert.Contains("diario-api", contexto);
        }
        finally
        {
            Directory.Delete(raiz, recursive: true);
        }
    }

    [Fact]
    public async Task ContinuarAsync_sem_politica_nao_injeta_contexto_extra()
    {
        FakeChatClient client = new FakeChatClient((_, _) =>
            new ChatResponse(new ChatMessage(ChatRole.Assistant, "{\"mensagem\":\"ok\",\"pronto\":false}")));
        GeradorHabilidade gerador = new GeradorHabilidade(client);

        await gerador.ContinuarAsync(Historico("quero uma habilidade"));

        Assert.Single(client.LastMessages!, m => m.Role == ChatRole.System);
    }

    [Fact]
    public async Task ContinuarAsync_com_pronto_retorna_rascunho_preenchido()
    {
        string json = """
            {
              "mensagem": "Montei o deploy da API. Quer revisar?",
              "pronto": true,
              "titulo": "Deploy da API do Diário",
              "descricao": "Publica a API do Diário em produção",
              "conteudo": "# Deploy\n1. dotnet publish\n2. copiar para a pasta"
            }
            """;
        FakeChatClient client = new FakeChatClient((_, _) => new ChatResponse(new ChatMessage(ChatRole.Assistant, json)));
        GeradorHabilidade gerador = new GeradorHabilidade(client);

        RespostaGeracao resposta = await gerador.ContinuarAsync(Historico("como faço o deploy?"));

        Assert.Equal("Montei o deploy da API. Quer revisar?", resposta.Mensagem);
        Assert.NotNull(resposta.Rascunho);
        Assert.Equal("Deploy da API do Diário", resposta.Rascunho!.Titulo);
        Assert.Equal("Publica a API do Diário em produção", resposta.Rascunho.Descricao);
        Assert.Contains("dotnet publish", resposta.Rascunho.Conteudo);
    }

    [Fact]
    public async Task ContinuarAsync_nao_oferece_ferramentas_ao_modelo()
    {
        FakeChatClient client = new FakeChatClient((_, _) =>
            new ChatResponse(new ChatMessage(ChatRole.Assistant, "{\"mensagem\":\"e aí?\",\"pronto\":false}")));
        GeradorHabilidade gerador = new GeradorHabilidade(client);

        await gerador.ContinuarAsync(Historico("quero uma habilidade"));

        Assert.True(client.LastOptions?.Tools is null || client.LastOptions.Tools.Count == 0);
        Assert.Equal(ChatRole.System, client.LastMessages![0].Role);
    }

    [Fact]
    public async Task ContinuarAsync_sem_pronto_nao_gera_rascunho()
    {
        FakeChatClient client = new FakeChatClient((_, _) =>
            new ChatResponse(new ChatMessage(ChatRole.Assistant,
                "{\"mensagem\":\"Qual comando você usa hoje?\",\"pronto\":false}")));
        GeradorHabilidade gerador = new GeradorHabilidade(client);

        RespostaGeracao resposta = await gerador.ContinuarAsync(Historico("automatizar o deploy"));

        Assert.Null(resposta.Rascunho);
        Assert.False(resposta.TemRascunho);
        Assert.Equal("Qual comando você usa hoje?", resposta.Mensagem);
    }

    [Fact]
    public async Task ContinuarAsync_ignora_cercas_de_codigo_no_json()
    {
        string comCercas = "```json\n{\"mensagem\":\"ok\",\"pronto\":true,\"titulo\":\"T\",\"conteudo\":\"# T\"}\n```";
        FakeChatClient client = new FakeChatClient((_, _) => new ChatResponse(new ChatMessage(ChatRole.Assistant, comCercas)));
        GeradorHabilidade gerador = new GeradorHabilidade(client);

        RespostaGeracao resposta = await gerador.ContinuarAsync(Historico("cria aí"));

        Assert.NotNull(resposta.Rascunho);
        Assert.Equal("T", resposta.Rascunho!.Titulo);
    }

    [Fact]
    public async Task ContinuarAsync_pronto_sem_conteudo_nao_gera_rascunho()
    {
        FakeChatClient client = new FakeChatClient((_, _) =>
            new ChatResponse(new ChatMessage(ChatRole.Assistant, "{\"mensagem\":\"quase lá\",\"pronto\":true,\"titulo\":\"T\"}")));
        GeradorHabilidade gerador = new GeradorHabilidade(client);

        RespostaGeracao resposta = await gerador.ContinuarAsync(Historico("cria"));

        Assert.Null(resposta.Rascunho);
        Assert.Equal("quase lá", resposta.Mensagem);
    }

    [Fact]
    public async Task ContinuarAsync_resposta_nao_json_vira_mensagem()
    {
        FakeChatClient client = new FakeChatClient((_, _) =>
            new ChatResponse(new ChatMessage(ChatRole.Assistant, "desculpa, não entendi o pedido")));
        GeradorHabilidade gerador = new GeradorHabilidade(client);

        RespostaGeracao resposta = await gerador.ContinuarAsync(Historico("???"));

        Assert.Null(resposta.Rascunho);
        Assert.Equal("desculpa, não entendi o pedido", resposta.Mensagem);
    }
}
