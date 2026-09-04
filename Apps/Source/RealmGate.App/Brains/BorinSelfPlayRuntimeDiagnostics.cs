using System.Text.Json;
using System.Text.RegularExpressions;

namespace RealmGate.App.Brains;

public sealed record BorinSelfPlayDiagnosticEvent(
    DateTimeOffset Timestamp,
    string Category,
    string Detail,
    string Command,
    string Phase,
    string Room,
    int HealthPercent,
    int ManaPercent,
    int CharacterLevel,
    long ExperienceCurrent,
    bool InCombat,
    bool Connected,
    bool LoginReady,
    bool CanAutonomousPlay);

public sealed record BorinSelfPlayDiagnosticSummary(
    string StoragePath,
    int BufferedEvents,
    DateTimeOffset? LastEventAt,
    string LastCategory,
    string LastDetail);

/// <summary>
/// Bounded, high-level runtime diagnostics for Borin's autonomous-player lifecycle.
/// It intentionally never records terminal transcripts, passwords, model prompts, model
/// reasoning, credentials, or the contents of Borin's durable memory. The log exists to
/// answer operational questions such as why self-play paused, why an action was rejected,
/// whether server evidence arrived, and whether cleanup/reconnect/resume completed.
/// </summary>
public sealed class BorinSelfPlayRuntimeDiagnostics
{
    private const int MaximumBufferedEvents = 200;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly Regex UnsafeDetailPattern = new(
        @"(?i)\b(password|passwd|token|secret|authorization|credential)\b\s*[:=]\s*[^\s;,]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex UnsafeUrlPattern = new(
        @"(?i)(https?://)[^/@\s:]+:[^/@\s]+@",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly object _gate = new();
    private readonly Queue<BorinSelfPlayDiagnosticEvent> _events = new();
    private string _storagePath = string.Empty;
    private string _directory = string.Empty;
    private string _sessionId = "borin-training-01";
    private bool _enabled;
    private long _maximumFileBytes = 1024L * 1024L;
    private int _retentionFiles = 5;
    private string _lastDeduplicationKey = string.Empty;
    private DateTimeOffset _lastDeduplicationAt = DateTimeOffset.MinValue;

    public void Configure(
        string repositoryRoot,
        string sessionId,
        bool enabled,
        int maximumFileKilobytes,
        int retentionFiles)
    {
        lock (_gate)
        {
            _enabled = enabled;
            _sessionId = SanitizeFilePart(sessionId, "borin-training-01");
            _maximumFileBytes = Math.Clamp(maximumFileKilobytes, 128, 4096) * 1024L;
            _retentionFiles = Math.Clamp(retentionFiles, 2, 20);
            _directory = Path.Combine(Path.GetFullPath(repositoryRoot), "Release", "Logs", "BorinBrain", "SelfPlayDiagnostics");
            _storagePath = Path.Combine(_directory, $"Borin-SelfPlay-{_sessionId}.jsonl");
            if (_enabled)
            {
                try
                {
                    Directory.CreateDirectory(_directory);
                    PruneUnsafe();
                }
                catch
                {
                    // Diagnostics are observational only and must never stop gameplay.
                }
            }
        }
    }

    public void Record(string category, string detail, BorinGameStateSnapshot state, string command = "")
    {
        ArgumentNullException.ThrowIfNull(state);
        lock (_gate)
        {
            if (!_enabled || string.IsNullOrWhiteSpace(_storagePath))
                return;

            var safeCategory = Normalize(category, 60);
            var safeDetail = Redact(Normalize(detail, 600));
            var safeCommand = RedactCommand(command);
            var now = DateTimeOffset.Now;
            var dedupeKey = $"{safeCategory}|{safeCommand}|{safeDetail}|{state.CurrentRoom}|{state.CanAutonomousPlay}";
            if (dedupeKey.Equals(_lastDeduplicationKey, StringComparison.Ordinal) &&
                now - _lastDeduplicationAt < TimeSpan.FromSeconds(5))
            {
                return;
            }

            _lastDeduplicationKey = dedupeKey;
            _lastDeduplicationAt = now;
            var entry = new BorinSelfPlayDiagnosticEvent(
                now,
                safeCategory,
                safeDetail,
                safeCommand,
                state.Phase.ToString(),
                state.HasVerifiedRoom ? Normalize(state.CurrentRoom, 180) : "Unknown",
                state.HealthPercent,
                state.ManaPercent,
                state.CharacterLevel,
                state.ExperienceCurrent,
                state.InCombat,
                state.IsConnected,
                state.IsLoggedIntoMajorMud,
                state.CanAutonomousPlay);

            _events.Enqueue(entry);
            while (_events.Count > MaximumBufferedEvents)
                _events.Dequeue();

            try
            {
                RotateIfNeededUnsafe();
                File.AppendAllText(_storagePath, JsonSerializer.Serialize(entry, Json) + Environment.NewLine);
            }
            catch
            {
                // A diagnostics failure is never a gameplay failure and never changes memory.
            }
        }
    }

    public BorinSelfPlayDiagnosticSummary Snapshot()
    {
        lock (_gate)
        {
            var last = _events.LastOrDefault();
            return new BorinSelfPlayDiagnosticSummary(
                _storagePath,
                _events.Count,
                last?.Timestamp,
                last?.Category ?? string.Empty,
                last?.Detail ?? string.Empty);
        }
    }

    private void RotateIfNeededUnsafe()
    {
        Directory.CreateDirectory(_directory);
        if (File.Exists(_storagePath) && new FileInfo(_storagePath).Length >= _maximumFileBytes)
        {
            var archive = Path.Combine(
                _directory,
                $"Borin-SelfPlay-{_sessionId}-{DateTimeOffset.Now:yyyyMMdd-HHmmss-fff}.jsonl");
            File.Move(_storagePath, archive, overwrite: false);
        }
        PruneUnsafe();
    }

    private void PruneUnsafe()
    {
        if (!Directory.Exists(_directory))
            return;
        var archives = Directory.EnumerateFiles(_directory, $"Borin-SelfPlay-{_sessionId}-*.jsonl")
            .Select(path => new FileInfo(path))
            .OrderByDescending(info => info.LastWriteTimeUtc)
            .ToArray();
        foreach (var stale in archives.Skip(_retentionFiles))
        {
            try { stale.Delete(); }
            catch { }
        }
    }

    private static string Redact(string value)
    {
        var redacted = UnsafeDetailPattern.Replace(value, match => $"{match.Groups[1].Value}=[REDACTED]");
        return UnsafeUrlPattern.Replace(redacted, "$1[REDACTED]@");
    }

    private static string RedactCommand(string? command)
    {
        var normalized = Normalize(command, 180);
        if (normalized.Length == 0)
            return string.Empty;
        var root = normalized.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries)[0];
        if (root.Equals("PASSWORD", StringComparison.OrdinalIgnoreCase) ||
            root.Equals("PASSWD", StringComparison.OrdinalIgnoreCase) ||
            root.Equals("LOGIN", StringComparison.OrdinalIgnoreCase))
        {
            return $"{root.ToUpperInvariant()} [REDACTED]";
        }
        return Redact(normalized);
    }

    private static string Normalize(string? value, int maximumLength)
    {
        var normalized = Regex.Replace(value?.Trim() ?? string.Empty, @"\s+", " ");
        return normalized[..Math.Min(maximumLength, normalized.Length)];
    }

    private static string SanitizeFilePart(string? value, string fallback)
    {
        var normalized = Regex.Replace(value?.Trim() ?? string.Empty, "[^A-Za-z0-9._-]", "-");
        if (normalized.Length == 0)
            normalized = fallback;
        return normalized[..Math.Min(64, normalized.Length)];
    }
}
