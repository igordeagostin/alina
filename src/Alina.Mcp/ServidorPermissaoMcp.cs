using Alina.Core.Tools;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Alina.Mcp;

/// <summary>
/// Servidor MCP HTTP hospedado dentro do próprio processo da Alina, em <c>127.0.0.1</c>, que
/// atende os pedidos de permissão do Claude Code (<c>--permission-prompt-tool</c>). Como roda
/// no mesmo processo, o handler tem acesso direto ao <see cref="IConfirmationService"/> — sem IPC.
/// </summary>
public sealed class ServidorPermissaoMcp : IServidorPermissao
{
    private readonly IConfirmationService _confirmacao;
    private readonly int _porta;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private WebApplication? _app;
    private string? _url;

    /// <param name="porta">Porta TCP em 127.0.0.1; use <c>0</c> para uma porta efêmera livre.</param>
    public ServidorPermissaoMcp(IConfirmationService confirmacao, int porta = 0)
    {
        _confirmacao = confirmacao;
        _porta = porta;
    }

    public bool Ativo => _app is not null;

    public string? Url => _url;

    public string NomeServidor => "permissoes";

    public string NomeFerramenta => "mcp__permissoes__aprovar";

    public async Task<string> IniciarAsync(CancellationToken cancellationToken = default)
    {
        if (_url is not null)
        {
            return _url;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_url is not null)
            {
                return _url;
            }

            var builder = WebApplication.CreateSlimBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls($"http://127.0.0.1:{_porta}");
            builder.Services.AddSingleton(_confirmacao);
            builder.Services.AddMcpServer().WithHttpTransport().WithTools<FerramentaPermissao>();

            var app = builder.Build();
            app.MapMcp("/mcp");

            await app.StartAsync(cancellationToken);

            var enderecos = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>();
            var baseUrl = enderecos?.Addresses.FirstOrDefault()
                ?? throw new InvalidOperationException("Não foi possível determinar o endereço do servidor de permissão.");

            _url = baseUrl.TrimEnd('/') + "/mcp";
            _app = app;
            return _url;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
            _app = null;
            _url = null;
        }

        _gate.Dispose();
    }
}
