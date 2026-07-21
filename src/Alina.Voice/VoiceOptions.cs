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
    /// diferenciar maiúsculas. Inclua variações fonéticas para reduzir falhas de detecção.
    /// </summary>
    public string[] PalavrasAtivacao { get; set; } = ["alina"];
}
