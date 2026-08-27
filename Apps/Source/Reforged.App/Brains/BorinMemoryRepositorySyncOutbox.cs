using System.Text.Json;
using System.Text.Json.Serialization;

namespace Reforged.App.Brains;

internal sealed record BorinMemoryRepositorySyncOutboxStatus(
    int PendingCount,
    DateTimeOffset? OldestPendingAt,
    DateTimeOffset? LatestPendingAt)
{
    public static BorinMemoryRepositorySyncOutboxStatus Empty { get; } = new(0, null, null);
}

/// <summary>
/// Small local-only durable outbox for cleanup snapshots that still need to reach Borin's
/// private memory repository. The outbox lives under AI/Memory, is intentionally excluded
/// from the remote-memory allowlist, and lets a failed Git push survive app restarts without
/// blocking gameplay or cleanup reconnect.
/// </summary>
internal sealed class BorinMemoryRepositorySyncOutboxStore
{
    private const int MaximumPendingSnapshots = 250;
    private const int MaximumCorruptOutboxArchives = 3;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private string _path = string.Empty;

    public void Configure(string memoryRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(memoryRoot);
        _path = Path.Combine(Path.GetFullPath(memoryRoot), ".borin-memory-sync-outbox.json");
    }

    public async Task StageAsync(BorinMemoryRepositorySyncMetadata metadata, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(metadata.CleanupId))
            throw new ArgumentException("A cleanup snapshot requires a stable cleanup ID.", nameof(metadata));

        await _gate.WaitAsync(token);
        try
        {
            var document = await LoadUnsafeAsync(token);
            document.Pending.RemoveAll(item => item.CleanupId.Equals(metadata.CleanupId, StringComparison.OrdinalIgnoreCase));
            document.Pending.Add(metadata);
            document.Pending.Sort((left, right) => left.Timestamp.CompareTo(right.Timestamp));
            if (document.Pending.Count > MaximumPendingSnapshots)
                document.Pending.RemoveRange(0, document.Pending.Count - MaximumPendingSnapshots);
            document.UpdatedAt = DateTimeOffset.UtcNow;
            await SaveUnsafeAsync(document, token);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<BorinMemoryRepositorySyncMetadata>> LoadPendingAsync(CancellationToken token = default)
    {
        await _gate.WaitAsync(token);
        try
        {
            var document = await LoadUnsafeAsync(token);
            return document.Pending
                .OrderBy(item => item.Timestamp)
                .ThenBy(item => item.CleanupId, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<BorinMemoryRepositorySyncOutboxStatus> GetStatusAsync(CancellationToken token = default)
    {
        var pending = await LoadPendingAsync(token);
        if (pending.Count == 0)
            return BorinMemoryRepositorySyncOutboxStatus.Empty;
        return new BorinMemoryRepositorySyncOutboxStatus(
            pending.Count,
            pending.Min(item => item.Timestamp),
            pending.Max(item => item.Timestamp));
    }

    public async Task RemoveAsync(IEnumerable<string> cleanupIds, CancellationToken token = default)
    {
        var ids = cleanupIds
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (ids.Count == 0)
            return;

        await _gate.WaitAsync(token);
        try
        {
            var document = await LoadUnsafeAsync(token);
            var removed = document.Pending.RemoveAll(item => ids.Contains(item.CleanupId));
            if (removed == 0)
                return;
            document.UpdatedAt = DateTimeOffset.UtcNow;
            if (document.Pending.Count == 0)
            {
                try
                {
                    if (File.Exists(_path))
                        File.Delete(_path);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    _ = exception;
                    // A stale outbox is safe: the next sync is idempotent and will clear it again.
                }
                return;
            }
            await SaveUnsafeAsync(document, token);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<OutboxDocument> LoadUnsafeAsync(CancellationToken token)
    {
        if (_path.Length == 0 || !File.Exists(_path))
            return new OutboxDocument();
        try
        {
            await using var stream = File.OpenRead(_path);
            return await JsonSerializer.DeserializeAsync<OutboxDocument>(stream, JsonOptions, token) ?? new OutboxDocument();
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            _ = exception;
            // Never let an operational outbox parse failure stop Borin from playing. Preserve
            // the unreadable bytes for diagnosis, then start a clean outbox. The authoritative
            // local durable-memory files remain untouched and an active cleanup checkpoint can
            // be staged again on the next confirmed boundary.
            try
            {
                var quarantine = _path + $".corrupt-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
                File.Move(_path, quarantine, overwrite: false);
                PruneCorruptArchives();
            }
            catch { }
            return new OutboxDocument();
        }
    }

    private void PruneCorruptArchives()
    {
        var directory = Path.GetDirectoryName(_path);
        var fileName = Path.GetFileName(_path);
        if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(fileName) || !Directory.Exists(directory))
            return;

        foreach (var stale in Directory.EnumerateFiles(directory, fileName + ".corrupt-*", SearchOption.TopDirectoryOnly)
                     .OrderByDescending(File.GetLastWriteTimeUtc)
                     .Skip(MaximumCorruptOutboxArchives))
        {
            try { File.Delete(stale); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private async Task SaveUnsafeAsync(OutboxDocument document, CancellationToken token)
    {
        if (_path.Length == 0)
            throw new InvalidOperationException("Borin's memory-sync outbox has not been configured.");
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temporary = _path + $".{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                await JsonSerializer.SerializeAsync(stream, document, JsonOptions, token);
            File.Move(temporary, _path, overwrite: true);
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); } catch { }
        }
    }

    private sealed class OutboxDocument
    {
        public int SchemaVersion { get; set; } = 1;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
        public List<BorinMemoryRepositorySyncMetadata> Pending { get; set; } = [];
    }
}
