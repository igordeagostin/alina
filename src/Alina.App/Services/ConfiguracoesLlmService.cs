using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Alina.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;

namespace Alina.App.Services;

/// <summary>
/// Preferências do LLM ajustáveis pela tela de configurações: modelo do cérebro e
/// chave da API. Sobrepõem o que veio do appsettings/user-secrets.
///
/// A chave é guardada apenas neste computador, criptografada via DPAPI (escopo do
/// usuário do Windows), num JSON dentro da pasta de dados (fora do repositório) —
/// nunca é versionada nem sai da máquina.
/// </summary>
public sealed class ConfiguracoesLlmService
{
    private sealed class Persistido
    {
        public string? Modelo { get; set; }

        /// <summary>Chave da API cifrada com DPAPI e codificada em base64.</summary>
        public string? ChaveProtegida { get; set; }
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _arquivo;
    private readonly IConfiguration _config;
    private Persistido _estado = new();

    /// <summary>Modelos oferecidos, do mais barato ao mais caro.</summary>
    public IReadOnlyList<ModeloLlm> ModelosDisponiveis { get; } =
    [
        new("gpt-4o-mini", "GPT-4o mini", "💲 econômico"),
        new("gpt-4.1-mini", "GPT-4.1 mini", "💲 econômico"),
        new("gpt-4.1", "GPT-4.1", "💲💲 intermediário"),
        new("gpt-4o", "GPT-4o", "💲💲💲 avançado"),
    ];

    public ConfiguracoesLlmService(StorageOptions armazenamento, IConfiguration config)
    {
        _config = config;
        _arquivo = Path.Combine(armazenamento.ResolveDataDirectory(), "llm.json");
        Carregar();
    }

    /// <summary>Modelo atual: o salvo pelo usuário ou, na falta dele, o do appsettings.</summary>
    public string ModeloAtual =>
        !string.IsNullOrWhiteSpace(_estado.Modelo)
            ? _estado.Modelo!
            : _config[$"{LlmOptions.SectionName}:Model"] ?? "gpt-4o-mini";

    /// <summary>Indica se há uma chave configurada (pela UI ou por user-secrets/env).</summary>
    public bool ChaveDefinida =>
        !string.IsNullOrWhiteSpace(_estado.ChaveProtegida)
        || !string.IsNullOrWhiteSpace(_config[$"{LlmOptions.SectionName}:ApiKey"]);

    /// <summary>Sobrepõe as preferências salvas nas opções vindas do appsettings/user-secrets.</summary>
    public void AplicarEm(LlmOptions o)
    {
        if (!string.IsNullOrWhiteSpace(_estado.Modelo))
        {
            o.Provider = LlmProvider.OpenAI;
            o.Model = _estado.Modelo!;
        }

        string? chave = DecifrarChave();
        if (!string.IsNullOrWhiteSpace(chave))
        {
            o.ApiKey = chave;
        }
    }

    /// <summary>Monta as opções efetivas (base do appsettings + preferências salvas).</summary>
    public LlmOptions MontarOpcoesEfetivas()
    {
        LlmOptions o = _config.GetSection(LlmOptions.SectionName).Get<LlmOptions>() ?? new LlmOptions();
        AplicarEm(o);
        return o;
    }

    /// <param name="modelo">Id do modelo escolhido.</param>
    /// <param name="chaveDigitada">Vazio = manter a chave atual; preenchido = substituir.</param>
    public void Salvar(string modelo, string chaveDigitada)
    {
        if (!string.IsNullOrWhiteSpace(modelo))
        {
            _estado.Modelo = modelo.Trim();
        }

        if (!string.IsNullOrWhiteSpace(chaveDigitada))
        {
            _estado.ChaveProtegida = Proteger(chaveDigitada.Trim());
        }

        Persistir();
    }

    private void Carregar()
    {
        try
        {
            if (File.Exists(_arquivo))
            {
                _estado = JsonSerializer.Deserialize<Persistido>(File.ReadAllText(_arquivo), JsonOptions) ?? new Persistido();
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            _estado = new Persistido();
        }
    }

    private void Persistir()
    {
        try
        {
            string? dir = Path.GetDirectoryName(_arquivo);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(_arquivo, JsonSerializer.Serialize(_estado, JsonOptions));
        }
        catch (IOException)
        {
        }
    }

    private string? DecifrarChave()
    {
        if (string.IsNullOrWhiteSpace(_estado.ChaveProtegida))
        {
            return null;
        }

        try
        {
            byte[] cifrado = Convert.FromBase64String(_estado.ChaveProtegida!);
            byte[] claro = ProtectedData.Unprotect(cifrado, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(claro);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            return null;
        }
    }

    private static string Proteger(string valor)
    {
        byte[] cifrado = ProtectedData.Protect(Encoding.UTF8.GetBytes(valor), null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(cifrado);
    }
}
