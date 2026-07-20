using Microsoft.Extensions.AI;

namespace Alina.Core.Tools;

/// <summary>
/// Contrato de uma ferramenta que o orquestrador pode disponibilizar ao LLM
/// via function-calling. Cada tool tem uma responsabilidade específica
/// (terminal, arquivos, git, browser, Claude Code, …).
/// </summary>
public interface ITool
{
    /// <summary>Nome curto e único da tool (usado como nome da função no LLM).</summary>
    string Name { get; }

    /// <summary>Descrição do que a tool faz — ajuda o LLM a decidir quando usá-la.</summary>
    string Description { get; }

    /// <summary>
    /// Indica se a execução exige confirmação explícita do usuário (SIM/NÃO)
    /// antes de rodar. Usado para operações críticas (terminal, deploy, etc.).
    /// </summary>
    bool RequiresConfirmation { get; }

    /// <summary>Expõe a tool como uma <see cref="AIFunction"/> para o pipeline de function-calling.</summary>
    AIFunction AsAIFunction();
}
