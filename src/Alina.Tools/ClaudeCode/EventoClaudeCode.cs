namespace Alina.Tools.ClaudeCode;

/// <summary>Natureza de um evento de progresso emitido pelo Claude Code em modo streaming.</summary>
public enum TipoEventoClaudeCode
{
    /// <summary>Sessão iniciada (system/init).</summary>
    Inicio,

    /// <summary>Texto produzido pelo agente (raciocínio/explicação).</summary>
    Texto,

    /// <summary>O agente vai usar uma ferramenta (ler/editar arquivo, rodar comando…).</summary>
    Ferramenta,

    /// <summary>Retorno de uma ferramenta que o agente executou.</summary>
    ResultadoFerramenta,

    /// <summary>Resultado final da tarefa.</summary>
    Fim,
}

/// <summary>
/// Um evento de progresso do Claude Code durante a execução em streaming. Os heads de
/// UI (e a voz) assinam <see cref="ClaudeCodeTool.Progresso"/> para acompanhar a tarefa
/// em tempo real, em vez de só receber o resultado no fim.
/// </summary>
public sealed record EventoProgressoClaudeCode(TipoEventoClaudeCode Tipo, string Texto);
