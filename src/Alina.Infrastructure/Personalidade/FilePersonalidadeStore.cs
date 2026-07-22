using System.Text.Encodings.Web;
using System.Text.Json;
using Alina.Core.Personalidade;
using Alina.Infrastructure.Configuration;

namespace Alina.Infrastructure.Personalidade;

/// <summary>
/// Personalidade persistida num JSON na pasta de dados. Relê o arquivo sempre que ele
/// muda no disco, então editar pela tela de configurações (ou na mão) vale já no
/// próximo turno, sem reiniciar.
/// </summary>
public sealed class FilePersonalidadeStore : IPersonalidadeStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly string _arquivo;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private PerfilPersonalidade _cache = new();
    private DateTime _carimbo = DateTime.MinValue;
    private bool _carregado;

    public FilePersonalidadeStore(string arquivo) => _arquivo = arquivo;

    public FilePersonalidadeStore(StorageOptions options)
        : this(Path.Combine(options.ResolveDataDirectory(), "personalidade.json")) { }

    public async Task<PerfilPersonalidade> ObterAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            DateTime carimbo = File.Exists(_arquivo) ? File.GetLastWriteTimeUtc(_arquivo) : DateTime.MinValue;
            if (!_carregado || carimbo != _carimbo)
            {
                _cache = await LerAsync(cancellationToken);
                _carimbo = carimbo;
                _carregado = true;
            }

            return _cache.Clonar();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SalvarAsync(PerfilPersonalidade perfil, CancellationToken cancellationToken = default)
    {
        PerfilPersonalidade normalizado = perfil.Normalizado();

        await _gate.WaitAsync(cancellationToken);
        try
        {
            string? diretorio = Path.GetDirectoryName(_arquivo);
            if (!string.IsNullOrWhiteSpace(diretorio))
            {
                Directory.CreateDirectory(diretorio);
            }

            await File.WriteAllTextAsync(_arquivo, JsonSerializer.Serialize(normalizado, JsonOptions), cancellationToken);

            _cache = normalizado;
            _carimbo = File.GetLastWriteTimeUtc(_arquivo);
            _carregado = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<PerfilPersonalidade> LerAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(_arquivo))
            {
                return new PerfilPersonalidade();
            }

            string json = await File.ReadAllTextAsync(_arquivo, cancellationToken);
            PerfilPersonalidade? lido = JsonSerializer.Deserialize<PerfilPersonalidade>(json, JsonOptions);
            return (lido ?? new PerfilPersonalidade()).Normalizado();
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return new PerfilPersonalidade();
        }
    }
}
