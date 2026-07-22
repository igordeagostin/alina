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

    /// <summary>Silêncio (segundos) que encerra a gravação por voz automaticamente.</summary>
    public double SegundosSilencioParaEncerrar { get; set; } = 1.5;

    /// <summary>Manter a conversa fluída: voltar a ouvir sozinha após responder.</summary>
    public bool ConversaContinua { get; set; } = true;

    /// <summary>Tempo (segundos) que a Alina segue ouvindo antes de dormir na conversa contínua.</summary>
    public double SegundosJanelaConversa { get; set; } = 10;

    /// <summary>Escala do texto da interface (1.0 = padrão). Multiplica o tamanho-base da fonte.</summary>
    public double EscalaInterface { get; set; } = 1.0;
}
