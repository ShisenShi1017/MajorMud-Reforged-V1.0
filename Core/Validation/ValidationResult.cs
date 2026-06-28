namespace MajorMud.Reforged.Core.Validation;

public sealed class ValidationResult
{
    public List<ValidationIssue> Issues { get; set; } = new();

    public bool HasErrors => Issues.Any(i => i.Severity is ValidationSeverity.Error or ValidationSeverity.Critical);
    public int ErrorCount => Issues.Count(i => i.Severity is ValidationSeverity.Error or ValidationSeverity.Critical);
    public int WarningCount => Issues.Count(i => i.Severity == ValidationSeverity.Warning);

    public void Add(ValidationSeverity severity, string code, string message, string? entityType = null, string? entityId = null, string? source = null)
    {
        Issues.Add(new ValidationIssue
        {
            Severity = severity,
            Code = code,
            Message = message,
            EntityType = entityType,
            EntityId = entityId,
            Source = source
        });
    }
}
