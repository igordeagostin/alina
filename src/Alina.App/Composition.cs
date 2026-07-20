using Alina.App.Services;
using Alina.Core.Orchestration;
using Alina.Core.Tools;
using Alina.Infrastructure.Configuration;
using Alina.Infrastructure.DependencyInjection;
using Alina.Tools;
using Alina.Tools.Background;
using Alina.Tools.ClaudeCode;
using Alina.Tools.Git;
using Alina.Tools.Memory;
using Alina.Tools.Plugins;
using Alina.Voice;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using System.ClientModel;
using System.IO;

namespace Alina.App;

/// <summary>
/// Composição de dependências do head gráfico. Reaproveita exatamente a mesma
/// montagem do <c>Alina.Console</c>, trocando apenas as implementações que
/// dependem de UI: a confirmação passa a ser gráfica (<see cref="GuiConfirmationService"/>).
/// </summary>
public static class Composition
{
    public static IHost BuildHost()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.Configuration.AddUserSecrets(typeof(Composition).Assembly, optional: true);

        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        // Núcleo (LLM, memória, orquestrador)
        builder.Services.AddAlina(builder.Configuration);

        // Serviços do BlazorWebView (WPF)
        builder.Services.AddWpfBlazorWebView();
#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
#endif

        // Confirmação gráfica (substitui o ConsoleConfirmationService)
        var confirmation = new GuiConfirmationService();
        builder.Services.AddSingleton<IConfirmationService>(confirmation);

        // Estado de UI compartilhado (modo compacto/detalhado)
        builder.Services.AddSingleton<ShellUiState>();

        // Tools básicas
        builder.Services.AddSingleton<ITool, TerminalTool>();
        builder.Services.AddSingleton<ITool, FileReadTool>();

        // Claude Code (Fase 3)
        var claudeCodeOptions = builder.Configuration.GetSection("ClaudeCode").Get<ClaudeCodeOptions>() ?? new ClaudeCodeOptions();
        builder.Services.AddSingleton(claudeCodeOptions);
        builder.Services.AddSingleton<ClaudeCodeTool>();
        builder.Services.AddSingleton<ITool>(sp => sp.GetRequiredService<ClaudeCodeTool>());

        // Execução em background (Fase 6)
        builder.Services.AddSingleton<IBackgroundTaskManager, BackgroundTaskManager>();
        builder.Services.AddSingleton<ITool, DelegateInBackgroundTool>();
        builder.Services.AddSingleton<ITool, ListTasksTool>();

        // Tools de Git (Fase 5)
        var gitOptions = builder.Configuration.GetSection(GitOptions.SectionName).Get<GitOptions>() ?? new GitOptions();
        builder.Services.AddSingleton(gitOptions);
        builder.Services.AddSingleton<ITool, GitStatusTool>();
        builder.Services.AddSingleton<ITool, GitDiffTool>();
        builder.Services.AddSingleton<ITool, GitLogTool>();
        builder.Services.AddSingleton<ITool, GitCommitTool>();
        builder.Services.AddSingleton<ITool, GitBranchTool>();

        // Memória permanente (Fase 6)
        builder.Services.AddSingleton<ITool, RememberTool>();
        builder.Services.AddSingleton<ITool, RecallTool>();
        builder.Services.AddSingleton<ITool, ForgetTool>();

        // Voz (Fase 2) — STT/TTS OpenAI + captura/reprodução NAudio
        var voiceOptions = builder.Configuration.GetSection(VoiceOptions.SectionName).Get<VoiceOptions>() ?? new VoiceOptions();
        builder.Services.AddSingleton(voiceOptions);
        builder.Services.AddSingleton<IAudioRecorder, NAudioRecorder>();
        builder.Services.AddSingleton<IAudioPlayer, NAudioPlayer>();
        builder.Services.AddSingleton(sp =>
        {
            var llm = sp.GetRequiredService<IOptions<LlmOptions>>().Value;
            var key = string.IsNullOrWhiteSpace(voiceOptions.ApiKey) ? llm.ApiKey : voiceOptions.ApiKey;
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new InvalidOperationException("Chave da OpenAI não configurada para voz (Voice:ApiKey ou Llm:ApiKey).");
            }

            return new OpenAIClient(new ApiKeyCredential(key));
        });
        builder.Services.AddSingleton<ISpeechToText, OpenAISpeechToText>();
        builder.Services.AddSingleton<ITextToSpeech, OpenAITextToSpeech>();

        // Plugins declarativos (Fase 6)
        var pluginsDir = builder.Configuration.GetValue<string>("Plugins:Directory");
        if (string.IsNullOrWhiteSpace(pluginsDir))
        {
            pluginsDir = Path.Combine(AppContext.BaseDirectory, "plugins");
        }

        var pluginResult = PluginLoader.Load(pluginsDir, confirmation);
        foreach (var pluginTool in pluginResult.Tools)
        {
            builder.Services.AddSingleton<ITool>(pluginTool);
        }

        return builder.Build();
    }
}
