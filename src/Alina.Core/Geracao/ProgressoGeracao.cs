namespace Alina.Core.Geracao;

/// <summary>Em que ponto da resposta o modelo está, do ponto de vista de quem espera na tela.</summary>
public enum FaseGeracao
{
    /// <summary>Ainda não veio nada aproveitável — o modelo está lendo o contexto.</summary>
    Preparando,

    /// <summary>A fala da Alina está sendo escrita; <see cref="ProgressoGeracao.Mensagem"/> traz o que já chegou.</summary>
    Escrevendo,

    /// <summary>A fala terminou e o modelo está montando o rascunho (documento ou definição da ferramenta).</summary>
    Rascunho,
}

/// <summary>
/// Um instante da resposta em construção, para a UI mostrar o texto chegando em vez de
/// um "pensando…" parado até o fim.
/// </summary>
public sealed record ProgressoGeracao(FaseGeracao Fase, string Mensagem);
