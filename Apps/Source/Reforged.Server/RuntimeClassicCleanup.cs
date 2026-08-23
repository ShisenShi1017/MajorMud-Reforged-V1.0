namespace Reforged.Server;

public sealed record RuntimeClassicCleanupResult(
    int HiddenRoomItems,
    int AlreadyHiddenRoomItems,
    int DeletedRoomItems,
    int HiddenCurrencyRooms,
    int ExemptItems,
    IReadOnlyList<string> DeletedInstanceIds,
    IReadOnlyList<string> HiddenInstanceIds,
    int RechargedItems = 0,
    IReadOnlyList<string>? RechargedInstanceIds = null);

/// <summary>
/// Classic daily cleanup boundary. Room-ground objects are hidden unless the decoded item
/// definition carries Del@Maint (ability 119), in which case they are deleted whether hidden or
/// visible. Surviving items in any ownership domain receive their decoded Recharge (ability 121)
/// uses up to their finite maximum. Character inventories/equipment remain exempt from deletion.
/// </summary>
public sealed class RuntimeClassicCleanupService
{
    private readonly RuntimeItemEngine _items;
    private readonly RuntimeItemCatalog _catalog;
    private readonly RuntimeNpcGroundDropService _groundDrops;

    public RuntimeClassicCleanupService(RuntimeItemEngine items, RuntimeItemCatalog catalog, RuntimeNpcGroundDropService groundDrops)
    {
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _groundDrops = groundDrops ?? throw new ArgumentNullException(nameof(groundDrops));
    }

    public RuntimeClassicCleanupResult Run(DateTimeOffset nowUtc) => CleanupRoomGround(nowUtc, null);

    public RuntimeClassicCleanupResult Run(DateTimeOffset nowUtc, string projectRoot, string worldId) =>
        CleanupRoomGround(nowUtc, new RuntimeClassicGangHouseProtectionService(projectRoot, worldId));

    public RuntimeClassicCleanupResult Run(
        DateTimeOffset nowUtc,
        RuntimeClassicGangHouseProtectionService? gangHouseProtection) =>
        CleanupRoomGround(nowUtc, gangHouseProtection);

    /// <summary>
    /// Authoritative Classic room-ground cleanup consumer. The caller owns scheduling because no
    /// decoded universal cleanup interval has been verified. Replaying the operation is safe:
    /// already-hidden objects stay hidden and are reported separately.
    /// </summary>
    public RuntimeClassicCleanupResult CleanupRoomGround(
        DateTimeOffset nowUtc,
        RuntimeClassicGangHouseProtectionService? gangHouseProtection)
    {
        var hidden = new List<string>();
        var deleted = new List<string>();
        var alreadyHidden = 0;
        var exempt = 0;
        foreach (var item in _items.Items.ToArray())
        {
            // Cleanup owns room-ground state only. Character, equipment, NPC, container, corpse,
            // merchant and other ownership domains are outside this consumer and remain untouched.
            if (!item.LocationKind.Equals(RuntimeItemLocations.Room, StringComparison.OrdinalIgnoreCase)) continue;
            if (gangHouseProtection is not null)
            {
                var decision = gangHouseProtection.Evaluate(item, nowUtc);
                if (decision.Protected) { exempt++; continue; }
            }
            var definition = _catalog.Require(item.CanonicalItemId);
            if (definition.DeleteAtCleanup)
            {
                _items.RemoveForClassicCleanup(item.InstanceId);
                deleted.Add(item.InstanceId);
                continue;
            }
            if (definition.VisibleAtCleanup)
            {
                exempt++;
                continue;
            }
            if (IsHiddenAtCleanup(item))
            {
                alreadyHidden++;
                continue;
            }
            _items.MarkHiddenAtClassicCleanup(item.InstanceId, nowUtc);
            hidden.Add(item.InstanceId);
        }
        var hiddenCurrencyRooms = _groundDrops.HideRoomCurrencyAtCleanup(nowUtc);
        var recharged = new List<string>();
        foreach (var item in _items.Items.ToArray())
        {
            var definition = _catalog.Require(item.CanonicalItemId);
            if (definition.RechargeAtCleanup <= 0 || definition.MaximumUses <= 0 || item.Charges >= definition.MaximumUses) continue;
            _items.RechargeForClassicCleanup(item.InstanceId, nowUtc);
            recharged.Add(item.InstanceId);
        }
        return new(hidden.Count, alreadyHidden, deleted.Count, hiddenCurrencyRooms, exempt, deleted, hidden,
            recharged.Count, recharged);
    }

    private static bool IsHiddenAtCleanup(RuntimeItemInstance item) =>
        item.Metadata is not null && item.Metadata.TryGetValue("hidden_at_cleanup", out var value) &&
        bool.TryParse(value, out var parsed) && parsed;
}

public sealed partial class RuntimeItemEngine
{
    public RuntimeItemInstance MarkHiddenAtClassicCleanup(string instanceId, DateTimeOffset nowUtc) => Atomic(() =>
    {
        var item = Require(instanceId);
        var metadata = item.Metadata is null
            ? new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase)
            : item.Metadata.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        metadata["hidden_at_cleanup"] = "true";
        metadata["hidden_at_cleanup_utc"] = nowUtc.ToString("O");
        var updated = item with { Metadata = metadata };
        _items[instanceId] = updated;
        return updated;
    });

    public RuntimeItemInstance RemoveForClassicCleanup(string instanceId) => Atomic(() =>
    {
        var item = Require(instanceId);
        // A container cannot survive without its parent location. Maintenance deletion therefore
        // removes the complete portable-container subtree atomically instead of leaving orphaned
        // contents behind in an unreachable Container location.
        foreach (var subtreeInstanceId in CollectSubtreeIdsCore(instanceId))
            _items.Remove(subtreeInstanceId);
        return item;
    });

    public RuntimeItemInstance RechargeForClassicCleanup(string instanceId, DateTimeOffset cleanupEventUtc) => Atomic(() =>
    {
        var item = Require(instanceId);
        var definition = _catalog.Require(item.CanonicalItemId);
        if (definition.RechargeAtCleanup <= 0)
            throw new InvalidOperationException("Item has no decoded Recharge ability 121 relationship.");
        if (definition.MaximumUses <= 0)
            throw new InvalidOperationException("Item has no finite decoded maximum-use capacity for cleanup recharge.");
        var eventKey = cleanupEventUtc.ToString("O");
        if (item.Metadata?.TryGetValue("last_classic_cleanup_recharge_utc", out var previousEvent) == true
            && previousEvent.Equals(eventKey, StringComparison.Ordinal)) return item;
        var metadata = item.Metadata is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : item.Metadata.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        metadata["last_classic_cleanup_recharge_utc"] = eventKey;
        return Replace(item with
        {
            Charges = Math.Min(definition.MaximumUses, checked(item.Charges + definition.RechargeAtCleanup)),
            Metadata = metadata
        });
    });
}
