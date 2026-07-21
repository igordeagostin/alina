using Alina.Core.Tools;

namespace Alina.Console;

/// <summary>
/// Roteia a confirmação para voz ou console conforme o modo ativo. No modo texto usa o
/// <see cref="ConsoleConfirmationService"/>; quando o usuário está conversando por voz,
/// a Alina pergunta e ouve a resposta (via serviço de voz), caindo no console se a voz
/// ainda não estiver disponível.
/// </summary>
public sealed class ConfirmacaoRoteada : IConfirmationService
{
    private readonly IConfirmationService _console;
    private IConfirmationService? _voz;

    public ConfirmacaoRoteada(IConfirmationService console) => _console = console;

    /// <summary>Quando <c>true</c>, as confirmações passam a ser faladas/ouvidas.</summary>
    public bool ModoVoz { get; set; }

    /// <summary>Define o serviço de confirmação por voz (criado sob demanda pela camada de voz).</summary>
    public void DefinirVoz(IConfirmationService voz) => _voz = voz;

    public Task<bool> ConfirmAsync(string action, string? details = null, CancellationToken cancellationToken = default)
        => (ModoVoz && _voz is not null ? _voz : _console).ConfirmAsync(action, details, cancellationToken);
}
