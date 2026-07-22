using Microsoft.Extensions.AI;

namespace Alina.Infrastructure.Llm;

/// <summary>
/// Fachada de <see cref="IChatClient"/> cujo cliente interno pode ser trocado em runtime
/// (pela tela de configurações) sem reiniciar o app. Como a referência da fachada não muda,
/// quem já a recebeu por injeção passa a usar o novo cliente sozinho; a troca só vale
/// entre turnos. Quem constrói o cliente interno é o chamador — pode vir do
/// <see cref="LlmClientFactory"/> ou de qualquer outra origem, como o CLI do Claude Code.
/// </summary>
public sealed class ReconfigurableChatClient : IChatClient
{
    private readonly Lock _sync = new();
    private IChatClient _inner;

    public ReconfigurableChatClient(IChatClient inicial) => _inner = inicial;

    /// <summary>Assume o novo cliente e descarta o anterior.</summary>
    public void Reconfigurar(IChatClient novo)
    {
        IChatClient antigo;
        lock (_sync)
        {
            if (ReferenceEquals(novo, _inner))
            {
                return;
            }

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
