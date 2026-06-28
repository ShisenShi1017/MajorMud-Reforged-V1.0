namespace MajorMud.Reforged.Core.Logging;

/// <summary>
/// Simple dependency-free file logger source for future compiled apps.
/// </summary>
public sealed class FileReforgedLogger : IReforgedLogger
{
    private readonly string _logFile;

    public FileReforgedLogger(string logFile)
    {
        _logFile = logFile;
    }

    public void Write(LogEvent entry)
    {
        var directory = Path.GetDirectoryName(_logFile);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var line = $"{entry.Timestamp:u} [{entry.Level}] {entry.Source}: {entry.Message}";
        File.AppendAllLines(_logFile, new[] { line });
    }

    public void Information(string source, string message) => Write(new LogEvent { Level = LogLevel.Information, Source = source, Message = message });
    public void Warning(string source, string message) => Write(new LogEvent { Level = LogLevel.Warning, Source = source, Message = message });
    public void Error(string source, string message) => Write(new LogEvent { Level = LogLevel.Error, Source = source, Message = message });
}
