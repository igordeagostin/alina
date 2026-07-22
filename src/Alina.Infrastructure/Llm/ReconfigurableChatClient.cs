using Alina.Infrastructure.Configuration;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Alina.Infrastructure.Llm;

/// <summary>
/// <see cref="IChatClient"/> que permite trocar modelo, provedor e chave em runtime
/// (pela tela de configurações) sem reiniciar o app. Delega para um cliente interno
/// reconstruído a cada <see cref="Reconfigurar"/>; a troca só vale entre turnos.
/// </summary>
public sealed class ReconfigurableChatClient : IChatClient
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly Lock _sync = new();
    private IChatClient _inner;

    public ReconfigurableChatClient(LlmOptions options, ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        _inner = LlmClientFactory.Create(options, loggerFactory);
    }

    /// <summary>
    /// Reconstrói o cliente interno com as novas opções. Se a construção falhar
    /// (ex.: chave inválida), lança e mantém o cliente anterior intacto.
    /// </summary>
    public void Reconfigurar(LlmOptions options)
    {
        IChatClient novo = LlmClientFactory.Create(options, _loggerFactory);
        IChatClient antigo;
        lock (_sync)
        {
            antigo = _inner;
            _inner = novo;
        }

        antigo.Dispose();
    }

    private IChatClient Atual
    {
        get { lock (_sync) return _inner; }
    }

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        => Atual.GetResponseAsync(messages, options, cancellationToken);

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        => Atual.GetStreamingResponseAsync(messages, options, cancellationToken);

    public object? GetService(Type serviceType, object? serviceKey = null)
        => Atual.GetService(serviceType, serviceKey);

    public void Dispose() => Atual.Dispose();
}
