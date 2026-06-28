namespace Reforged.Core.Logging;

/// <summary>
/// Fan-out logger used when applications need to write to multiple runtime-safe destinations.
/// </summary>
public sealed class CompositeReforgedLogger : IReforgedLogger
{
    private readonly IReadOnlyList<IReforgedLogger> loggers;

    public CompositeReforgedLogger(IEnumerable<IReforgedLogger> loggers)
    {
        this.loggers = loggers.ToList();
    }

    public void Write(LogSeverity severity, string source, string message, string? detail = null)
    {
        foreach (var logger in loggers)
        {
            logger.Write(severity, source, message, detail);
        }
    }
}
