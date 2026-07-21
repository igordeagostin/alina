namespace Alina.App.Services;

/// <summary>
/// Log de conversa observável, compartilhado entre o shell Blazor e o
/// <see cref="VoiceController"/>. Permite que a voz (clique no orbe ou hotkey
/// global) e o texto alimentem a mesma lista de mensagens exibida na janela.
/// </summary>
public sealed class ConversationUiState
{
    private readonly object _gate = new();
    private readonly List<ChatEntry> _entries = new();

    /// <summary>Disparado sempre que uma mensagem é adicionada.</summary>
    public event Action? Alterado;

    public void Adicionar(string tipo, string texto)
    {
        lock (_gate)
        {
            _entries.Add(new ChatEntry(tipo, texto));
        }

        Alterado?.Invoke();
    }

    /// <summary>Cópia imutável das mensagens, segura para renderizar na UI.</summary>
    public IReadOnlyList<ChatEntry> Snapshot()
    {
        lock (_gate)
        {
            return _entries.ToArray();
        }
    }
}

public sealed record ChatEntry(string Tipo, string Texto);
