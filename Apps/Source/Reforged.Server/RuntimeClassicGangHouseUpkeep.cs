namespace Reforged.Server;

public sealed record RuntimeClassicGangHouseUpkeepResult(
    string GangId,
    string HouseId,
    string DeedHolderCharacterId,
    long TaxGold,
    string Outcome,
    string SundownCycleId);

public sealed class RuntimeClassicGangHouseUpkeepService
{
    private readonly RuntimeCharacterService _characters;
    private readonly RuntimeClassicGangStateService _gangs;
    private readonly IReadOnlyDictionary<string, long> _taxByHouse;

    public RuntimeClassicGangHouseUpkeepService(RuntimeCharacterService characters, string worldId)
    {
        _characters = characters;
        _gangs = new RuntimeClassicGangStateService(characters.ProjectRoot);
        var houses = RuntimeClassicGangHouseCatalog.Load(Path.Combine(characters.ProjectRoot, "Data", worldId, "gang_houses.json"));
        var items = RuntimeItemCatalog.Load(characters.ProjectRoot, worldId);
        _taxByHouse = RuntimeClassicGangHouseAbilityAuthority.Resolve(items, houses)
            .Where(value => value.IsDeed)
            .ToDictionary(value => value.HouseId, value => checked((long)value.TaxValue), StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<RuntimeClassicGangHouseUpkeepResult> ProcessSundown(
        string sundownCycleId,
        DateTimeOffset sundownUtc)
    {
        if (string.IsNullOrWhiteSpace(sundownCycleId)) throw new ArgumentException("A stable sundown cycle ID is required.", nameof(sundownCycleId));
        var results = new List<RuntimeClassicGangHouseUpkeepResult>();
        foreach (var gang in _gangs.List().Where(value => !value.Disbanded && !string.IsNullOrWhiteSpace(value.GangHouseId)))
        {
            if (string.Equals(gang.LastGangHouseSundownCycleId, sundownCycleId, StringComparison.OrdinalIgnoreCase)) continue;
            var houseId = gang.GangHouseId!;
            if (!_taxByHouse.TryGetValue(houseId, out var taxGold) || taxGold <= 0)
                throw new InvalidDataException($"No decoded positive GHouseTax exists for {houseId}.");
            var holderId = gang.GangHouseDeedHolderCharacterId ?? gang.LeaderCharacterId;
            var holder = _characters.LoadCharacter(holderId)
                ?? throw new InvalidDataException($"Gang-house deed holder '{holderId}' has no persistent character.");
            var state = holder.ItemState ?? new RuntimeItemState([], 0, new Dictionary<string, string>(), []);
            RuntimeItemState debited;
            try
            {
                debited = RuntimeBundleCEconomyAuthority.DebitBankGold(state, taxGold);
            }
            catch (InvalidOperationException)
            {
                _gangs.RepossessHouse(gang, sundownCycleId);
                results.Add(new(gang.GangId, houseId, holderId, taxGold, "repossessed-insufficient-silvermere-bank-gold", sundownCycleId));
                continue;
            }

            var updated = holder with { ItemState = debited, UpdatedAtUtc = sundownUtc };
            _characters.SaveCharacter(updated, $"gang-house-sundown-tax:{sundownCycleId}:{houseId}");
            try
            {
                _gangs.RecordHouseUpkeep(gang, sundownCycleId, sundownUtc);
            }
            catch
            {
                _characters.SaveCharacter(holder, $"gang-house-sundown-tax-rollback:{sundownCycleId}:{houseId}");
                throw;
            }
            results.Add(new(gang.GangId, houseId, holderId, taxGold, "paid-from-silvermere-bank-gold", sundownCycleId));
        }
        return results;
    }
}
