using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Alina.Core.IO;
using Microsoft.Extensions.AI;

namespace Alina.Tools.ClaudeCode;

/// <summary>
/// <see cref="IChatClient"/> que responde pelo CLI do Claude Code (<c>claude -p</c>), ou seja,
/// pela assinatura do usuário em vez da API cobrada por uso.
///
/// Serve aos papéis que só precisam de texto — gerar e treinar habilidades, criar ferramentas.
/// Não serve para conversar e executar: o CLI traz as ferramentas dele e não aceita as
/// <c>ITool</c> em C# da Alina, então o function-calling do orquestrador não atravessa.
///
/// Como aqui a tarefa é só redigir, o CLI é chamado no modo mais leve que existe: sem
/// ferramentas, sem MCP, sem skills e com o prompt de sistema trocado por uma linha —
/// o prompt padrão do Claude Code custa alguns milhares de tokens que não servem a nada
/// nesta conversa. A resposta vem em streaming, para a UI mostrar o texto chegando em vez
/// de esperar o documento inteiro.
///
/// Cada chamada é um processo novo e sem estado: o histórico é achatado num único prompt e
/// nada que exija permissão é liberado — a resposta esperada é texto.
/// </summary>
public sealed class ClienteChatClaudeCode : IChatClient
{
    private const string PromptSistema =
        "Você redige texto sob encomenda. Siga à risca o formato pedido na mensagem do usuário " +
        "e responda apenas com ele, sem preâmbulo e sem comentar o formato.";

    private readonly ClaudeCodeOptions _opcoes;
    private readonly string? _modelo;

    /// <param name="modelo">Alias do modelo (<c>opus</c>, <c>sonnet</c>, <c>haiku</c>); vazio = padrão da assinatura.</param>
    public ClienteChatClaudeCode(ClaudeCodeOptions opcoes, string? modelo = null)
    {
        _opcoes = opcoes;
        _modelo = string.IsNullOrWhiteSpace(modelo) ? null : modelo.Trim();
    }

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        string texto = await ExecutarAsync(MontarPrompt(messages), aoReceberTexto: null, cancellationToken);
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, texto));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Channel<string> pedacos = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true,
        });

        string prompt = MontarPrompt(messages);
        Task<string> execucao = Task.Run(async () =>
        {
            try
            {
                return await ExecutarAsync(prompt, texto => pedacos.Writer.TryWrite(texto), cancellationToken);
            }
            finally
            {
                pedacos.Writer.TryComplete();
            }
        }, CancellationToken.None);

        bool emitiuAlgo = false;
        await foreach (string pedaco in pedacos.Reader.ReadAllAsync(cancellationToken))
        {
            emitiuAlgo = true;
            yield return new ChatResponseUpdate(ChatRole.Assistant, pedaco);
        }

        string final = await execucao;
        if (!emitiuAlgo && final.Length > 0)
        {
            yield return new ChatResponseUpdate(ChatRole.Assistant, final);
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose()
    {
    }

    /// <summary>
    /// Achata a conversa num prompt só. As mensagens de sistema viram instruções no topo;
    /// o resto vira um diálogo rotulado, para o CLI saber de quem é cada fala.
    /// </summary>
    internal static string MontarPrompt(IEnumerable<ChatMessage> messages)
    {
        StringBuilder instrucoes = new StringBuilder();
        StringBuilder dialogo = new StringBuilder();

        foreach (ChatMessage mensagem in messages)
        {
            string texto = mensagem.Text;
            if (string.IsNullOrWhiteSpace(texto))
            {
                continue;
            }

            if (mensagem.Role == ChatRole.System)
            {
                instrucoes.AppendLine(texto).AppendLine();
            }
            else
            {
                string quem = mensagem.Role == ChatRole.Assistant ? "Você" : "Usuário";
                dialogo.AppendLine($"{quem}: {texto}").AppendLine();
            }
        }

        return new StringBuilder()
            .AppendLine("<instrucoes>")
            .Append(instrucoes)
            .AppendLine("</instrucoes>")
            .AppendLine()
            .AppendLine("<conversa>")
            .Append(dialogo)
            .AppendLine("</conversa>")
            .AppendLine()
            .Append("Responda agora a última fala do usuário, no papel de \"Você\", seguindo as instruções acima. " +
                    "Escreva apenas a resposta, sem comentar o formato nem usar ferramentas.")
            .ToString();
    }

    private async Task<string> ExecutarAsync(
        string prompt,
        Action<string>? aoReceberTexto,
        CancellationToken cancellationToken)
    {
        try
        {
            using Process processo = new Process { StartInfo = MontarStartInfo() };
            processo.Start();

            await processo.StandardInput.WriteAsync(prompt);
            processo.StandardInput.Close();

            using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(_opcoes.TimeoutSeconds));

            StringBuilder erro = new StringBuilder();
            Task leituraErro = Task.Run(async () =>
            {
                string? linha;
                while ((linha = await processo.StandardError.ReadLineAsync(CancellationToken.None)) is not null)
                {
                    erro.AppendLine(linha);
                }
            }, CancellationToken.None);

            LeitorFluxo leitor = new LeitorFluxo(aoReceberTexto);
            try
            {
                string? linha;
                while ((linha = await processo.StandardOutput.ReadLineAsync(timeout.Token)) is not null)
                {
                    leitor.Processar(linha);
                }

                await processo.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                Encerrar(processo);
                return $"O Claude Code excedeu o tempo limite de {_opcoes.TimeoutSeconds}s e foi encerrado.";
            }

            await leituraErro;
            return leitor.Concluir(erro.ToString());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return $"Não consegui falar com o Claude Code: {ex.Message}. " +
                   $"Verifique se o executável '{_opcoes.Executable}' está no PATH ou ajuste 'ClaudeCode:Executable'.";
        }
    }

    private ProcessStartInfo MontarStartInfo()
    {
        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = _opcoes.Executable,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        }.AplicarUtf8();

        if (!string.IsNullOrWhiteSpace(_opcoes.DefaultWorkingDirectory)
            && Directory.Exists(_opcoes.DefaultWorkingDirectory))
        {
            psi.WorkingDirectory = _opcoes.DefaultWorkingDirectory;
        }

        psi.ArgumentList.Add("-p");
        psi.ArgumentList.Add("--output-format");
        psi.ArgumentList.Add("stream-json");
        psi.ArgumentList.Add("--include-partial-messages");
        psi.ArgumentList.Add("--verbose");

        psi.ArgumentList.Add("--system-prompt");
        psi.ArgumentList.Add(PromptSistema);
        psi.ArgumentList.Add("--tools");
        psi.ArgumentList.Add(string.Empty);
        psi.ArgumentList.Add("--strict-mcp-config");
        psi.ArgumentList.Add("--disable-slash-commands");
        psi.ArgumentList.Add("--no-session-persistence");
        if (!string.IsNullOrWhiteSpace(_opcoes.EsforcoTexto))
        {
            psi.ArgumentList.Add("--effort");
            psi.ArgumentList.Add(_opcoes.EsforcoTexto);
        }

        if (_modelo is not null)
        {
            psi.ArgumentList.Add("--model");
            psi.ArgumentList.Add(_modelo);
        }

        return psi;
    }

    private static void Encerrar(Process processo)
    {
        try
        {
            if (!processo.HasExited)
            {
                processo.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
        }
    }

    /// <summary>
    /// Lê as linhas de <c>--output-format stream-json</c>, repassando cada pedaço de texto
    /// assim que chega e guardando o resultado final. Extraído para permitir teste sem subprocesso.
    /// </summary>
    internal sealed class LeitorFluxo
    {
        private readonly Action<string>? _aoReceberTexto;
        private readonly StringBuilder _acumulado = new StringBuilder();
        private string? _resultado;
        private bool _erro;

        public LeitorFluxo(Action<string>? aoReceberTexto = null) => _aoReceberTexto = aoReceberTexto;

        public void Processar(string linha)
        {
            if (string.IsNullOrWhiteSpace(linha))
            {
                return;
            }

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(linha);
            }
            catch (JsonException)
            {
                return;
            }

            using (doc)
            {
                JsonElement raiz = doc.RootElement;
                if (raiz.ValueKind != JsonValueKind.Object
                    || !raiz.TryGetProperty("type", out JsonElement tipo)
                    || tipo.ValueKind != JsonValueKind.String)
                {
                    return;
                }

                switch (tipo.GetString())
                {
                    case "stream_event":
                        Acumular(TextoDoDelta(raiz));
                        break;

                    case "result":
                        _erro = raiz.TryGetProperty("is_error", out JsonElement err)
                            && err.ValueKind is JsonValueKind.True;
                        if (raiz.TryGetProperty("result", out JsonElement res)
                            && res.ValueKind == JsonValueKind.String)
                        {
                            _resultado = res.GetString();
                        }
                        break;
                }
            }
        }

        public string Concluir(string erro)
        {
            string texto = _acumulado.Length > 0 ? _acumulado.ToString() : _resultado ?? string.Empty;

            if (_erro)
            {
                return texto.Length > 0
                    ? $"O Claude Code retornou um erro: {texto}"
                    : $"O Claude Code falhou: {erro.Trim()}";
            }

            if (texto.Length > 0)
            {
                return texto;
            }

            return string.IsNullOrWhiteSpace(erro)
                ? "O Claude Code não retornou resposta."
                : $"O Claude Code falhou: {erro.Trim()}";
        }

        private void Acumular(string? texto)
        {
            if (string.IsNullOrEmpty(texto))
            {
                return;
            }

            _acumulado.Append(texto);
            _aoReceberTexto?.Invoke(texto);
        }

        private static string? TextoDoDelta(JsonElement raiz)
        {
            if (!raiz.TryGetProperty("event", out JsonElement evento)
                || !evento.TryGetProperty("type", out JsonElement tipo)
                || tipo.ValueKind != JsonValueKind.String
                || tipo.GetString() != "content_block_delta"
                || !evento.TryGetProperty("delta", out JsonElement delta)
                || !delta.TryGetProperty("text", out JsonElement texto)
                || texto.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return texto.GetString();
        }
    }
}
