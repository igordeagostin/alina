namespace Alina.Tools.ClaudeCode;

/// <summary>
/// Configuração da integração com o Claude Code (CLI headless).
/// Ligada à seção "ClaudeCode" da configuração.
/// </summary>
public sealed class ClaudeCodeOptions
{
    /// <summary>Executável do Claude Code (default "claude", resolvido pelo PATH).</summary>
    public string Executable { get; set; } = "claude";

    /// <summary>
    /// Modelo passado ao CLI (<c>--model</c>): um alias ("opus", "sonnet", "haiku") ou o
    /// ID completo do modelo. Quando vazio, o Claude Code usa o modelo padrão da conta/config
    /// do usuário.
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Modo de permissão passado ao CLI (--permission-mode):
    /// "default", "acceptEdits", "plan" ou "bypassPermissions".
    /// Como a própria Alina já pede confirmação antes de delegar, o default
    /// "acceptEdits" permite que o Claude Code edite arquivos sem travar em modo headless.
    /// </summary>
    public string PermissionMode { get; set; } = "acceptEdits";

    /// <summary>
    /// Se <c>true</c>, usa --dangerously-skip-permissions (autonomia total, sem
    /// nenhuma barreira do Claude Code). Use com cautela. Default <c>false</c>.
    /// </summary>
    public bool SkipPermissions { get; set; }

    /// <summary>
    /// Se <c>true</c>, cada permissão que o Claude Code precisar no meio da execução é
    /// perguntada ao usuário via <c>--permission-prompt-tool</c> (servidor MCP da Alina),
    /// em vez de aceitar/bloquear automaticamente. Exige um <see cref="IServidorPermissao"/>
    /// registrado. Ignora <see cref="PermissionMode"/>/<see cref="SkipPermissions"/> quando ativo.
    /// </summary>
    public bool PermissaoInterativa { get; set; }

    /// <summary>
    /// Se <c>true</c> (padrão), executa em modo streaming (<c>--output-format stream-json</c>),
    /// emitindo progresso ao vivo via <see cref="ClaudeCodeTool.Progresso"/>. Se <c>false</c>,
    /// usa o modo antigo (<c>--output-format json</c>), que só entrega o resultado no fim.
    /// </summary>
    public bool Streaming { get; set; } = true;

    /// <summary>Tempo máximo de execução em segundos (tarefas de código podem demorar).</summary>
    public int TimeoutSeconds { get; set; } = 600;

    /// <summary>Limite de turnos do agente (0 = sem limite explícito).</summary>
    public int MaxTurns { get; set; }

    /// <summary>Diretório de trabalho padrão quando o LLM não informar um.</summary>
    public string? DefaultWorkingDirectory { get; set; }

    /// <summary>Argumentos extras repassados diretamente ao CLI.</summary>
    public string[] ExtraArgs { get; set; } = Array.Empty<string>();
}
