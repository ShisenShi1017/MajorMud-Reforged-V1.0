namespace Reforged.Core.Logging;

public interface IReforgedLogger
{
    void Write(LogEvent entry);
    void Information(string source, string message);
    void Warning(string source, string message);
    void Error(string source, string message);
}
