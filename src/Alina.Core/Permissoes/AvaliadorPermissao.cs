namespace Alina.Core.Permissoes;

/// <summary>
/// Lógica pura de decisão da política de permissões. Aplica a precedência: regras explícitas do
/// usuário primeiro (autonomia), depois os pisos de segurança (comandos perigosos, caminhos
/// protegidos, fora do workspace), depois as liberações (diretório confiável, só-leitura,
/// comandos allowlisted) e, por fim, o modo padrão.
/// </summary>
public static class AvaliadorPermissao
{
    private static readonly StringComparison Cmp =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public static DecisaoPermissao Avaliar(
        PedidoPermissao pedido,
        PoliticaPermissaoOptions opcoes,
        IReadOnlyList<RegraPermissao> regras)
    {
        // 1-2. Regras explícitas do usuário: deny tem prioridade sobre allow.
        var casadas = regras.Where(r => RegraCasa(r, pedido)).ToList();
        if (casadas.Any(r => !r.Permitir))
        {
            return DecisaoPermissao.Negar;
        }
        if (casadas.Any(r => r.Permitir))
        {
            return DecisaoPermissao.Permitir;
        }

        // 3-4. Pisos de segurança: forçam confirmação mesmo em modo permissivo.
        if (ComandoPerigoso(pedido, opcoes) || CaminhoProtegido(pedido, opcoes))
        {
            return DecisaoPermissao.Perguntar;
        }

        // 5. Fora do workspace (só quando há diretórios confiáveis definidos).
        if (opcoes.ConfirmarForaDoWorkspace && opcoes.DiretoriosConfiaveis.Count > 0 && !SobConfiavel(pedido, opcoes))
        {
            return DecisaoPermissao.Perguntar;
        }

        // 6-8. Liberações automáticas.
        if (SobConfiavel(pedido, opcoes))
        {
            return DecisaoPermissao.Permitir;
        }
        if (FerramentaSomenteLeitura(pedido, opcoes))
        {
            return DecisaoPermissao.Permitir;
        }
        if (ComandoPermitido(pedido, opcoes))
        {
            return DecisaoPermissao.Permitir;
        }

        // 9. Modo padrão.
        return opcoes.ModoPadrao switch
        {
            ModoPermissao.Autonomia => DecisaoPermissao.Permitir,
            ModoPermissao.AceitarEdicoes when EhEdicaoDeArquivo(pedido) => DecisaoPermissao.Permitir,
            _ => DecisaoPermissao.Perguntar,
        };
    }

    public static bool RegraCasa(RegraPermissao regra, PedidoPermissao pedido)
    {
        if (regra.Ferramenta != "*" && !regra.Ferramenta.Equals(pedido.Ferramenta, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(regra.PrefixoComando))
        {
            if (string.IsNullOrWhiteSpace(pedido.Comando) ||
                !pedido.Comando.TrimStart().StartsWith(regra.PrefixoComando.TrimStart(), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(regra.PrefixoCaminho))
        {
            var alvo = ResolverAlvo(pedido);
            if (alvo is null || !EstaSob(alvo, regra.PrefixoCaminho))
            {
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(regra.Diretorio))
        {
            var dir = ResolverDiretorio(pedido);
            if (dir is null || !EstaSob(dir, regra.Diretorio))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ComandoPerigoso(PedidoPermissao pedido, PoliticaPermissaoOptions opcoes)
    {
        if (string.IsNullOrWhiteSpace(pedido.Comando))
        {
            return false;
        }

        var comando = pedido.Comando.ToLowerInvariant();
        return opcoes.ComandosSempreConfirmar.Any(t => comando.Contains(t.ToLowerInvariant(), StringComparison.Ordinal));
    }

    private static bool CaminhoProtegido(PedidoPermissao pedido, PoliticaPermissaoOptions opcoes)
    {
        var alvo = (pedido.Caminho ?? string.Empty).ToLowerInvariant();
        var comando = (pedido.Comando ?? string.Empty).ToLowerInvariant();
        if (alvo.Length == 0 && comando.Length == 0)
        {
            return false;
        }

        return opcoes.CaminhosProtegidos.Any(p =>
        {
            var padrao = p.ToLowerInvariant();
            return alvo.Contains(padrao, StringComparison.Ordinal) || comando.Contains(padrao, StringComparison.Ordinal);
        });
    }

    private static bool SobConfiavel(PedidoPermissao pedido, PoliticaPermissaoOptions opcoes)
    {
        if (opcoes.DiretoriosConfiaveis.Count == 0)
        {
            return false;
        }

        var alvo = ResolverAlvo(pedido) ?? ResolverDiretorio(pedido);
        return alvo is not null && opcoes.DiretoriosConfiaveis.Any(d => EstaSob(alvo, d));
    }

    private static bool FerramentaSomenteLeitura(PedidoPermissao pedido, PoliticaPermissaoOptions opcoes)
        => opcoes.FerramentasSomenteLeitura.Any(f => f.Equals(pedido.Ferramenta, StringComparison.OrdinalIgnoreCase));

    private static bool ComandoPermitido(PedidoPermissao pedido, PoliticaPermissaoOptions opcoes)
    {
        if (string.IsNullOrWhiteSpace(pedido.Comando))
        {
            return false;
        }

        var comando = pedido.Comando.TrimStart();
        return opcoes.ComandosPermitidos.Any(p => comando.StartsWith(p.TrimStart(), StringComparison.OrdinalIgnoreCase));
    }

    private static bool EhEdicaoDeArquivo(PedidoPermissao pedido)
        => !string.IsNullOrWhiteSpace(pedido.Caminho) && string.IsNullOrWhiteSpace(pedido.Comando);

    private static string? ResolverAlvo(PedidoPermissao pedido)
    {
        if (string.IsNullOrWhiteSpace(pedido.Caminho))
        {
            return null;
        }

        var caminho = pedido.Caminho;
        if (!Path.IsPathRooted(caminho) && !string.IsNullOrWhiteSpace(pedido.DiretorioTrabalho))
        {
            caminho = Path.Combine(pedido.DiretorioTrabalho, caminho);
        }

        return NormalizarCaminho(caminho);
    }

    private static string? ResolverDiretorio(PedidoPermissao pedido)
        => string.IsNullOrWhiteSpace(pedido.DiretorioTrabalho) ? null : NormalizarCaminho(pedido.DiretorioTrabalho);

    private static bool EstaSob(string alvo, string diretorio)
    {
        var raiz = NormalizarCaminho(diretorio);
        if (raiz is null)
        {
            return false;
        }

        var a = alvo.TrimEnd(Path.DirectorySeparatorChar);
        var r = raiz.TrimEnd(Path.DirectorySeparatorChar);

        return a.Equals(r, Cmp) || a.StartsWith(r + Path.DirectorySeparatorChar, Cmp);
    }

    private static string? NormalizarCaminho(string caminho)
    {
        try
        {
            return Path.GetFullPath(caminho);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }
}
