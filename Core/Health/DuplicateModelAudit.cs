namespace Reforged.Core.Health;

/// <summary>
/// Records duplicate model findings so they can be removed before Milestone 1 is closed.
/// </summary>
public sealed class DuplicateModelAudit
{
    public string ModelName { get; set; } = string.Empty;
    public List<string> Locations { get; set; } = new();
    public string Resolution { get; set; } = string.Empty;
}
