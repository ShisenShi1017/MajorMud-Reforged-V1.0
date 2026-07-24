namespace Reforged.Server;

public sealed record RuntimeClassicDeedState(
    string CharacterId,
    string DeedItemId,
    string HouseId,
    DateTimeOffset UpkeepPaidThroughUtc,
    bool Revoked = false);

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
/// Cleanup protection is granted only when the owning character has a matching active deed and
/// upkeep remains paid. Merely being inside a room marked as a gang house is not enough.
/// </summary>
public sealed class RuntimeClassicGangHouseProtectionService
{
    private readonly IReadOnlyDictionary<string, RuntimeClassicGangHouseLocation> _locations;
    private readonly IReadOnlyList<RuntimeClassicDeedState> _deeds;

    public RuntimeClassicGangHouseProtectionService(
        IEnumerable<RuntimeClassicGangHouseLocation> locations,
        IEnumerable<RuntimeClassicDeedState> deeds)
    {
        _locations = (locations ?? throw new ArgumentNullException(nameof(locations)))
            .ToDictionary(x => x.LocationId, StringComparer.OrdinalIgnoreCase);
        _deeds = (deeds ?? throw new ArgumentNullException(nameof(deeds))).ToArray();
    }

    public RuntimeClassicGangHouseProtectionDecision Evaluate(
        RuntimeItemInstance item,
        string? owningCharacterId,
        DateTimeOffset nowUtc)
    {
        if (!_locations.TryGetValue(item.LocationId, out var location) ||
            !(location.IsGangHouse || location.IsVault || location.IsGangHouseShop))
            return new(false, "not-gang-house-location", owningCharacterId, null, nowUtc);
        if (string.IsNullOrWhiteSpace(owningCharacterId))
            return new(false, "missing-owning-character", null, location.HouseId, nowUtc);

        var deed = _deeds.FirstOrDefault(x =>
            x.CharacterId.Equals(owningCharacterId, StringComparison.OrdinalIgnoreCase) &&
            x.HouseId.Equals(location.HouseId, StringComparison.OrdinalIgnoreCase) && !x.Revoked);
        if (deed is null) return new(false, "matching-deed-not-owned", owningCharacterId, location.HouseId, nowUtc);
        if (deed.UpkeepPaidThroughUtc < nowUtc) return new(false, "gang-house-upkeep-expired", owningCharacterId, location.HouseId, nowUtc);
        return new(true, "matching-deed-and-upkeep-current", owningCharacterId, location.HouseId, nowUtc);
    }
}
