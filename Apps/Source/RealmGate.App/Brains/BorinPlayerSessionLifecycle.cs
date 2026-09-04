using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace RealmGate.App.Brains;

internal enum BorinCleanupSignalKind
{
    None = 0,
    Warning = 1,
    Starting = 2,
    Completed = 3,
}

internal sealed record BorinPlayerSessionCheckpoint(
    string Id,
    DateTimeOffset StartedAt,
    DateTimeOffset UpdatedAt,
    string State,
    string Trigger,
    string Assignment,
    bool ResumeAutonomousPlay,
    string Server,
    string World,
    string Character,
    string Room,
    int HealthPercent,
    int ManaPercent,
    int CharacterLevel,
    long ExperienceCurrent,
    string LastCommand,
    int VerifiedObservationCount,
    IReadOnlyList<string> VerifiedLessons,
    string Reflection,
    string Summary,
    DateTimeOffset? ConsolidatedAt,
    DateTimeOffset? DisconnectedAt,
    DateTimeOffset? ResumedAt)
{
    public static BorinPlayerSessionCheckpoint Empty { get; } = new(
        string.Empty,
        DateTimeOffset.MinValue,
        DateTimeOffset.MinValue,
        "None",
        string.Empty,
        string.Empty,
        false,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        0,
        0,
        0,
        0,
        string.Empty,
        0,
        Array.Empty<string>(),
        string.Empty,
        string.Empty,
        null,
        null,
        null);

    public bool Exists => Id.Length > 0;
    public bool AwaitingResume => Exists && ResumedAt is null && ResumeAutonomousPlay;
}

/// <summary>
/// Durable cleanup/restart checkpoint for Borin's external-player session.
/// It stores concise verified state and high-level reflection summaries only.
/// Raw model reasoning, credentials, and terminal transcripts are never copied here.
/// </summary>
internal sealed class BorinPlayerSessionLifecycleStore
{
    private const int MaximumHistory = 120;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly Regex CleanupCompletedPattern = new(
        @"\b(?:server|system|world|majormud)\b.{0,90}\b(?:cleanup|clean-up|restart|reboot|maintenance|reset)\b.{0,90}\b(?:complete|completed|finished|done)\b|\b(?:cleanup|restart|reboot|maintenance)\b.{0,70}\b(?:complete|completed|finished|done)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex CleanupStartingPattern = new(
        @"\b(?:server|system|world|majormud)\b.{0,100}\b(?:is\s+)?(?:restarting|rebooting|shutting\s+down|going\s+down|closing|resetting)\b|\b(?:disconnecting|logging\s+off)\s+(?:all\s+)?(?:users|players)\b|\bshutdown\s+now\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex CleanupWarningPattern = new(
        @"\b(?:server|system|world|majormud)\b.{0,100}\b(?:cleanup|clean-up|restart|reboot|shutdown|shut\s+down|maintenance|reset)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private string _storagePath = string.Empty;
    private LifecycleDocument _document = new();

    public string StoragePath => _storagePath;
    public BorinPlayerSessionCheckpoint Current => _document.Current?.ToPublic() ?? BorinPlayerSessionCheckpoint.Empty;

    public static bool IsConfirmedCleanupMemoryBoundary(BorinCleanupSignalKind signal) =>
        signal == BorinCleanupSignalKind.Completed;

    public static bool HasConfirmedCleanupMemoryBoundary(BorinPlayerSessionCheckpoint checkpoint) =>
        checkpoint.Exists &&
        (checkpoint.DisconnectedAt is not null ||
         checkpoint.State.Equals("DisconnectedForCleanup", StringComparison.OrdinalIgnoreCase) ||
         checkpoint.State.Equals("CleanupBoundaryConfirmed", StringComparison.OrdinalIgnoreCase));

    public static BorinCleanupSignalKind DetectCleanupSignal(string line, string classification)
    {
        if (string.IsNullOrWhiteSpace(line))
            return BorinCleanupSignalKind.None;
        if (classification is "Speech" or "Telepath" or "Group")
            return BorinCleanupSignalKind.None;

        var candidate = line.Trim();
        if (CleanupCompletedPattern.IsMatch(candidate))
            return BorinCleanupSignalKind.Completed;
        if (CleanupStartingPattern.IsMatch(candidate))
            return BorinCleanupSignalKind.Starting;
        if (CleanupWarningPattern.IsMatch(candidate))
            return BorinCleanupSignalKind.Warning;
        return BorinCleanupSignalKind.None;
    }

    public async Task ConfigureAsync(string repositoryRoot, string sessionId, CancellationToken token)
    {
        var root = Path.GetFullPath(repositoryRoot);
        var safeSession = Regex.Replace(sessionId ?? string.Empty, "[^A-Za-z0-9._-]", "-");
        if (safeSession.Length == 0)
            safeSession = "borin-training-01";
        _storagePath = Path.Combine(root, "AI", "Memory", "Gameplay", safeSession, "borin-session-lifecycle.json");

        await _gate.WaitAsync(token);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_storagePath)!);
            if (File.Exists(_storagePath))
            {
                try
                {
                    await using var stream = File.OpenRead(_storagePath);
                    _document = await JsonSerializer.DeserializeAsync<LifecycleDocument>(stream, JsonOptions, token) ?? new LifecycleDocument();
                }
                catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
                {
                    _document = new LifecycleDocument();
                }
            }
            else
            {
                _document = new LifecycleDocument();
                await SaveUnsafeAsync(token);
            }
            NormalizeUnsafe();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<BorinPlayerSessionCheckpoint> BeginCleanupAsync(
        BorinCleanupSignalKind signal,
        string trigger,
        string assignment,
        bool resumeAutonomousPlay,
        BorinGameStateSnapshot state,
        CancellationToken token)
    {
        await _gate.WaitAsync(token);
        try
        {
            var now = DateTimeOffset.Now;
            var checkpoint = new CheckpointEntry
            {
                Id = $"cleanup-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..46],
                StartedAt = now,
                UpdatedAt = now,
                State = signal == BorinCleanupSignalKind.Starting ? "CleanupStarting" : "CleanupWarning",
                Trigger = Normalize(trigger, 500),
                Assignment = Normalize(assignment, 500),
                ResumeAutonomousPlay = resumeAutonomousPlay,
                Server = Normalize(state.Server, 100),
                World = Normalize(state.World, 100),
                Character = Normalize(state.CharacterName, 80),
                Room = Normalize(state.HasVerifiedRoom ? state.CurrentRoom : "Unknown room", 200),
                HealthPercent = state.HealthPercent,
                ManaPercent = state.ManaPercent,
                CharacterLevel = state.CharacterLevel,
                ExperienceCurrent = state.ExperienceCurrent,
                LastCommand = Normalize(state.LastCommand, 160),
            };
            _document.Current = checkpoint;
            _document.UpdatedAt = now;
            await SaveUnsafeAsync(token);
            return checkpoint.ToPublic();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<BorinPlayerSessionCheckpoint> CompleteConsolidationAsync(
        BorinGameplayLearningSnapshot gameplay,
        string reflection,
        CancellationToken token)
    {
        await _gate.WaitAsync(token);
        try
        {
            var entry = _document.Current ?? new CheckpointEntry
            {
                Id = $"cleanup-{DateTimeOffset.Now:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..46],
                StartedAt = DateTimeOffset.Now,
                Assignment = string.Empty,
            };
            var now = DateTimeOffset.Now;
            entry.UpdatedAt = now;
            entry.State = "ConsolidatedWaitingForRestart";
            entry.ConsolidatedAt = now;
            entry.VerifiedObservationCount = gameplay.VerifiedObservationCount;
            entry.VerifiedLessons = gameplay.RelevantLessons.Take(8).Select(item => Normalize(item, 700)).ToList();
            entry.Reflection = Normalize(reflection, 1800);
            entry.Summary = BuildSummary(entry);
            _document.Current = entry;
            _document.UpdatedAt = now;
            await SaveUnsafeAsync(token);
            return entry.ToPublic();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<BorinPlayerSessionCheckpoint> MarkBoundaryConfirmedAsync(string detail, CancellationToken token)
    {
        await _gate.WaitAsync(token);
        try
        {
            if (_document.Current is null)
                return BorinPlayerSessionCheckpoint.Empty;

            var now = DateTimeOffset.Now;
            _document.Current.UpdatedAt = now;
            _document.Current.State = "CleanupBoundaryConfirmed";
            if (!string.IsNullOrWhiteSpace(detail))
                _document.Current.Trigger = Normalize(detail, 500);
            _document.UpdatedAt = now;
            await SaveUnsafeAsync(token);
            return _document.Current.ToPublic();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<BorinPlayerSessionCheckpoint> MarkDisconnectedAsync(string detail, CancellationToken token)
    {
        await _gate.WaitAsync(token);
        try
        {
            if (_document.Current is null)
                return BorinPlayerSessionCheckpoint.Empty;
            var now = DateTimeOffset.Now;
            _document.Current.DisconnectedAt ??= now;
            _document.Current.UpdatedAt = now;
            _document.Current.State = "DisconnectedForCleanup";
            if (!string.IsNullOrWhiteSpace(detail))
                _document.Current.Trigger = Normalize($"{_document.Current.Trigger} | Disconnect: {detail}", 700);
            _document.UpdatedAt = now;
            await SaveUnsafeAsync(token);
            return _document.Current.ToPublic();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<BorinPlayerSessionCheckpoint> MarkResumedAsync(BorinGameStateSnapshot state, CancellationToken token)
    {
        await _gate.WaitAsync(token);
        try
        {
            if (_document.Current is null)
                return BorinPlayerSessionCheckpoint.Empty;
            var now = DateTimeOffset.Now;
            _document.Current.ResumedAt = now;
            _document.Current.UpdatedAt = now;
            _document.Current.State = "Resumed";
            if (state.HasVerifiedRoom)
                _document.Current.Room = Normalize(state.CurrentRoom, 200);
            _document.Current.HealthPercent = state.HealthPercent;
            _document.Current.ManaPercent = state.ManaPercent;
            _document.Current.CharacterLevel = state.CharacterLevel;
            _document.Current.ExperienceCurrent = state.ExperienceCurrent;
            _document.Current.Summary = BuildSummary(_document.Current);

            _document.History.Add(_document.Current);
            while (_document.History.Count > MaximumHistory)
                _document.History.RemoveAt(0);
            var completed = _document.Current.ToPublic();
            _document.Current = null;
            _document.UpdatedAt = now;
            await SaveUnsafeAsync(token);
            return completed;
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task<int> PruneRawGameplayJournalsAsync(
        string currentJournalPath,
        int retentionDays,
        int maximumMegabytes,
        int maximumFiles,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (_storagePath.Length == 0)
            return Task.FromResult(0);
        var directory = Path.GetDirectoryName(_storagePath)!;
        if (!Directory.Exists(directory))
            return Task.FromResult(0);

        var cutoff = DateTime.UtcNow.Subtract(TimeSpan.FromDays(Math.Clamp(retentionDays, 1, 90)));
        var maximumBytes = Math.Clamp(maximumMegabytes, 8, 1024) * 1024L * 1024L;
        var boundedMaximumFiles = Math.Clamp(maximumFiles, 4, 500);
        var current = string.IsNullOrWhiteSpace(currentJournalPath) ? string.Empty : Path.GetFullPath(currentJournalPath);
        var deleted = 0;

        var candidates = Directory.EnumerateFiles(directory, "events-*.jsonl", SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(Path.GetFullPath(path)))
            .Where(file => current.Length == 0 || !file.FullName.Equals(current, StringComparison.OrdinalIgnoreCase))
            .OrderBy(file => file.LastWriteTimeUtc)
            .ToList();

        foreach (var file in candidates.Where(file => file.LastWriteTimeUtc < cutoff).ToArray())
        {
            token.ThrowIfCancellationRequested();
            if (TryDeleteJournal(file.FullName))
            {
                candidates.Remove(file);
                deleted++;
            }
        }

        long totalBytes = candidates.Sum(file => SafeLength(file));
        var consecutiveDeleteFailures = 0;
        while (candidates.Count > boundedMaximumFiles || totalBytes > maximumBytes)
        {
            token.ThrowIfCancellationRequested();
            var oldest = candidates[0];
            var length = SafeLength(oldest);
            if (!TryDeleteJournal(oldest.FullName))
            {
                // Do not spin forever on locked/read-only files. Rotate each failed candidate
                // once; if an entire pass cannot delete anything, leave the files in place.
                candidates.RemoveAt(0);
                candidates.Add(oldest);
                consecutiveDeleteFailures++;
                if (consecutiveDeleteFailures >= candidates.Count)
                    break;
                continue;
            }

            candidates.RemoveAt(0);
            totalBytes = Math.Max(0, totalBytes - length);
            deleted++;
            consecutiveDeleteFailures = 0;
        }

        return Task.FromResult(deleted);
    }

    private static long SafeLength(FileInfo file)
    {
        try { return file.Exists ? file.Length : 0L; }
        catch (IOException) { return 0L; }
        catch (UnauthorizedAccessException) { return 0L; }
    }

    private static bool TryDeleteJournal(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
            return true;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }


    private async Task SaveUnsafeAsync(CancellationToken token)
    {
        if (_storagePath.Length == 0)
            return;
        Directory.CreateDirectory(Path.GetDirectoryName(_storagePath)!);
        var temp = _storagePath + ".tmp";
        await using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            await JsonSerializer.SerializeAsync(stream, _document, JsonOptions, token);
        File.Move(temp, _storagePath, overwrite: true);
    }

    private void NormalizeUnsafe()
    {
        _document.History ??= [];
        if (_document.History.Count > MaximumHistory)
            _document.History = _document.History.TakeLast(MaximumHistory).ToList();
    }

    private static string BuildSummary(CheckpointEntry entry)
    {
        var lessons = entry.VerifiedLessons.Count == 0
            ? "No verified gameplay lesson was selected during consolidation."
            : string.Join(" | ", entry.VerifiedLessons.Take(4));
        var reflection = string.IsNullOrWhiteSpace(entry.Reflection) ? "No reflection was written." : entry.Reflection;
        return Normalize(
            $"Goal: {entry.Assignment}. Last verified room: {entry.Room}. Level {entry.CharacterLevel}; XP {entry.ExperienceCurrent:N0}; HP/MP {entry.HealthPercent}/{entry.ManaPercent}. " +
            $"Verified observations retained: {entry.VerifiedObservationCount}. Lessons: {lessons} Reflection: {reflection}",
            3500);
    }

    private static string Normalize(string? value, int maximumLength)
    {
        var candidate = Regex.Replace(value ?? string.Empty, @"\s+", " ").Trim();
        return candidate.Length <= maximumLength ? candidate : candidate[..maximumLength];
    }

    private sealed class LifecycleDocument
    {
        public DateTimeOffset UpdatedAt { get; set; }
        public CheckpointEntry? Current { get; set; }
        public List<CheckpointEntry> History { get; set; } = [];
    }

    private sealed class CheckpointEntry
    {
        public string Id { get; set; } = string.Empty;
        public DateTimeOffset StartedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public string State { get; set; } = "None";
        public string Trigger { get; set; } = string.Empty;
        public string Assignment { get; set; } = string.Empty;
        public bool ResumeAutonomousPlay { get; set; }
        public string Server { get; set; } = string.Empty;
        public string World { get; set; } = string.Empty;
        public string Character { get; set; } = string.Empty;
        public string Room { get; set; } = string.Empty;
        public int HealthPercent { get; set; }
        public int ManaPercent { get; set; }
        public int CharacterLevel { get; set; }
        public long ExperienceCurrent { get; set; }
        public string LastCommand { get; set; } = string.Empty;
        public int VerifiedObservationCount { get; set; }
        public List<string> VerifiedLessons { get; set; } = [];
        public string Reflection { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public DateTimeOffset? ConsolidatedAt { get; set; }
        public DateTimeOffset? DisconnectedAt { get; set; }
        public DateTimeOffset? ResumedAt { get; set; }

        public BorinPlayerSessionCheckpoint ToPublic() => new(
            Id,
            StartedAt,
            UpdatedAt,
            State,
            Trigger,
            Assignment,
            ResumeAutonomousPlay,
            Server,
            World,
            Character,
            Room,
            HealthPercent,
            ManaPercent,
            CharacterLevel,
            ExperienceCurrent,
            LastCommand,
            VerifiedObservationCount,
            VerifiedLessons.ToArray(),
            Reflection,
            Summary,
            ConsolidatedAt,
            DisconnectedAt,
            ResumedAt);
    }
}
