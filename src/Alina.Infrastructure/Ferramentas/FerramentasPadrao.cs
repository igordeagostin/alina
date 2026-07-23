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
                "Abre uma pasta existente em uma NOVA janela do VS Code. Passe o caminho absoluto do projeto — " +
                "se o usuário citou o projeto pelo nome, descubra o caminho antes com 'localizar_projeto'. " +
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
                    Tipo = TipoParametroFerramenta.Diretorio,
                },
            ],
        };

        yield return new DefinicaoFerramenta
        {
            Nome = "abrir_url",
            Descricao =
                "Abre um endereço no navegador padrão do usuário. Informe a URL completa (https://...). " +
                "Use para atender pedidos como \"abra o site X\", \"abra meu e-mail\" ou para qualquer página " +
                "cujo endereço você saiba montar — inclusive buscas em sites específicos.",
            ExigeConfirmacao = false,
            Comando = "cmd",
            Argumentos = ["/c", "start", string.Empty, "{url}"],
            Parametros =
            [
                new ParametroFerramenta
                {
                    Nome = "url",
                    Descricao = "Endereço completo a abrir.",
                    Obrigatorio = true,
                    Tipo = TipoParametroFerramenta.Url,
                },
            ],
        };

        yield return new DefinicaoFerramenta
        {
            Nome = "pesquisar_google",
            Descricao =
                "Abre uma pesquisa do Google no navegador, já com o termo preenchido. Apenas ABRE a busca para " +
                "o usuário ver — não devolve os resultados para você. Se precisar LER o conteúdo da web para " +
                "responder algo, delegue ao Claude Code em vez desta ferramenta.",
            ExigeConfirmacao = false,
            Comando = "cmd",
            Argumentos = ["/c", "start", string.Empty, "https://www.google.com/search?q={termo:url}"],
            Parametros =
            [
                new ParametroFerramenta
                {
                    Nome = "termo",
                    Descricao = "O que pesquisar, em texto livre.",
                    Obrigatorio = true,
                    Tipo = TipoParametroFerramenta.Texto,
                },
            ],
        };

        yield return new DefinicaoFerramenta
        {
            Nome = "pesquisar_youtube",
            Descricao =
                "Abre a busca do YouTube no navegador, já com o termo preenchido. Use para pedidos como " +
                "\"procure X no YouTube\". Apenas abre a página de resultados; não toca o vídeo sozinho.",
            ExigeConfirmacao = false,
            Comando = "cmd",
            Argumentos = ["/c", "start", string.Empty, "https://www.youtube.com/results?search_query={termo:url}"],
            Parametros =
            [
                new ParametroFerramenta
                {
                    Nome = "termo",
                    Descricao = "O que procurar no YouTube.",
                    Obrigatorio = true,
                    Tipo = TipoParametroFerramenta.Texto,
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
