namespace Reforged.Core.Health;

/// <summary>
/// Captures whether an application is expected to depend on Core and whether the reference was observed.
/// </summary>
public sealed class CoreReferenceAudit
{
    public string ApplicationName { get; set; } = string.Empty;
    public string ApplicationPath { get; set; } = string.Empty;
    public bool ExpectedToReferenceCore { get; set; } = true;
    public bool CoreReferenceObserved { get; set; }
    public string Notes { get; set; } = string.Empty;
}
