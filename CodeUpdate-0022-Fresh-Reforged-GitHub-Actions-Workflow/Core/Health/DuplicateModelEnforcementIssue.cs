namespace Reforged.Core.Health;

/// <summary>
/// Represents a concrete duplicate-model policy violation found outside Core.
/// </summary>
public sealed class DuplicateModelEnforcementIssue
{
    public string ModelName { get; set; } = string.Empty;
    public string CoreLocation { get; set; } = string.Empty;
    public string ApplicationLocation { get; set; } = string.Empty;
    public string Severity { get; set; } = "Error";
    public string Message { get; set; } = string.Empty;
}
