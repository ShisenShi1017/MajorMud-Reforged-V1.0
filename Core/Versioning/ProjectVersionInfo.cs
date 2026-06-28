namespace Reforged.Core.Versioning;

public sealed class ProjectVersionInfo
{
    public string ProjectVersion { get; set; } = "0.1.0";
    public string CodeUpdate { get; set; } = "0005";
    public string DatabaseVersion { get; set; } = "0009";
    public string Milestone { get; set; } = "Milestone 1 - Core Library";
    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
}
