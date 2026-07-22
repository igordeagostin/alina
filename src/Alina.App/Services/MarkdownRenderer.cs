using Markdig;

namespace Alina.App.Services;

/// <summary>
/// Converte o Markdown de uma habilidade em HTML para exibição. HTML bruto embutido
/// é desabilitado de propósito: o conteúdo vem do LLM/usuário e não deve injetar
/// marcação arbitrária na janela.
/// </summary>
public static class MarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .DisableHtml()
        .Build();

    public static string ParaHtml(string? markdown)
        => string.IsNullOrWhiteSpace(markdown) ? string.Empty : Markdown.ToHtml(markdown, Pipeline);
}
