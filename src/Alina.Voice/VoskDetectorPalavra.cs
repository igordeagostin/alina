using System.IO;
using System.Text.Json;
using NAudio.Wave;
using Vosk;

namespace Alina.Voice;

/// <summary>
/// Detector de palavra de ativação baseado no Vosk (reconhecimento de fala offline).
/// Escuta o microfone continuamente via NAudio e sinaliza quando uma das
/// <see cref="VoiceOptions.PalavrasAtivacao"/> ("alina") aparece na transcrição
/// parcial. Todo o processamento é local — nenhum áudio sai da máquina.
/// </summary>
public sealed class VoskDetectorPalavra : IDetectorPalavraAtivacao
{
    private const int TaxaAmostragem = 16000;

    private readonly VoiceOptions _opcoes;
    private readonly object _trava = new();

    private Model? _modelo;
    private VoskRecognizer? _reconhecedor;
    private WaveInEvent? _microfone;
    private volatile bool _capturando;
    private volatile bool _podeDisparar;

    public event Action? PalavraDetectada;
    public event Action? InterrupcaoDetectada;
    public event Action<Exception>? Falhou;

    public VoskDetectorPalavra(VoiceOptions opcoes) => _opcoes = opcoes;

    public bool Ativo { get; private set; }

    public bool Configurado =>
        !string.IsNullOrWhiteSpace(_opcoes.CaminhoModeloVosk)
        && Directory.Exists(_opcoes.CaminhoModeloVosk);

    public void Iniciar()
    {
        lock (_trava)
        {
            if (Ativo || !Configurado)
            {
                return;
            }

            global::Vosk.Vosk.SetLogLevel(-1);
            _modelo = new Model(_opcoes.CaminhoModeloVosk);
            Ativo = true;
            ComecarCaptura();
        }
    }

    public void Parar()
    {
        lock (_trava)
        {
            Ativo = false;
            PararCaptura();
            _modelo?.Dispose();
            _modelo = null;
        }
    }

    public void Pausar()
    {
        lock (_trava)
        {
            PararCaptura();
        }
    }

    public void Retomar()
    {
        lock (_trava)
        {
            if (Ativo && _modelo is not null && _microfone is null)
            {
                ComecarCaptura();
            }
        }
    }

    private void ComecarCaptura()
    {
        _reconhecedor = new VoskRecognizer(_modelo, TaxaAmostragem, MontarGramatica());
        _microfone = new WaveInEvent { WaveFormat = new WaveFormat(TaxaAmostragem, 16, 1), BufferMilliseconds = 100 };
        _microfone.DataAvailable += AoReceberAudio;

        _podeDisparar = true;
        _capturando = true;
        _microfone.StartRecording();
    }

    private void PararCaptura()
    {
        _capturando = false;

        if (_microfone is not null)
        {
            _microfone.DataAvailable -= AoReceberAudio;
            _microfone.StopRecording();
            _microfone.Dispose();
            _microfone = null;
        }

        _reconhecedor?.Dispose();
        _reconhecedor = null;
    }

    private void AoReceberAudio(object? sender, WaveInEventArgs e)
    {
        VoskRecognizer? reconhecedor = _reconhecedor;
        if (!_capturando || !_podeDisparar || reconhecedor is null)
        {
            return;
        }

        try
        {
            bool completo = reconhecedor.AcceptWaveform(e.Buffer, e.BytesRecorded);
            string texto = ExtrairTexto(completo ? reconhecedor.Result() : reconhecedor.PartialResult(), completo);

            bool ativacao = ContemAlguma(texto, _opcoes.PalavrasAtivacao);
            bool interrupcao = !ativacao && ContemAlguma(texto, _opcoes.PalavrasInterrupcao);
            if (!ativacao && !interrupcao)
            {
                return;
            }

            _podeDisparar = false;
            reconhecedor.Reset();

            if (ativacao)
            {
                PalavraDetectada?.Invoke();
            }
            else
            {
                InterrupcaoDetectada?.Invoke();
            }
        }
        catch (Exception ex)
        {
            _capturando = false;
            Falhou?.Invoke(ex);
        }
    }

    private static string ExtrairTexto(string json, bool completo)
    {
        using JsonDocument doc = JsonDocument.Parse(json);
        string campo = completo ? "text" : "partial";
        return doc.RootElement.TryGetProperty(campo, out JsonElement valor) ? valor.GetString() ?? string.Empty : string.Empty;
    }

    private string MontarGramatica()
    {
        IEnumerable<string> frases = _opcoes.PalavrasAtivacao
            .Concat(_opcoes.PalavrasInterrupcao)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim().ToLowerInvariant())
            .Distinct()
            .Append("[unk]");

        return JsonSerializer.Serialize(frases);
    }

    private static bool ContemAlguma(string texto, IEnumerable<string> palavras)
    {
        if (texto.Length == 0)
        {
            return false;
        }

        foreach (string palavra in palavras)
        {
            if (!string.IsNullOrWhiteSpace(palavra) && texto.Contains(palavra, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public void Dispose() => Parar();
}
