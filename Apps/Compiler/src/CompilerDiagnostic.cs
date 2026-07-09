namespace MajorMud.Reforged.Compiler;

public enum DiagnosticSeverity
{
    Info,
    Warning,
    Error
}

public sealed record CompilerDiagnostic(DiagnosticSeverity Severity, string Code, string Message, string? Path = null);
