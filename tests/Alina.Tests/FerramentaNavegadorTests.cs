using Alina.Core.Ferramentas;
using Alina.Core.Habilidades;
using Alina.Infrastructure.Ferramentas;
using Alina.Infrastructure.Habilidades;
using Microsoft.Extensions.AI;

namespace Alina.Tests;

public sealed class ParametroUrlTests
{
    private static DefinicaoFerramenta ComUrl(TipoParametroFerramenta tipo, string nomeParametro = "url") => new()
    {
        Nome = "abrir_url",
        Descricao = "abre um endereço",
        ExigeConfirmacao = false,
        Comando = OperatingSystem.IsWindows() ? "cmd" : "echo",
        Argumentos = OperatingSystem.IsWindows()
            ? ["/c", "echo", "{" + nomeParametro + "}"]
            : ["{" + nomeParametro + "}"],
        Parametros = [new ParametroFerramenta { Nome = nomeParametro, Descricao = "endereço", Tipo = tipo }],
    };

    private static async Task<string> Invocar(DefinicaoFerramenta definicao, string nomeParametro, string valor)
    {
        AIFunction function = new FerramentaDeclarada(definicao, new FakeConfirmationService(true)).AsAIFunction();
        object? resultado = await function.InvokeAsync(new AIFunctionArguments { [nomeParametro] = valor });
        return resultado?.ToString() ?? string.Empty;
    }

    [Fact]
    public async Task Url_https_valida_executa()
    {
        string texto = await Invocar(ComUrl(TipoParametroFerramenta.Url), "url", "https://www.youtube.com");

        Assert.Contains("youtube.com", texto);
    }

    [Fact]
    public async Task Esquema_nao_http_e_recusado()
    {
        string texto = await Invocar(ComUrl(TipoParametroFerramenta.Url), "url", "file:///C:/Windows/system32");

        Assert.Contains("http/https", texto);
        Assert.DoesNotContain("[exit", texto);
    }

    [Fact]
    public async Task Valor_que_nao_e_url_e_recusado()
    {
        string texto = await Invocar(ComUrl(TipoParametroFerramenta.Url), "url", "youtube");

        Assert.Contains("Erro", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("[exit", texto);
    }

    [Fact]
    public async Task Metacaractere_de_shell_e_recusado_antes_de_executar()
    {
        string texto = await Invocar(ComUrl(TipoParametroFerramenta.Url), "url", "https://ok.com&calc");

        Assert.Contains("não permitidos", texto);
        Assert.DoesNotContain("[exit", texto);
    }

    [Fact]
    public async Task Parametro_chamado_url_valida_mesmo_sem_tipo_declarado()
    {
        string texto = await Invocar(ComUrl(TipoParametroFerramenta.Automatico), "url", "só um texto");

        Assert.Contains("Erro", texto, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Parametro_chamado_termo_continua_sendo_texto_livre()
    {
        DefinicaoFerramenta definicao = ComUrl(TipoParametroFerramenta.Automatico, "termo");

        string texto = await Invocar(definicao, "termo", "receita de bolo");

        Assert.Contains("receita de bolo", texto);
    }
}

public sealed class FerramentasPadraoNavegadorTests
{
    private static DefinicaoFerramenta Obter(string nome)
        => FerramentasPadrao.Todas().Single(d => d.Nome == nome);

    [Theory]
    [InlineData("abrir_url")]
    [InlineData("pesquisar_google")]
    [InlineData("pesquisar_youtube")]
    public void Usa_lancador_que_retorna_na_hora(string nome)
    {
        DefinicaoFerramenta definicao = Obter(nome);

        Assert.Equal("cmd", definicao.Comando);
        Assert.Equal(["/c", "start"], definicao.Argumentos.Take(2));
    }

    [Theory]
    [InlineData("pesquisar_google")]
    [InlineData("pesquisar_youtube")]
    public void Busca_codifica_o_termo_na_url(string nome)
    {
        DefinicaoFerramenta definicao = Obter(nome);

        Assert.Contains(definicao.Argumentos, a => a.Contains("{termo:url}", StringComparison.Ordinal));
    }

    [Fact]
    public void Abrir_url_valida_o_endereco()
    {
        DefinicaoFerramenta definicao = Obter("abrir_url");

        Assert.Equal(TipoParametroFerramenta.Url, definicao.Parametros.Single().Tipo);
    }

    [Fact]
    public async Task Titulo_vazio_do_start_chega_como_aspas_ao_processo()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        DefinicaoFerramenta definicao = new DefinicaoFerramenta
        {
            Nome = "eco_lancador",
            Descricao = "reproduz os argumentos do lançador",
            ExigeConfirmacao = false,
            Comando = "cmd",
            Argumentos = ["/c", "echo", string.Empty, "{url}"],
            Parametros = [new ParametroFerramenta { Nome = "url", Tipo = TipoParametroFerramenta.Url }],
        };

        AIFunction function = new FerramentaDeclarada(definicao, new FakeConfirmationService(true)).AsAIFunction();
        object? resultado = await function.InvokeAsync(new AIFunctionArguments { ["url"] = "https://example.com" });

        Assert.Contains("\"\" https://example.com", resultado?.ToString());
    }

    [Fact]
    public void Todo_placeholder_tem_parametro_correspondente()
    {
        foreach (DefinicaoFerramenta definicao in FerramentasPadrao.Todas())
        {
            IEnumerable<string> referencias = definicao.Argumentos
                .Concat([definicao.DiretorioTrabalho ?? string.Empty])
                .SelectMany(SubstituidorPlaceholder.Referencias);

            foreach (string referencia in referencias)
            {
                Assert.Contains(definicao.Parametros, p => p.Nome.Equals(referencia, StringComparison.OrdinalIgnoreCase));
            }
        }
    }
}

public sealed class HabilidadesPadraoTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "alina-habilidades-" + Guid.NewGuid().ToString("n"));

    [Fact]
    public async Task Semeia_habilidade_de_navegador_em_pasta_nova()
    {
        FileHabilidadeStore store = new FileHabilidadeStore(_dir);

        Habilidade? habilidade = await store.ObterAsync("navegador");

        Assert.NotNull(habilidade);
        Assert.Contains("abrir_url", habilidade!.Conteudo);
        Assert.False(string.IsNullOrWhiteSpace(habilidade.Descricao));
    }

    [Fact]
    public async Task Habilidade_apagada_nao_volta_na_proxima_abertura()
    {
        FileHabilidadeStore store = new FileHabilidadeStore(_dir);
        Assert.True(await store.RemoverAsync("navegador"));

        FileHabilidadeStore outra = new FileHabilidadeStore(_dir);

        Assert.Null(await outra.ObterAsync("navegador"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }
}
