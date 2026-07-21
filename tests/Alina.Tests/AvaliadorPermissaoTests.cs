using Alina.Core.Permissoes;

namespace Alina.Tests;

public sealed class AvaliadorPermissaoTests
{
    private static readonly string Raiz = OperatingSystem.IsWindows() ? @"C:\proj" : "/proj";
    private static string Sob(params string[] partes) => Path.Combine([Raiz, .. partes]);

    private static PedidoPermissao Bash(string comando, string? dir = null) => new()
    {
        Ferramenta = "Bash",
        Comando = comando,
        DiretorioTrabalho = dir,
        Descricao = comando,
    };

    private static PedidoPermissao Editar(string caminho, string? dir = null) => new()
    {
        Ferramenta = "Edit",
        Caminho = caminho,
        DiretorioTrabalho = dir,
        Descricao = caminho,
    };

    private static DecisaoPermissao Avaliar(PedidoPermissao p, PoliticaPermissaoOptions o, params RegraPermissao[] regras)
        => AvaliadorPermissao.Avaliar(p, o, regras);

    [Fact]
    public void Ferramenta_somente_leitura_e_liberada()
    {
        var o = new PoliticaPermissaoOptions();
        var pedido = new PedidoPermissao { Ferramenta = "Read", Caminho = Sob("a.cs"), Descricao = "ler" };
        Assert.Equal(DecisaoPermissao.Permitir, Avaliar(pedido, o));
    }

    [Fact]
    public void Comando_allowlisted_por_prefixo_e_liberado()
        => Assert.Equal(DecisaoPermissao.Permitir, Avaliar(Bash("git status --short"), new PoliticaPermissaoOptions()));

    [Fact]
    public void Comando_perigoso_sempre_pergunta()
        => Assert.Equal(DecisaoPermissao.Perguntar, Avaliar(Bash("rm -rf build"), new PoliticaPermissaoOptions()));

    [Fact]
    public void Diretorio_confiavel_libera_edicao_dentro_dele()
    {
        var o = new PoliticaPermissaoOptions { DiretoriosConfiaveis = [Raiz] };
        Assert.Equal(DecisaoPermissao.Permitir, Avaliar(Editar(Sob("src", "x.cs")), o));
    }

    [Fact]
    public void Fora_do_workspace_pergunta_quando_ha_dir_confiavel()
    {
        var o = new PoliticaPermissaoOptions { DiretoriosConfiaveis = [Raiz] };
        var fora = OperatingSystem.IsWindows() ? @"C:\outro\y.cs" : "/outro/y.cs";
        Assert.Equal(DecisaoPermissao.Perguntar, Avaliar(Editar(fora), o));
    }

    [Fact]
    public void Caminho_protegido_pergunta_mesmo_em_dir_confiavel()
    {
        var o = new PoliticaPermissaoOptions { DiretoriosConfiaveis = [Raiz] };
        Assert.Equal(DecisaoPermissao.Perguntar, Avaliar(Editar(Sob(".env")), o));
    }

    [Fact]
    public void Regra_aprendida_allow_libera()
    {
        var o = new PoliticaPermissaoOptions();
        var regra = new RegraPermissao { Ferramenta = "Bash", PrefixoComando = "npm install", Permitir = true };
        Assert.Equal(DecisaoPermissao.Permitir, Avaliar(Bash("npm install left-pad"), o, regra));
    }

    [Fact]
    public void Regra_deny_vence_regra_allow()
    {
        var o = new PoliticaPermissaoOptions();
        var allow = new RegraPermissao { Ferramenta = "*", Permitir = true };
        var deny = new RegraPermissao { Ferramenta = "Bash", PrefixoComando = "npm", Permitir = false };
        Assert.Equal(DecisaoPermissao.Negar, Avaliar(Bash("npm publish"), o, allow, deny));
    }

    [Fact]
    public void Regra_no_diretorio_so_casa_dentro_dele()
    {
        var o = new PoliticaPermissaoOptions { DiretoriosConfiaveis = [] };
        var regra = new RegraPermissao { Ferramenta = "Edit", Diretorio = Raiz, Permitir = true };

        Assert.Equal(DecisaoPermissao.Permitir, Avaliar(Editar(Sob("a.cs"), Sob()), o, regra));

        var fora = OperatingSystem.IsWindows() ? @"C:\outro" : "/outro";
        Assert.Equal(DecisaoPermissao.Perguntar,
            Avaliar(Editar(Path.Combine(fora, "a.cs"), fora), o, regra));
    }

    [Fact]
    public void Modo_autonomia_libera_o_que_sobra()
    {
        var o = new PoliticaPermissaoOptions { ModoPadrao = ModoPermissao.Autonomia };
        Assert.Equal(DecisaoPermissao.Permitir, Avaliar(Bash("dotnet publish"), o));
    }

    [Fact]
    public void Modo_aceitar_edicoes_libera_arquivo_mas_pergunta_comando()
    {
        var o = new PoliticaPermissaoOptions { ModoPadrao = ModoPermissao.AceitarEdicoes };
        Assert.Equal(DecisaoPermissao.Permitir, Avaliar(Editar(Sob("a.cs")), o));
        Assert.Equal(DecisaoPermissao.Perguntar, Avaliar(Bash("dotnet publish"), o));
    }

    [Fact]
    public void Modo_perguntar_e_o_padrao_para_o_desconhecido()
        => Assert.Equal(DecisaoPermissao.Perguntar, Avaliar(Bash("dotnet publish"), new PoliticaPermissaoOptions()));
}
