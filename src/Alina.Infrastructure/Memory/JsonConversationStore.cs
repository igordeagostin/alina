using System.Text.Json;
using Alina.Core.Memory;
using Alina.Core.Models;
using Alina.Infrastructure.Configuration;
using Microsoft.Extensions.AI;

namespace Alina.Infrastructure.Memory;

/// <summary>
/// Persiste conversas como arquivos JSON. Usa <see cref="AIJsonUtilities.DefaultOptions"/>
/// para lidar com o polimorfismo das mensagens do Microsoft.Extensions.AI.
/// </summary>
public sealed class JsonConversationStore : IConversationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(AIJsonUtilities.DefaultOptions)
    {
        WriteIndented = true,
    };

    private readonly string _directory;

    public JsonConversationStore(string conversationsDirectory)
    {
        _directory = conversationsDirectory;
        Directory.CreateDirectory(_directory);
    }

    public JsonConversationStore(StorageOptions options)
        : this(options.ResolveConversationsDirectory()) { }

    public async Task SaveAsync(Conversation conversation, CancellationToken cancellationToken = default)
    {
        var path = PathFor(conversation.Id);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, conversation, JsonOptions, cancellationToken);
    }

    public async Task<Conversation?> LoadAsync(string id, CancellationToken cancellationToken = default)
    {
        var path = PathFor(id);
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<Conversation>(stream, JsonOptions, cancellationToken);
    }

    public async Task<IReadOnlyList<ConversationSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        var summaries = new List<ConversationSummary>();

        foreach (var file in Directory.EnumerateFiles(_directory, "*.json"))
        {
            try
            {
                await using var stream = File.OpenRead(file);
                var conversation = await JsonSerializer.DeserializeAsync<Conversation>(stream, JsonOptions, cancellationToken);
                if (conversation is not null)
                {
                    summaries.Add(new ConversationSummary(
                        conversation.Id,
                        conversation.Title,
                        conversation.UpdatedAt,
                        conversation.Messages.Count));
                }
            }
            catch (JsonException)
            {
                // ignora arquivos corrompidos/incompatíveis.
            }
        }

        return summaries
            .OrderByDescending(s => s.UpdatedAt)
            .ToList();
    }

    private string PathFor(string id)
    {
        var safeId = string.Concat(id.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_'));
        if (string.IsNullOrEmpty(safeId))
        {
            throw new ArgumentException("Id de conversa inválido.", nameof(id));
        }

        return Path.Combine(_directory, safeId + ".json");
    }
}
