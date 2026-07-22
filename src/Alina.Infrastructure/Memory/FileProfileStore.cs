using Alina.Core.Memory;

namespace Alina.Infrastructure.Memory;

/// <summary>
/// Memória permanente lida de um arquivo Markdown (preferências, convenções de
/// código, padrões arquiteturais). Se o arquivo não existir, retorna <c>null</c>.
/// </summary>
public sealed class FileProfileStore : IProfileStore
{
    private readonly string _preferencesFile;

    public FileProfileStore(string preferencesFile) => _preferencesFile = preferencesFile;

    public async Task<string?> GetPreferencesAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_preferencesFile))
        {
            return null;
        }

        string content = await File.ReadAllTextAsync(_preferencesFile, cancellationToken);
        return string.IsNullOrWhiteSpace(content) ? null : content;
    }
}
