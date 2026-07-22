namespace Alina.Core.Personalidade;

/// <summary>
/// Como a Alina se comporta ao conversar: quanto ela fala, quanto se antecipa,
/// quanto brinca e quão formal soa. Cada eixo vai de <see cref="NivelMinimo"/> a
/// <see cref="NivelMaximo"/>. <see cref="Instrucoes"/> é texto livre do usuário que
/// entra no system prompt como um prompt inicial e prevalece sobre os eixos.
/// </summary>
public sealed class PerfilPersonalidade
{
    public const int NivelMinimo = 1;
    public const int NivelMaximo = 5;

    /// <summary>Quanto texto ela produz por resposta (1 = telegráfica, 5 = detalhada).</summary>
    public int Verbosidade { get; set; } = 2;

    /// <summary>Quanto ela antecipa próximos passos e oferece ajuda extra (1 = só o pedido, 5 = antecipa tudo).</summary>
    public int Proatividade { get; set; } = 2;

    /// <summary>Quanto humor e ironia entram no tom (1 = nenhum, 5 = sarcástica).</summary>
    public int Humor { get; set; } = 3;

    /// <summary>Registro da linguagem (1 = bem informal, 5 = formal).</summary>
    public int Formalidade { get; set; } = 2;

    /// <summary>Orientações livres do usuário, injetadas no system prompt.</summary>
    public string Instrucoes { get; set; } = string.Empty;

    public PerfilPersonalidade Clonar() => new()
    {
        Verbosidade = Verbosidade,
        Proatividade = Proatividade,
        Humor = Humor,
        Formalidade = Formalidade,
        Instrucoes = Instrucoes,
    };

    /// <summary>Devolve uma cópia com os eixos dentro da faixa válida e as instruções aparadas.</summary>
    public PerfilPersonalidade Normalizado() => new()
    {
        Verbosidade = Ajustar(Verbosidade),
        Proatividade = Ajustar(Proatividade),
        Humor = Ajustar(Humor),
        Formalidade = Ajustar(Formalidade),
        Instrucoes = (Instrucoes ?? string.Empty).Trim(),
    };

    private static int Ajustar(int nivel) => Math.Clamp(nivel, NivelMinimo, NivelMaximo);
}
