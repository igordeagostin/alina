using Alina.Voice;

namespace Alina.Tests;

public sealed class VozConfirmationTests
{
    private static readonly VoiceOptions Opcoes = new();

    private static bool? Interpretar(string? texto)
        => VozConfirmationService.InterpretarResposta(texto, Opcoes.PalavrasSim, Opcoes.PalavrasNao);

    [Theory]
    [InlineData("Sim")]
    [InlineData("sim, pode")]
    [InlineData("Pode prosseguir")]
    [InlineData("claro, autorizo")]
    [InlineData("OK")]
    public void Reconhece_sim(string texto)
        => Assert.True(Interpretar(texto));

    [Theory]
    [InlineData("Não")]
    [InlineData("nao")]
    [InlineData("negativo")]
    [InlineData("melhor não")]
    [InlineData("cancela isso")]
    public void Reconhece_nao(string texto)
        => Assert.False(Interpretar(texto));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("não sei bem o que fazer")]
    public void Nao_reconhece_ambiguo_ou_vazio(string? texto)
    {
        // "não sei" contém "nao" → deve negar por segurança; os demais retornam null.
        var resultado = Interpretar(texto);
        if (texto is not null && texto.Contains("não", StringComparison.OrdinalIgnoreCase))
        {
            Assert.False(resultado);
        }
        else
        {
            Assert.Null(resultado);
        }
    }

    [Fact]
    public void Prioriza_nao_quando_sim_e_nao_aparecem()
        => Assert.False(Interpretar("sim, mas melhor não"));

    [Fact]
    public void Ignora_pontuacao_e_acentos()
    {
        Assert.True(Interpretar("Sim!!!"));
        Assert.False(Interpretar("Não."));
    }
}
