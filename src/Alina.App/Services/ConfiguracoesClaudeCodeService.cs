using System.IO;
using System.Text.Json;
using Alina.Infrastructure.Configuration;
using Alina.Tools.ClaudeCode;

namespace Alina.App.Services;

/// <summary>
/// Preferência do modelo do Claude Code ajustável pela tela de configurações. Sobrepõe o
/// que veio do appsettings, aplicando o valor diretamente no singleton
/// <see cref="ClaudeCodeOptions"/> — como a tool lê <c>Model</c> a cada delegação, a troca
/// vale imediatamente, sem reiniciar.
///
/// O valor (apenas o alias/ID do modelo, nada sensível) é guardado num JSON dentro da pasta
/// de dados, fora do repositório.
/// </summary>
public sealed class ConfiguracoesClaudeCodeService
{
    private sealed class Persistido
    {
        public string? Modelo { get; set; }
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _arquivo;
    private readonly ClaudeCodeOptions _opcoes;
    private readonly string? _padrao;
    private Persistido _estado = new();

    /// <summary>Modelos oferecidos; o primeiro (Id vazio) mantém o padrão da assinatura.</summary>
    public IReadOnlyList<ModeloClaudeCode> ModelosDisponiveis { get; } =
    [
        new("", "Padrão da assinatura", "usa o modelo configurado no Claude Code"),
        new("opus", "Claude Opus", "mais capaz"),
        new("sonnet", "Claude Sonnet", "equilibrado"),
        new("haiku", "Claude Haiku", "mais rápido"),
    ];

    public ConfiguracoesClaudeCodeService(StorageOptions armazenamento, ClaudeCodeOptions opcoes)
    {
        _opcoes = opcoes;
        _padrao = string.IsNullOrWhiteSpace(opcoes.Model) ? null : opcoes.Model;
        _arquivo = Path.Combine(armazenamento.ResolveDataDirectory(), "claude-code.json");
        Carregar();
        Aplicar();
    }

    /// <summary>Modelo atual: o salvo pelo usuário ou, na falta dele, o do appsettings.</summary>
    public string ModeloAtual => _estado.Modelo ?? _padrao ?? string.Empty;

    /// <param name="modelo">Alias/ID do modelo; vazio = padrão da assinatura.</param>
    public void Salvar(string modelo)
    {
        _estado.Modelo = string.IsNullOrWhiteSpace(modelo) ? null : modelo.Trim();
        Aplicar();
        Persistir();
    }

    private void Aplicar() =>
        _opcoes.Model = string.IsNullOrWhiteSpace(_estado.Modelo) ? _padrao : _estado.Modelo;

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
