using Alina.Core.Orchestration;
using Alina.Voice;

using SysConsole = System.Console;

namespace Alina.Console;

/// <summary>
/// Modo de conversa por voz (push-to-talk): grava o microfone, transcreve,
/// envia ao orquestrador e responde por voz.
/// </summary>
public sealed class VoiceChat
{
    private readonly IOrchestrator _orchestrator;
    private readonly ISpeechToText _stt;
    private readonly ITextToSpeech _tts;
    private readonly IAudioRecorder _recorder;
    private readonly IAudioPlayer _player;

    public VoiceChat(
        IOrchestrator orchestrator,
        ISpeechToText stt,
        ITextToSpeech tts,
        IAudioRecorder recorder,
        IAudioPlayer player)
    {
        _orchestrator = orchestrator;
        _stt = stt;
        _tts = tts;
        _recorder = recorder;
        _player = player;
    }

    /// <summary>Executa o sub-loop de voz até o usuário digitar /texto ou /sair.</summary>
    public async Task RunAsync()
    {
        WriteLine("🎙  Modo voz ativado (push-to-talk).", ConsoleColor.Magenta);
        WriteLine("   Enter para gravar, Enter de novo para parar. Digite /texto para voltar ao teclado.", ConsoleColor.DarkGray);

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
