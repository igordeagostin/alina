using Alina.Core.Permissoes;
using Alina.Voice;

namespace Alina.Tests;

public sealed class ConfirmacaoPermissaoVozTests
{
    private static readonly VoiceOptions Opcoes = new();

    private static RespostaConfirmacaoPermissao? Interpretar(string? texto)
        => ConfirmacaoPermissaoVoz.Interpretar(texto, Opcoes.PalavrasSim, Opcoes.PalavrasNao);

    [Fact]
    public void Sim_simples_permite_uma_vez()
    {
        RespostaConfirmacaoPermissao? r = Interpretar("sim, pode");
        Assert.Equal(new RespostaConfirmacaoPermissao(true, EscopoPermissao.UmaVez), r);
    }

    [Fact]
    public void Sempre_permite_com_escopo_sempre()
    {
        RespostaConfirmacaoPermissao? r = Interpretar("pode sempre");
        Assert.Equal(new RespostaConfirmacaoPermissao(true, EscopoPermissao.Sempre), r);
    }

    [Theory]
    [InlineData("sempre neste diretório")]
    [InlineData("sempre neste projeto")]
    [InlineData("pode sempre aqui")]
    public void Sempre_no_diretorio_detecta_escopo_de_diretorio(string texto)
    {
        RespostaConfirmacaoPermissao? r = Interpretar(texto);
        Assert.Equal(new RespostaConfirmacaoPermissao(true, EscopoPermissao.SempreNesteDiretorio), r);
    }

    [Fact]
    public void Nao_nega()
        => Assert.Equal(RespostaConfirmacaoPermissao.Negada, Interpretar("não"));

    [Fact]
    public void Nao_tem_prioridade_sobre_sempre()
        => Assert.Equal(RespostaConfirmacaoPermissao.Negada, Interpretar("não, nunca faça isso sempre"));

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("hmm sei lá")]
    public void Ambiguo_retorna_nulo(string? texto)
        => Assert.Null(Interpretar(texto));
}
