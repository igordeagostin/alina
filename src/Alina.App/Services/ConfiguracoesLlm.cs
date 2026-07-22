using Alina.Infrastructure.Configuration;

namespace Alina.App.Services;

/// <summary>
/// Um modelo de LLM oferecido na tela de configurações, com uma indicação simples
/// de custo relativo (💲 barato → 💲💲💲 caro) para o usuário decidir.
/// </summary>
public sealed record ModeloLlm(LlmProvider Provedor, string Id, string Rotulo, string Custo)
{
    /// <summary>Valor usado nos selects da tela de configurações.</summary>
    public string Selecao => $"{Provedor}|{Id}";
}

/// <summary>
/// Um papel de LLM como aparece na tela de configurações.
/// </summary>
/// <param name="Herdavel">
/// Quando <c>true</c>, o papel pode simplesmente seguir o modelo da conversa em vez
/// de ter um modelo próprio.
/// </param>
/// <param name="ExigeFerramentas">
/// Quando <c>true</c>, o papel depende do function-calling sobre as <c>ITool</c> em C# da
/// Alina e por isso só aceita provedores de API. Papéis que só trocam texto podem rodar
/// pelo CLI do Claude Code, na assinatura.
/// </param>
public sealed record PapelLlmInfo(
    PapelLlm Papel,
    string Titulo,
    string Descricao,
    bool Herdavel,
    bool ExigeFerramentas);
