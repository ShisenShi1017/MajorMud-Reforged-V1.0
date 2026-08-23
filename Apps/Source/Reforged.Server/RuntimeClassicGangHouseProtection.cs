namespace Reforged.Server;

public sealed record RuntimeClassicGangHouseLocation(
    string LocationId,
    string HouseId,
    string LocationKind,
    bool IsGangHouse,
    bool IsVault,
    bool IsGangHouseShop);

public sealed record RuntimeClassicGangHouseProtectionDecision(
    bool Protected,
    string Code,
    string? CharacterId,
    string? HouseId,
    DateTimeOffset EvaluatedAtUtc);

/// <summary>
/// Cleanup protection follows the authoritative persistent gang-house assignment. Successful
/// sundown processing leaves the assignment active; failed upkeep repossesses and clears it.
/// Merely being inside a cataloged gang-house service room is not enough.
/// </summary>
public sealed class RuntimeClassicGangHouseProtectionService
{
    private readonly IReadOnlyDictionary<string, RuntimeClassicGangHouseLocation> _locations;
    private readonly IReadOnlyDictionary<string, RuntimeClassicGangRecord> _ownersByHouse;

    public RuntimeClassicGangHouseProtectionService(string projectRoot, string worldId)
        : this(
            RuntimeClassicGangHouseCatalog.Load(Path.Combine(projectRoot, "Data", worldId, "gang_houses.json")).ToProtectionLocations(),
            new RuntimeClassicGangStateService(projectRoot).List())
    {
    }

    public RuntimeClassicGangHouseProtectionService(
        IEnumerable<RuntimeClassicGangHouseLocation> locations,
        IEnumerable<RuntimeClassicGangRecord> gangs)
    {
        _locations = (locations ?? throw new ArgumentNullException(nameof(locations)))
            .ToDictionary(x => x.LocationId, StringComparer.OrdinalIgnoreCase);
        _ownersByHouse = (gangs ?? throw new ArgumentNullException(nameof(gangs)))
            .Where(x => !x.Disbanded && !string.IsNullOrWhiteSpace(x.GangHouseId))
            .ToDictionary(x => x.GangHouseId!, StringComparer.OrdinalIgnoreCase);
    }

    public RuntimeClassicGangHouseProtectionDecision Evaluate(
        RuntimeItemInstance item,
        DateTimeOffset nowUtc)
    {
        if (!_locations.TryGetValue(item.LocationId, out var location) ||
            !(location.IsGangHouse || location.IsVault || location.IsGangHouseShop))
            return new(false, "not-gang-house-location", null, null, nowUtc);
        if (!_ownersByHouse.TryGetValue(location.HouseId, out var gang))
            return new(false, "gang-house-not-actively-owned", null, location.HouseId, nowUtc);
        var deedHolder = gang.GangHouseDeedHolderCharacterId ?? gang.LeaderCharacterId;
        return new(true, "active-persistent-gang-house-assignment", deedHolder, location.HouseId, nowUtc);
    }
}
