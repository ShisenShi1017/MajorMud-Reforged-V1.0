namespace MajorMud.Reforged.Core.Configuration;

public sealed class RuntimeConfiguration
{
    public string ProjectName { get; set; } = "MajorMud Reforged";
    public string Mode { get; set; } = "Development";
    public bool AllowLiveEditing { get; set; } = false;
    public bool RequireValidationBeforeStart { get; set; } = true;
    public ProjectPaths Paths { get; set; } = new();
}
