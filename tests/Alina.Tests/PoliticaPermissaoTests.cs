using Alina.Core.Permissoes;
using Alina.Infrastructure.Permissoes;

namespace Alina.Tests;

public sealed class PoliticaPermissaoTests : IDisposable
{
    private readonly string _arquivo = Path.Combine(Path.GetTempPath(), "alina-pol-" + Guid.NewGuid().ToString("n") + ".json");

    private PedidoPermissao Bash(string comando, string? dir = null) => new()
    {
        Ferramenta = "Bash",
        Comando = comando,
        DiretorioTrabalho = dir,
        Descricao = comando,
    };

    [Fact]
    public void Aprender_uma_vez_nao_cria_regra()
    {
        var pol = new PoliticaPermissao(_arquivo);
        pol.Aprender(Bash("npm publish"), RespostaConfirmacaoPermissao.PermitidaUmaVez);
        Assert.Empty(pol.Regras);
    }

    [Fact]
    public void Aprender_sempre_cria_regra_por_prefixo_de_comando_e_persiste()
    {
        var pol = new PoliticaPermissao(_arquivo);
        pol.Aprender(Bash("npm install left-pad"), new RespostaConfirmacaoPermissao(true, EscopoPermissao.Sempre));

        Assert.Single(pol.Regras);
        Assert.Equal("npm install", pol.Regras[0].PrefixoComando);

        // Persistiu: uma nova instância enxerga a regra e libera o mesmo comando.
        var recarregada = new PoliticaPermissao(_arquivo);
        Assert.Equal(DecisaoPermissao.Permitir, recarregada.Avaliar(Bash("npm install react")));
    }

    [Fact]
    public void Aprender_sessao_nao_persiste_mas_vale_na_instancia()
    {
        var pol = new PoliticaPermissao(_arquivo);
        pol.Aprender(Bash("dotnet publish"), new RespostaConfirmacaoPermissao(true, EscopoPermissao.Sessao));

        Assert.Empty(pol.Regras); // não entra na lista persistida
        Assert.Equal(DecisaoPermissao.Permitir, pol.Avaliar(Bash("dotnet publish -c Release")));

        var recarregada = new PoliticaPermissao(_arquivo);
        Assert.Equal(DecisaoPermissao.Perguntar, recarregada.Avaliar(Bash("dotnet publish -c Release")));
    }

    [Fact]
    public void Aprender_neste_diretorio_restringe_ao_dir()
    {
        var raiz = OperatingSystem.IsWindows() ? @"C:\proj" : "/proj";
        var pol = new PoliticaPermissao(_arquivo);
        pol.Aprender(Bash("dotnet run", raiz), new RespostaConfirmacaoPermissao(true, EscopoPermissao.SempreNesteDiretorio));

        Assert.Equal(DecisaoPermissao.Permitir, pol.Avaliar(Bash("dotnet run --project x", raiz)));

        var outro = OperatingSystem.IsWindows() ? @"C:\outro" : "/outro";
        Assert.Equal(DecisaoPermissao.Perguntar, pol.Avaliar(Bash("dotnet run", outro)));
    }

    [Fact]
    public void RemoverRegra_apaga_e_persiste()
    {
        var pol = new PoliticaPermissao(_arquivo);
        pol.Aprender(Bash("npm install x"), new RespostaConfirmacaoPermissao(true, EscopoPermissao.Sempre));
        var id = pol.Regras[0].Id;

        pol.RemoverRegra(id);

        Assert.Empty(pol.Regras);
        Assert.Empty(new PoliticaPermissao(_arquivo).Regras);
    }

    public void Dispose()
    {
        if (File.Exists(_arquivo))
        {
            File.Delete(_arquivo);
        }
    }
}
