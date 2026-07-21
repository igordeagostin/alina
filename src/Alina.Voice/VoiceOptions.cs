namespace Alina.Voice;

/// <summary>Configuração da camada de voz (seção "Voice" da configuração).</summary>
public sealed class VoiceOptions
{
    public const string SectionName = "Voice";

    /// <summary>Habilita o modo voz (comando /voz no console).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Chave da OpenAI para áudio. Se vazio, reutiliza a chave do LLM (Llm:ApiKey).</summary>
    public string? ApiKey { get; set; }

    /// <summary>Modelo de transcrição (STT). Ex: "whisper-1" ou "gpt-4o-mini-transcribe".</summary>
    public string SttModel { get; set; } = "whisper-1";

    /// <summary>Idioma esperado na transcrição (ISO-639-1), melhora a acurácia. Ex: "pt".</summary>
    public string Language { get; set; } = "pt";

    /// <summary>Modelo de síntese de fala (TTS). Ex: "gpt-4o-mini-tts" ou "tts-1".</summary>
    public string TtsModel { get; set; } = "gpt-4o-mini-tts";

    /// <summary>Voz do TTS. Ex: "alloy", "nova", "shimmer", "coral", "sage".</summary>
    public string Voice { get; set; } = "nova";

    /// <summary>Velocidade da fala (1.0 = normal).</summary>
    public float Speed { get; set; } = 1.0f;

    /// <summary>
    /// Caminho da pasta do modelo Vosk (descompactado) usado para detectar a palavra
    /// de ativação localmente. Ex.: um modelo pequeno de português. Vazio desliga a detecção.
    /// </summary>
    public string? CaminhoModeloVosk { get; set; }

    /// <summary>
    /// Palavras/variações que acionam a assistente ao serem ditas. Comparação sem
    /// diferenciar maiúsculas. Como o Vosk (modelo pequeno) raramente transcreve o
    /// nome próprio "alina" de forma exata, incluímos as variações fonéticas que ele
    /// costuma produzir. Também alimentam a gramática restrita do reconhecedor.
    /// </summary>
    public string[] PalavrasAtivacao { get; set; } = ["alina", "aline", "malina", "adelina", "elina"];

    /// <summary>
    /// Silêncio (em segundos) que encerra automaticamente a gravação depois que o
    /// usuário falou, mandando processar sem precisar de clique. Valor menor ou
    /// igual a zero desliga o corte automático (encerra só pelo orbe/hotkey).
    /// </summary>
    public double SegundosSilencioParaEncerrar { get; set; } = 1.5;

    /// <summary>
    /// Conversa contínua: depois de responder, a Alina volta a ouvir sozinha, sem
    /// exigir que se diga "Alina" de novo a cada frase (como o modo voz do ChatGPT).
    /// </summary>
    public bool ConversaContinua { get; set; } = true;

    /// <summary>
    /// Quanto tempo (segundos) a Alina continua ouvindo, aguardando você voltar a
    /// falar, antes de "dormir" e voltar a exigir a palavra de ativação.
    /// </summary>
    public double SegundosJanelaConversa { get; set; } = 10;

    /// <summary>Duração (segundos) da janela de captura da resposta numa confirmação por voz.</summary>
    public int SegundosRespostaConfirmacao { get; set; } = 6;

    /// <summary>Quantas vezes repetir a pergunta quando a resposta não é compreendida.</summary>
    public int TentativasConfirmacaoVoz { get; set; } = 2;

    /// <summary>Termos interpretados como "sim" numa confirmação por voz (sem acento/maiúsculas).</summary>
    public string[] PalavrasSim { get; set; } =
        ["sim", "pode", "prossiga", "prossegue", "confirmo", "confirmado", "confirmar",
         "autorizo", "autorizado", "claro", "positivo", "ok", "okei", "vai", "manda", "beleza", "isso"];

    /// <summary>Termos interpretados como "não" numa confirmação por voz (sem acento/maiúsculas).</summary>
    public string[] PalavrasNao { get; set; } =
        ["nao", "negativo", "cancela", "cancelar", "cancelado", "para", "pare", "pode nao",
         "melhor nao", "nunca", "recuso", "negado"];
}
