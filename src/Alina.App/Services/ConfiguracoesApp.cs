namespace Alina.App.Services;

/// <summary>
/// Preferências do aplicativo que o usuário pode ajustar pela tela de
/// configurações e que são persistidas entre sessões. O autostart não mora
/// aqui: sua fonte de verdade é o registro do Windows (<see cref="Autostart"/>).
/// </summary>
public sealed class ConfiguracoesApp
{
    /// <summary>Abrir a janela em modo compacto (voz em destaque) ao iniciar.</summary>
    public bool AbrirEmModoCompacto { get; set; } = true;

    /// <summary>Falar as respostas em voz alta quando a interação for por voz.</summary>
    public bool FalarRespostas { get; set; } = true;

    /// <summary>Atender quando o nome "Alina" for dito (palavra de ativação), além da hotkey.</summary>
    public bool EscutarPalavraAtivacao { get; set; }

    /// <summary>Voz do TTS. Ex.: "nova", "alloy", "shimmer".</summary>
    public string Voz { get; set; } = "nova";

    /// <summary>Velocidade da fala (1.0 = normal).</summary>
    public double VelocidadeFala { get; set; } = 1.0;
}
