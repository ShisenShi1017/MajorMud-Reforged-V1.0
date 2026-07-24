using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeLootItemGrant(string ItemId, long Quantity);

public sealed record RuntimeLootWeightedEntry(
    string EntryId,
    long Weight,
    long CurrencyBaseUnits,
    IReadOnlyList<RuntimeLootItemGrant> Items);

public sealed record RuntimeLootTableDefinition(
    string LootTableId,
    string CurrencyId,
    long GuaranteedCurrencyBaseUnits,
    IReadOnlyList<RuntimeLootItemGrant> GuaranteedItems,
    IReadOnlyList<RuntimeLootWeightedEntry> WeightedEntries,
    int WeightedRollCount,
    bool RollsWithReplacement);

public sealed record RuntimeLootResolution(
    string LootTableId,
    ulong Seed,
    long CurrencyBaseUnits,
    IReadOnlyDictionary<string, long> ItemQuantities,
    IReadOnlyList<string> SelectedEntryIds);

public sealed record RuntimeLootCorpseSnapshot(
    string CorpseId,
    string SourceEntityId,
    string CurrencyId,
    IReadOnlyList<string> OwnerIds,
    bool PublicLoot,
    long CurrencyBaseUnits,
    IReadOnlyDictionary<string, long> ItemQuantities,
    DateTimeOffset CreatedUtc,
    DateTimeOffset ExpiresUtc,
    long Version,
    IReadOnlyList<string> AppliedClaimIds,
    bool CleanedUp);

public sealed record RuntimeLootClaimResult(
    string ClaimId,
    string CorpseId,
    string ClaimerId,
    long CurrencyBaseUnits,
    IReadOnlyDictionary<string, long> ItemQuantities,
    long CorpseVersion,
    long WalletVersion,
    long InventoryVersion,
    bool Replayed);

public static class RuntimeLootResolver
{
    public static RuntimeLootResolution Resolve(RuntimeLootTableDefinition table, ulong seed)
    {
        Validate(table);
        var currency = table.GuaranteedCurrencyBaseUnits;
        var items = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var grant in table.GuaranteedItems) Add(items, grant);
        var selected = new List<string>();
        var available = table.WeightedEntries.ToList();
        var random = new DeterministicRandom(seed);
        for (var roll = 0; roll < table.WeightedRollCount; roll++)
        {
            if (available.Count == 0) break;
            var totalWeight = available.Aggregate<RuntimeLootWeightedEntry, long>(0, (sum, entry) => checked(sum + entry.Weight));
            if (totalWeight <= 0) throw new InvalidOperationException("loot-weight-total-invalid");
            var target = random.NextInt64(totalWeight);
            long cursor = 0;
            RuntimeLootWeightedEntry? chosen = null;
            foreach (var entry in available)
            {
                cursor = checked(cursor + entry.Weight);
                if (target < cursor) { chosen = entry; break; }
            }
            chosen ??= available[^1];
            selected.Add(chosen.EntryId);
            currency = checked(currency + chosen.CurrencyBaseUnits);
            foreach (var grant in chosen.Items) Add(items, grant);
            if (!table.RollsWithReplacement) available.Remove(chosen);
        }
        return new(table.LootTableId, seed, currency, items, selected);
    }

    private static void Add(Dictionary<string, long> items, RuntimeLootItemGrant grant)
    {
        if (string.IsNullOrWhiteSpace(grant.ItemId) || grant.Quantity <= 0) throw new InvalidOperationException("loot-item-grant-invalid");
        items[grant.ItemId] = checked((items.TryGetValue(grant.ItemId, out var current) ? current : 0) + grant.Quantity);
    }

    private static void Validate(RuntimeLootTableDefinition table)
    {
        if (string.IsNullOrWhiteSpace(table.LootTableId) || string.IsNullOrWhiteSpace(table.CurrencyId)) throw new InvalidOperationException("loot-table-identity-required");
        if (table.GuaranteedCurrencyBaseUnits < 0 || table.WeightedRollCount < 0) throw new InvalidOperationException("loot-table-value-invalid");
        if (table.WeightedEntries.Select(x => x.EntryId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != table.WeightedEntries.Count) throw new InvalidOperationException("duplicate-loot-entry-id");
        foreach (var entry in table.WeightedEntries)
        {
            if (string.IsNullOrWhiteSpace(entry.EntryId) || entry.Weight <= 0 || entry.CurrencyBaseUnits < 0) throw new InvalidOperationException("loot-weighted-entry-invalid");
            foreach (var grant in entry.Items) if (grant.Quantity <= 0 || string.IsNullOrWhiteSpace(grant.ItemId)) throw new InvalidOperationException("loot-item-grant-invalid");
        }
    }

    private sealed class DeterministicRandom
    {
        private ulong _state;
        public DeterministicRandom(ulong seed) => _state = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;
        private ulong NextUInt64()
        {
            var x = _state;
            x ^= x >> 12; x ^= x << 25; x ^= x >> 27;
            _state = x;
            return x * 2685821657736338717UL;
        }
        public long NextInt64(long exclusiveMaximum)
        {
            if (exclusiveMaximum <= 0) throw new ArgumentOutOfRangeException(nameof(exclusiveMaximum));
            return (long)(NextUInt64() % (ulong)exclusiveMaximum);
        }
    }
}

public sealed class RuntimeLootCorpse
{
    private readonly object _gate = new();
    private readonly HashSet<string> _ownerIds;
    private readonly HashSet<string> _appliedClaimIds;
    private Dictionary<string, long> _itemQuantities;
    private long _currencyBaseUnits;
    private long _version;
    private bool _cleanedUp;

    public RuntimeLootCorpse(string corpseId, string sourceEntityId, string currencyId, IEnumerable<string> ownerIds, bool publicLoot, RuntimeLootResolution resolution, DateTimeOffset createdUtc, DateTimeOffset expiresUtc)
    {
        if (string.IsNullOrWhiteSpace(corpseId) || string.IsNullOrWhiteSpace(sourceEntityId) || string.IsNullOrWhiteSpace(currencyId)) throw new InvalidOperationException("loot-corpse-identity-required");
        if (expiresUtc < createdUtc) throw new InvalidOperationException("loot-corpse-expiry-invalid");
        CorpseId = corpseId; SourceEntityId = sourceEntityId; CurrencyId = currencyId; PublicLoot = publicLoot; CreatedUtc = createdUtc; ExpiresUtc = expiresUtc;
        _ownerIds = ownerIds.Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!publicLoot && _ownerIds.Count == 0) throw new InvalidOperationException("loot-owner-required");
        if (!resolution.LootTableId.Equals(resolution.LootTableId, StringComparison.OrdinalIgnoreCase) || resolution.CurrencyBaseUnits < 0) throw new InvalidOperationException("loot-resolution-invalid");
        _currencyBaseUnits = resolution.CurrencyBaseUnits;
        _itemQuantities = new Dictionary<string, long>(resolution.ItemQuantities, StringComparer.OrdinalIgnoreCase);
        if (_itemQuantities.Any(x => string.IsNullOrWhiteSpace(x.Key) || x.Value <= 0)) throw new InvalidOperationException("loot-resolution-item-invalid");
        _appliedClaimIds = new(StringComparer.OrdinalIgnoreCase);
    }

    private RuntimeLootCorpse(RuntimeLootCorpseSnapshot snapshot)
    {
        CorpseId = snapshot.CorpseId; SourceEntityId = snapshot.SourceEntityId; CurrencyId = snapshot.CurrencyId; PublicLoot = snapshot.PublicLoot; CreatedUtc = snapshot.CreatedUtc; ExpiresUtc = snapshot.ExpiresUtc;
        _ownerIds = snapshot.OwnerIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        _appliedClaimIds = snapshot.AppliedClaimIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        _itemQuantities = new(snapshot.ItemQuantities, StringComparer.OrdinalIgnoreCase);
        _currencyBaseUnits = snapshot.CurrencyBaseUnits; _version = snapshot.Version; _cleanedUp = snapshot.CleanedUp;
    }

    public string CorpseId { get; }
    public string SourceEntityId { get; }
    public string CurrencyId { get; }
    public bool PublicLoot { get; }
    public DateTimeOffset CreatedUtc { get; }
    public DateTimeOffset ExpiresUtc { get; }
    internal object Gate => _gate;

    public RuntimeLootCorpseSnapshot Capture() { lock (_gate) return CaptureUnsafe(); }
    public static RuntimeLootCorpse Restore(RuntimeLootCorpseSnapshot snapshot) { ValidateSnapshot(snapshot); return new(snapshot); }

    public bool IsExpired(DateTimeOffset nowUtc) { lock (_gate) return nowUtc >= ExpiresUtc; }
    public bool IsEmpty() { lock (_gate) return _currencyBaseUnits == 0 && _itemQuantities.Count == 0; }

    public void MakePublic(DateTimeOffset nowUtc)
    {
        lock (_gate)
        {
            EnsureActiveUnsafe(nowUtc);
            if (PublicLoot) return;
            throw new InvalidOperationException("loot-public-state-immutable");
        }
    }

    public void Cleanup(DateTimeOffset nowUtc)
    {
        lock (_gate)
        {
            if (_cleanedUp) return;
            if (nowUtc < ExpiresUtc && (_currencyBaseUnits != 0 || _itemQuantities.Count != 0)) throw new InvalidOperationException("loot-cleanup-not-due");
            _currencyBaseUnits = 0; _itemQuantities.Clear(); _cleanedUp = true; _version = checked(_version + 1);
        }
    }

    internal RuntimeLootCorpseSnapshot CaptureUnsafe() => new(CorpseId, SourceEntityId, CurrencyId, _ownerIds.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(), PublicLoot, _currencyBaseUnits, new Dictionary<string, long>(_itemQuantities, StringComparer.OrdinalIgnoreCase), CreatedUtc, ExpiresUtc, _version, _appliedClaimIds.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(), _cleanedUp);
    internal void RestoreUnsafe(RuntimeLootCorpseSnapshot snapshot) { _currencyBaseUnits = snapshot.CurrencyBaseUnits; _itemQuantities = new(snapshot.ItemQuantities, StringComparer.OrdinalIgnoreCase); _version = snapshot.Version; _cleanedUp = snapshot.CleanedUp; _appliedClaimIds.Clear(); foreach (var id in snapshot.AppliedClaimIds) _appliedClaimIds.Add(id); }
    internal void EnsureClaimAccessUnsafe(string claimerId, DateTimeOffset nowUtc) { EnsureActiveUnsafe(nowUtc); if (!PublicLoot && !_ownerIds.Contains(claimerId)) throw new UnauthorizedAccessException("loot-access-denied"); }
    internal bool IsAppliedUnsafe(string claimId) => _appliedClaimIds.Contains(claimId);
    internal void MarkAppliedUnsafe(string claimId) => _appliedClaimIds.Add(claimId);
    internal long CurrencyUnsafe => _currencyBaseUnits;
    internal IReadOnlyDictionary<string, long> ItemsUnsafe => _itemQuantities;
    internal long VersionUnsafe => _version;
    internal void ClearUnsafe() { _currencyBaseUnits = 0; _itemQuantities.Clear(); _version = checked(_version + 1); }
    private void EnsureActiveUnsafe(DateTimeOffset nowUtc) { if (_cleanedUp) throw new InvalidOperationException("loot-corpse-cleaned-up"); if (nowUtc >= ExpiresUtc) throw new InvalidOperationException("loot-corpse-expired"); }

    private static void ValidateSnapshot(RuntimeLootCorpseSnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(snapshot.CorpseId) || string.IsNullOrWhiteSpace(snapshot.SourceEntityId) || string.IsNullOrWhiteSpace(snapshot.CurrencyId)) throw new InvalidOperationException("loot-snapshot-identity-required");
        if (!snapshot.PublicLoot && snapshot.OwnerIds.Count == 0) throw new InvalidOperationException("loot-snapshot-owner-required");
        if (snapshot.CurrencyBaseUnits < 0 || snapshot.Version < 0 || snapshot.ExpiresUtc < snapshot.CreatedUtc) throw new InvalidOperationException("loot-snapshot-value-invalid");
        if (snapshot.ItemQuantities.Any(x => string.IsNullOrWhiteSpace(x.Key) || x.Value <= 0)) throw new InvalidOperationException("loot-snapshot-item-invalid");
    }
}

public static class RuntimeLootClaimEngine
{
    public static RuntimeLootClaimResult ClaimAll(string claimId, string claimerId, RuntimeLootCorpse corpse, RuntimeCurrencyWallet wallet, RuntimeEconomyInventory inventory, DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(claimId) || string.IsNullOrWhiteSpace(claimerId)) throw new InvalidOperationException("loot-claim-identity-required");
        if (!corpse.CurrencyId.Equals(wallet.CurrencyId, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("loot-currency-mismatch");
        var locks = new[] { (corpse.CorpseId, corpse.Gate), (wallet.WalletId, wallet.Gate), (inventory.InventoryId, inventory.Gate) }.OrderBy(x => x.Item1, StringComparer.OrdinalIgnoreCase).ToArray();
        lock (locks[0].Item2) lock (locks[1].Item2) lock (locks[2].Item2)
        {
            corpse.EnsureClaimAccessUnsafe(claimerId, nowUtc);
            if (corpse.IsAppliedUnsafe(claimId))
            {
                var existing = corpse.CaptureUnsafe();
                return new(claimId, corpse.CorpseId, claimerId, 0, new Dictionary<string, long>(), existing.Version, wallet.CaptureUnsafe().Version, inventory.CaptureUnsafe().Version, true);
            }
            var corpseBefore = corpse.CaptureUnsafe(); var walletBefore = wallet.CaptureUnsafe(); var inventoryBefore = inventory.CaptureUnsafe();
            try
            {
                foreach (var item in corpse.ItemsUnsafe) inventory.AddUnsafe(item.Key, item.Value);
                wallet.SetTotalUnsafe(checked(walletBefore.TotalBaseUnits + corpse.CurrencyUnsafe));
                var grantedCurrency = corpse.CurrencyUnsafe;
                var grantedItems = new Dictionary<string, long>(corpse.ItemsUnsafe, StringComparer.OrdinalIgnoreCase);
                corpse.ClearUnsafe(); corpse.MarkAppliedUnsafe(claimId);
                return new(claimId, corpse.CorpseId, claimerId, grantedCurrency, grantedItems, corpse.VersionUnsafe, wallet.CaptureUnsafe().Version, inventory.CaptureUnsafe().Version, false);
            }
            catch
            {
                corpse.RestoreUnsafe(corpseBefore); wallet.RestoreUnsafe(walletBefore); inventory.RestoreUnsafe(inventoryBefore); throw;
            }
        }
    }
}

public sealed record RuntimeLootStoreSnapshot(int SchemaVersion, IReadOnlyList<RuntimeLootCorpseSnapshot> Corpses);

public static class RuntimeLootPersistence
{
    public const int CurrentSchemaVersion = 1;
    public static string Serialize(RuntimeLootStoreSnapshot snapshot) => JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
    public static RuntimeLootStoreSnapshot Deserialize(string json)
    {
        var snapshot = JsonSerializer.Deserialize<RuntimeLootStoreSnapshot>(json) ?? throw new InvalidOperationException("loot-store-snapshot-invalid");
        var errors = Validate(snapshot); if (errors.Count != 0) throw new InvalidOperationException(string.Join(';', errors)); return snapshot;
    }
    public static IReadOnlyList<string> Validate(RuntimeLootStoreSnapshot snapshot)
    {
        var errors = new List<string>();
        if (snapshot.SchemaVersion != CurrentSchemaVersion) errors.Add("loot-schema-version-unsupported");
        if (snapshot.Corpses.Select(x => x.CorpseId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != snapshot.Corpses.Count) errors.Add("duplicate-corpse-id");
        foreach (var corpse in snapshot.Corpses)
        {
            try { RuntimeLootCorpse.Restore(corpse); } catch (Exception ex) { errors.Add($"corpse:{corpse.CorpseId}:{ex.Message}"); }
        }
        return errors;
    }
}
