using System.Globalization;
using System.Text;
using Alina.Core.Habilidades;
using Alina.Infrastructure.Configuration;

namespace Alina.Infrastructure.Habilidades;

/// <summary>
/// Habilidades persistidas como arquivos Markdown numa pasta dedicada. Cada arquivo
/// tem um frontmatter simples (<c>nome</c>/<c>descricao</c>) seguido do conteúdo.
/// Serializa a escrita com um semáforo, já que tools e UI podem gravar concorrentemente.
/// </summary>
public sealed class FileHabilidadeStore : IHabilidadeStore
{
    private readonly string _diretorio;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public FileHabilidadeStore(string diretorio)
    {
        _diretorio = diretorio;
        bool nova = !Directory.Exists(_diretorio);
        Directory.CreateDirectory(_diretorio);
        if (nova)
        {
            Semear();
        }
    }

    public FileHabilidadeStore(StorageOptions options)
        : this(options.ResolveHabilidadesDirectory()) { }

    public async Task<IReadOnlyList<HabilidadeResumo>> ListarAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            List<HabilidadeResumo> resumos = new List<HabilidadeResumo>();
            foreach (string arquivo in Directory.EnumerateFiles(_diretorio, "*.md", SearchOption.TopDirectoryOnly))
            {
                Habilidade? habilidade = await LerArquivoAsync(arquivo, cancellationToken);
                if (habilidade is not null)
                {
                    resumos.Add(new HabilidadeResumo(habilidade.Nome, habilidade.Descricao));
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

    public async Task<Habilidade?> ObterAsync(string nome, CancellationToken cancellationToken = default)
    {
        string caminho = CaminhoDe(nome);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return File.Exists(caminho) ? await LerArquivoAsync(caminho, cancellationToken) : null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SalvarAsync(Habilidade habilidade, CancellationToken cancellationToken = default)
    {
        string slug = Slugificar(habilidade.Nome);
        if (slug.Length == 0)
        {
            throw new ArgumentException("Habilidade sem nome válido.", nameof(habilidade));
        }

        habilidade.Nome = slug;
        habilidade.AtualizadaEm = DateTimeOffset.Now;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            string texto = Serializar(habilidade);
            await File.WriteAllTextAsync(Path.Combine(_diretorio, slug + ".md"), texto, cancellationToken);
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

    private string CaminhoDe(string nome) => Path.Combine(_diretorio, Slugificar(nome) + ".md");

    private void Semear()
    {
        foreach (Habilidade habilidade in HabilidadesPadrao.Todas())
        {
            string slug = Slugificar(habilidade.Nome);
            if (slug.Length == 0)
            {
                continue;
            }

            habilidade.Nome = slug;
            File.WriteAllText(Path.Combine(_diretorio, slug + ".md"), Serializar(habilidade));
        }
    }

    private static async Task<Habilidade?> LerArquivoAsync(string caminho, CancellationToken cancellationToken)
    {
        try
        {
            string texto = await File.ReadAllTextAsync(caminho, cancellationToken);
            (string? nome, string? descricao, string? conteudo) = ParsearFrontmatter(texto);
            if (string.IsNullOrWhiteSpace(nome))
            {
                nome = Path.GetFileNameWithoutExtension(caminho);
            }

            return new Habilidade
            {
                Nome = nome,
                Descricao = descricao,
                Conteudo = conteudo,
                CriadaEm = File.GetCreationTime(caminho),
                AtualizadaEm = File.GetLastWriteTime(caminho),
            };
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static (string Nome, string Descricao, string Conteudo) ParsearFrontmatter(string texto)
    {
        string normalizado = texto.Replace("\r\n", "\n");
        if (!normalizado.StartsWith("---\n", StringComparison.Ordinal))
        {
            return (string.Empty, string.Empty, texto.Trim());
        }

        int fim = normalizado.IndexOf("\n---", 4, StringComparison.Ordinal);
        if (fim < 0)
        {
            return (string.Empty, string.Empty, texto.Trim());
        }

        string bloco = normalizado[4..fim];
        string nome = string.Empty;
        string descricao = string.Empty;
        foreach (string linha in bloco.Split('\n'))
        {
            int separador = linha.IndexOf(':');
            if (separador <= 0)
            {
                continue;
            }

            string chave = linha[..separador].Trim();
            string valor = linha[(separador + 1)..].Trim();
            if (chave.Equals("nome", StringComparison.OrdinalIgnoreCase))
            {
                nome = valor;
            }
            else if (chave.Equals("descricao", StringComparison.OrdinalIgnoreCase))
            {
                descricao = valor;
            }
        }

        string restante = normalizado[(fim + 4)..].TrimStart('\n', '-').Trim();
        return (nome, descricao, restante);
    }

    private static string Serializar(Habilidade habilidade)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("---\n");
        sb.Append("nome: ").Append(habilidade.Nome).Append('\n');
        sb.Append("descricao: ").Append(habilidade.Descricao.Replace("\r", string.Empty).Replace("\n", " ")).Append('\n');
        sb.Append("---\n\n");
        sb.Append(habilidade.Conteudo.Replace("\r\n", "\n").Trim());
        sb.Append('\n');
        return sb.ToString();
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
        bool tracoPendente = false;
        foreach (char c in semAcento.ToString().Normalize(NormalizationForm.FormC))
        {
            if (c is (>= 'a' and <= 'z') or (>= '0' and <= '9'))
            {
                if (tracoPendente && slug.Length > 0)
                {
                    slug.Append('-');
                }

                tracoPendente = false;
                slug.Append(c);
            }
            else
            {
                tracoPendente = true;
            }
        }

        return slug.ToString();
    }
}
