namespace Alina.App.Services;

/// <summary>
/// Um modelo do Claude Code oferecido na tela de configurações. <see cref="Id"/> vazio
/// significa "usar o padrão da assinatura/config do usuário" (sem passar <c>--model</c>).
/// </summary>
public sealed record ModeloClaudeCode(string Id, string Rotulo, string Nota);
