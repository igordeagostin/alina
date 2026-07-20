using System.Text.Json;
using Alina.Core.Memory;
using Alina.Infrastructure.Configuration;

namespace Alina.Infrastructure.Memory;

/// <summary>
/// Memória permanente persistida em um único arquivo JSON. Serializa o acesso
/// (read-modify-write) com um semáforo, já que tools podem rodar concorrentemente.
/// </summary>
public sealed class JsonMemoryStore : IMemoryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public JsonMemoryStore(string filePath)
    {
        _filePath = filePath;
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    public JsonMemoryStore(StorageOptions options)
        : this(Path.Combine(options.ResolveDataDirectory(), "memory.json")) { }

    public Task<MemoryItem> AddAsync(string content, string? category = null, CancellationToken cancellationToken = default)
        => AddAsync(new MemoryItem { Content = content.Trim(), Category = category?.Trim() }, cancellationToken);

    public async Task<MemoryItem> AddAsync(MemoryItem item, CancellationToken cancellationToken = default)
    {
        item.Content = item.Content.Trim();
        item.Category = item.Category?.Trim();

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var items = await LoadAsync(cancellationToken);
            items.Add(item);
            await SaveAsync(items, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }

        return item;
    }

    public async Task<bool> UpdateAsync(MemoryItem item, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var items = await LoadAsync(cancellationToken);
            var index = items.FindIndex(i => string.Equals(i.Id, item.Id, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                return false;
            }

            item.UpdatedAt = DateTimeOffset.Now;
            items[index] = item;
            await SaveAsync(items, cancellationToken);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<MemoryItem>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await LoadAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> RemoveAsync(string id, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var items = await LoadAsync(cancellationToken);
            var removed = items.RemoveAll(i => string.Equals(i.Id, id, StringComparison.OrdinalIgnoreCase));
            if (removed > 0)
            {
                await SaveAsync(items, cancellationToken);
            }

            return removed > 0;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<List<MemoryItem>> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return new List<MemoryItem>();
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);
            var items = await JsonSerializer.DeserializeAsync<List<MemoryItem>>(stream, JsonOptions, cancellationToken);
            return items ?? new List<MemoryItem>();
        }
        catch (JsonException)
        {
            return new List<MemoryItem>();
        }
    }

    private async Task SaveAsync(List<MemoryItem> items, CancellationToken cancellationToken)
    {
        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, items, JsonOptions, cancellationToken);
    }
}
