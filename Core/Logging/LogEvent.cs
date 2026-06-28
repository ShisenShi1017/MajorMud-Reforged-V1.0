namespace Reforged.Core.Logging;

public sealed class LogEvent
{
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public LogLevel Level { get; set; } = LogLevel.Information;
    public string Source { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, string> Properties { get; set; } = new();
}
