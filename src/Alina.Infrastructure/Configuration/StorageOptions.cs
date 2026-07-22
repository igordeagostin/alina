namespace Alina.Infrastructure.Configuration;

/// <summary>Configuração de persistência (seção "Storage" do appsettings).</summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>
    /// Pasta base de dados. Se vazio, usa %APPDATA%/Alina (ou ~/.alina).
    /// </summary>
    public string? DataDirectory { get; set; }

    /// <summary>Caminho do arquivo de preferências permanentes (Markdown).</summary>
    public string? PreferencesFile { get; set; }

    public string ResolveDataDirectory()
    {
        if (!string.IsNullOrWhiteSpace(DataDirectory))
        {
            return DataDirectory!;
        }

        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string baseDir = string.IsNullOrWhiteSpace(appData)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".alina")
            : Path.Combine(appData, "Alina");
        return baseDir;
    }

    public string ResolveConversationsDirectory() => Path.Combine(ResolveDataDirectory(), "conversations");

    public string ResolveHabilidadesDirectory() => Path.Combine(ResolveDataDirectory(), "habilidades");

    public string ResolveFerramentasDirectory() => Path.Combine(ResolveDataDirectory(), "ferramentas");

    public string ResolvePreferencesFile() =>
        string.IsNullOrWhiteSpace(PreferencesFile)
            ? Path.Combine(ResolveDataDirectory(), "preferences.md")
            : PreferencesFile!;
}
