namespace Reforged.Core.Logging;

/// <summary>
/// Logger that fans out to multiple sinks while preserving a single Core logging contract.
/// </summary>
public sealed class CompositeReforgedLogger : IReforgedLogger
{
    private readonly List<ILogSink> _sinks = new();

    public CompositeReforgedLogger AddSink(ILogSink sink)
    {
        _sinks.Add(sink ?? throw new ArgumentNullException(nameof(sink)));
        return this;
    }

    public void Write(LogEvent entry)
    {
        foreach (var sink in _sinks)
        {
            sink.Write(entry);
        }
    }

    public void Information(string source, string message) => Write(new LogEvent { Level = LogLevel.Information, Source = source, Message = message });
    public void Warning(string source, string message) => Write(new LogEvent { Level = LogLevel.Warning, Source = source, Message = message });
    public void Error(string source, string message) => Write(new LogEvent { Level = LogLevel.Error, Source = source, Message = message });
}
