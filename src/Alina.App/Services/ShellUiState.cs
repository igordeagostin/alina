namespace Alina.App.Services;

/// <summary>
/// Estado de UI compartilhado entre o shell Blazor e a janela WPF. Controla a
/// alternância entre o modo compacto (voz em destaque) e o modo detalhado
/// (chat completo), permitindo que a janela seja redimensionada de acordo.
/// </summary>
public sealed class ShellUiState
{
    /// <summary>Modo padrão é o compacto (voz em primeiro plano).</summary>
    public bool Compacto { get; private set; } = true;

    /// <summary>Disparado sempre que o modo muda.</summary>
    public event Action? ModoAlterado;

    public void DefinirModo(bool compacto)
    {
        if (Compacto == compacto)
        {
            return;
        }

        Compacto = compacto;
        ModoAlterado?.Invoke();
    }

    public void Alternar() => DefinirModo(!Compacto);
}
