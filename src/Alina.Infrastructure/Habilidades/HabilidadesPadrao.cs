using Alina.Core.Habilidades;

namespace Alina.Infrastructure.Habilidades;

/// <summary>
/// Habilidades que já nascem com a Alina, semeadas na pasta de dados na primeira
/// execução. São documentos editáveis como quaisquer outros: o usuário pode ajustar
/// ou apagar sem que voltem.
/// </summary>
internal static class HabilidadesPadrao
{
    public static IEnumerable<Habilidade> Todas()
    {
        yield return new Habilidade
        {
            Nome = "navegador",
            Descricao = "Como abrir sites, buscas e vídeos no navegador do usuário, e o que fazer quando o pedido exige LER a web.",
            Conteudo = Navegador,
        };
    }

    private const string Navegador = """
        Use quando o usuário pedir para abrir um site, pesquisar algo, ver um vídeo, ou
        qualquer coisa que termine numa página aberta na tela dele.

        ## Ferramentas

        - `abrir_url` — abre qualquer endereço. É a ferramenta principal: se você sabe montar
          a URL, não precisa de nada além dela.
        - `pesquisar_google` — atalho para a busca do Google.
        - `pesquisar_youtube` — atalho para a busca do YouTube.

        Todas apenas ABREM a página. Nenhuma delas devolve o conteúdo para você.

        ## Endereços úteis

        Monte a URL e chame `abrir_url`. O termo de busca vai codificado — quando a ferramenta
        tiver um parâmetro próprio para ele, passe o texto puro e deixe a codificação com ela;
        quando você mesmo montar a URL, codifique espaços e acentos.

        | Intenção | Endereço |
        | --- | --- |
        | Vídeo específico no YouTube | `https://www.youtube.com/results?search_query=TERMO` e diga ao usuário para escolher |
        | Tradução | `https://translate.google.com/?sl=auto&tl=pt&text=TERMO` |
        | Mapa / rota | `https://www.google.com/maps/search/TERMO` |
        | Repositório no GitHub | `https://github.com/search?q=TERMO` |
        | Pacote NuGet | `https://www.nuget.org/packages?q=TERMO` |
        | Documentação .NET | `https://learn.microsoft.com/pt-br/search/?terms=TERMO` |
        | Stack Overflow | `https://stackoverflow.com/search?q=TERMO` |
        | E-mail, agenda, drive | `https://mail.google.com`, `https://calendar.google.com`, `https://drive.google.com` |

        Para um site que o usuário citou pelo nome ("abra o Nubank"), monte o domínio óbvio
        (`https://nubank.com.br`). Se não tiver certeza do domínio, prefira uma busca no Google
        a chutar um endereço que pode não existir — ou pergunte.

        ## Limite importante: você abre, mas não vê

        A tela é do usuário; o resultado não volta para você. Então:

        - "Abra o YouTube e procure X" → use a ferramenta. Perfeito para o caso.
        - "Que horas começa o jogo?", "resuma essa página", "o que dizem sobre X" → abrir uma
          busca NÃO responde. Você precisa do conteúdo, e para isso delegue ao Claude Code
          (`delegar_claude_code`), que tem acesso de leitura à web. Diga na tarefa exatamente
          o que você quer saber.

        Não finja ter lido o que você só abriu. Se abriu uma busca sem ler os resultados, diga
        que abriu — não invente o que apareceu na tela.

        ## O que não dá para fazer

        Clicar, preencher formulário, rolar a página, fazer login, extrair dados de dentro do
        site: nada disso é possível hoje. A Alina lança o navegador e para por aí. Se o usuário
        pedir esse tipo de automação, diga com clareza que ainda não é suportado, e ofereça o
        que dá: abrir a página no ponto certo para ele terminar à mão.
        """;
}
