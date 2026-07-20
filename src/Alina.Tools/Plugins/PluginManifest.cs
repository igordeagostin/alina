using System.Text.Json.Serialization;

namespace Alina.Tools.Plugins;

/// <summary>
/// Descreve um plugin declarativo: um comando externo que vira uma tool da
/// Alina. Carregado de um arquivo JSON na pasta de plugins.
/// </summary>
public sealed class PluginManifest
{
    /// <summary>Nome da tool exposta ao LLM (ex: "deploy_app").</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Descrição do que o plugin faz (ajuda o LLM a decidir usá-lo).</summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>Se a execução exige confirmação do usuário. Default true (segurança).</summary>
    [JsonPropertyName("requiresConfirmation")]
    public bool RequiresConfirmation { get; set; } = true;

    /// <summary>Executável a ser chamado (ex: "pwsh", "git", "node").</summary>
    [JsonPropertyName("command")]
    public string Command { get; set; } = string.Empty;

    /// <summary>
    /// Argumentos passados ao comando. Podem conter placeholders <c>{parametro}</c>
    /// que serão substituídos pelos valores informados pelo LLM.
    /// </summary>
    [JsonPropertyName("args")]
    public string[] Args { get; set; } = Array.Empty<string>();

    /// <summary>Parâmetros que o LLM deve preencher.</summary>
    [JsonPropertyName("parameters")]
    public List<PluginParameter> Parameters { get; set; } = new();

    /// <summary>Diretório de trabalho (pode conter placeholders). Opcional.</summary>
    [JsonPropertyName("workingDirectory")]
    public string? WorkingDirectory { get; set; }

    /// <summary>Tempo máximo de execução, em segundos.</summary>
    [JsonPropertyName("timeoutSeconds")]
    public int TimeoutSeconds { get; set; } = 300;
}

/// <summary>Um parâmetro de plugin preenchido pelo LLM.</summary>
public sealed class PluginParameter
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("required")]
    public bool Required { get; set; } = true;
}
