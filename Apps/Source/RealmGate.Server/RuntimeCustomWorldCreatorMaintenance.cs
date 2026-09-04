using System.Globalization;
using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeCustomWorldShopResetResult(
    string ShopId,
    int StockEntries,
    int TotalQuantity,
    string StatePath,
    DateTimeOffset ResetAtUtc);

public sealed record RuntimeCustomWorldRoomItemCombineResult(
    string RoomId,
    int EligibleStacks,
    int FinalStacks,
    int RemovedStacks,
    int TotalQuantity,
    string StatePath,
    bool StateFileFound);

/// <summary>
/// Explicit creator-side maintenance helpers for non-Classic worlds. These helpers only touch
/// generated/custom runtime state under Saves/&lt;world&gt;. They never mutate Librarium or Classic.
/// </summary>
public static class RuntimeCustomWorldCreatorMaintenance
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public static RuntimeCustomWorldShopResetResult ResetShopToGeneratedNormalStock(
        string projectRoot,
        string worldId,
        string shopId)
    {
        projectRoot = Require(projectRoot, "project root");
        worldId = RequireCustomWorld(worldId);
        shopId = Require(shopId, "shop ID");

        var dataRoot = ResolveDataRoot(projectRoot, worldId);
        var shopsPath = Path.Combine(dataRoot, "shops.json");
        if (!File.Exists(shopsPath))
            throw new FileNotFoundException($"Generated shop data was not found for world '{worldId}'. Build the custom world first.", shopsPath);

        JsonElement? generated = null;
        foreach (var record in RuntimeChunkedClassicCatalog.LoadRecords(shopsPath))
        {
            if (!ReadText(record, "id").Equals(shopId, StringComparison.OrdinalIgnoreCase)) continue;
            generated = record.Clone();
            break;
        }
        if (generated is null) throw new KeyNotFoundException($"Generated shop '{shopId}' was not found in {shopsPath}.");

        var now = DateTimeOffset.UtcNow;
        var stock = new Dictionary<string, RuntimeShopStockEntry>(StringComparer.OrdinalIgnoreCase);
        if (generated.Value.TryGetProperty("inventory", out var inventory) && inventory.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in inventory.EnumerateArray())
            {
                var itemId = ReadText(entry, "item_ref");
                if (string.IsNullOrWhiteSpace(itemId)) continue;
                var normal = Math.Max(0, ReadInt(entry, "normal_qty"));
                stock[itemId] = new RuntimeShopStockEntry(itemId, normal, NextRestock(entry, now));
            }
        }

        var service = new RuntimeServiceDefinition(shopId, "shop", generated.Value);
        var stateStore = new RuntimeGameplayCompletionStateStore(projectRoot, worldId);
        stateStore.ExecuteExactShopStateTransaction(service, _ =>
        {
            var state = new RuntimeShopState(
                shopId,
                stock,
                now,
                Array.Empty<string>());
            return (state, 0);
        });

        var statePath = Path.Combine(
            RuntimeSavePaths.RealmRoot(projectRoot, worldId),
            "GameplayCompletion",
            "shops",
            RuntimeIdentity.Normalize(shopId, "shop") + ".json");
        return new RuntimeCustomWorldShopResetResult(shopId, stock.Count, stock.Values.Sum(entry => entry.Quantity), statePath, now);
    }

    /// <summary>
    /// Compacts duplicate dynamic room-ground stacks in the persisted living-world save. Generated
    /// static room placements are deliberately excluded because cleanup/reset owns their stable
    /// placement identities. Run this maintenance while the world host is stopped so a live in-memory
    /// living-world snapshot cannot overwrite the cleaned save afterward.
    /// </summary>
    public static RuntimeCustomWorldRoomItemCombineResult CombineLikeDynamicRoomItems(
        string projectRoot,
        string worldId,
        string roomId)
    {
        projectRoot = Require(projectRoot, "project root");
        worldId = RequireCustomWorld(worldId);
        roomId = Require(roomId, "room ID");

        var path = Path.Combine(RuntimeSavePaths.RealmRoot(projectRoot, worldId), "WorldState", "living-world.json");
        if (!File.Exists(path))
            return new RuntimeCustomWorldRoomItemCombineResult(roomId, 0, 0, 0, 0, path, false);

        var snapshot = JsonSerializer.Deserialize<RuntimeLivingWorldSnapshot>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidOperationException("The Custom1 living-world save could not be read.");
        if (!snapshot.WorldId.Equals(worldId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"The living-world save belongs to '{snapshot.WorldId}', not '{worldId}'.");

        var items = (snapshot.Items ?? Array.Empty<RuntimeItemInstance>()).ToList();
        var catalog = RuntimeItemCatalog.Load(projectRoot, worldId);
        var eligible = items.Where(item =>
                item.LocationKind.Equals(RuntimeItemLocations.Room, StringComparison.OrdinalIgnoreCase) &&
                item.LocationId.Equals(roomId, StringComparison.OrdinalIgnoreCase) &&
                !IsGeneratedStaticPlacement(item) &&
                catalog.Require(item.CanonicalItemId).Stackable)
            .ToArray();

        var eligibleIds = eligible.Select(item => item.InstanceId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var replacements = new Dictionary<string, RuntimeItemInstance>(StringComparer.OrdinalIgnoreCase);
        var removeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var totalQuantity = eligible.Sum(item => Math.Max(0, item.StackQuantity));

        foreach (var group in eligible.GroupBy(MergeKey, StringComparer.Ordinal))
        {
            var ordered = group.OrderBy(item => item.InstanceId, StringComparer.OrdinalIgnoreCase).ToArray();
            if (ordered.Length < 2) continue;
            var quantity = ordered.Sum(item => item.StackQuantity);
            var maxStack = MaximumStack(ordered[0]);
            var requiredStacks = checked((quantity + maxStack - 1) / maxStack);
            if (requiredStacks >= ordered.Length) continue;

            var remaining = quantity;
            for (var index = 0; index < ordered.Length; index++)
            {
                var item = ordered[index];
                if (index < requiredStacks)
                {
                    var stackQuantity = Math.Min(maxStack, remaining);
                    remaining -= stackQuantity;
                    replacements[item.InstanceId] = item with { StackQuantity = stackQuantity };
                }
                else
                {
                    removeIds.Add(item.InstanceId);
                }
            }
        }

        if (removeIds.Count > 0)
        {
            for (var index = items.Count - 1; index >= 0; index--)
            {
                var item = items[index];
                if (removeIds.Contains(item.InstanceId))
                {
                    items.RemoveAt(index);
                    continue;
                }
                if (replacements.TryGetValue(item.InstanceId, out var replacement)) items[index] = replacement;
            }

            var updated = snapshot with { Items = items, SavedAtUtc = DateTimeOffset.UtcNow };
            var backup = path + ".pre-combine-" + DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".bak";
            File.Copy(path, backup, overwrite: false);
            var temp = path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(updated, JsonOptions));
            File.Move(temp, path, true);
        }

        var finalStacks = eligible.Length - removeIds.Count;
        return new RuntimeCustomWorldRoomItemCombineResult(
            roomId,
            eligible.Length,
            finalStacks,
            removeIds.Count,
            totalQuantity,
            path,
            true);
    }

    private static string ResolveDataRoot(string projectRoot, string worldId)
    {
        var configPath = Path.Combine(projectRoot, "Config", "server.worlds.json");
        if (File.Exists(configPath))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(configPath));
            if (document.RootElement.TryGetProperty("worlds", out var worlds) && worlds.ValueKind == JsonValueKind.Array)
            {
                foreach (var world in worlds.EnumerateArray())
                {
                    if (!ReadText(world, "id").Equals(worldId, StringComparison.OrdinalIgnoreCase)) continue;
                    var root = ReadText(world, "dataRoot");
                    if (!string.IsNullOrWhiteSpace(root)) return Path.GetFullPath(Path.Combine(projectRoot, root));
                }
            }
        }
        return Path.Combine(projectRoot, "Data", worldId);
    }

    private static DateTimeOffset NextRestock(JsonElement generated, DateTimeOffset from)
    {
        var rounds = ReadInt(generated, "regen_time");
        if (rounds <= 0 || ReadInt(generated, "regen_qty") <= 0 || ReadInt(generated, "regen_percent") <= 0)
            return DateTimeOffset.MaxValue;
        return from.AddSeconds(checked(rounds * 6L));
    }

    private static bool IsGeneratedStaticPlacement(RuntimeItemInstance item) =>
        item.Metadata?.TryGetValue("generated_static_room_item", out var raw) == true &&
        raw.Equals("true", StringComparison.OrdinalIgnoreCase);

    private static int MaximumStack(RuntimeItemInstance item)
    {
        if (item.Metadata?.TryGetValue("maximum_stack", out var raw) == true &&
            int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0)
            return parsed;
        return int.MaxValue;
    }

    private static string MergeKey(RuntimeItemInstance item)
    {
        var metadata = item.Metadata is null
            ? string.Empty
            : string.Join("\u001e", item.Metadata.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => pair.Key.ToLowerInvariant() + "=" + pair.Value));
        return string.Join("\u001f",
            item.CanonicalItemId.ToLowerInvariant(),
            (item.OwnerId ?? string.Empty).ToLowerInvariant(),
            item.LocationKind.ToLowerInvariant(),
            item.LocationId.ToLowerInvariant(),
            (item.EquippedSlot ?? string.Empty).ToLowerInvariant(),
            item.Charges.ToString(CultureInfo.InvariantCulture),
            item.Durability?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            metadata);
    }

    private static string Require(string value, string label)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0) throw new ArgumentException($"{label} is required.", label);
        return normalized;
    }

    private static string RequireCustomWorld(string worldId)
    {
        var normalized = Require(worldId, "world ID");
        if (normalized.Equals("classic", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Creator maintenance is blocked for protected Classic.");
        return normalized;
    }

    private static string ReadText(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) ? value.ToString().Trim() : string.Empty;

    private static int ReadInt(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && int.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
}
