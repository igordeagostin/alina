namespace Alina.Core.Orchestration;

/// <summary>
/// Opções de geração relidas a cada turno, para valerem sem reiniciar. O orquestrador
/// as consulta ao montar a requisição; quem implementa decide de onde vêm os valores
/// (appsettings, tela de configurações etc.).
/// </summary>
public interface IOpcoesGeracao
{
    /// <summary>Temperatura de amostragem (0 a 2), ou nulo para manter o padrão do provedor.</summary>
    double? Temperatura { get; }
}
