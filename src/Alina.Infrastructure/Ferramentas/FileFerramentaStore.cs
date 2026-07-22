using System.Globalization;
using System.Text;
using System.Text.Json;
using Alina.Core.Ferramentas;
using Alina.Infrastructure.Configuration;

namespace Alina.Infrastructure.Ferramentas;

/// <summary>
/// Ferramentas declarativas persistidas como arquivos <c>*.tool.json</c> numa pasta
/// dedicada dentro da pasta de dados do usuário. Serializa a escrita com um semáforo,
/// já que a UI, as tools e o provider podem tocar os arquivos concorrentemente.
/// Na primeira execução (pasta ainda inexistente), semeia as ferramentas padrão.
/// </summary>
public sealed class FileFerramentaStore : IFerramentaStore
{
    private const string Extensao = ".tool.json";

    private static readonly JsonSerializerOptions OpcoesLeitura = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static readonly JsonSerializerOptions OpcoesEscrita = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly string _diretorio;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public FileFerramentaStore(string diretorio)
    {
        _diretorio = diretorio;
        bool nova = !Directory.Exists(_diretorio);
        Directory.CreateDirectory(_diretorio);
        if (nova)
        {
            Semear();
        }
    }

    public FileFerramentaStore(StorageOptions options)
        : this(options.ResolveFerramentasDirectory()) { }

    public async Task<IReadOnlyList<FerramentaResumo>> ListarAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            List<FerramentaResumo> resumos = new List<FerramentaResumo>();
            foreach (string arquivo in Directory.EnumerateFiles(_diretorio, "*" + Extensao, SearchOption.TopDirectoryOnly))
            {
                DefinicaoFerramenta? definicao = Ler(arquivo);
                if (definicao is not null)
                {
                    resumos.Add(new FerramentaResumo(definicao.Nome, definicao.Descricao, definicao.ExigeConfirmacao));
                }
            }

            return resumos
                .OrderBy(r => r.Nome, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<DefinicaoFerramenta?> ObterAsync(string nome, CancellationToken cancellationToken = default)
    {
        string caminho = CaminhoDe(nome);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return File.Exists(caminho) ? Ler(caminho) : null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SalvarAsync(DefinicaoFerramenta definicao, CancellationToken cancellationToken = default)
    {
        string slug = Slugificar(definicao.Nome);
        if (slug.Length == 0)
        {
            throw new ArgumentException("Ferramenta sem nome válido.", nameof(definicao));
        }

        definicao.Nome = slug;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            string texto = JsonSerializer.Serialize(definicao, OpcoesEscrita);
            await File.WriteAllTextAsync(Path.Combine(_diretorio, slug + Extensao), texto, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> RemoverAsync(string nome, CancellationToken cancellationToken = default)
    {
        string caminho = CaminhoDe(nome);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(caminho))
            {
                return false;
            }

            File.Delete(caminho);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task<bool> ExisteAsync(string nome, CancellationToken cancellationToken = default)
        => Task.FromResult(File.Exists(CaminhoDe(nome)));

    public IReadOnlyList<DefinicaoFerramenta> LerDefinicoes()
    {
        _gate.Wait();
        try
        {
            List<DefinicaoFerramenta> definicoes = new List<DefinicaoFerramenta>();
            foreach (string arquivo in Directory.EnumerateFiles(_diretorio, "*" + Extensao, SearchOption.TopDirectoryOnly))
            {
                DefinicaoFerramenta? definicao = Ler(arquivo);
                if (definicao is not null && !string.IsNullOrWhiteSpace(definicao.Nome) && !string.IsNullOrWhiteSpace(definicao.Comando))
                {
                    definicoes.Add(definicao);
                }
            }

            return definicoes;
        }
        finally
        {
            _gate.Release();
        }
    }

    private string CaminhoDe(string nome) => Path.Combine(_diretorio, Slugificar(nome) + Extensao);

    private static DefinicaoFerramenta? Ler(string caminho)
    {
        try
        {
            DefinicaoFerramenta? definicao = JsonSerializer.Deserialize<DefinicaoFerramenta>(File.ReadAllText(caminho), OpcoesLeitura);
            if (definicao is not null && string.IsNullOrWhiteSpace(definicao.Nome))
            {
                definicao.Nome = Path.GetFileNameWithoutExtension(caminho).Replace(".tool", string.Empty, StringComparison.OrdinalIgnoreCase);
            }

            return definicao;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private void Semear()
    {
        foreach (DefinicaoFerramenta definicao in FerramentasPadrao.Todas())
        {
            string slug = Slugificar(definicao.Nome);
            if (slug.Length == 0)
            {
                continue;
            }

            string texto = JsonSerializer.Serialize(definicao, OpcoesEscrita);
            File.WriteAllText(Path.Combine(_diretorio, slug + Extensao), texto);
        }
    }

    private static string Slugificar(string valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return string.Empty;
        }

        StringBuilder semAcento = new StringBuilder();
        foreach (char c in valor.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                semAcento.Append(c);
            }
        }

        StringBuilder slug = new StringBuilder();
        bool separadorPendente = false;
        foreach (char c in semAcento.ToString().Normalize(NormalizationForm.FormC))
        {
            if (c is (>= 'a' and <= 'z') or (>= '0' and <= '9'))
            {
                if (separadorPendente && slug.Length > 0)
                {
                    slug.Append('_');
                }

                separadorPendente = false;
                slug.Append(c);
            }
            else
            {
                separadorPendente = true;
            }
        }

        return slug.ToString();
    }
}
