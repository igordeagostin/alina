namespace Alina.Voice;

/// <summary>Configuração da camada de voz (seção "Voice" da configuração).</summary>
public sealed class VoiceOptions
{
    public const string SectionName = "Voice";

    /// <summary>Habilita o modo voz (comando /voz no console).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Chave da OpenAI para áudio. Se vazio, reutiliza a chave do LLM (Llm:ApiKey).</summary>
    public string? ApiKey { get; set; }

    /// <summary>Modelo de transcrição (STT). Ex: "gpt-4o-mini-transcribe" ou "whisper-1".</summary>
    public string SttModel { get; set; } = "gpt-4o-mini-transcribe";

    /// <summary>Idioma esperado na transcrição (ISO-639-1), melhora a acurácia. Ex: "pt".</summary>
    public string Language { get; set; } = "pt";

    /// <summary>
    /// Dica de contexto passada ao modelo de transcrição para enviesar o reconhecimento
    /// do vocabulário técnico (evita trocas como "API" → "aqui"). Deve ser uma frase no
    /// mesmo idioma e estilo do que costuma ser dito. Vazio desliga a dica.
    /// </summary>
    public string PromptTranscricao { get; set; } =
        "Comandos de voz para uma assistente de desenvolvimento de software. Vocabulário " +
        "técnico frequente: API, VS Code, deploy, endpoint, commit, branch, pull request, " +
        "front-end, back-end, banco de dados, build, log, token.";

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
    /// Permite falar por cima: enquanto a Alina responde, o microfone segue escutando e
    /// basta você falar para a voz dela calar na hora e a palavra voltar a ser sua. Nada
    /// do que ela estiver executando é cancelado por isso — para cancelar de verdade só
    /// com uma das <see cref="PalavrasInterrupcao"/>. Funciona melhor com fones de ouvido:
    /// em caixas de som o microfone capta a própria voz dela.
    /// </summary>
    public bool InterromperPorVoz { get; set; } = true;

    /// <summary>
    /// Termos que mandam a Alina parar de verdade o que está fazendo, reconhecidos no
    /// texto transcrito quando você diz só isso e mais nada. Evite palavras comuns em
    /// pedidos normais para não cancelar trabalho sem querer.
    /// </summary>
    public string[] PalavrasInterrupcao { get; set; } = ["espera", "espere", "chega", "cancela", "esquece"];

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
