using System.Diagnostics;

namespace RealmGate.App.Services;

public static class RealmGatePerformanceTrace
{
    private static readonly object Sync = new();

    public static string LogPath =>
        Path.Combine(AppContext.BaseDirectory, "realmgate-performance.log");

    public static void Mark(string area, string message)
    {
        Write($"{area} | {message}");
    }

    public static IDisposable Measure(string area, string operation)
    {
        return new Measurement(area, operation);
    }

    public static void Reset()
    {
        lock (Sync)
        {
            try
            {
                if (File.Exists(LogPath))
                    File.Delete(LogPath);
            }
            catch
            {
                // Performance diagnostics must never stop RealmGate.
            }
        }
    }

    private static void Write(string message)
    {
        lock (Sync)
        {
            try
            {
                File.AppendAllText(
                    LogPath,
                    $"{DateTimeOffset.Now:O} | {message}{Environment.NewLine}");
            }
            catch
            {
                // Performance diagnostics must never stop RealmGate.
            }
        }
    }

    private sealed class Measurement : IDisposable
    {
        private readonly string _area;
        private readonly string _operation;
        private readonly Stopwatch _stopwatch;
        private bool _disposed;

        public Measurement(string area, string operation)
        {
            _area = area;
            _operation = operation;
            _stopwatch = Stopwatch.StartNew();

            Write($"{_area} | START | {_operation}");
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _stopwatch.Stop();

            Write(
                $"{_area} | DONE | {_operation} | " +
                $"Elapsed={_stopwatch.Elapsed} | " +
                $"Milliseconds={_stopwatch.Elapsed.TotalMilliseconds:F1}");
        }
    }
}
