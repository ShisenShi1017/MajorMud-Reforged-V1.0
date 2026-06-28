namespace MajorMud.Reforged.Core.Logging;

/// <summary>
/// Sink contract for structured log destinations.
/// </summary>
public interface ILogSink
{
    void Write(LogEvent entry);
}
