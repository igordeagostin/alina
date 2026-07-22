using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Alina.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;

namespace Alina.App.Services;

/// <summary>
/// Preferências do LLM ajustáveis pela tela de configurações: um provedor e um modelo por
/// papel (conversa, habilidades, ferramentas), mais a chave da OpenAI. Sobrepõem o que veio
/// do appsettings/user-secrets.
///
/// Papéis sem escolha própria seguem o da conversa, que por sua vez cai no appsettings.
///
/// A única chave de API em jogo é a da OpenAI: os modelos Claude oferecidos aqui rodam pelo
/// CLI do Claude Code, na assinatura, sem chave. Ela é guardada apenas neste computador,
/// criptografada via DPAPI (escopo do usuário do Windows), num JSON dentro da pasta de dados
/// (fora do repositório) — nunca é versionada nem sai da máquina.
/// </summary>
public sealed class ConfiguracoesLlmService
{
    private sealed class PerfilPersistido
    {
        public LlmProvider Provedor { get; set; }
        public string? Modelo { get; set; }
    }

    private sealed class Persistido
    {
        /// <summary>Formato antigo (modelo único da OpenAI); migrado para o papel Conversa.</summary>
        public string? Modelo { get; set; }

        /// <summary>Formato antigo (chave única); migrada para <see cref="ChaveOpenAiProtegida"/>.</summary>
        public string? ChaveProtegida { get; set; }

        /// <summary>Chave da OpenAI cifrada com DPAPI e codificada em base64.</summary>
        public string? ChaveOpenAiProtegida { get; set; }

        public Dictionary<string, PerfilPersistido> Papeis { get; set; } = [];
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _arquivo;
    private readonly IConfiguration _config;
    private Persistido _estado = new();

    /// <summary>
    /// Modelos oferecidos, do mais barato ao mais caro dentro de cada provedor. Os modelos
    /// Claude entram pelo CLI do Claude Code (assinatura), então só aparecem nos papéis que
    /// não dependem das ferramentas em C# da Alina.
    /// </summary>
    public IReadOnlyList<ModeloLlm> ModelosDisponiveis { get; } =
    [
        new(LlmProvider.OpenAI, "gpt-4o-mini", "GPT-4o mini", "💲 econômico"),
        new(LlmProvider.OpenAI, "gpt-4.1-mini", "GPT-4.1 mini", "💲 econômico"),
        new(LlmProvider.OpenAI, "gpt-4.1", "GPT-4.1", "💲💲 intermediário"),
        new(LlmProvider.OpenAI, "gpt-4o", "GPT-4o", "💲💲💲 avançado"),
        new(LlmProvider.ClaudeCode, "haiku", "Claude Haiku", "assinatura — mais rápido"),
        new(LlmProvider.ClaudeCode, "sonnet", "Claude Sonnet", "assinatura — equilibrado"),
        new(LlmProvider.ClaudeCode, "opus", "Claude Opus", "assinatura — mais capaz"),
    ];

    /// <summary>Papéis configuráveis, na ordem em que aparecem na tela.</summary>
    public IReadOnlyList<PapelLlmInfo> Papeis { get; } =
    [
        new(PapelLlm.Conversa, "Conversa e execução",
            "O cérebro do dia a dia: entende o pedido, escolhe as ferramentas e responde. Precisa das ferramentas em C# da Alina, que só a API oferece.",
            Herdavel: false, ExigeFerramentas: true),
        new(PapelLlm.Habilidades, "Habilidades",
            "Criar, editar e treinar habilidades. Descobrir por que uma execução falhou e reescrever o documento pede raciocínio; como só troca texto, pode rodar pelo Claude Code.",
            Herdavel: true, ExigeFerramentas: false),
        new(PapelLlm.Ferramentas, "Ferramentas",
            "Criar e ajustar as ferramentas declarativas a partir da conversa.",
            Herdavel: true, ExigeFerramentas: false),
    ];

    /// <summary>Modelos que o papel pode usar: sem ferramentas, o Claude Code também entra.</summary>
    public IEnumerable<ModeloLlm> ModelosDe(PapelLlmInfo papel) =>
        papel.ExigeFerramentas
            ? ModelosDisponiveis.Where(m => m.Provedor != LlmProvider.ClaudeCode)
            : ModelosDisponiveis;

    public ConfiguracoesLlmService(StorageOptions armazenamento, IConfiguration config)
    {
        _config = config;
        _arquivo = Path.Combine(armazenamento.ResolveDataDirectory(), "llm.json");
        Carregar();
    }

    /// <summary>Provedor e modelo efetivos do papel: o dele, o da conversa ou o do appsettings.</summary>
    public PerfilLlm PerfilDe(PapelLlm papel)
    {
        if (_estado.Papeis.TryGetValue(papel.ToString(), out PerfilPersistido? perfil)
            && !string.IsNullOrWhiteSpace(perfil.Modelo))
        {
            return new PerfilLlm(perfil.Provedor, perfil.Modelo!);
        }

        return papel == PapelLlm.Conversa ? PerfilDoAppsettings() : PerfilDe(PapelLlm.Conversa);
    }

    /// <summary>Chave da OpenAI: a salva pela UI ou, na falta dela, a do appsettings/user-secrets.</summary>
    public string? ChaveOpenAi()
    {
        string? salva = Decifrar(_estado.ChaveOpenAiProtegida);
        if (!string.IsNullOrWhiteSpace(salva))
        {
            return salva;
        }

        return PerfilDoAppsettings().Provedor == LlmProvider.OpenAI
            ? _config[$"{LlmOptions.SectionName}:ApiKey"]
            : null;
    }

    /// <summary>Indica se há uma chave da OpenAI utilizável.</summary>
    public bool ChaveOpenAiDefinida => !string.IsNullOrWhiteSpace(ChaveOpenAi());

    /// <summary>
    /// Seleção atual do papel no formato usado pelos selects, ou vazio quando o papel
    /// herda o modelo da conversa.
    /// </summary>
    public string SelecaoDe(PapelLlm papel)
    {
        if (papel != PapelLlm.Conversa && !_estado.Papeis.ContainsKey(papel.ToString()))
        {
            return string.Empty;
        }

        PerfilLlm perfil = PerfilDe(papel);
        return $"{perfil.Provedor}|{perfil.Modelo}";
    }

    /// <summary>Monta as opções efetivas do papel (base do appsettings + preferências salvas).</summary>
    public LlmOptions MontarOpcoesEfetivas(PapelLlm papel)
    {
        LlmOptions o = _config.GetSection(LlmOptions.SectionName).Get<LlmOptions>() ?? new LlmOptions();
        PerfilLlm perfil = PerfilDe(papel);

        o.Provider = perfil.Provedor;
        o.Model = perfil.Modelo;
        o.EmbeddingApiKey = ChaveOpenAi();

        if (perfil.Provedor == LlmProvider.OpenAI)
        {
            o.ApiKey = ChaveOpenAi();
        }

        return o;
    }

    /// <summary>
    /// Sobrepõe as preferências do papel de conversa nas opções vindas do appsettings.
    /// É o que o restante do app enxerga como <c>IOptions&lt;LlmOptions&gt;</c>.
    /// </summary>
    public void AplicarEm(LlmOptions o)
    {
        PerfilLlm perfil = PerfilDe(PapelLlm.Conversa);
        o.Provider = perfil.Provedor;
        o.Model = perfil.Modelo;

        string? chave = ChaveOpenAi();
        if (perfil.Provedor == LlmProvider.OpenAI && !string.IsNullOrWhiteSpace(chave))
        {
            o.ApiKey = chave;
        }

        o.EmbeddingApiKey = chave;
    }

    /// <param name="selecoes">Seleção por papel; vazio = herdar o papel de conversa.</param>
    /// <param name="chaveOpenAi">Vazio = manter a chave atual; preenchido = substituir.</param>
    public void Salvar(IReadOnlyDictionary<PapelLlm, string> selecoes, string chaveOpenAi)
    {
        foreach ((PapelLlm papel, string selecao) in selecoes)
        {
            PerfilLlm? perfil = Interpretar(selecao);
            if (perfil is null)
            {
                _estado.Papeis.Remove(papel.ToString());
            }
            else
            {
                _estado.Papeis[papel.ToString()] = new PerfilPersistido
                {
                    Provedor = perfil.Provedor,
                    Modelo = perfil.Modelo,
                };
            }
        }

        if (!string.IsNullOrWhiteSpace(chaveOpenAi))
        {
            _estado.ChaveOpenAiProtegida = Proteger(chaveOpenAi.Trim());
        }

        _estado.Modelo = null;
        _estado.ChaveProtegida = null;

        Persistir();
    }

    private PerfilLlm PerfilDoAppsettings()
    {
        LlmProvider provedor = Enum.TryParse(_config[$"{LlmOptions.SectionName}:Provider"], ignoreCase: true, out LlmProvider lido)
            ? lido
            : LlmProvider.OpenAI;

        return new PerfilLlm(provedor, _config[$"{LlmOptions.SectionName}:Model"] ?? "gpt-4o-mini");
    }

    private static PerfilLlm? Interpretar(string selecao)
    {
        string[] partes = selecao.Split('|');
        if (partes.Length != 2
            || partes[1].Length == 0
            || !Enum.TryParse(partes[0], ignoreCase: true, out LlmProvider provedor))
        {
            return null;
        }

        return new PerfilLlm(provedor, partes[1]);
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

        Migrar();
    }

    private void Migrar()
    {
        if (!string.IsNullOrWhiteSpace(_estado.Modelo) && _estado.Papeis.Count == 0)
        {
            _estado.Papeis[PapelLlm.Conversa.ToString()] = new PerfilPersistido
            {
                Provedor = LlmProvider.OpenAI,
                Modelo = _estado.Modelo,
            };
        }

        if (!string.IsNullOrWhiteSpace(_estado.ChaveProtegida) && string.IsNullOrWhiteSpace(_estado.ChaveOpenAiProtegida))
        {
            _estado.ChaveOpenAiProtegida = _estado.ChaveProtegida;
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

    private static string? Decifrar(string? protegida)
    {
        if (string.IsNullOrWhiteSpace(protegida))
        {
            return null;
        }

        try
        {
            byte[] cifrado = Convert.FromBase64String(protegida);
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
