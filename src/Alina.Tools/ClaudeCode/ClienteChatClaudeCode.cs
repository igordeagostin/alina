using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
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
/// Cada chamada é um processo novo e sem estado: o histórico é achatado num único prompt e
/// nada que exija permissão é liberado — a resposta esperada é texto.
/// </summary>
public sealed class ClienteChatClaudeCode : IChatClient
{
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
        string texto = await ExecutarAsync(MontarPrompt(messages), cancellationToken);
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, texto));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ChatResponse resposta = await GetResponseAsync(messages, options, cancellationToken);
        yield return new ChatResponseUpdate(ChatRole.Assistant, resposta.Text);
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

    private async Task<string> ExecutarAsync(string prompt, CancellationToken cancellationToken)
    {
        try
        {
            using Process processo = new Process { StartInfo = MontarStartInfo() };
            processo.Start();

            await processo.StandardInput.WriteAsync(prompt);
            processo.StandardInput.Close();

            using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(_opcoes.TimeoutSeconds));

            Task<string> saida = processo.StandardOutput.ReadToEndAsync(timeout.Token);
            Task<string> erro = processo.StandardError.ReadToEndAsync(timeout.Token);

            try
            {
                await processo.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                Encerrar(processo);
                return $"O Claude Code excedeu o tempo limite de {_opcoes.TimeoutSeconds}s e foi encerrado.";
            }

            return Interpretar(await saida, await erro);
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
        };

        if (!string.IsNullOrWhiteSpace(_opcoes.DefaultWorkingDirectory)
            && Directory.Exists(_opcoes.DefaultWorkingDirectory))
        {
            psi.WorkingDirectory = _opcoes.DefaultWorkingDirectory;
        }

        psi.ArgumentList.Add("-p");
        psi.ArgumentList.Add("--output-format");
        psi.ArgumentList.Add("json");

        if (_modelo is not null)
        {
            psi.ArgumentList.Add("--model");
            psi.ArgumentList.Add(_modelo);
        }

        return psi;
    }

    internal static string Interpretar(string saida, string erro)
    {
        if (saida.Length > 0)
        {
            try
            {
                ClaudeCodeResult? resultado = JsonSerializer.Deserialize<ClaudeCodeResult>(saida);
                if (resultado is not null && !resultado.IsError && !string.IsNullOrWhiteSpace(resultado.Result))
                {
                    return resultado.Result!;
                }

                if (!string.IsNullOrWhiteSpace(resultado?.Result))
                {
                    return $"O Claude Code retornou um erro: {resultado!.Result}";
                }
            }
            catch (JsonException)
            {
                return saida;
            }
        }

        return string.IsNullOrWhiteSpace(erro)
            ? "O Claude Code não retornou resposta."
            : $"O Claude Code falhou: {erro.Trim()}";
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
}
