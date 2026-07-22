using System.IO;
using System.Text.Json;
using Alina.Infrastructure.Configuration;

namespace Alina.App.Services;

/// <summary>
/// Gerencia onde a Alina guarda seus dados (memória, conversas, habilidades,
/// preferências). A escolha do usuário mora num ponteiro fixo — <c>local.json</c>
/// dentro da pasta-âncora (<c>%APPDATA%/Alina</c> ou <c>~/.alina</c>) — que aponta
/// para a pasta real, esteja ela onde estiver.
///
/// O ponteiro precisa viver num local fixo: se ficasse dentro da própria pasta de
/// dados, mudá-la faria o app perder a referência de para onde os dados foram.
/// </summary>
public sealed class PastaDadosService
{
    private sealed class Ponteiro
    {
        public string? DataDirectory { get; set; }
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _arquivoPonteiro;
    private Ponteiro _estado = new();

    public PastaDadosService()
    {
        _arquivoPonteiro = Path.Combine(PastaAncora, "local.json");
        Carregar();
    }

    /// <summary>Pasta-âncora fixa que hospeda apenas o ponteiro <c>local.json</c>.</summary>
    public static string PastaAncora
    {
        get
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return string.IsNullOrWhiteSpace(appData)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".alina")
                : Path.Combine(appData, "Alina");
        }
    }

    /// <summary>Pasta de dados em uso: a escolhida pelo usuário ou a âncora padrão.</summary>
    public string PastaAtual =>
        string.IsNullOrWhiteSpace(_estado.DataDirectory) ? PastaAncora : _estado.DataDirectory!;

    /// <summary>Sobrepõe o <see cref="StorageOptions.DataDirectory"/> com o ponteiro salvo.</summary>
    public void AplicarEm(StorageOptions opcoes)
    {
        if (!string.IsNullOrWhiteSpace(_estado.DataDirectory))
        {
            opcoes.DataDirectory = _estado.DataDirectory;
        }
    }

    /// <summary>
    /// Aponta a pasta de dados para <paramref name="novaPasta"/>, movendo o conteúdo
    /// atual para lá. A mudança só passa a valer após reiniciar o app, pois os stores
    /// resolvem o caminho no arranque.
    /// </summary>
    public void Mover(string novaPasta)
    {
        string destino = Path.GetFullPath(novaPasta.Trim());
        string origem = Path.GetFullPath(PastaAtual);

        if (string.Equals(destino, origem, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Directory.CreateDirectory(destino);

        if (Directory.Exists(origem))
        {
            MoverConteudo(origem, destino);
        }

        _estado.DataDirectory = destino;
        Persistir();
    }

    private static void MoverConteudo(string origem, string destino)
    {
        foreach (string arquivo in Directory.GetFiles(origem))
        {
            string alvo = Path.Combine(destino, Path.GetFileName(arquivo));
            if (!File.Exists(alvo))
            {
                MoverArquivo(arquivo, alvo);
            }
        }

        foreach (string subpasta in Directory.GetDirectories(origem))
        {
            string alvo = Path.Combine(destino, Path.GetFileName(subpasta));
            Directory.CreateDirectory(alvo);
            MoverConteudo(subpasta, alvo);
            if (Directory.GetFileSystemEntries(subpasta).Length == 0)
            {
                Directory.Delete(subpasta);
            }
        }
    }

    private static void MoverArquivo(string origem, string destino)
    {
        try
        {
            File.Move(origem, destino);
        }
        catch (IOException)
        {
            File.Copy(origem, destino, overwrite: false);
            File.Delete(origem);
        }
    }

    private void Carregar()
    {
        try
        {
            if (File.Exists(_arquivoPonteiro))
            {
                _estado = JsonSerializer.Deserialize<Ponteiro>(File.ReadAllText(_arquivoPonteiro), JsonOptions) ?? new Ponteiro();
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            _estado = new Ponteiro();
        }
    }

    private void Persistir()
    {
        Directory.CreateDirectory(PastaAncora);
        File.WriteAllText(_arquivoPonteiro, JsonSerializer.Serialize(_estado, JsonOptions));
    }
}
