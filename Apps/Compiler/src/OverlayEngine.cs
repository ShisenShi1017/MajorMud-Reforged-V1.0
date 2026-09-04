using System.Text.Json;

namespace RealmGate.Compiler;

public sealed class OverlayEngine
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public OverlayDefinition LoadOverlay(string repositoryRoot, string worldId)
    {
        var normalized = LibrariumCompilerOptions.NormalizeWorld(worldId);
        var path = Path.Combine(repositoryRoot, "Librarium", "Overlays", normalized, "overlay_manifest.json");
        if (!File.Exists(path) && normalized.Equals("Classic", StringComparison.OrdinalIgnoreCase))
            path = Path.Combine(repositoryRoot, "Librarium", "Overlays", "Classic", "overlay_manifest.json");
        if (!File.Exists(path))
            throw new FileNotFoundException($"Overlay manifest not found for world '{worldId}'.", path);
        return JsonSerializer.Deserialize<OverlayDefinition>(File.ReadAllText(path), Options)
               ?? throw new InvalidOperationException($"Overlay manifest could not be read: {path}");
    }
}
