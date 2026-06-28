namespace MajorMud.Reforged.Core.Manifest;

/// <summary>
/// Machine-readable project state loaded from ProjectManifest.json.
/// </summary>
public sealed class ProjectManifest
{
    public string Project { get; set; } = "MajorMud Reforged";
    public string ProjectVersion { get; set; } = "0.1.0";
    public string CodeUpdate { get; set; } = "0008";
    public string DatabaseVersion { get; set; } = string.Empty;
    public string Milestone { get; set; } = "Milestone 1 - Core Framework";
    public string RuntimePolicy { get; set; } = "No .NET SDK required for normal runtime; launch compiled/runtime-ready artifacts from Bin.";
    public string UpdatePolicy { get; set; } = "Run Apply-*.bat from extracted update folder to apply to main project folder.";
    public List<string> Preserve { get; set; } = new();
}
