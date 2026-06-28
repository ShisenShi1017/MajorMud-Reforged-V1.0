namespace MajorMud.Reforged.Core.Health;

/// <summary>
/// Reports whether application projects are attempting to redefine models that belong in Core.
/// </summary>
public sealed class DuplicateModelEnforcementReport
{
    public DateTimeOffset GeneratedUtc { get; set; }
    public List<DuplicateModelEnforcementIssue> Issues { get; set; } = new();

    public bool Passed => Issues.Count == 0;
}
