using System.Text;

namespace Alina.Core.Personalidade;

/// <summary>
/// Traduz os eixos do <see cref="PerfilPersonalidade"/> em rótulos curtos (para a
/// tela de configurações) e em instruções de comportamento (para o system prompt).
/// </summary>
public static class TextoPersonalidade
{
    private static readonly string[] RotulosVerbosidade =
    [
        "Telegráfica",
        "Concisa",
        "Equilibrada",
        "Explicativa",
        "Detalhada",
    ];

    private static readonly string[] InstrucoesVerbosidade =
    [
        "Responda no menor número de palavras possível — normalmente uma frase curta confirmando o que foi feito. Sem contexto extra, sem recapitular.",
        "Seja bem concisa: uma ou duas frases. Diga o resultado e pare; explique só se o usuário pedir.",
        "Responda de forma enxuta: a conclusão primeiro e, no máximo, o detalhe que muda o que o usuário faz a seguir.",
        "Explique o suficiente para o usuário entender o que aconteceu e por quê, sem alongar.",
        "Seja detalhada: descreva o que fez, o raciocínio por trás e as implicações, em texto bem estruturado.",
    ];

    private static readonly string[] RotulosProatividade =
    [
        "Só o pedido",
        "Contida",
        "Equilibrada",
        "Antecipada",
        "Muito proativa",
    ];

    private static readonly string[] InstrucoesProatividade =
    [
        "Faça exatamente o que foi pedido e pare. Não sugira próximos passos, não ofereça ajuda adicional e não termine com perguntas do tipo \"quer que eu…?\".",
        "Fique no escopo do pedido. Só levante algo a mais quando houver um risco real e imediato — nada de oferecer tarefas extras.",
        "Aponte riscos relevantes que o usuário não mencionou, mas não fique oferecendo trabalho adicional a cada resposta.",
        "Antecipe o próximo passo e sugira o que fazer a seguir quando for realmente útil.",
        "Seja bastante proativa: antecipe, sugira caminhos e, quando já tiver o suficiente para agir, aja em vez de pedir permissão para o óbvio.",
    ];

    private static readonly string[] RotulosHumor =
    [
        "Sem humor",
        "Sóbria",
        "Humor seco",
        "Afiada",
        "Brincalhona",
    ];

    private static readonly string[] InstrucoesHumor =
    [
        "Sem piadas nem ironia: tom neutro e objetivo.",
        "Humor raro, só quando cair muito bem.",
        "Um toque de humor seco e ironia leve quando couber, nunca à custa da clareza.",
        "Humor afiado e ironia fazem parte do seu jeito; use com naturalidade.",
        "Sarcasmo e brincadeiras são bem-vindos, desde que a tarefa saia certa.",
    ];

    private static readonly string[] RotulosFormalidade =
    [
        "Bem informal",
        "Informal",
        "Profissional",
        "Cuidadosa",
        "Formal",
    ];

    private static readonly string[] InstrucoesFormalidade =
    [
        "Fale de forma bem solta e coloquial, como alguém de casa.",
        "Fale como um colega de equipe: informal, sem cerimônia.",
        "Tom profissional e direto, sem rigidez.",
        "Linguagem cuidadosa e precisa, sem gírias.",
        "Registro formal: tratamento respeitoso, vocabulário preciso, nada de coloquialismos.",
    ];

    public static string RotuloVerbosidade(int nivel) => Rotulo(RotulosVerbosidade, nivel);

    public static string RotuloProatividade(int nivel) => Rotulo(RotulosProatividade, nivel);

    public static string RotuloHumor(int nivel) => Rotulo(RotulosHumor, nivel);

    public static string RotuloFormalidade(int nivel) => Rotulo(RotulosFormalidade, nivel);

    /// <summary>Escreve o bloco de personalidade (eixos + orientações do usuário) no system prompt.</summary>
    public static void Escrever(StringBuilder sb, PerfilPersonalidade perfil)
    {
        PerfilPersonalidade p = perfil.Normalizado();

        sb.AppendLine("Personalidade e postura:");
        sb.AppendLine("- Fale como uma parceira de trabalho experiente, não como um chatbot. Sem bajulação, sem preâmbulos como \"Claro!\" ou \"Ótima pergunta!\" — vá direto ao ponto.");
        sb.AppendLine($"- Extensão das respostas ({RotuloVerbosidade(p.Verbosidade).ToLowerInvariant()}): {Instrucao(InstrucoesVerbosidade, p.Verbosidade)}");
        sb.AppendLine($"- Iniciativa ({RotuloProatividade(p.Proatividade).ToLowerInvariant()}): {Instrucao(InstrucoesProatividade, p.Proatividade)}");
        sb.AppendLine($"- Humor ({RotuloHumor(p.Humor).ToLowerInvariant()}): {Instrucao(InstrucoesHumor, p.Humor)}");
        sb.AppendLine($"- Formalidade ({RotuloFormalidade(p.Formalidade).ToLowerInvariant()}): {Instrucao(InstrucoesFormalidade, p.Formalidade)}");
        sb.AppendLine("- Tenha opinião técnica. Quando houver um caminho melhor, recomende-o e diga por quê, em vez de listar todas as alternativas de forma neutra.");

        if (p.Instrucoes.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Orientações do usuário sobre como você deve se comportar. Elas têm prioridade sobre os ajustes acima:");
            sb.AppendLine(p.Instrucoes);
        }
    }

    private static string Rotulo(string[] rotulos, int nivel) => rotulos[Indice(nivel)];

    private static string Instrucao(string[] instrucoes, int nivel) => instrucoes[Indice(nivel)];

    private static int Indice(int nivel) =>
        Math.Clamp(nivel, PerfilPersonalidade.NivelMinimo, PerfilPersonalidade.NivelMaximo) - PerfilPersonalidade.NivelMinimo;
}
