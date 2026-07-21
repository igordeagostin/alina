namespace Alina.Core.Permissoes;

/// <summary>Implementação simples e thread-safe de <see cref="IContextoPermissao"/>.</summary>
public sealed class ContextoPermissao : IContextoPermissao
{
    private volatile string? _diretorioAtual;

    public string? DiretorioAtual
    {
        get => _diretorioAtual;
        set => _diretorioAtual = value;
    }
}
