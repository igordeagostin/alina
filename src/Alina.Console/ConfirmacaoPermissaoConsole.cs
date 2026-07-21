using Alina.Core.Permissoes;

namespace Alina.Console;

/// <summary>
/// Confirmação de permissão no console com opções de escopo: uma vez, sempre, ou sempre neste
/// diretório. Escolher "sempre" faz a política aprender uma regra e parar de perguntar.
/// </summary>
public sealed class ConfirmacaoPermissaoConsole : IConfirmacaoPermissao
{
    public Task<RespostaConfirmacaoPermissao> ConfirmarAsync(PedidoPermissao pedido, CancellationToken cancellationToken = default)
    {
        var previous = System.Console.ForegroundColor;
        System.Console.ForegroundColor = ConsoleColor.Yellow;
        System.Console.WriteLine();
        System.Console.WriteLine($"⚠  Permissão solicitada pelo Claude Code");
        System.Console.WriteLine($"   {pedido.Descricao}");
        var dir = pedido.DiretorioTrabalho is null ? "este diretório" : pedido.DiretorioTrabalho;
        System.Console.Write($"Permitir? [1] uma vez  [2] sempre  [3] sempre em {dir}  [N] não: ");
        System.Console.ForegroundColor = previous;

        var resposta = (System.Console.ReadLine() ?? string.Empty).Trim().ToLowerInvariant();

        var decisao = resposta switch
        {
            "1" or "s" or "sim" or "y" => RespostaConfirmacaoPermissao.PermitidaUmaVez,
            "2" or "sempre" => new RespostaConfirmacaoPermissao(true, EscopoPermissao.Sempre),
            "3" => new RespostaConfirmacaoPermissao(true, EscopoPermissao.SempreNesteDiretorio),
            _ => RespostaConfirmacaoPermissao.Negada,
        };

        return Task.FromResult(decisao);
    }
}
