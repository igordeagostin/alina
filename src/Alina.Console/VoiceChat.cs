using Alina.Core.Orchestration;
using Alina.Voice;

using SysConsole = System.Console;

namespace Alina.Console;

/// <summary>
/// Modo de conversa por voz (push-to-talk): grava o microfone, transcreve,
/// envia ao orquestrador e responde por voz. Enquanto ativo, roteia as confirmações
/// de segurança para a voz (a Alina pergunta e ouve a resposta).
/// </summary>
public sealed class VoiceChat
{
    private readonly IOrchestrator _orchestrator;
    private readonly ISpeechToText _stt;
    private readonly ITextToSpeech _tts;
    private readonly IAudioRecorder _recorder;
    private readonly IAudioPlayer _player;
    private readonly VoiceOptions _options;
    private readonly ConfirmacaoRoteada _confirmacao;

    public VoiceChat(
        IOrchestrator orchestrator,
        ISpeechToText stt,
        ITextToSpeech tts,
        IAudioRecorder recorder,
        IAudioPlayer player,
        VoiceOptions options,
        ConfirmacaoRoteada confirmacao)
    {
        _orchestrator = orchestrator;
        _stt = stt;
        _tts = tts;
        _recorder = recorder;
        _player = player;
        _options = options;
        _confirmacao = confirmacao;
    }

    /// <summary>Executa o sub-loop de voz até o usuário digitar /texto ou /sair.</summary>
    public async Task RunAsync()
    {
        WriteLine("🎙  Modo voz ativado (push-to-talk).", ConsoleColor.Magenta);
        WriteLine("   Enter para gravar, Enter de novo para parar. Digite /texto para voltar ao teclado.", ConsoleColor.DarkGray);

        // Confirmações passam a ser faladas/ouvidas enquanto o modo voz estiver ativo;
        // o console continua como fallback se a voz não compreender.
        var vozConfirmacao = new VozConfirmationService(_tts, _stt, _recorder, _player, _options, fallback: new ConsoleConfirmationService());
        _confirmacao.DefinirVoz(vozConfirmacao);
        _confirmacao.ModoVoz = true;
        try
        {
            await LoopAsync();
        }
        finally
        {
            _confirmacao.ModoVoz = false;
        }
    }

    private async Task LoopAsync()
    {
        while (true)
        {
            Write("\n[voz] Enter para gravar (ou /texto): ", ConsoleColor.Cyan);
            var command = SysConsole.ReadLine();

            if (command is null)
            {
                return;
            }

            var trimmed = command.Trim().ToLowerInvariant();
            if (trimmed is "/texto" or "/sair" or "/exit")
            {
                WriteLine("Voltando ao modo texto.", ConsoleColor.DarkGray);
                return;
            }

            try
            {
                await HandleTurnAsync();
            }
            catch (Exception ex)
            {
                WriteLine($"[erro no modo voz] {ex.Message}", ConsoleColor.Red);
            }
        }
    }

    private async Task HandleTurnAsync()
    {
        WriteLine("🔴 Gravando... (Enter para parar)", ConsoleColor.Red);

        var audio = await _recorder.RecordAsync(_ => Task.Run(() => SysConsole.ReadLine()));

        Write("⏳ Transcrevendo... ", ConsoleColor.DarkGray);
        var text = await _stt.TranscribeAsync(audio);

        if (string.IsNullOrWhiteSpace(text))
        {
            WriteLine("(não entendi nada, tente de novo)", ConsoleColor.DarkGray);
            return;
        }

        WriteLine($"\nVocê (voz)> {text}", ConsoleColor.Cyan);

        Write("Alina> ", ConsoleColor.Green);
        var response = await _orchestrator.SendAsync(text);
        SysConsole.WriteLine(response);

        Write("🔊 Falando...", ConsoleColor.DarkGray);
        var mp3 = await _tts.SynthesizeAsync(response);
        await _player.PlayMp3Async(mp3);
        SysConsole.WriteLine();
    }

    private static void Write(string text, ConsoleColor color)
    {
        var previous = SysConsole.ForegroundColor;
        SysConsole.ForegroundColor = color;
        SysConsole.Write(text);
        SysConsole.ForegroundColor = previous;
    }

    private static void WriteLine(string text, ConsoleColor color)
    {
        var previous = SysConsole.ForegroundColor;
        SysConsole.ForegroundColor = color;
        SysConsole.WriteLine(text);
        SysConsole.ForegroundColor = previous;
    }
}
