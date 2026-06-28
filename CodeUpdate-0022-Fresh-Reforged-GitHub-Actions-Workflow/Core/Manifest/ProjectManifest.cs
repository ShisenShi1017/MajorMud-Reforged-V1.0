namespace Reforged.Core.Manifest;

/// <summary>
/// Machine-readable project state loaded from ProjectManifest.json.
/// </summary>
public sealed class ProjectManifest
{
    public string Project { get; set; } = "Reforged";
    public string ProjectVersion { get; set; } = "0.1.0";
    public string CodeUpdate { get; set; } = "0008";
    public string DatabaseVersion { get; set; } = string.Empty;
    public string Milestone { get; set; } = "Milestone 1 - Core Framework";
    public string RuntimePolicy { get; set; } = "No .NET SDK required for normal runtime; launch compiled/runtime-ready artifacts from Bin.";
    public string UpdatePolicy { get; set; } = "Run Apply-*.bat from extracted update folder to apply to main project folder.";
    public string UpdatePackagePolicy { get; set; } = "Every update ZIP contains exactly one labeled top-level folder; users extract that folder into the project root and run Apply-*.bat from inside it.";
    public string SuggestionPolicy { get; set; } = "All approved suggestions become implementation requirements unless explicitly deferred or rejected.";
    public string HandoffPolicy { get; set; } = "AI_CONTEXT.md is a living handoff document and must be updated every Code Update.";
    public List<string> Preserve { get; set; } = new();
    public Dictionary<string, string> MilestoneStatus { get; set; } = new();
    public List<string> NextRecommendedWork { get; set; } = new();
}
