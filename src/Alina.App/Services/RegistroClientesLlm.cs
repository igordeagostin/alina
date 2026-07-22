using Alina.Infrastructure.Configuration;
using Alina.Infrastructure.Llm;
using Alina.Tools.ClaudeCode;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Alina.App.Services;

/// <summary>
/// Um cliente de chat por <see cref="PapelLlm"/>, criado sob demanda a partir das
/// preferências salvas. Cada papel pode apontar para um provedor e um modelo diferentes —
/// inclusive para o CLI do Claude Code, que não passa pelo <see cref="LlmClientFactory"/>.
///
/// Cada papel recebe uma fachada estável: ao salvar a tela de configurações, o cliente
/// interno é trocado e quem já tinha a referência passa a usar o novo sem reiniciar.
/// </summary>
public sealed class RegistroClientesLlm
{
    private readonly ConfiguracoesLlmService _configuracoes;
    private readonly ClaudeCodeOptions _claudeCode;
    private readonly ILoggerFactory _loggerFactory;
    private readonly Lock _sync = new();
    private readonly Dictionary<PapelLlm, ReconfigurableChatClient> _clientes = [];

    public RegistroClientesLlm(
        ConfiguracoesLlmService configuracoes,
        ClaudeCodeOptions claudeCode,
        ILoggerFactory loggerFactory)
    {
        _configuracoes = configuracoes;
        _claudeCode = claudeCode;
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// Cliente do papel. Se ele não puder ser criado — tipicamente por falta da chave da
    /// OpenAI —, cai no cliente da conversa: a funcionalidade segue de pé com o modelo do
    /// dia a dia em vez de quebrar a tela.
    /// </summary>
    public IChatClient Obter(PapelLlm papel)
    {
        lock (_sync)
        {
            if (_clientes.TryGetValue(papel, out ReconfigurableChatClient? existente))
            {
                return existente;
            }

            IChatClient interno;
            try
            {
                interno = Criar(papel);
            }
            catch (InvalidOperationException) when (papel != PapelLlm.Conversa)
            {
                return Obter(PapelLlm.Conversa);
            }

            ReconfigurableChatClient cliente = new ReconfigurableChatClient(interno);
            _clientes[papel] = cliente;
            return cliente;
        }
    }

    /// <summary>
    /// Aplica as preferências atuais a todos os clientes já criados. Um papel que não puder
    /// ser recriado é ignorado — os demais seguem sendo atualizados e o cliente anterior
    /// continua válido até a configuração ficar completa.
    /// </summary>
    public void Reconfigurar()
    {
        lock (_sync)
        {
            foreach ((PapelLlm papel, ReconfigurableChatClient cliente) in _clientes)
            {
                try
                {
                    cliente.Reconfigurar(Criar(papel));
                }
                catch (InvalidOperationException)
                {
                }
            }
        }
    }

    private IChatClient Criar(PapelLlm papel)
    {
        PerfilLlm perfil = _configuracoes.PerfilDe(papel);

        return perfil.Provedor == LlmProvider.ClaudeCode
            ? new ClienteChatClaudeCode(_claudeCode, perfil.Modelo)
            : LlmClientFactory.Create(_configuracoes.MontarOpcoesEfetivas(papel), _loggerFactory);
    }
}
