using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeClassicPermanentDeathInfoRecord(
    string AccountId,
    string WorldId,
    string SourceCharacterId,
    long RetainedExperience,
    DateTimeOffset SavedAtUtc);

/// <summary>
/// Reforged persistence bridge for Classic WCCMMUD permanent-info retained XP.
/// Native MajorMUD stored this at the BBS-user level and consumed it once while
/// creating the replacement character. Reforged scopes the bridge by account + world
/// because it supports multiple worlds and more than one character save per account.
/// </summary>
public static class RuntimeClassicPermanentDeathInfoStore
{
    private sealed class StoreDocument
    {
        public Dictionary<string, RuntimeClassicPermanentDeathInfoRecord> Pending { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private static readonly object Gate = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public static void Save(string projectRoot, string accountId, string worldId, string sourceCharacterId, long retainedExperience, DateTimeOffset savedAtUtc)
    {
        if (retainedExperience < 0) throw new ArgumentOutOfRangeException(nameof(retainedExperience));
        if (string.IsNullOrWhiteSpace(accountId)) throw new ArgumentException("Account id is required.", nameof(accountId));
        if (string.IsNullOrWhiteSpace(worldId)) throw new ArgumentException("World id is required.", nameof(worldId));
        lock (Gate)
        {
            var document = Load(projectRoot, worldId);
            document.Pending[accountId] = new(accountId, worldId, sourceCharacterId, retainedExperience, savedAtUtc);
            SaveDocument(projectRoot, worldId, document);
        }
    }

    public static long Consume(string projectRoot, string accountId, string worldId)
    {
        lock (Gate)
        {
            var document = Load(projectRoot, worldId);
            if (!document.Pending.Remove(accountId, out var pending)) return 0;
            SaveDocument(projectRoot, worldId, document);
            return Math.Max(0, pending.RetainedExperience);
        }
    }

    public static RuntimeClassicPermanentDeathInfoRecord? Peek(string projectRoot, string accountId, string worldId)
    {
        lock (Gate)
        {
            var document = Load(projectRoot, worldId);
            return document.Pending.TryGetValue(accountId, out var pending) ? pending : null;
        }
    }

    private static StoreDocument Load(string projectRoot, string worldId)
    {
        var path = StorePath(projectRoot, worldId);
        if (!File.Exists(path)) return new StoreDocument();
        try { return JsonSerializer.Deserialize<StoreDocument>(File.ReadAllText(path), JsonOptions) ?? new StoreDocument(); }
        catch { return new StoreDocument(); }
    }

    private static void SaveDocument(string projectRoot, string worldId, StoreDocument document)
    {
        var path = StorePath(projectRoot, worldId);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(document, JsonOptions) + Environment.NewLine);
        File.Move(temp, path, true);
    }

    private static string StorePath(string projectRoot, string worldId) =>
        Path.Combine(RuntimeSavePaths.RealmRoot(projectRoot, worldId), "classic-permanent-death-info.json");
}
