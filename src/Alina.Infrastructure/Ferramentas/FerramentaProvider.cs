using Alina.Core.Ferramentas;
using Alina.Core.Tools;
using Alina.Infrastructure.Configuration;

namespace Alina.Infrastructure.Ferramentas;

/// <summary>
/// Monta as ferramentas declarativas como <see cref="ITool"/> a cada consulta do
/// <see cref="ToolRegistry"/>. Guarda um cache invalidado pela assinatura da pasta
/// (nomes + horário de escrita dos arquivos), de modo que criar/editar/remover uma
/// ferramenta — pela UI, pela Alina ou editando o arquivo — reflete no próximo turno,
/// sem reconstruir as <see cref="Microsoft.Extensions.AI.AIFunction"/> à toa.
/// </summary>
public sealed class FerramentaProvider : IFerramentaProvider
{
    private readonly IFerramentaStore _store;
    private readonly IConfirmationService _confirmation;
    private readonly string _diretorio;
    private readonly object _trava = new();

    private string _assinatura = string.Empty;
    private IReadOnlyList<ITool> _cache = Array.Empty<ITool>();

    public FerramentaProvider(IFerramentaStore store, IConfirmationService confirmation, StorageOptions options)
    {
        _store = store;
        _confirmation = confirmation;
        _diretorio = options.ResolveFerramentasDirectory();
    }

    public IReadOnlyList<ITool> ObterFerramentas()
    {
        string assinatura = CalcularAssinatura();
        lock (_trava)
        {
            if (assinatura == _assinatura && _cache.Count > 0)
            {
                return _cache;
            }

            _cache = _store.LerDefinicoes()
                .Select(ITool (d) => new FerramentaDeclarada(d, _confirmation))
                .ToList();
            _assinatura = assinatura;
            return _cache;
        }
    }

    private string CalcularAssinatura()
    {
        if (!Directory.Exists(_diretorio))
        {
            return string.Empty;
        }

        IEnumerable<string> partes = Directory
            .EnumerateFiles(_diretorio, "*.tool.json", SearchOption.TopDirectoryOnly)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .Select(f => $"{Path.GetFileName(f)}:{File.GetLastWriteTimeUtc(f).Ticks}");

        return string.Join('|', partes);
    }
}
