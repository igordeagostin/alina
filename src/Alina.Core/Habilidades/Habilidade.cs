namespace Alina.Core.Habilidades;

/// <summary>
/// Uma habilidade ensinada à Alina: um passo a passo ou conhecimento nomeado,
/// persistido como arquivo Markdown. Só o índice (nome + descrição) fica sempre
/// visível no system prompt; o <see cref="Conteudo"/> completo é carregado sob
/// demanda quando a habilidade for relevante.
/// </summary>
public sealed class Habilidade
{
    /// <summary>Identificador em kebab-case; também é o nome do arquivo <c>.md</c>.</summary>
    public string Nome { get; set; } = string.Empty;

    /// <summary>Uma linha descrevendo a habilidade; é o que aparece no índice do prompt.</summary>
    public string Descricao { get; set; } = string.Empty;

    /// <summary>As instruções completas, em Markdown livre.</summary>
    public string Conteudo { get; set; } = string.Empty;

    public DateTimeOffset CriadaEm { get; set; } = DateTimeOffset.Now;

    public DateTimeOffset AtualizadaEm { get; set; } = DateTimeOffset.Now;
}
