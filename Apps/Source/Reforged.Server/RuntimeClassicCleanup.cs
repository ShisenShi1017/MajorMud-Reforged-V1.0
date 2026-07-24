namespace Reforged.Server;

public sealed record RuntimeClassicCleanupResult(
    int HiddenRoomItems,
    int AlreadyHiddenRoomItems,
    int DeletedRoomItems,
    int HiddenCurrencyRooms,
    int ExemptItems,
    IReadOnlyList<string> DeletedInstanceIds,
    IReadOnlyList<string> HiddenInstanceIds);

/// <summary>
/// Classic daily cleanup boundary. Room-ground objects are hidden unless the decoded item
/// definition carries Del@Maint (ability 119), in which case they are deleted whether hidden or
/// visible. Character inventories/equipment, gang houses and gang-house shops are exempt.
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

    public RuntimeClassicCleanupResult Run(DateTimeOffset nowUtc) => CleanupRoomGround(nowUtc, null, null);

    public RuntimeClassicCleanupResult Run(
        DateTimeOffset nowUtc,
        RuntimeClassicGangHouseProtectionService? gangHouseProtection,
        Func<RuntimeItemInstance, string?>? owningCharacterResolver) =>
        CleanupRoomGround(nowUtc, gangHouseProtection, owningCharacterResolver);

    /// <summary>
    /// Authoritative Classic room-ground cleanup consumer. The caller owns scheduling because no
    /// decoded universal cleanup interval has been verified. Replaying the operation is safe:
    /// already-hidden objects stay hidden and are reported separately.
    /// </summary>
    public RuntimeClassicCleanupResult CleanupRoomGround(
        DateTimeOffset nowUtc,
        RuntimeClassicGangHouseProtectionService? gangHouseProtection,
        Func<RuntimeItemInstance, string?>? owningCharacterResolver)
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
            if (gangHouseProtection is not null && owningCharacterResolver is not null)
            {
                var decision = gangHouseProtection.Evaluate(item, owningCharacterResolver(item), nowUtc);
                if (decision.Protected) { exempt++; continue; }
            }
            var definition = _catalog.Require(item.CanonicalItemId);
            if (definition.DeleteAtCleanup)
            {
                _items.RemoveForClassicCleanup(item.InstanceId);
                deleted.Add(item.InstanceId);
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
        return new(hidden.Count, alreadyHidden, deleted.Count, hiddenCurrencyRooms, exempt, deleted, hidden);
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
        _items.Remove(instanceId);
        return item;
    });
}
