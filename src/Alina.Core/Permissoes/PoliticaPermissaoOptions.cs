namespace Alina.Core.Permissoes;

/// <summary>
/// Configuração estática da política de permissões, ajustável pelo usuário (tela/arquivo).
/// As listas são casos de uso pensados para desenvolvedores; os valores padrão já cobrem o
/// comum sem abrir mão da segurança.
/// </summary>
public sealed class PoliticaPermissaoOptions
{
    /// <summary>O que fazer quando nenhuma regra libera nem bloqueia o pedido.</summary>
    public ModoPermissao ModoPadrao { get; set; } = ModoPermissao.Perguntar;

    /// <summary>Diretórios com permissão total: qualquer ação sob eles é liberada.</summary>
    public List<string> DiretoriosConfiaveis { get; set; } = [];

    /// <summary>Ferramentas somente-leitura, liberadas automaticamente (não alteram nada).</summary>
    public List<string> FerramentasSomenteLeitura { get; set; } =
        ["Read", "Grep", "Glob", "LS", "NotebookRead", "WebFetch", "WebSearch"];

    /// <summary>Prefixos de comando de terminal liberados sem perguntar.</summary>
    public List<string> ComandosPermitidos { get; set; } =
        ["git status", "git diff", "git log", "git branch", "git show",
         "dotnet build", "dotnet test", "dotnet restore",
         "ls", "cat", "pwd", "echo", "npm run", "npm test"];

    /// <summary>Trechos de comando que SEMPRE exigem confirmação, mesmo com modo permissivo.</summary>
    public List<string> ComandosSempreConfirmar { get; set; } =
        ["rm ", "rm -rf", "rmdir", "del ", "erase ", "format ",
         "git push", "git reset --hard", "git clean", "git checkout -- ",
         "drop table", "drop database", "shutdown", "reg delete",
         "curl", "wget", "iwr", "invoke-webrequest", "| sh", "| bash"];

    /// <summary>Padrões de caminho protegidos (segredos): edições/leituras sempre confirmam.</summary>
    public List<string> CaminhosProtegidos { get; set; } =
        [".env", "secrets.json", "appsettings.Production.json", ".pem", ".key",
         ".pfx", "id_rsa", ".ssh", ".aws", "credentials"];

    /// <summary>Se <c>true</c>, ações fora dos diretórios confiáveis sempre exigem confirmação.</summary>
    public bool ConfirmarForaDoWorkspace { get; set; } = true;
}
