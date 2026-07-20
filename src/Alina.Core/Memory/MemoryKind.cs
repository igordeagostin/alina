namespace Alina.Core.Memory;

/// <summary>Tipo de memória permanente.</summary>
public enum MemoryKind
{
    /// <summary>Fato ou preferência aprendido (ex: "sempre uso Clean Architecture").</summary>
    Fact,

    /// <summary>
    /// Procedimento resolvido: uma sequência de passos para repetir um comando sem
    /// reexplorar (ex: "como fazer deploy do projeto X").
    /// </summary>
    Procedure,
}
