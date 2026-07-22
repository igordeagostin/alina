using Alina.App.Services;
using Alina.Core.Habilidades;
using Alina.Core.Orchestration;
using Alina.Core.Permissoes;
using Alina.Core.Tools;
using Alina.Infrastructure.Configuration;
using Alina.Infrastructure.DependencyInjection;
using Alina.Infrastructure.Llm;
using Alina.Mcp;
using Alina.Tools;
using Alina.Tools.Background;
using Alina.Tools.ClaudeCode;
using Alina.Tools.Git;
using Alina.Tools.Habilidades;
using Alina.Tools.Memory;
using Alina.Tools.Plugins;
using Alina.Voice;
using Microsoft.Extensions.AI;
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
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        builder.Configuration.AddUserSecrets(typeof(Composition).Assembly, optional: true);

        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        // Núcleo (LLM, memória, orquestrador)
        builder.Services.AddAlina(builder.Configuration);

        // Preferências de LLM ajustáveis pela UI (modelo + chave da API, guardada
        // criptografada e fora do repositório). Sobrepõem o appsettings/user-secrets,
        // então a voz e o cérebro já usam esses valores no arranque.
        builder.Services.AddSingleton(sp => new ConfiguracoesLlmService(
            sp.GetRequiredService<IOptions<StorageOptions>>().Value,
            sp.GetRequiredService<IConfiguration>()));
        builder.Services.AddOptions<LlmOptions>()
            .PostConfigure<ConfiguracoesLlmService>((opcoes, cfg) => cfg.AplicarEm(opcoes));

        // Cliente de chat reconfigurável: permite trocar modelo/chave em runtime pela
        // tela de configurações. Substitui o IChatClient registrado por AddAlina.
        builder.Services.AddSingleton<ReconfigurableChatClient>(sp =>
            new ReconfigurableChatClient(
                sp.GetRequiredService<IOptions<LlmOptions>>().Value,
                sp.GetRequiredService<ILoggerFactory>()));
        builder.Services.AddSingleton<IChatClient>(sp => sp.GetRequiredService<ReconfigurableChatClient>());

        // Serviços do BlazorWebView (WPF)
        builder.Services.AddWpfBlazorWebView();
#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
#endif

        // Confirmação como overlay dentro da janela (substitui o ConsoleConfirmationService)
        UiConfirmationService confirmation = new UiConfirmationService();
        builder.Services.AddSingleton(confirmation);
        builder.Services.AddSingleton<IConfirmationService>(confirmation);

        // Estado de UI compartilhado (modo compacto/detalhado)
        builder.Services.AddSingleton<ShellUiState>();

        // Seletor de pasta nativo (usado no botão "confiar neste projeto")
        builder.Services.AddSingleton<SeletorPasta>();

        // Configurações do app (persistidas em JSON, aplicadas em runtime)
        builder.Services.AddSingleton(sp => new ConfiguracoesService(
            sp.GetRequiredService<IOptions<StorageOptions>>().Value,
            sp.GetRequiredService<VoiceOptions>(),
            sp.GetRequiredService<ShellUiState>(),
            sp.GetRequiredService<GerenciadorPalavraAtivacao>()));

        // Log de conversa observável + controlador de voz (clique no orbe e hotkey global)
        builder.Services.AddSingleton<ConversationUiState>();
        builder.Services.AddSingleton<VoiceController>();

        // Palavra de ativação ("Alina") — detecção local via Vosk + coordenação com o fluxo de voz
        builder.Services.AddSingleton<IDetectorPalavraAtivacao, VoskDetectorPalavra>();
        builder.Services.AddSingleton<GerenciadorPalavraAtivacao>();

        // Tools básicas
        builder.Services.AddSingleton<ITool, TerminalTool>();
        builder.Services.AddSingleton<ITool, FileReadTool>();

        // Servidor de permissão (Opção A): pedidos de permissão do Claude Code em modo headless
        // aparecem como overlay na UI (com opções de escopo), em vez de serem bloqueados.
        // A política decide automaticamente o que já foi liberado antes de interromper.
        builder.Services.AddSingleton<UiConfirmacaoPermissao>();
        builder.Services.AddSingleton<IConfirmacaoPermissao>(sp => sp.GetRequiredService<UiConfirmacaoPermissao>());
        builder.Services.AddSingleton<IServidorPermissao>(sp =>
            new ServidorPermissaoMcp(
                sp.GetRequiredService<IPoliticaPermissao>(),
                sp.GetRequiredService<IConfirmacaoPermissao>(),
                sp.GetRequiredService<IContextoPermissao>()));

        // Claude Code (Fase 3)
        ClaudeCodeOptions claudeCodeOptions = builder.Configuration.GetSection("ClaudeCode").Get<ClaudeCodeOptions>() ?? new ClaudeCodeOptions();
        builder.Services.AddSingleton(claudeCodeOptions);
        builder.Services.AddSingleton(sp => new ConfiguracoesClaudeCodeService(
            sp.GetRequiredService<IOptions<StorageOptions>>().Value,
            sp.GetRequiredService<ClaudeCodeOptions>()));
        builder.Services.AddSingleton<ClaudeCodeTool>();
        builder.Services.AddSingleton<ITool>(sp => sp.GetRequiredService<ClaudeCodeTool>());

        // Execução em background (Fase 6)
        builder.Services.AddSingleton<IBackgroundTaskManager, BackgroundTaskManager>();
        builder.Services.AddSingleton<ITool, DelegateInBackgroundTool>();
        builder.Services.AddSingleton<ITool, ListTasksTool>();

        // Tools de Git (Fase 5)
        GitOptions gitOptions = builder.Configuration.GetSection(GitOptions.SectionName).Get<GitOptions>() ?? new GitOptions();
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

        // Habilidades
        builder.Services.AddSingleton<ITool, AprenderHabilidadeTool>();
        builder.Services.AddSingleton<ITool, UsarHabilidadeTool>();
        builder.Services.AddSingleton<ITool, EsquecerHabilidadeTool>();
        builder.Services.AddSingleton<IGeradorHabilidade>(sp =>
            new GeradorHabilidade(sp.GetRequiredService<IChatClient>()));

        // Voz (Fase 2) — STT/TTS OpenAI + captura/reprodução NAudio
        VoiceOptions voiceOptions = builder.Configuration.GetSection(VoiceOptions.SectionName).Get<VoiceOptions>() ?? new VoiceOptions();
        if (string.IsNullOrWhiteSpace(voiceOptions.CaminhoModeloVosk))
        {
            voiceOptions.CaminhoModeloVosk = Path.Combine(AppContext.BaseDirectory, "Modelos", "vosk-model-small-pt-0.3");
        }

        builder.Services.AddSingleton(voiceOptions);
        builder.Services.AddSingleton<IAudioRecorder, NAudioRecorder>();
        builder.Services.AddSingleton<IAudioPlayer, NAudioPlayer>();
        builder.Services.AddSingleton(sp =>
        {
            LlmOptions llm = sp.GetRequiredService<IOptions<LlmOptions>>().Value;
            string? key = string.IsNullOrWhiteSpace(voiceOptions.ApiKey) ? llm.ApiKey : voiceOptions.ApiKey;
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new InvalidOperationException("Chave da OpenAI não configurada para voz (Voice:ApiKey ou Llm:ApiKey).");
            }

            return new OpenAIClient(new ApiKeyCredential(key));
        });
        builder.Services.AddSingleton<ISpeechToText, OpenAISpeechToText>();
        builder.Services.AddSingleton<ITextToSpeech, OpenAITextToSpeech>();

        // Plugins declarativos (Fase 6)
        string? pluginsDir = builder.Configuration.GetValue<string>("Plugins:Directory");
        if (string.IsNullOrWhiteSpace(pluginsDir))
        {
            pluginsDir = Path.Combine(AppContext.BaseDirectory, "plugins");
        }

        PluginLoadResult pluginResult = PluginLoader.Load(pluginsDir, confirmation);
        foreach (PluginTool pluginTool in pluginResult.Tools)
        {
            builder.Services.AddSingleton<ITool>(pluginTool);
        }

        return builder.Build();
    }
}
