using System.Text;
using Alina.Core.Habilidades;
using Alina.Core.Memory;
using Alina.Core.Personalidade;
using Alina.Core.Tools;

namespace Alina.Core.Orchestration;

/// <summary>Monta o system prompt que define a identidade e a política da Alina.</summary>
public static class SystemPromptBuilder
{
    public static string Build(
        IReadOnlyList<ITool> tools,
        string? preferences,
        IReadOnlyList<MemoryIndexEntry>? memoryIndex = null,
        IReadOnlyList<MemoryItem>? detailedMemories = null,
        IReadOnlyList<HabilidadeResumo>? habilidades = null,
        IReadOnlyList<string>? raizesProjetos = null,
        PerfilPersonalidade? personalidade = null)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("Você é a Alina, a assistente pessoal de desenvolvimento de software do usuário — no espírito do Jarvis do Tony Stark: competente e direta.");
        sb.AppendLine();
        TextoPersonalidade.Escrever(sb, personalidade ?? new PerfilPersonalidade());
        sb.AppendLine();
        sb.AppendLine("Como raciocinar e responder:");
        sb.AppendLine("- Entenda o pedido, decida qual ferramenta usar e execute as ações necessárias. Prefira agir a teorizar.");
        sb.AppendLine("- Se o pedido for genuinamente ambíguo e a escolha mudar o que você vai fazer, faça UMA pergunta objetiva. Caso contrário, assuma o padrão mais razoável, siga em frente e diga qual suposição adotou.");
        sb.AppendLine("- Admita incerteza quando ela existir e diga como confirmaria — não invente fatos, resultados ou saídas de ferramentas.");
        sb.AppendLine("- Responda sempre em português do Brasil.");
        sb.AppendLine();

        if (tools.Count > 0)
        {
            sb.AppendLine("Ferramentas disponíveis:");
            foreach (ITool tool in tools)
            {
                string flag = tool.RequiresConfirmation ? " (exige confirmação do usuário)" : string.Empty;
                sb.AppendLine($"- {tool.Name}: {tool.Description}{flag}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("Caminhos e projetos: você NÃO sabe de cor onde cada projeto fica no disco. Quando o usuário citar um projeto pelo nome (\"abre o diário API\"), chame 'localizar_projeto' com esse nome e use o caminho absoluto que voltar — nunca monte, adivinhe ou repita o nome falado no lugar de um caminho. Se a busca trouxer vários candidatos plausíveis, pergunte qual; se não trouxer nenhum, diga isso em vez de tentar um palpite. Toda ferramenta que recebe caminho rejeita pasta inexistente: se vier esse erro, localize o caminho real e refaça a chamada, e não afirme que a ação deu certo.");

        if (raizesProjetos is { Count: > 0 })
        {
            sb.AppendLine($"Pastas de projeto confiáveis do usuário (é onde 'localizar_projeto' procura): {string.Join("; ", raizesProjetos)}.");
        }

        sb.AppendLine();
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
            foreach (MemoryItem memory in detailedMemories)
            {
                sb.AppendLine($"- {FormatDetailed(memory)}");
            }
        }

        if (memoryIndex is { Count: > 0 })
        {
            sb.AppendLine();
            sb.AppendLine("Índice do que você já sabe (títulos apenas). Se precisar do conteúdo completo de " +
                          "algum item que não esteja detalhado acima, chame a ferramenta 'recuperar_memoria' com o id ou uma consulta:");
            foreach (MemoryIndexEntry entry in memoryIndex)
            {
                string kind = entry.Kind == MemoryKind.Procedure ? "procedimento" : "fato";
                string category = string.IsNullOrWhiteSpace(entry.Category) ? string.Empty : $"/{entry.Category}";
                sb.AppendLine($"- [{entry.Id}] ({kind}{category}) {entry.Title}");
            }
        }

        if (habilidades is { Count: > 0 })
        {
            sb.AppendLine();
            sb.AppendLine("Habilidades que você aprendeu (nome — descrição). Se o pedido casar com uma delas, " +
                          "você DEVE chamar a ferramenta 'usar_habilidade' com o nome e seguir as instruções " +
                          "carregadas ANTES de chamar qualquer outra ferramenta. Não resolva por conta própria " +
                          "(caminhos, comandos, nomes, regras de correspondência) aquilo que a habilidade já " +
                          "define — carregue-a primeiro, mesmo que a tarefa pareça óbvia:");
            foreach (HabilidadeResumo habilidade in habilidades)
            {
                sb.AppendLine($"- {habilidade.Nome} — {habilidade.Descricao}");
            }
        }

        return sb.ToString();
    }

    private static string FormatDetailed(MemoryItem memory)
    {
        string pin = memory.Pinned ? "📌 " : string.Empty;
        string category = string.IsNullOrWhiteSpace(memory.Category) ? string.Empty : $" ({memory.Category})";
        if (memory.Kind == MemoryKind.Procedure)
        {
            string name = memory.DisplayTitle();
            return $"{pin}[{memory.Id}]{category} procedimento \"{name}\":\n{memory.Content}";
        }

        return $"{pin}[{memory.Id}]{category} {memory.Content}";
    }
}