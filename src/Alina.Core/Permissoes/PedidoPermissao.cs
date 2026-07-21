namespace Alina.Core.Permissoes;

/// <summary>
/// Um pedido de permissão normalizado, derivado da chamada de ferramenta que o Claude Code
/// quer executar. Os campos opcionais são preenchidos conforme a ferramenta (ex.: <see cref="Comando"/>
/// para Bash, <see cref="Caminho"/> para edições/leituras de arquivo).
/// </summary>
public sealed record PedidoPermissao
{
    /// <summary>Nome da ferramenta (ex.: <c>Bash</c>, <c>Edit</c>, <c>Write</c>, <c>Read</c>).</summary>
    public required string Ferramenta { get; init; }

    /// <summary>Comando de shell, quando a ferramenta é de terminal.</summary>
    public string? Comando { get; init; }

    /// <summary>Caminho de arquivo alvo, quando a ferramenta lê ou escreve arquivos.</summary>
    public string? Caminho { get; init; }

    /// <summary>Diretório de trabalho da execução do Claude Code (para resolver caminhos relativos).</summary>
    public string? DiretorioTrabalho { get; init; }

    /// <summary>Descrição legível do pedido, usada nas confirmações.</summary>
    public required string Descricao { get; init; }
}
