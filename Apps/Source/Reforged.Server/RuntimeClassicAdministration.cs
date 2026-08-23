using System.Collections.Concurrent;
using System.Text.Json;

namespace Reforged.Server;

public sealed class RuntimeClassicAdministrationPolicy
{
    public int SchemaVersion { get; set; } = 1;
    public string WorldId { get; set; } = "classic";

    // When enabled, positive native GameLimit values are ignored and item creation is unrestricted.
    // Turning this off restores the decoded per-item game limits.
    public bool UnlimitedItemsEnabled { get; set; } = true;

    // Allows ordinary authenticated players to use only SYSOP GOTO. It does not grant any other
    // SYSOP command or administrative capability.
    public bool EveryoneCanUseSysGoto { get; set; }

    // Local Host/editor-managed character grants. This replaces the insecure legacy plaintext
    // SYSOPKEY behavior with an explicit persisted character capability.
    public List<string> SysopCharacterIds { get; set; } = [];

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string UpdatedBy { get; set; } = "system-default";

    public bool HasCharacterSysopAccess(string characterId) =>
        SysopCharacterIds?.Any(id => id.Equals(characterId, StringComparison.OrdinalIgnoreCase)) == true;
}

public static class RuntimeClassicAdministrationPolicyStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static RuntimeClassicAdministrationPolicy Load(string projectRoot, string worldId)
    {
        var normalizedWorld = RuntimeIdentity.Normalize(worldId, "world");
        var path = PathFor(projectRoot, normalizedWorld);
        if (!File.Exists(path))
        {
            return new RuntimeClassicAdministrationPolicy { WorldId = normalizedWorld };
        }

        try
        {
            var policy = JsonSerializer.Deserialize<RuntimeClassicAdministrationPolicy>(File.ReadAllText(path), JsonOptions)
                ?? throw new InvalidDataException("Classic administration policy deserialized to null.");
            policy.WorldId = normalizedWorld;
            policy.SysopCharacterIds = (policy.SysopCharacterIds ?? [])
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                .ToList();
            return policy;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"Classic administration policy is invalid: {path}", exception);
        }
    }

    public static void Save(string projectRoot, RuntimeClassicAdministrationPolicy policy, string updatedBy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        var normalizedWorld = RuntimeIdentity.Normalize(policy.WorldId, "world");
        var path = PathFor(projectRoot, normalizedWorld);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        policy.SchemaVersion = 1;
        policy.WorldId = normalizedWorld;
        policy.UpdatedAtUtc = DateTimeOffset.UtcNow;
        policy.UpdatedBy = string.IsNullOrWhiteSpace(updatedBy) ? "local-editor" : updatedBy.Trim();
        policy.SysopCharacterIds = (policy.SysopCharacterIds ?? [])
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(policy, JsonOptions) + Environment.NewLine);
            File.Move(temporary, path, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    public static string PathFor(string projectRoot, string worldId) =>
        Path.Combine(projectRoot, "Saves", "Runtime0137", "administration",
            RuntimeIdentity.Normalize(worldId, "world") + ".classic-policy.json");
}

public static class RuntimeClassicItemLimitCoordinator
{
    private static readonly ConcurrentDictionary<string, object> Gates = new(StringComparer.OrdinalIgnoreCase);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static void EnsureCanCreate(
        string projectRoot,
        string worldId,
        RuntimeItemDefinition definition,
        int requestedQuantity,
        int unsavedQuantityDelta)
    {
        if (requestedQuantity <= 0) throw new InvalidOperationException("Item creation quantity must be positive.");
        if (definition.GameLimit <= 0) return;

        var policy = RuntimeClassicAdministrationPolicyStore.Load(projectRoot, worldId);
        if (policy.UnlimitedItemsEnabled) return;

        var key = $"{Path.GetFullPath(projectRoot)}|{RuntimeIdentity.Normalize(worldId, "world")}|{definition.Id}";
        lock (Gates.GetOrAdd(key, _ => new object()))
        {
            var persisted = CountPersisted(projectRoot, worldId, definition.Id);
            // CurrentInstances is native/source evidence. Use it as a floor when no runtime save has
            // yet materialized those instances, but do not double-count instances already in saves.
            var authoritativeObserved = Math.Max(persisted, Math.Max(0, definition.CurrentInstances));
            var projected = checked(authoritativeObserved + Math.Max(0, unsavedQuantityDelta) + requestedQuantity);
            if (projected > definition.GameLimit)
            {
                throw new InvalidOperationException(
                    $"{definition.Name} is limited to {definition.GameLimit} instance(s) in this Classic world. " +
                    $"Unlimited items are disabled and {authoritativeObserved + Math.Max(0, unsavedQuantityDelta)} already exist.");
            }
        }
    }

    private static int CountPersisted(string projectRoot, string worldId, string canonicalItemId)
    {
        var total = 0;
        var runtimeRoot = Path.Combine(projectRoot, "Saves", "Runtime0137");
        var normalizedWorld = RuntimeIdentity.Normalize(worldId, "world");

        var characters = Path.Combine(runtimeRoot, "characters");
        if (Directory.Exists(characters))
        {
            foreach (var path in Directory.EnumerateFiles(characters, "*.json", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    var character = JsonSerializer.Deserialize<RuntimeCharacterRecord>(File.ReadAllText(path), JsonOptions);
                    if (character is null || !string.Equals(character.WorldId, normalizedWorld, StringComparison.OrdinalIgnoreCase)) continue;
                    total = checked(total + Count(character.ItemState, canonicalItemId));
                }
                catch (JsonException exception)
                {
                    throw new InvalidDataException($"Cannot verify Classic item limits because character save is invalid: {path}", exception);
                }
            }
        }

        var worldPath = Path.Combine(runtimeRoot, "worlds", normalizedWorld + ".items.json");
        if (File.Exists(worldPath))
        {
            try
            {
                var state = JsonSerializer.Deserialize<RuntimeItemState>(File.ReadAllText(worldPath), JsonOptions);
                total = checked(total + Count(state, canonicalItemId));
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException($"Cannot verify Classic item limits because world item state is invalid: {worldPath}", exception);
            }
        }

        // Generated/static room items are owned by the living-world save rather than the
        // Runtime0137 item repository. Include them so GameLimit is world-wide.
        var livingWorldPath = Path.Combine(projectRoot, "Saves", "Worlds", normalizedWorld, "living-world.json");
        if (File.Exists(livingWorldPath))
        {
            try
            {
                var snapshot = JsonSerializer.Deserialize<RuntimeLivingWorldSnapshot>(File.ReadAllText(livingWorldPath), JsonOptions);
                total = checked(total + Count(snapshot?.Items is null
                    ? null
                    : new RuntimeItemState(snapshot.Items, 0, new Dictionary<string, string>(), Array.Empty<RuntimeCorpseState>()), canonicalItemId));
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException($"Cannot verify Classic item limits because living-world item state is invalid: {livingWorldPath}", exception);
            }
        }

        return total;
    }

    private static int Count(RuntimeItemState? state, string canonicalItemId) =>
        state?.Items?.Where(item => string.Equals(item.CanonicalItemId, canonicalItemId, StringComparison.OrdinalIgnoreCase))
            .Aggregate(0, (sum, item) => checked(sum + Math.Max(0, item.StackQuantity))) ?? 0;
}
