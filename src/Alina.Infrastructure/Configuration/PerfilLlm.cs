namespace Alina.Infrastructure.Configuration;

/// <summary>Provedor e modelo efetivos de um <see cref="PapelLlm"/>.</summary>
public sealed record PerfilLlm(LlmProvider Provedor, string Modelo);
