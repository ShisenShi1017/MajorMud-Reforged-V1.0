using System.Text.Json;

namespace Reforged.Core.Manifest;

/// <summary>
/// Shared manifest reader so applications do not duplicate project-state parsing.
/// </summary>
public sealed class ProjectManifestReader
{
    public ProjectManifest Read(string projectRoot)
    {
        var path = Path.Combine(projectRoot, "ProjectManifest.json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("ProjectManifest.json was not found.", path);
        }
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<ProjectManifest>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new ProjectManifest();
    }
}
