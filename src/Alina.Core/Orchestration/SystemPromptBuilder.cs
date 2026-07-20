using System.Text;
using Alina.Core.Memory;
using Alina.Core.Tools;

namespace Alina.Core.Orchestration;

/// <summary>Monta o system prompt que define a identidade e a política da Alina.</summary>
public static class SystemPromptBuilder
{
    public static string Build(
        IReadOnlyList<ITool> tools,
        string? preferences,
        IReadOnlyList<MemoryIndexEntry>? memoryIndex = null,
        IReadOnlyList<MemoryItem>? detailedMemories = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Você é a Alina, uma assistente pessoal de desenvolvimento de software (estilo \"Jarvis\").");
        sb.AppendLine("Você entende os pedidos do usuário, decide qual ferramenta usar e executa as ações necessárias.");
        sb.AppendLine("Responda sempre em português do Brasil, de forma objetiva.");
        sb.AppendLine();

        if (tools.Count > 0)
        {
            sb.AppendLine("Ferramentas disponíveis:");
            foreach (var tool in tools)
            {
                var flag = tool.RequiresConfirmation ? " (exige confirmação do usuário)" : string.Empty;
                sb.AppendLine($"- {tool.Name}: {tool.Description}{flag}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("Política de segurança: operações críticas (executar comandos no terminal, deploy, alterar banco, deletar arquivos) exigem confirmação. A própria ferramenta pedirá SIM/NÃO ao usuário antes de agir — não invente que executou algo que foi recusado.");

        sb.AppendLine();
        sb.AppendLine("Memória sob demanda: só grave na memória permanente quando o usuário PEDIR explicitamente para você lembrar/memorizar. Nunca memorize por conta própria. Escolha a ferramenta certa: 'lembrar' para um fato/preferência pontual; 'memorizar_procedimento' para um passo a passo repetível; e, se o usuário pedir para lembrar da CONVERSA (ex: \"lembre dessa conversa\"), resuma você mesmo o que foi conversado — fatos, decisões e preferências duradouras — e salve com 'lembrar' usando a categoria \"conversa\". Antes de salvar, confira os ids do índice abaixo para não duplicar (se já existir e mudou, atualize/esqueça o antigo). Seja conciso e inclua boas palavras-chave. Não memorize segredos ou senhas.");

        if (!string.IsNullOrWhiteSpace(preferences))
        {
            sb.AppendLine();
            sb.AppendLine("Preferências e convenções do usuário:");
            sb.AppendLine(preferences!.Trim());
        }

        if (detailedMemories is { Count: > 0 })
        {
            sb.AppendLine();
            sb.AppendLine("Memórias relevantes para este pedido (fixadas + mais próximas; " +
                          "cada item tem um id entre colchetes que pode ser usado para esquecê-lo):");
            foreach (var memory in detailedMemories)
            {
                sb.AppendLine($"- {FormatDetailed(memory)}");
            }
        }

        if (memoryIndex is { Count: > 0 })
        {
            sb.AppendLine();
            sb.AppendLine("Índice do que você já sabe (títulos apenas). Se precisar do conteúdo completo de " +
                          "algum item que não esteja detalhado acima, chame a ferramenta 'recuperar_memoria' com o id ou uma consulta:");
            foreach (var entry in memoryIndex)
            {
                var kind = entry.Kind == MemoryKind.Procedure ? "procedimento" : "fato";
                var category = string.IsNullOrWhiteSpace(entry.Category) ? string.Empty : $"/{entry.Category}";
                sb.AppendLine($"- [{entry.Id}] ({kind}{category}) {entry.Title}");
            }
        }

        return sb.ToString();
    }

    private static string FormatDetailed(MemoryItem memory)
    {
        var pin = memory.Pinned ? "📌 " : string.Empty;
        var category = string.IsNullOrWhiteSpace(memory.Category) ? string.Empty : $" ({memory.Category})";
        if (memory.Kind == MemoryKind.Procedure)
        {
            var name = memory.DisplayTitle();
            return $"{pin}[{memory.Id}]{category} procedimento \"{name}\":\n{memory.Content}";
        }

        return $"{pin}[{memory.Id}]{category} {memory.Content}";
    }
}