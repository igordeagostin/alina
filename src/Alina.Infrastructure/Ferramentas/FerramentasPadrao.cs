using Alina.Core.Ferramentas;

namespace Alina.Infrastructure.Ferramentas;

/// <summary>
/// Ferramentas declarativas que já nascem com a Alina, semeadas na pasta de dados na
/// primeira execução. Servem de exemplo editável e cobrem tools que antes eram classes
/// C#, mas que na prática só disparam um comando externo (ex.: abrir o VS Code).
/// </summary>
internal static class FerramentasPadrao
{
    public static IEnumerable<DefinicaoFerramenta> Todas()
    {
        yield return new DefinicaoFerramenta
        {
            Nome = "abrir_no_vscode",
            Descricao =
                "Abre uma pasta existente em uma NOVA janela do VS Code. Passe o caminho absoluto do projeto. " +
                "Use para atender pedidos como \"abra o projeto X\".",
            ExigeConfirmacao = false,
            Comando = "cmd",
            Argumentos = ["/c", "code", "-n", "{caminho}"],
            Parametros =
            [
                new ParametroFerramenta
                {
                    Nome = "caminho",
                    Descricao = "Caminho absoluto da pasta do projeto a abrir.",
                    Obrigatorio = true,
                },
            ],
        };

        yield return new DefinicaoFerramenta
        {
            Nome = "saudar",
            Descricao = "Ferramenta de exemplo: exibe uma saudação personalizada. Edite ou remova à vontade.",
            ExigeConfirmacao = true,
            Comando = "powershell",
            Argumentos =
            [
                "-NoProfile",
                "-Command",
                "Write-Output 'Olá, {nome}! Esta é uma ferramenta de exemplo da Alina.'",
            ],
            Parametros =
            [
                new ParametroFerramenta
                {
                    Nome = "nome",
                    Descricao = "Nome de quem será saudado.",
                    Obrigatorio = true,
                },
            ],
        };
    }
}
