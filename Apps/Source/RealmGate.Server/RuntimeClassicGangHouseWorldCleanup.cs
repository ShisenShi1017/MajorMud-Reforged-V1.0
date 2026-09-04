namespace RealmGate.Server;

public sealed record RuntimeClassicGangHouseWorldCleanupResult(
    string WorldId,
    int ActiveHouseCount,
    int InactiveHouseCount,
    int RemovedGroundItemCount,
    int ClearedCurrencyRoomCount,
    int ReturnedDeedCount,
    int SuppressedDeedCount);

/// <summary>
/// Recovered WCCMMUD mcurou gang-house world-state owner. After house tax/repossessions are resolved,
/// inactive houses lose their room item/currency state and their singleton deed is returned to the
/// Realm Deed Office. Active houses keep their contents and their deed remains suppressed.
/// </summary>
public sealed partial class RuntimeLiveGameplayFacade
{
    internal RuntimeClassicGangHouseWorldCleanupResult ApplyClassicGangHouseDailyCleanup(RuntimeWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        var worldId = world.Definition.Id;
        var houses = RuntimeClassicGangHouseCatalog.Load(Path.Combine(_characters.ProjectRoot, "Data", worldId, "gang_houses.json"));
        var gangs = new RuntimeClassicGangStateService(_characters.ProjectRoot).List();
        var activeHouseIds = gangs
            .Where(gang => !gang.Disbanded && !string.IsNullOrWhiteSpace(gang.GangHouseId))
            .Select(gang => gang.GangHouseId!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var inactive = houses.Records.Where(house => !activeHouseIds.Contains(house.HouseId)).ToArray();
        var living = Living(world);

        var inactiveRoomIds = inactive.SelectMany(house => house.Locations)
            .Where(location => location.IsGangHouse && !string.IsNullOrWhiteSpace(location.RoomId))
            .Select(location => location.RoomId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var removed = living.RemoveGroundItemTreesInRooms(inactiveRoomIds);
        var clearedCurrencyRooms = RuntimeClassicRoomCurrencyStore.ClearRooms(_characters.ProjectRoot, worldId, inactiveRoomIds);

        // Native mcurou removes stray deed copies from room floors before returning unowned deeds to
        // the deed office. Carried/equipped deeds are not touched by this room-owner pass.
        _ = living.RemoveGroundItemTreesByCanonicalIds(houses.Records.Select(house => house.DeedItemId));

        var services = RuntimeServiceCatalog.Load(_characters.ProjectRoot, worldId);
        if (!services.TryGetService("shop.124", out var deedOffice))
            throw new InvalidDataException("Classic Realm Deed Office shop.124 is required for gang-house cleanup.");

        var reconciled = GameplayStore(worldId).ExecuteExactShopStateTransaction(deedOffice, state =>
        {
            var stock = new Dictionary<string, RuntimeShopStockEntry>(state.Stock, StringComparer.OrdinalIgnoreCase);
            var returned = 0;
            var suppressed = 0;
            foreach (var house in houses.Records)
            {
                var quantity = activeHouseIds.Contains(house.HouseId) ? 0 : 1;
                if (quantity == 0) suppressed++; else returned++;
                stock[house.DeedItemId] = new RuntimeShopStockEntry(house.DeedItemId, quantity, DateTimeOffset.MaxValue);
            }
            var next = state with { Stock = stock, UpdatedAtUtc = DateTimeOffset.UtcNow };
            return (next, (returned, suppressed));
        });

        return new(worldId, activeHouseIds.Count, inactive.Length, removed, clearedCurrencyRooms,
            reconciled.returned, reconciled.suppressed);
    }
}

public sealed partial class RuntimeLivingWorldService
{
    public int RemoveAllGroundItemTreesInRoom(string roomId) => RemoveGroundItemTreesInRooms([roomId]);

    public int RemoveGroundItemTreesInRooms(IEnumerable<string> roomIds)
    {
        ArgumentNullException.ThrowIfNull(roomIds);
        lock (_gate)
        {
            var rooms = roomIds.Where(room => !string.IsNullOrWhiteSpace(room))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (rooms.Count == 0) return 0;
            var roots = _items.Values
                .Where(item => item.LocationKind.Equals(RuntimeItemLocations.Room, StringComparison.OrdinalIgnoreCase)
                    && rooms.Contains(item.LocationId))
                .Select(item => item.InstanceId)
                .ToArray();
            return RemoveGroundItemTreesLocked(roots);
        }
    }

    public int RemoveGroundItemTreesByCanonicalIds(IEnumerable<string> canonicalItemIds)
    {
        ArgumentNullException.ThrowIfNull(canonicalItemIds);
        lock (_gate)
        {
            var ids = canonicalItemIds.Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (ids.Count == 0) return 0;
            var roots = _items.Values
                .Where(item => item.LocationKind.Equals(RuntimeItemLocations.Room, StringComparison.OrdinalIgnoreCase)
                    && ids.Contains(item.CanonicalItemId))
                .Select(item => item.InstanceId)
                .ToArray();
            return RemoveGroundItemTreesLocked(roots);
        }
    }

    public bool RemoveSummonedNpc(string instanceId)
    {
        lock (_gate)
        {
            if (!_npcs.Remove(instanceId)) return false;
            _npcAi.Remove(instanceId);
            Save();
            return true;
        }
    }

    private int RemoveGroundItemTreesLocked(IEnumerable<string> rootIds)
    {
        var children = _items.Values
            .Where(item => item.LocationKind.Equals(RuntimeItemLocations.Container, StringComparison.OrdinalIgnoreCase))
            .GroupBy(item => item.LocationId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Select(item => item.InstanceId).ToArray(), StringComparer.OrdinalIgnoreCase);
        var pending = new Stack<string>(rootIds.Distinct(StringComparer.OrdinalIgnoreCase));
        var removedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (pending.Count > 0)
        {
            var id = pending.Pop();
            if (!removedIds.Add(id)) continue;
            if (children.TryGetValue(id, out var descendants))
                foreach (var descendant in descendants) pending.Push(descendant);
        }
        foreach (var id in removedIds) _items.Remove(id);
        if (removedIds.Count > 0) Save();
        return removedIds.Count;
    }
}
