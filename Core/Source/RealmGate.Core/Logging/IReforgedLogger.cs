namespace RealmGate.Core.Logging;

/// <summary>
/// Minimal runtime-safe logger contract used by applications without requiring a hosting framework.
/// </summary>
public interface IReforgedLogger
{
    void Write(LogSeverity severity, string source, string message, string? detail = null);

    void Trace(string source, string message, string? detail = null)
        => Write(LogSeverity.Trace, source, message, detail);

    void Info(string source, string message, string? detail = null)
        => Write(LogSeverity.Information, source, message, detail);

    void Warning(string source, string message, string? detail = null)
        => Write(LogSeverity.Warning, source, message, detail);

    void Error(string source, string message, string? detail = null)
        => Write(LogSeverity.Error, source, message, detail);

    void Critical(string source, string message, string? detail = null)
        => Write(LogSeverity.Critical, source, message, detail);
}
