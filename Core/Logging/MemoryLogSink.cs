namespace MajorMud.Reforged.Core.Logging;

/// <summary>
/// In-memory log sink useful for tests, startup diagnostics, editor display, and launcher health reports.
/// </summary>
public sealed class MemoryLogSink : ILogSink
{
    private readonly List<LogEvent> _entries = new();

    public IReadOnlyCollection<LogEvent> Entries => _entries.ToArray();

    public void Write(LogEvent entry)
    {
        _entries.Add(entry);
    }

    public void Clear() => _entries.Clear();
}
