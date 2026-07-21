namespace Alina.Core.Tools;

/// <summary>
/// Servidor local que atende os pedidos de permissão do Claude Code em modo headless
/// (<c>--permission-prompt-tool</c>). Cada pedido é encaminhado ao
/// <see cref="IConfirmationService"/>, permitindo que a Alina pergunte ao usuário (por voz,
/// UI ou console) no meio da execução — em vez de bloquear silenciosamente.
/// </summary>
public interface IServidorPermissao : IAsyncDisposable
{
    /// <summary>Indica se o servidor já está no ar.</summary>
    bool Ativo { get; }

    /// <summary>URL do endpoint MCP (ex.: <c>http://127.0.0.1:PORT/mcp</c>). Nulo antes de iniciar.</summary>
    string? Url { get; }

    /// <summary>Chave do servidor em <c>mcpServers</c> (ex.: <c>permissoes</c>).</summary>
    string NomeServidor { get; }

    /// <summary>Nome completo da ferramenta de permissão (ex.: <c>mcp__permissoes__aprovar</c>).</summary>
    string NomeFerramenta { get; }

    /// <summary>Inicia o servidor (idempotente) e devolve a URL do endpoint MCP.</summary>
    Task<string> IniciarAsync(CancellationToken cancellationToken = default);
}
