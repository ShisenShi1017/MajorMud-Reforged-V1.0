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

    public IReadOnlyList<RuntimeClassicGangHouseUpkeepResult> ProcessDailyAutoCleanup(
        string cleanupCycleId,
        DateTimeOffset cleanupUtc)
    {
        if (string.IsNullOrWhiteSpace(cleanupCycleId)) throw new ArgumentException("A stable mcurou cleanup cycle ID is required.", nameof(cleanupCycleId));
        var results = new List<RuntimeClassicGangHouseUpkeepResult>();
        foreach (var gang in _gangs.List().Where(value => !value.Disbanded && !string.IsNullOrWhiteSpace(value.GangHouseId)))
        {
            if (string.Equals(gang.LastGangHouseSundownCycleId, cleanupCycleId, StringComparison.OrdinalIgnoreCase)) continue;
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
                _gangs.RepossessHouse(gang, cleanupCycleId);
                results.Add(new(gang.GangId, houseId, holderId, taxGold, "repossessed-insufficient-silvermere-bank-gold", cleanupCycleId));
                continue;
            }

            var updated = holder with { ItemState = debited, UpdatedAtUtc = cleanupUtc };
            _characters.SaveCharacter(updated, $"gang-house-auto-cleanup-tax:{cleanupCycleId}:{houseId}");
            try
            {
                _gangs.RecordHouseUpkeep(gang, cleanupCycleId, cleanupUtc);
            }
            catch
            {
                _characters.SaveCharacter(holder, $"gang-house-auto-cleanup-tax-rollback:{cleanupCycleId}:{houseId}");
                throw;
            }
            results.Add(new(gang.GangId, houseId, holderId, taxGold, "paid-from-silvermere-bank-gold", cleanupCycleId));
        }
        return results;
    }
}
