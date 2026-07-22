using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Alina.Core.Tools;
using Microsoft.Extensions.AI;

namespace Alina.Core.Ferramentas;

/// <summary>
/// <see cref="AIFunction"/> de schema dinâmico gerada a partir de uma
/// <see cref="DefinicaoFerramenta"/>. Substitui os placeholders <c>{parametro}</c>
/// pelos valores do LLM, pede confirmação (quando exigida) e executa o comando.
/// Os valores entram via <see cref="ProcessStartInfo.ArgumentList"/> — cada argumento
/// é passado literalmente ao processo, sem reinterpretação por um shell.
/// </summary>
internal sealed class FuncaoFerramenta : AIFunction
{
    private readonly DefinicaoFerramenta _definicao;
    private readonly IConfirmationService _confirmation;
    private readonly JsonElement _schema;

    public FuncaoFerramenta(DefinicaoFerramenta definicao, IConfirmationService confirmation)
    {
        _definicao = definicao;
        _confirmation = confirmation;
        _schema = MontarSchema(definicao);
    }

    public override string Name => _definicao.Nome;

    public override string Description => _definicao.Descricao;

    public override JsonElement JsonSchema => _schema;

    protected override async ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        Dictionary<string, string> valores = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (ParametroFerramenta parametro in _definicao.Parametros)
        {
            arguments.TryGetValue(parametro.Nome, out object? bruto);
            string valor = ComoTexto(bruto);

            if (parametro.Obrigatorio && string.IsNullOrWhiteSpace(valor))
            {
                return $"Erro: parâmetro obrigatório '{parametro.Nome}' não informado.";
            }

            valores[parametro.Nome] = valor;
        }

        string[] args = _definicao.Argumentos.Select(a => Substituir(a, valores)).ToArray();
        string diretorio = Substituir(_definicao.DiretorioTrabalho, valores);

        if (_definicao.ExigeConfirmacao)
        {
            string previa = $"{_definicao.Comando} {string.Join(' ', args)}";
            bool confirmado = await _confirmation.ConfirmAsync($"Executar ferramenta '{_definicao.Nome}'", previa, cancellationToken);
            if (!confirmado)
            {
                return $"Operação cancelada pelo usuário — a ferramenta '{_definicao.Nome}' NÃO foi executada.";
            }
        }

        return await ExecutarProcessoAsync(_definicao.Comando, args, diretorio, _definicao.TimeoutSegundos, cancellationToken);
    }

    private static JsonElement MontarSchema(DefinicaoFerramenta definicao)
    {
        JsonObject propriedades = new JsonObject();
        JsonArray obrigatorios = new JsonArray();

        foreach (ParametroFerramenta parametro in definicao.Parametros)
        {
            propriedades[parametro.Nome] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = parametro.Descricao,
            };

            if (parametro.Obrigatorio)
            {
                obrigatorios.Add(parametro.Nome);
            }
        }

        JsonObject schema = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = propriedades,
        };

        if (obrigatorios.Count > 0)
        {
            schema["required"] = obrigatorios;
        }

        return JsonSerializer.SerializeToElement(schema);
    }

    private static string Substituir(string? modelo, IReadOnlyDictionary<string, string> valores)
    {
        if (string.IsNullOrEmpty(modelo))
        {
            return string.Empty;
        }

        string resultado = modelo;
        foreach ((string chave, string valor) in valores)
        {
            resultado = resultado.Replace("{" + chave + "}", valor, StringComparison.OrdinalIgnoreCase);
        }

        return resultado;
    }

    private static string ComoTexto(object? bruto) => bruto switch
    {
        null => string.Empty,
        string s => s,
        JsonElement { ValueKind: JsonValueKind.String } e => e.GetString() ?? string.Empty,
        JsonElement e => e.ToString(),
        _ => bruto.ToString() ?? string.Empty,
    };

    private static async Task<string> ExecutarProcessoAsync(
        string comando, string[] args, string? diretorioTrabalho, int timeoutSegundos, CancellationToken cancellationToken)
    {
        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = comando,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        if (!string.IsNullOrWhiteSpace(diretorioTrabalho))
        {
            psi.WorkingDirectory = diretorioTrabalho;
        }

        foreach (string arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        try
        {
            using Process processo = new Process { StartInfo = psi };
            StringBuilder saida = new StringBuilder();

            processo.OutputDataReceived += (_, e) => { if (e.Data is not null) saida.AppendLine(e.Data); };
            processo.ErrorDataReceived += (_, e) => { if (e.Data is not null) saida.AppendLine(e.Data); };

            processo.Start();
            processo.BeginOutputReadLine();
            processo.BeginErrorReadLine();

            using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSegundos));

            try
            {
                await processo.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                TentarEncerrar(processo);
                return $"Erro: a ferramenta excedeu {timeoutSegundos}s e foi encerrada.";
            }

            string texto = saida.ToString().TrimEnd();
            return $"[exit {processo.ExitCode}]\n{texto}";
        }
        catch (Exception ex)
        {
            return $"Erro ao executar a ferramenta: {ex.Message}. Verifique se '{comando}' está no PATH.";
        }
    }

    private static void TentarEncerrar(Process processo)
    {
        try
        {
            if (!processo.HasExited)
            {
                processo.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // ignora
        }
    }
}
