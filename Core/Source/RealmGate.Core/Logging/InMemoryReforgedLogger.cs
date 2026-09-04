namespace RealmGate.Core.Logging;

/// <summary>
/// Dependency-free logger useful for startup checks, tests, and tools.
/// </summary>
public sealed class InMemoryReforgedLogger : IReforgedLogger
{
    private readonly List<LogEntry> entries = new();

    public IReadOnlyList<LogEntry> Entries => entries;

    public void Write(LogSeverity severity, string source, string message, string? detail = null)
    {
        entries.Add(new LogEntry(DateTimeOffset.UtcNow, severity, source, message, detail));
    }
}
