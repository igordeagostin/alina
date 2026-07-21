namespace Alina.App.Services;

/// <summary>
/// Um modelo de LLM oferecido na tela de configurações, com uma indicação simples
/// de custo relativo (💲 barato → 💲💲💲 caro) para o usuário decidir.
/// </summary>
public sealed record ModeloLlm(string Id, string Rotulo, string Custo);
