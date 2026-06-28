namespace Reforged.Core.Validation;

public sealed class ValidationIssue
{
    public ValidationSeverity Severity { get; set; } = ValidationSeverity.Warning;
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? Source { get; set; }
}
