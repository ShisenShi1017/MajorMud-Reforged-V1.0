namespace MajorMud.Core.Validation;

public sealed class ValidationResult
{
    public List<ValidationIssue> Issues { get; } = new();

    public bool HasErrors => Issues.Any(i => i.Severity is ValidationSeverity.Error or ValidationSeverity.Fatal);

    public void Add(ValidationSeverity severity, string code, string message, string? path = null, string? reference = null)
        => Issues.Add(new ValidationIssue(severity, code, message, path, reference));
}
