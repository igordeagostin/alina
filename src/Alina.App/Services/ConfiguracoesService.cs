using System.IO;
using System.Text.Json;
using Alina.Infrastructure.Configuration;
using Alina.Voice;

namespace Alina.App.Services;

/// <summary>
/// Carrega, persiste e aplica as <see cref="ConfiguracoesApp"/>. As preferências
/// ficam num JSON na pasta de dados (a mesma da memória permanente). Ao aplicar,
/// reflete os valores diretamente nas opções vivas de voz (<see cref="VoiceOptions"/>)
/// e no estado de UI, evitando exigir reinício.
/// </summary>
public sealed class ConfiguracoesService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _arquivo;
    private readonly VoiceOptions _voz;
    private readonly ShellUiState _uiState;
    private readonly GerenciadorPalavraAtivacao _palavra;
    private readonly MiniPlayerController _miniPlayer;

    public ConfiguracoesApp Atual { get; private set; } = new();

    /// <summary>Vozes de TTS oferecidas na tela de configurações.</summary>
    public IReadOnlyList<string> VozesDisponiveis { get; } =
        ["alloy", "echo", "fable", "onyx", "nova", "shimmer", "coral", "sage"];

    public ConfiguracoesService(
        StorageOptions armazenamento,
        VoiceOptions voz,
        ShellUiState uiState,
        GerenciadorPalavraAtivacao palavra,
        MiniPlayerController miniPlayer)
    {
        _voz = voz;
        _uiState = uiState;
        _palavra = palavra;
        _miniPlayer = miniPlayer;
        _arquivo = Path.Combine(armazenamento.ResolveDataDirectory(), "configuracoes.json");
        Carregar();
    }

    /// <summary>Indica se o modelo de voz da palavra de ativação está presente.</summary>
    public bool PalavraAtivacaoDisponivel => _palavra.Disponivel;

    /// <summary>Pasta onde o modelo Vosk deve ser extraído para habilitar a palavra de ativação.</summary>
    public string CaminhoModeloVosk => _voz.CaminhoModeloVosk ?? string.Empty;

    /// <summary>Página oficial de download dos modelos Vosk.</summary>
    public string UrlModelosVosk => "https://alphacephei.com/vosk/models";

    /// <summary>Iniciar com o Windows — mora no registro, exposto aqui por conveniência.</summary>
    public bool IniciarComWindows
    {
        get => Autostart.Ativo;
        set => Autostart.Definir(value);
    }

    /// <summary>Avisa a janela principal de que os tamanhos memorizados foram descartados.</summary>
    public event Action? TamanhoJanelaRedefinido;

    /// <summary>Há algum tamanho de janela memorizado que possa ser restaurado ao padrão.</summary>
    public bool TemTamanhoJanelaMemorizado => Atual.JanelaCompacta is not null || Atual.JanelaDetalhada is not null;

    /// <summary>
    /// Aplica e persiste as preferências vindas da tela de configurações. Os
    /// tamanhos memorizados da janela não passam por esse formulário — vêm do
    /// próprio redimensionamento — por isso são preservados aqui, e só zerados
    /// quando o usuário desliga o "lembrar tamanho da janela".
    /// </summary>
    public void Salvar(ConfiguracoesApp novas)
    {
        bool esqueceu = Atual.LembrarTamanhoJanela && !novas.LembrarTamanhoJanela;
        novas.JanelaCompacta = novas.LembrarTamanhoJanela ? Atual.JanelaCompacta : null;
        novas.JanelaDetalhada = novas.LembrarTamanhoJanela ? Atual.JanelaDetalhada : null;

        Atual = novas;
        AplicarVoz();
        _palavra.Definir(Atual.EscutarPalavraAtivacao);
        _miniPlayer.Aplicar(Atual);
        Persistir();

        if (esqueceu)
        {
            TamanhoJanelaRedefinido?.Invoke();
        }
    }

    /// <summary>Memoriza o tamanho atual da janela para o modo em uso, se o usuário pediu para lembrar.</summary>
    public void RegistrarTamanhoJanela(bool compacto, double largura, double altura)
    {
        if (!Atual.LembrarTamanhoJanela || largura <= 0 || altura <= 0)
        {
            return;
        }

        TamanhoJanela tamanho = new() { Largura = largura, Altura = altura };
        if (compacto)
        {
            Atual.JanelaCompacta = tamanho;
        }
        else
        {
            Atual.JanelaDetalhada = tamanho;
        }

        Persistir();
    }

    /// <summary>Descarta os tamanhos memorizados e devolve a janela às dimensões padrão.</summary>
    public void EsquecerTamanhoJanela()
    {
        Atual.JanelaCompacta = null;
        Atual.JanelaDetalhada = null;
        Persistir();
        TamanhoJanelaRedefinido?.Invoke();
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

            File.WriteAllText(_arquivo, JsonSerializer.Serialize(Atual, JsonOptions));
        }
        catch (IOException)
        {
        }
    }

    private void Carregar()
    {
        try
        {
            if (File.Exists(_arquivo))
            {
                string json = File.ReadAllText(_arquivo);
                Atual = JsonSerializer.Deserialize<ConfiguracoesApp>(json, JsonOptions) ?? new ConfiguracoesApp();
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            Atual = new ConfiguracoesApp();
        }

        AplicarVoz();
        _palavra.Definir(Atual.EscutarPalavraAtivacao);
        _miniPlayer.Aplicar(Atual);
        _uiState.DefinirModo(Atual.AbrirEmModoCompacto);
    }

    private void AplicarVoz()
    {
        _voz.Voice = Atual.Voz;
        _voz.Speed = (float)Atual.VelocidadeFala;
        _voz.Enabled = Atual.FalarRespostas;
        _voz.SegundosSilencioParaEncerrar = Atual.SegundosSilencioParaEncerrar;
        _voz.ConversaContinua = Atual.ConversaContinua;
        _voz.SegundosJanelaConversa = Atual.SegundosJanelaConversa;
        _voz.InterromperPorVoz = Atual.InterromperPorVoz;
        _voz.PalavrasInterrupcao = Atual.PalavrasInterrupcao
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .ToArray();
    }
}
