using Alina.Core.Ferramentas;
using Alina.Core.Habilidades;
using Alina.Core.Permissoes;
using Alina.Core.Tools;
using Alina.Infrastructure.Ferramentas;
using Microsoft.Extensions.AI;

namespace Alina.Tests;

public sealed class GeradorHabilidadeTests
{
    private static IReadOnlyList<ChatMessage> Historico(string texto)
        => new[] { new ChatMessage(ChatRole.User, texto) };

    private static ToolRegistry RegistroCom(params string[] nomes)
    {
        FakeConfirmationService confirmation = new FakeConfirmationService(result: true);
        IEnumerable<ITool> tools = nomes.Select(nome => (ITool)new FerramentaDeclarada(
            new DefinicaoFerramenta { Nome = nome, Descricao = "ferramenta de teste", Comando = "cmd" },
            confirmation));
        return new ToolRegistry(tools);
    }

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

    private static Habilidade HabilidadeExistente() => new Habilidade
    {
        Nome = "deploy-da-api-do-diario",
        Descricao = "Publica a API do Diário",
        Conteudo = "# Deploy\n1. dotnet publish",
    };

    [Fact]
    public async Task ContinuarAsync_em_edicao_injeta_a_habilidade_atual()
    {
        FakeChatClient client = new FakeChatClient((_, _) =>
            new ChatResponse(new ChatMessage(ChatRole.Assistant, "{\"mensagem\":\"ok\",\"pronto\":false}")));
        GeradorHabilidade gerador = new GeradorHabilidade(client);

        await gerador.ContinuarAsync(
            Historico("troca o passo 1"),
            new ContextoHabilidade(HabilidadeExistente(), ModoConversaHabilidade.Edicao));

        string contexto = string.Concat(
            client.LastMessages!.Where(m => m.Role == ChatRole.System).Select(m => m.Text));
        Assert.Contains("editar uma \"habilidade\" que já existe", contexto);
        Assert.Contains("deploy-da-api-do-diario", contexto);
        Assert.Contains("dotnet publish", contexto);
    }

    [Fact]
    public async Task ContinuarAsync_em_treino_orienta_a_partir_dos_testes()
    {
        FakeChatClient client = new FakeChatClient((_, _) =>
            new ChatResponse(new ChatMessage(ChatRole.Assistant, "{\"mensagem\":\"ok\",\"pronto\":false}")));
        GeradorHabilidade gerador = new GeradorHabilidade(client);

        await gerador.ContinuarAsync(
            Historico("[resultado] rodou no branch errado"),
            new ContextoHabilidade(HabilidadeExistente(), ModoConversaHabilidade.Treino));

        string contexto = string.Concat(
            client.LastMessages!.Where(m => m.Role == ChatRole.System).Select(m => m.Text));
        Assert.Contains("treinando", contexto);
        Assert.Contains("[resultado]", contexto);
        Assert.Contains("deploy-da-api-do-diario", contexto);
    }

    [Fact]
    public async Task ContinuarAsync_sem_contexto_nao_menciona_habilidade_existente()
    {
        FakeChatClient client = new FakeChatClient((_, _) =>
            new ChatResponse(new ChatMessage(ChatRole.Assistant, "{\"mensagem\":\"ok\",\"pronto\":false}")));
        GeradorHabilidade gerador = new GeradorHabilidade(client);

        await gerador.ContinuarAsync(Historico("quero uma habilidade"));

        string contexto = string.Concat(
            client.LastMessages!.Where(m => m.Role == ChatRole.System).Select(m => m.Text));
        Assert.Contains("criar uma nova \"habilidade\"", contexto);
        Assert.DoesNotContain("Habilidade atual", contexto);
    }

    [Fact]
    public async Task ContinuarAsync_traz_as_ferramentas_propostas_no_rascunho()
    {
        string json = """
            {
              "mensagem": "Precisa de uma ferramenta para tocar no Spotify.",
              "pronto": true,
              "titulo": "Gerenciar o Spotify",
              "descricao": "Controla a reprodução no Spotify",
              "conteudo": "# Spotify\n1. chame spotify_tocar",
              "ferramentas": [
                {
                  "nome": "spotify_tocar",
                  "descricao": "Toca uma playlist no Spotify",
                  "motivo": "Nenhuma ferramenta atual fala com o Spotify.",
                  "comando": "powershell",
                  "argumentos": ["-NoProfile", "-Command", "spotify play {playlist}"],
                  "exigeConfirmacao": false,
                  "parametros": [
                    { "nome": "playlist", "descricao": "Nome da playlist", "obrigatorio": true }
                  ]
                }
              ]
            }
            """;
        FakeChatClient client = new FakeChatClient((_, _) => new ChatResponse(new ChatMessage(ChatRole.Assistant, json)));
        GeradorHabilidade gerador = new GeradorHabilidade(client);

        RespostaGeracao resposta = await gerador.ContinuarAsync(Historico("quero gerenciar o spotify"));

        FerramentaProposta proposta = Assert.Single(resposta.Rascunho!.Ferramentas);
        Assert.Equal("spotify_tocar", proposta.Definicao.Nome);
        Assert.Equal("Nenhuma ferramenta atual fala com o Spotify.", proposta.Motivo);
        Assert.False(proposta.Definicao.ExigeConfirmacao);
        Assert.False(proposta.SubstituiExistente);
        Assert.Equal("playlist", Assert.Single(proposta.Definicao.Parametros).Nome);
    }

    [Fact]
    public async Task ContinuarAsync_marca_proposta_que_substitui_ferramenta_existente()
    {
        string json = """
            {
              "mensagem": "Vou trocar a ferramenta atual.",
              "pronto": true,
              "titulo": "Spotify",
              "conteudo": "# Spotify",
              "ferramentas": [
                { "nome": "spotify_tocar", "descricao": "nova versão", "comando": "powershell" }
              ]
            }
            """;
        FakeChatClient client = new FakeChatClient((_, _) => new ChatResponse(new ChatMessage(ChatRole.Assistant, json)));
        GeradorHabilidade gerador = new GeradorHabilidade(client, politica: null, RegistroCom("spotify_tocar"));

        RespostaGeracao resposta = await gerador.ContinuarAsync(Historico("ajusta o spotify"));

        Assert.True(Assert.Single(resposta.Rascunho!.Ferramentas).SubstituiExistente);
    }

    [Fact]
    public async Task ContinuarAsync_descarta_proposta_sem_nome_ou_comando()
    {
        string json = """
            {
              "mensagem": "ok",
              "pronto": true,
              "titulo": "T",
              "conteudo": "# T",
              "ferramentas": [
                { "descricao": "sem nome", "comando": "powershell" },
                { "nome": "sem_comando", "descricao": "sem comando" },
                { "nome": "valida", "descricao": "ok", "comando": "git" }
              ]
            }
            """;
        FakeChatClient client = new FakeChatClient((_, _) => new ChatResponse(new ChatMessage(ChatRole.Assistant, json)));
        GeradorHabilidade gerador = new GeradorHabilidade(client);

        RespostaGeracao resposta = await gerador.ContinuarAsync(Historico("cria"));

        Assert.Equal("valida", Assert.Single(resposta.Rascunho!.Ferramentas).Definicao.Nome);
    }

    [Fact]
    public async Task ContinuarAsync_sem_ferramentas_propostas_devolve_lista_vazia()
    {
        FakeChatClient client = new FakeChatClient((_, _) => new ChatResponse(new ChatMessage(ChatRole.Assistant,
            "{\"mensagem\":\"ok\",\"pronto\":true,\"titulo\":\"T\",\"conteudo\":\"# T\"}")));
        GeradorHabilidade gerador = new GeradorHabilidade(client);

        RespostaGeracao resposta = await gerador.ContinuarAsync(Historico("cria"));

        Assert.Empty(resposta.Rascunho!.Ferramentas);
    }

    [Fact]
    public async Task ContinuarAsync_orienta_a_propor_ferramenta_quando_faltar()
    {
        FakeChatClient client = new FakeChatClient((_, _) =>
            new ChatResponse(new ChatMessage(ChatRole.Assistant, "{\"mensagem\":\"ok\",\"pronto\":false}")));
        GeradorHabilidade gerador = new GeradorHabilidade(client, politica: null, RegistroCom("abrir_projeto"));

        await gerador.ContinuarAsync(Historico("quero gerenciar o spotify"));

        string contexto = string.Concat(
            client.LastMessages!.Where(m => m.Role == ChatRole.System).Select(m => m.Text));
        Assert.Contains("FERRAMENTAS novas", contexto);
        Assert.Contains("\"ferramentas\"", contexto);
        Assert.Contains("abrir_projeto", contexto);
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
