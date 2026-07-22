namespace Alina.App.Services;

/// <summary>
/// Largura e altura memorizadas da janela principal, em unidades independentes
/// de DPI (as mesmas do WPF), para um dos modos de exibição.
/// </summary>
public sealed class TamanhoJanela
{
    public double Largura { get; set; }

    public double Altura { get; set; }
}
