namespace Alina.Core.Ferramentas;

/// <summary>
/// Valida os valores informados pelo LLM antes de a ferramenta disparar o processo.
/// Parâmetros de caminho só passam se apontarem para algo que existe no disco: um
/// nome inventado ("diario api") vira erro instrutivo, não um comando executado.
/// </summary>
internal static class ValidadorParametro
{
    private static readonly HashSet<string> NomesDeDiretorio = new(StringComparer.OrdinalIgnoreCase)
    {
        "caminho", "pasta", "diretorio", "diretório", "projeto", "repositorio", "repositório",
        "workspace", "path", "folder", "dir", "cwd",
    };

    private static readonly HashSet<string> NomesDeArquivo = new(StringComparer.OrdinalIgnoreCase)
    {
        "arquivo", "file", "script", "solucao", "solução",
    };

    /// <summary>Tipo realmente aplicado — resolve <see cref="TipoParametroFerramenta.Automatico"/> pelo nome.</summary>
    public static TipoParametroFerramenta TipoEfetivo(ParametroFerramenta parametro)
        => parametro.Tipo == TipoParametroFerramenta.Automatico ? Inferir(parametro.Nome) : parametro.Tipo;

    /// <summary>Devolve a mensagem de erro quando o valor é inválido, ou <c>null</c> quando está tudo certo.</summary>
    public static string? Validar(ParametroFerramenta parametro, string valor, string? diretorioTrabalho)
    {
        TipoParametroFerramenta tipo = TipoEfetivo(parametro);

        if (tipo == TipoParametroFerramenta.Texto || string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        string alvo = Resolver(valor, diretorioTrabalho);

        if (tipo == TipoParametroFerramenta.Diretorio)
        {
            if (Directory.Exists(alvo))
            {
                return null;
            }

            string detalhe = File.Exists(alvo)
                ? "esse caminho é um arquivo, não uma pasta"
                : "essa pasta não existe no disco";

            return $"Erro: o parâmetro '{parametro.Nome}' recebeu \"{valor}\" e {detalhe}. Nada foi executado. " +
                "Não invente caminhos: descubra o caminho absoluto real com 'localizar_projeto' (busca pelo nome) " +
                "ou 'listar_diretorio' e chame a ferramenta de novo.";
        }

        if (tipo == TipoParametroFerramenta.Arquivo && !File.Exists(alvo))
        {
            string detalhe = Directory.Exists(alvo)
                ? "esse caminho é uma pasta, não um arquivo"
                : "esse arquivo não existe no disco";

            return $"Erro: o parâmetro '{parametro.Nome}' recebeu \"{valor}\" e {detalhe}. Nada foi executado. " +
                "Confirme o caminho absoluto com 'listar_diretorio' antes de chamar a ferramenta de novo.";
        }

        return null;
    }

    private static TipoParametroFerramenta Inferir(string nome)
    {
        if (NomesDeDiretorio.Contains(nome))
        {
            return TipoParametroFerramenta.Diretorio;
        }

        return NomesDeArquivo.Contains(nome) ? TipoParametroFerramenta.Arquivo : TipoParametroFerramenta.Texto;
    }

    private static string Resolver(string valor, string? diretorioTrabalho)
    {
        string limpo = Environment.ExpandEnvironmentVariables(valor.Trim().Trim('"'));

        try
        {
            return string.IsNullOrWhiteSpace(diretorioTrabalho) || Path.IsPathFullyQualified(limpo)
                ? limpo
                : Path.GetFullPath(limpo, diretorioTrabalho);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return limpo;
        }
    }
}
