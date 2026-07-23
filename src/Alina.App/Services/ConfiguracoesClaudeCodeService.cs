using System.IO;
using System.Text.Json;
using Alina.Infrastructure.Configuration;
using Alina.Tools.ClaudeCode;

namespace Alina.App.Services;

/// <summary>
/// Preferências do Claude Code ajustáveis pela tela de configurações: modelo e esforço de
/// raciocínio dos papéis de texto. Sobrepõem o que veio do appsettings, aplicando os valores
/// diretamente no singleton <see cref="ClaudeCodeOptions"/> — como cada execução relê as
/// opções, a troca vale imediatamente, sem reiniciar.
///
/// Os valores (alias/ID do modelo e nível de esforço, nada sensível) são guardados num JSON
/// dentro da pasta de dados, fora do repositório.
/// </summary>
public sealed class ConfiguracoesClaudeCodeService
{
    private sealed class Persistido
    {
        public string? Modelo { get; set; }

        public string? EsforcoTexto { get; set; }
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _arquivo;
    private readonly ClaudeCodeOptions _opcoes;
    private readonly string? _padrao;
    private readonly string _esforcoPadrao;
    private Persistido _estado = new();

    /// <summary>Modelos oferecidos; o primeiro (Id vazio) mantém o padrão da assinatura.</summary>
    public IReadOnlyList<ModeloClaudeCode> ModelosDisponiveis { get; } =
    [
        new("", "Padrão da assinatura", "usa o modelo configurado no Claude Code"),
        new("opus", "Claude Opus", "mais capaz"),
        new("sonnet", "Claude Sonnet", "equilibrado"),
        new("haiku", "Claude Haiku", "mais rápido"),
    ];

    /// <summary>Níveis de esforço oferecidos; o primeiro (Id vazio) deixa o CLI decidir.</summary>
    public IReadOnlyList<EsforcoClaudeCode> EsforcosDisponiveis { get; } =
    [
        new("low", "Baixo", "responde quase de imediato"),
        new("medium", "Médio", "pensa um pouco antes de escrever"),
        new("high", "Alto", "raciocina bastante, demora mais"),
        new("", "Padrão do Claude Code", "deixa o CLI decidir"),
    ];

    public ConfiguracoesClaudeCodeService(StorageOptions armazenamento, ClaudeCodeOptions opcoes)
    {
        _opcoes = opcoes;
        _padrao = string.IsNullOrWhiteSpace(opcoes.Model) ? null : opcoes.Model;
        _esforcoPadrao = opcoes.EsforcoTexto ?? string.Empty;
        _arquivo = Path.Combine(armazenamento.ResolveDataDirectory(), "claude-code.json");
        Carregar();
        Aplicar();
    }

    /// <summary>Modelo atual: o salvo pelo usuário ou, na falta dele, o do appsettings.</summary>
    public string ModeloAtual => _estado.Modelo ?? _padrao ?? string.Empty;

    /// <summary>Esforço atual: o salvo pelo usuário ou, na falta dele, o do appsettings.</summary>
    public string EsforcoTextoAtual => _estado.EsforcoTexto ?? _esforcoPadrao;

    /// <param name="modelo">Alias/ID do modelo; vazio = padrão da assinatura.</param>
    /// <param name="esforcoTexto">Nível de esforço ("low"/"medium"/"high"); vazio = padrão do CLI.</param>
    public void Salvar(string modelo, string esforcoTexto)
    {
        _estado.Modelo = string.IsNullOrWhiteSpace(modelo) ? null : modelo.Trim();
        _estado.EsforcoTexto = esforcoTexto.Trim();
        Aplicar();
        Persistir();
    }

    private void Aplicar()
    {
        _opcoes.Model = string.IsNullOrWhiteSpace(_estado.Modelo) ? _padrao : _estado.Modelo;
        _opcoes.EsforcoTexto = _estado.EsforcoTexto ?? _esforcoPadrao;
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
}
