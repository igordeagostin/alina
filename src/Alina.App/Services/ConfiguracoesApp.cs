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

    /// <summary>Deixar cortá-la no meio: falar durante a resposta cancela o turno em andamento.</summary>
    public bool InterromperPorVoz { get; set; } = true;

    /// <summary>Termos que interrompem a Alina enquanto ela pensa ou fala, além do próprio nome.</summary>
    public List<string> PalavrasInterrupcao { get; set; } = ["espera", "espere", "chega", "cancela", "esquece"];

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

    /// <summary>Reabrir a janela no último tamanho deixado pelo usuário em cada modo.</summary>
    public bool LembrarTamanhoJanela { get; set; } = true;

    /// <summary>Último tamanho usado no modo compacto; nulo enquanto o padrão não for alterado.</summary>
    public TamanhoJanela? JanelaCompacta { get; set; }

    /// <summary>Último tamanho usado no modo detalhado; nulo enquanto o padrão não for alterado.</summary>
    public TamanhoJanela? JanelaDetalhada { get; set; }

    /// <summary>
    /// Mostrar o mini player flutuante quando a Alina for acionada com a janela
    /// oculta (iniciada com o Windows ou fechada para a bandeja).
    /// </summary>
    public bool MostrarMiniPlayer { get; set; } = true;

    /// <summary>Canto da tela onde o mini player aparece.</summary>
    public CantoTela CantoMiniPlayer { get; set; } = CantoTela.InferiorDireito;

    /// <summary>Exibir no mini player a última fala reconhecida e a resposta.</summary>
    public bool MiniPlayerMostraTexto { get; set; } = true;

    /// <summary>Tempo (segundos) que o mini player continua visível após o fim do diálogo.</summary>
    public double SegundosMiniPlayerVisivel { get; set; } = 5;
}
