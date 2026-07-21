using Alina.Console;
using Alina.Core.Memory;
using Alina.Core.Orchestration;
using Alina.Core.Tools;
using Alina.Core.Permissoes;
using Alina.Infrastructure.Configuration;
using Alina.Infrastructure.DependencyInjection;
using Alina.Mcp;
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

using SysConsole = System.Console;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddUserSecrets<Program>(optional: true);

// Logs discretos para não poluir o chat.
builder.Logging.SetMinimumLevel(LogLevel.Warning);

// Núcleo (LLM, memória, orquestrador)
builder.Services.AddAlina(builder.Configuration);

// Composição da UI: confirmação roteada (voz no modo voz, console no modo texto) e tools
var confirmation = new ConfirmacaoRoteada(new ConsoleConfirmationService());
builder.Services.AddSingleton(confirmation);
builder.Services.AddSingleton<IConfirmationService>(confirmation);
builder.Services.AddSingleton<ITool, TerminalTool>();
builder.Services.AddSingleton<ITool, FileReadTool>();

// Confirmação de permissão com escopo (uma vez / sempre / sempre neste diretório),
// roteada entre console e voz conforme o modo ativo.
var confirmacaoPermissao = new ConfirmacaoPermissaoRoteada(confirmation, new ConfirmacaoPermissaoConsole());
builder.Services.AddSingleton(confirmacaoPermissao);
builder.Services.AddSingleton<IConfirmacaoPermissao>(confirmacaoPermissao);

// Servidor de permissão (Opção A): consulta a política e, quando necessário, pergunta ao
// usuário (voz/console) em vez de bloquear silenciosamente.
builder.Services.AddSingleton<IServidorPermissao>(sp =>
    new ServidorPermissaoMcp(
        sp.GetRequiredService<IPoliticaPermissao>(),
        sp.GetRequiredService<IConfirmacaoPermissao>(),
        sp.GetRequiredService<IContextoPermissao>()));

// Tool do Claude Code (Fase 3) — registrada como concreta e como ITool (mesma instância)
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

// Tools de memória permanente (Fase 6)
builder.Services.AddSingleton<ITool, RememberTool>();
builder.Services.AddSingleton<ITool, RememberProcedureTool>();
builder.Services.AddSingleton<ITool, RetrieveMemoryTool>();
builder.Services.AddSingleton<ITool, RecallTool>();
builder.Services.AddSingleton<ITool, ForgetTool>();

// Voz (Fase 2) — STT/TTS OpenAI + captura/reprodução NAudio
var voiceOptions = builder.Configuration.GetSection(VoiceOptions.SectionName).Get<VoiceOptions>() ?? new VoiceOptions();
builder.Services.AddSingleton(voiceOptions);
builder.Services.AddSingleton<IAudioRecorder, NAudioRecorder>();
builder.Services.AddSingleton<IAudioPlayer, NAudioPlayer>();
builder.Services.AddSingleton(sp =>
{
    // Reutiliza a chave do LLM se a de voz não estiver definida.
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
builder.Services.AddSingleton<VoiceChat>();

// Plugins declarativos (Fase 6) — carregados de arquivos *.plugin.json
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

using var host = builder.Build();

if (pluginResult.Tools.Count > 0 || pluginResult.Warnings.Count > 0)
{
    WriteLineColored($"Plugins: {pluginResult.Tools.Count} carregado(s) de {pluginsDir}", ConsoleColor.DarkGray);
    foreach (var warning in pluginResult.Warnings)
    {
        WriteLineColored($"  ⚠ plugin ignorado — {warning}", ConsoleColor.Yellow);
    }
}

var store = host.Services.GetRequiredService<IConversationStore>();
var memory = host.Services.GetRequiredService<IMemoryStore>();
var tasks = host.Services.GetRequiredService<IBackgroundTaskManager>();

// Progresso ao vivo do Claude Code (streaming): mostra o que ele está fazendo no console.
var claudeCode = host.Services.GetRequiredService<ClaudeCodeTool>();
claudeCode.Progresso += EscreverProgressoClaudeCode;

// Notifica no console quando uma tarefa em background termina.
tasks.TaskFinished += (_, task) =>
{
    var color = task.Status == BackgroundTaskStatus.Completed ? ConsoleColor.Green : ConsoleColor.Yellow;
    var icon = task.Status == BackgroundTaskStatus.Completed ? "✅" : "⚠";
    WriteLineColored($"\n{icon} Tarefa [{task.Id}] {task.Status}: {task.Description} (veja /tarefa {task.Id})", color);
};

// Resolução preguiçosa: o IChatClient (e a validação da API key) só é criado
// no primeiro uso, para o banner e o histórico funcionarem sem chave configurada.
var orchestrator = new Lazy<IOrchestrator>(() => host.Services.GetRequiredService<IOrchestrator>());
var voiceChat = new Lazy<VoiceChat>(() => host.Services.GetRequiredService<VoiceChat>());

await RunReplAsync(orchestrator, store, voiceChat, memory, pluginResult.Tools, tasks);
return;

static async Task RunReplAsync(Lazy<IOrchestrator> orchestrator, IConversationStore store, Lazy<VoiceChat> voiceChat, IMemoryStore memory, IReadOnlyList<PluginTool> plugins, IBackgroundTaskManager tasks)
{
    PrintBanner();

    while (true)
    {
        WriteColored("\nVocê> ", ConsoleColor.Cyan);
        var input = SysConsole.ReadLine();

        if (input is null)
        {
            break; // EOF
        }

        input = input.Trim();
        if (input.Length == 0)
        {
            continue;
        }

        // Comandos com argumento (/tarefa <id>, /cancelar <id>).
        if (input.StartsWith("/tarefa ", StringComparison.OrdinalIgnoreCase))
        {
            PrintTaskDetail(tasks, input[8..].Trim());
            continue;
        }

        if (input.StartsWith("/cancelar ", StringComparison.OrdinalIgnoreCase))
        {
            var id = input[10..].Trim();
            var cancelled = tasks.Cancel(id);
            WriteLineColored(cancelled ? $"Cancelamento solicitado para [{id}]." : $"Tarefa [{id}] não está em execução.", ConsoleColor.DarkGray);
            continue;
        }

        switch (input.ToLowerInvariant())
        {
            case "/sair":
            case "/exit":
                WriteLineColored("Até logo!", ConsoleColor.DarkGray);
                return;

            case "/nova":
            case "/new":
                try
                {
                    orchestrator.Value.StartNew();
                    WriteLineColored("Nova conversa iniciada.", ConsoleColor.DarkGray);
                }
                catch (Exception ex)
                {
                    WriteLineColored($"[erro] {ex.Message}", ConsoleColor.Red);
                }
                continue;

            case "/historico":
            case "/history":
                await PrintHistoryAsync(store);
                continue;

            case "/memorias":
            case "/memories":
                await PrintMemoriesAsync(memory);
                continue;

            case "/lembrar":
            case "/remember":
                try
                {
                    await RememberConversationAsync(orchestrator.Value, memory);
                }
                catch (Exception ex)
                {
                    WriteLineColored($"[erro] {ex.Message}", ConsoleColor.Red);
                }
                continue;

            case "/plugins":
                PrintPlugins(plugins);
                continue;

            case "/tarefas":
            case "/tasks":
                PrintTasks(tasks);
                continue;

            case "/voz":
            case "/voice":
                try
                {
                    await voiceChat.Value.RunAsync();
                }
                catch (Exception ex)
                {
                    WriteLineColored($"[erro] {ex.Message}", ConsoleColor.Red);
                }
                continue;

            case "/ajuda":
            case "/help":
                PrintHelp();
                continue;
        }

        try
        {
            WriteColored("Alina> ", ConsoleColor.Green);
            var response = await orchestrator.Value.SendAsync(input);
            SysConsole.WriteLine(response);
        }
        catch (Exception ex)
        {
            WriteLineColored($"[erro] {ex.Message}", ConsoleColor.Red);
        }
    }
}

static async Task PrintHistoryAsync(IConversationStore store)
{
    var conversations = await store.ListAsync();
    if (conversations.Count == 0)
    {
        WriteLineColored("Nenhuma conversa no histórico.", ConsoleColor.DarkGray);
        return;
    }

    WriteLineColored("Histórico de conversas:", ConsoleColor.DarkGray);
    foreach (var c in conversations)
    {
        WriteLineColored($"  [{c.UpdatedAt:yyyy-MM-dd HH:mm}] {c.Title} ({c.MessageCount} msgs) — {c.Id}", ConsoleColor.DarkGray);
    }
}

static async Task PrintMemoriesAsync(IMemoryStore memory)
{
    var items = await memory.GetAllAsync();
    if (items.Count == 0)
    {
        WriteLineColored("Nenhuma memória salva ainda.", ConsoleColor.DarkGray);
        return;
    }

    WriteLineColored("Memórias permanentes:", ConsoleColor.DarkGray);
    foreach (var item in items)
    {
        var category = string.IsNullOrWhiteSpace(item.Category) ? string.Empty : $" ({item.Category})";
        var kind = item.Kind == MemoryKind.Procedure ? "⚙ " : string.Empty;
        var pin = item.Pinned ? "📌 " : string.Empty;
        WriteLineColored($"  [{item.Id}] {pin}{kind}{category} {item.DisplayTitle()}", ConsoleColor.DarkGray);
    }
}

static async Task RememberConversationAsync(IOrchestrator orchestrator, IMemoryStore memory)
{
    var convo = orchestrator.Current;
    if (convo.Messages.Count == 0)
    {
        WriteLineColored("Nada para memorizar: a conversa atual está vazia.", ConsoleColor.DarkGray);
        return;
    }

    WriteLineColored("Resumindo a conversa…", ConsoleColor.DarkGray);
    var summary = await orchestrator.SummarizeConversationAsync();
    if (string.IsNullOrWhiteSpace(summary))
    {
        WriteLineColored("Não foi possível gerar um resumo da conversa.", ConsoleColor.Yellow);
        return;
    }

    var item = new MemoryItem
    {
        Kind = MemoryKind.Fact,
        Title = $"Resumo: {convo.Title}",
        Content = summary,
        Category = "conversa",
    };

    await memory.AddAsync(item);
    WriteLineColored($"Conversa memorizada [{item.Id}].", ConsoleColor.Green);
}

static void PrintTasks(IBackgroundTaskManager tasks)
{
    var all = tasks.GetAll();
    if (all.Count == 0)
    {
        WriteLineColored("Nenhuma tarefa em background.", ConsoleColor.DarkGray);
        return;
    }

    WriteLineColored("Tarefas em background:", ConsoleColor.DarkGray);
    foreach (var task in all)
    {
        WriteLineColored($"  [{task.Id}] {task.Status}: {task.Description}", ConsoleColor.DarkGray);
    }
    WriteLineColored("Detalhe: /tarefa <id>   Cancelar: /cancelar <id>", ConsoleColor.DarkGray);
}

static void PrintTaskDetail(IBackgroundTaskManager tasks, string id)
{
    var task = tasks.Get(id);
    if (task is null)
    {
        WriteLineColored($"Tarefa [{id}] não encontrada.", ConsoleColor.DarkGray);
        return;
    }

    WriteLineColored($"[{task.Id}] {task.Status} — {task.Description}", ConsoleColor.DarkGray);
    if (task.Result is not null)
    {
        SysConsole.WriteLine(task.Result);
    }
    else
    {
        WriteLineColored("(ainda em execução)", ConsoleColor.DarkGray);
    }
}

static void PrintPlugins(IReadOnlyList<PluginTool> plugins)
{
    if (plugins.Count == 0)
    {
        WriteLineColored("Nenhum plugin carregado. Adicione arquivos *.plugin.json na pasta de plugins.", ConsoleColor.DarkGray);
        return;
    }

    WriteLineColored("Plugins carregados:", ConsoleColor.DarkGray);
    foreach (var plugin in plugins)
    {
        var flag = plugin.RequiresConfirmation ? " (confirmação)" : string.Empty;
        WriteLineColored($"  {plugin.Name}{flag} — {plugin.Description}", ConsoleColor.DarkGray);
    }
}

static void PrintBanner()
{
    WriteLineColored("╭──────────────────────────────────────────────╮", ConsoleColor.Magenta);
    WriteLineColored("│  Alina — assistente de desenvolvimento    │", ConsoleColor.Magenta);
    WriteLineColored("╰──────────────────────────────────────────────╯", ConsoleColor.Magenta);
    PrintHelp();
}

static void PrintHelp()
{
    WriteLineColored("Comandos: /voz  /lembrar  /memorias  /plugins  /tarefas  /nova  /historico  /ajuda  /sair", ConsoleColor.DarkGray);
}

static void EscreverProgressoClaudeCode(EventoProgressoClaudeCode e)
{
    switch (e.Tipo)
    {
        case TipoEventoClaudeCode.Inicio:
            WriteLineColored($"\n  ⟳ {e.Texto}", ConsoleColor.DarkGray);
            break;
        case TipoEventoClaudeCode.Texto:
            WriteLineColored($"  » {e.Texto}", ConsoleColor.DarkGray);
            break;
        case TipoEventoClaudeCode.Ferramenta:
            WriteLineColored($"  ⚙ {e.Texto}", ConsoleColor.DarkCyan);
            break;
        case TipoEventoClaudeCode.ResultadoFerramenta:
            WriteLineColored($"  ← {e.Texto}", ConsoleColor.DarkGray);
            break;
        case TipoEventoClaudeCode.Fim:
            // O resultado final também é retornado pela tool; aqui só marcamos a conclusão.
            WriteLineColored("  ✓ Claude Code concluiu.", ConsoleColor.DarkGreen);
            break;
    }
}

static void WriteColored(string text, ConsoleColor color)
{
    var previous = SysConsole.ForegroundColor;
    SysConsole.ForegroundColor = color;
    SysConsole.Write(text);
    SysConsole.ForegroundColor = previous;
}

static void WriteLineColored(string text, ConsoleColor color)
{
    var previous = SysConsole.ForegroundColor;
    SysConsole.ForegroundColor = color;
    SysConsole.WriteLine(text);
    SysConsole.ForegroundColor = previous;
}

// Necessário para AddUserSecrets<Program>() com top-level statements.
public partial class Program;
