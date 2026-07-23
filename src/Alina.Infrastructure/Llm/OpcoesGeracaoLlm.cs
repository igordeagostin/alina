using Alina.Core.Orchestration;
using Alina.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Alina.Infrastructure.Llm;

/// <summary>
/// Fonte padrão das opções de geração: lê a temperatura configurada em
/// <see cref="LlmOptions"/> (appsettings/user-secrets). Heads de UI que ajustam a
/// temperatura em runtime podem sobrepor este registro por uma implementação própria.
/// </summary>
public sealed class OpcoesGeracaoLlm(IOptions<LlmOptions> opcoes) : IOpcoesGeracao
{
    public double? Temperatura => opcoes.Value.Temperature;
}
