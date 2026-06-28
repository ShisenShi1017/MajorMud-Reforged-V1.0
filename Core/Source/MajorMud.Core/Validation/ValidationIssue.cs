namespace MajorMud.Core.Validation;

public enum ValidationSeverity
{
    Info,
    Warning,
    Error,
    Fatal
}

public sealed record ValidationIssue(
    ValidationSeverity Severity,
    string Code,
    string Message,
    string? Path = null,
    string? Reference = null);
