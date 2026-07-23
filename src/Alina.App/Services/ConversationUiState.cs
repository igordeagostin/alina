namespace Alina.App.Services;

/// <summary>
/// Log de conversa observável, compartilhado entre o shell Blazor e o
/// <see cref="VoiceController"/>. Permite que a voz (clique no orbe ou hotkey
/// global) e o texto alimentem a mesma lista de mensagens exibida na janela.
/// </summary>
public sealed class ConversationUiState
{
    /// <summary>Intervalo mínimo entre notificações durante o streaming, para não afogar a UI.</summary>
    private const long MilissegundosEntreAvisos = 80;

    private readonly object _gate = new();
    private readonly List<ChatEntry> _entries = new();

    private int _parcial = -1;
    private long _ultimoAviso;

    /// <summary>Disparado sempre que uma mensagem é adicionada ou atualizada.</summary>
    public event Action? Alterado;

    /// <summary>Há uma resposta da assistente ainda sendo escrita (streaming).</summary>
    public bool ParcialAtiva
    {
        get { lock (_gate) return _parcial >= 0; }
    }

    public void Adicionar(string tipo, string texto)
    {
        lock (_gate)
        {
            _entries.Add(new ChatEntry(tipo, texto));
        }

        Alterado?.Invoke();
    }

    /// <summary>
    /// Atualiza (ou cria, na primeira chamada) a resposta em streaming com o texto
    /// acumulado até aqui. As notificações são espaçadas; o texto fica sempre correto
    /// porque cada chamada substitui o conteúdo inteiro e <see cref="ConcluirParcial"/>
    /// notifica incondicionalmente ao final.
    /// </summary>
    public void AtualizarParcial(string textoAcumulado)
    {
        bool avisar;
        lock (_gate)
        {
            if (_parcial < 0)
            {
                _entries.Add(new ChatEntry("bot", textoAcumulado));
                _parcial = _entries.Count - 1;
                avisar = true;
            }
            else
            {
                _entries[_parcial] = _entries[_parcial] with { Texto = textoAcumulado };
                avisar = Environment.TickCount64 - _ultimoAviso >= MilissegundosEntreAvisos;
            }

            if (avisar)
            {
                _ultimoAviso = Environment.TickCount64;
            }
        }

        if (avisar)
        {
            Alterado?.Invoke();
        }
    }

    /// <summary>Sela a resposta em streaming, opcionalmente com o texto definitivo.</summary>
    public void ConcluirParcial(string? textoFinal = null)
    {
        lock (_gate)
        {
            if (_parcial < 0)
            {
                return;
            }

            if (textoFinal is not null)
            {
                _entries[_parcial] = _entries[_parcial] with { Texto = textoFinal };
            }

            _parcial = -1;
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
