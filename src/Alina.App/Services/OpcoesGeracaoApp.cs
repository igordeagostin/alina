using Alina.Core.Orchestration;

namespace Alina.App.Services;

/// <summary>
/// Opções de geração do head gráfico: lê a temperatura direto da tela de configurações
/// a cada turno, de modo que o ajuste vale sem reiniciar.
/// </summary>
public sealed class OpcoesGeracaoApp(ConfiguracoesLlmService configuracoes) : IOpcoesGeracao
{
    public double? Temperatura => configuracoes.Temperatura;
}
