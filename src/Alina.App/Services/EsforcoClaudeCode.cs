namespace Alina.App.Services;

/// <summary>
/// Um nível de esforço de raciocínio oferecido na tela de configurações. <see cref="Id"/>
/// vazio significa "usar o padrão do CLI" (sem passar <c>--effort</c>).
/// </summary>
public sealed record EsforcoClaudeCode(string Id, string Rotulo, string Nota);
