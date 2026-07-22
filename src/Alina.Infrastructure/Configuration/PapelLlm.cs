namespace Alina.Infrastructure.Configuration;

/// <summary>
/// As funções distintas que a Alina delega a um LLM. Cada papel pode apontar para
/// um provedor e um modelo próprios: conversar é frequente e barato, enquanto
/// diagnosticar por que uma habilidade falhou é raro e exige raciocínio.
/// </summary>
public enum PapelLlm
{
    /// <summary>O cérebro do dia a dia: entende o pedido, escolhe ferramentas e responde.</summary>
    Conversa,

    /// <summary>Criação, edição e treino de habilidades.</summary>
    Habilidades,

    /// <summary>Criação e ajuste das ferramentas declarativas.</summary>
    Ferramentas,
}
