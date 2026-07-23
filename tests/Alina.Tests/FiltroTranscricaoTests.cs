using Alina.Voice;

namespace Alina.Tests;

public sealed class FiltroTranscricaoTests
{
    private const string Prompt =
        "Comandos de voz para uma assistente de desenvolvimento de software. Vocabulário " +
        "técnico frequente: API, VS Code, deploy, endpoint, commit, branch, pull request, " +
        "front-end, back-end, banco de dados, build, log, token.";

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("...")]
    public void DescartaTextoVazioOuSemConteudo(string texto)
    {
        Assert.True(FiltroTranscricao.Descartavel(texto, Prompt));
    }

    [Fact]
    public void DescartaEcoIntegralDoPrompt()
    {
        Assert.True(FiltroTranscricao.Descartavel(Prompt, Prompt));
    }

    [Fact]
    public void DescartaEcoParcialDoPrompt()
    {
        Assert.True(FiltroTranscricao.Descartavel(
            "Comandos de voz para uma assistente de desenvolvimento de software.", Prompt));
    }

    [Theory]
    [InlineData("Legendas pela comunidade da Amara.org")]
    [InlineData("legendas pela comunidade da amara org")]
    [InlineData("Thanks for watching!")]
    [InlineData("Thank you for watching.")]
    [InlineData("Obrigado por assistir!")]
    [InlineData("Inscreva-se no canal")]
    public void DescartaAlucinacoesConhecidasDeSilencio(string texto)
    {
        Assert.True(FiltroTranscricao.Descartavel(texto, Prompt));
    }

    [Theory]
    [InlineData("Toca música.")]
    [InlineData("Abre o Spotify para mim.")]
    [InlineData("Faz o commit e sobe pra branch de deploy.")]
    [InlineData("Próxima música.")]
    public void PreservaFalaLegitima(string texto)
    {
        Assert.False(FiltroTranscricao.Descartavel(texto, Prompt));
    }

    [Fact]
    public void PreservaFalaQueUsaVocabularioDoPromptSemEcoar()
    {
        Assert.False(FiltroTranscricao.Descartavel("Abre o VS Code e roda o build.", Prompt));
    }

    [Fact]
    public void SemPromptAindaDescartaAlucinacaoConhecida()
    {
        Assert.True(FiltroTranscricao.Descartavel("Legendas pela comunidade da Amara.org"));
        Assert.False(FiltroTranscricao.Descartavel("Toca música."));
    }
}
