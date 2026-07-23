using System.Text;
using Microsoft.Extensions.AI;

namespace Alina.Core.Geracao;

/// <summary>
/// Consome a resposta do modelo em streaming, devolvendo o texto completo no fim e
/// reportando a fala parcial pelo caminho. É o que os geradores de habilidade e de
/// ferramenta usam no lugar de esperar a resposta inteira calada.
/// </summary>
public static class LeituraStream
{
    public static async Task<string> AcumularAsync(
        IChatClient client,
        IReadOnlyList<ChatMessage> mensagens,
        IProgress<ProgressoGeracao>? progresso,
        CancellationToken cancellationToken = default)
    {
        progresso?.Report(new ProgressoGeracao(FaseGeracao.Preparando, string.Empty));

        StringBuilder bruto = new StringBuilder();
        LeitorMensagemParcial leitor = new LeitorMensagemParcial();

        await foreach (ChatResponseUpdate atualizacao in
            client.GetStreamingResponseAsync(mensagens, new ChatOptions(), cancellationToken))
        {
            string texto = atualizacao.Text;
            if (texto.Length == 0)
            {
                continue;
            }

            bruto.Append(texto);

            ProgressoGeracao? passo = leitor.Acrescentar(texto);
            if (passo is not null)
            {
                progresso?.Report(passo);
            }
        }

        return bruto.ToString();
    }
}
