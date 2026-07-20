using Alina.Core.Tools;
using Microsoft.Extensions.AI;

namespace Alina.Tools.Plugins;

/// <summary>Adapta um <see cref="PluginManifest"/> à interface <see cref="ITool"/>.</summary>
public sealed class PluginTool : ITool
{
    private readonly PluginManifest _manifest;
    private readonly IConfirmationService _confirmation;

    public PluginTool(PluginManifest manifest, IConfirmationService confirmation)
    {
        _manifest = manifest;
        _confirmation = confirmation;
    }

    public string Name => _manifest.Name;

    public string Description => _manifest.Description;

    public bool RequiresConfirmation => _manifest.RequiresConfirmation;

    public AIFunction AsAIFunction() => new ManifestFunction(_manifest, _confirmation);
}
