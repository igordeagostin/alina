using Alina.Tools;

namespace Alina.Tests;

public sealed class AbrirNoVsCodeToolTests
{
    private static AbrirNoVsCodeTool Criar()
        => new AbrirNoVsCodeTool(new FakeConfirmationService(true));

    [Fact]
    public void Abrir_sem_caminho_retorna_erro()
    {
        string resultado = Criar().Abrir("   ");

        Assert.StartsWith("Erro:", resultado);
    }

    [Fact]
    public void Abrir_pasta_inexistente_retorna_erro_sem_iniciar_processo()
    {
        string inexistente = Path.Combine(Path.GetTempPath(), "alina-vscode-" + Guid.NewGuid().ToString("N"));

        string resultado = Criar().Abrir(inexistente);

        Assert.Contains("não encontrada", resultado);
    }

    [Fact]
    public void Abrir_nao_exige_confirmacao()
    {
        Assert.False(Criar().RequiresConfirmation);
    }
}
