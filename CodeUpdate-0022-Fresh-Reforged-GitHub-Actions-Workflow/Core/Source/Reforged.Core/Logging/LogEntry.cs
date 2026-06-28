namespace Reforged.Core.Logging;

public enum LogSeverity
{
    Trace,
    Information,
    Warning,
    Error,
    Critical
}

public sealed record LogEntry(
    DateTimeOffset TimestampUtc,
    LogSeverity Severity,
    string Source,
    string Message,
    string? Detail = null);
