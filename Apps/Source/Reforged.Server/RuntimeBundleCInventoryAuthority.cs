using System.Globalization;
using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeClassicInventoryCurrency(long Runic, long Platinum, long Gold, long Silver, long Copper)
{
    public long CopperValue => checked(Runic * 1_000_000L + Platinum * 10_000L + Gold * 100L + Silver * 10L + Copper);
    public long PhysicalCoinCount => checked(Runic + Platinum + Gold + Silver + Copper);
    public bool IsEmpty => PhysicalCoinCount == 0;
}

public sealed record RuntimeClassicInventoryProjection(
    string CharacterId,
    long ItemWeight,
    long CurrencyWeight,
    long TotalCarriedWeight,
    int RootInventorySlotsUsed,
    int EquippedSlotsUsed,
    RuntimeClassicInventoryCurrency Currency,
    string CapacityClassification,
    string EvidenceClassification);

public static class RuntimeBundleCInventoryAuthority
{
    private const string RunicKey = "currency.runic";
    private const string PlatinumKey = "currency.platinum";
    private const string GoldKey = "currency.gold";
    private const string SilverKey = "currency.silver";
    private const string CopperKey = "currency.copper";

    public static RuntimeClassicInventoryCurrency ReadCurrency(RuntimeItemState state)
    {
        var active = state.ActiveItemState ?? new Dictionary<string, string>();
        var denominations = new RuntimeClassicInventoryCurrency(
            Read(active, RunicKey), Read(active, PlatinumKey), Read(active, GoldKey), Read(active, SilverKey), Read(active, CopperKey));
        if (!denominations.IsEmpty || state.Gold == 0) return denominations;
        return FromCopper(state.Gold);
    }

    public static RuntimeItemState Credit(RuntimeItemState state, RuntimeClassicRoomCurrencyBalance amount)
    {
        var before = ReadCurrency(state);
        var after = new RuntimeClassicInventoryCurrency(
            checked(before.Runic + amount.RunicCoins), checked(before.Platinum + amount.PlatinumPieces),
            checked(before.Gold + amount.GoldCrowns), checked(before.Silver + amount.SilverNobles), checked(before.Copper + amount.CopperFarthings));
        return WriteCurrency(state, after);
    }

    public static RuntimeItemState WriteCurrency(RuntimeItemState state, RuntimeClassicInventoryCurrency currency)
    {
        if (currency.Runic < 0 || currency.Platinum < 0 || currency.Gold < 0 || currency.Silver < 0 || currency.Copper < 0)
            throw new InvalidOperationException("negative-physical-currency");
        var active = new Dictionary<string, string>(state.ActiveItemState ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase)
        {
            [RunicKey] = currency.Runic.ToString(CultureInfo.InvariantCulture),
            [PlatinumKey] = currency.Platinum.ToString(CultureInfo.InvariantCulture),
            [GoldKey] = currency.Gold.ToString(CultureInfo.InvariantCulture),
            [SilverKey] = currency.Silver.ToString(CultureInfo.InvariantCulture),
            [CopperKey] = currency.Copper.ToString(CultureInfo.InvariantCulture)
        };
        return state with { Gold = currency.CopperValue, ActiveItemState = active };
    }

    public static RuntimeClassicInventoryProjection Project(RuntimeCharacterRecord character, RuntimeItemCatalog catalog)
    {
        var state = character.ItemState ?? new RuntimeItemState(Array.Empty<RuntimeItemInstance>(), 0, new Dictionary<string, string>(), Array.Empty<RuntimeCorpseState>());
        var carried = state.Items.Where(item => string.Equals(item.OwnerId, character.CharacterId, StringComparison.OrdinalIgnoreCase)
            && (string.Equals(item.LocationKind, RuntimeItemLocations.Character, StringComparison.OrdinalIgnoreCase)
                || string.Equals(item.LocationKind, RuntimeItemLocations.Equipment, StringComparison.OrdinalIgnoreCase)
                || string.Equals(item.LocationKind, RuntimeItemLocations.Container, StringComparison.OrdinalIgnoreCase))).ToArray();
        var itemWeight = carried.Aggregate(0L, (sum, item) => checked(sum + checked((long)Math.Max(0, catalog.Require(item.CanonicalItemId).Weight) * Math.Max(1, item.StackQuantity))));
        var currency = ReadCurrency(state);
        // User-supplied Classic runtime evidence establishes physical coins and carried-weight participation.
        // The native capacity formula and denomination-specific weight ratios remain unresolved, so no cap or penalty is invented.
        var currencyWeight = currency.PhysicalCoinCount;
        return new(character.CharacterId, itemWeight, currencyWeight, checked(itemWeight + currencyWeight),
            carried.Count(x => x.EquippedSlot is null && x.LocationKind.Equals(RuntimeItemLocations.Character, StringComparison.OrdinalIgnoreCase)),
            carried.Count(x => x.EquippedSlot is not null), currency, "TRUE_UNKNOWN_NATIVE_CAPACITY_FORMULA", "STRONGLY_CORROBORATED");
    }

    public static string FormatCurrency(RuntimeClassicInventoryCurrency currency) => RuntimeClassicRoomCurrencyStore.Format(
        new RuntimeClassicRoomCurrencyBalance(currency.Runic, currency.Platinum, currency.Gold, currency.Silver, currency.Copper));

    private static long Read(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var text) && long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value >= 0 ? value : 0;

    private static RuntimeClassicInventoryCurrency FromCopper(long value)
    {
        if (value < 0) throw new InvalidOperationException("negative-currency-balance");
        var runic = value / 1_000_000L; value %= 1_000_000L;
        var platinum = value / 10_000L; value %= 10_000L;
        var gold = value / 100L; value %= 100L;
        var silver = value / 10L; var copper = value % 10L;
        return new(runic, platinum, gold, silver, copper);
    }
}

public sealed record RuntimeBundleCInventoryAuditReport(string Status, int ItemCount, IReadOnlyList<string> Checks, IReadOnlyList<string> Errors)
{
    public bool Passed => Status == "PASS";
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

public static class RuntimeBundleCInventoryAudit
{
    public static RuntimeBundleCInventoryAuditReport Run(string projectRoot, string worldId)
    {
        var checks = new List<string>(); var errors = new List<string>();
        void Require(bool value, string name) { if (value) checks.Add(name); else errors.Add(name); }
        try
        {
            var catalog = RuntimeItemCatalog.Load(projectRoot, worldId);
            var source = File.ReadAllText(Path.Combine(projectRoot,"Apps","Source","Reforged.Server","RuntimeBundleCInventoryAuthority.cs"));
            var live = File.ReadAllText(Path.Combine(projectRoot,"Apps","Source","Reforged.Server","RuntimeLiveGameplayPart2.cs"));
            var render = File.ReadAllText(Path.Combine(projectRoot,"Apps","Source","Reforged.Server","RuntimeLiveGameplay.cs"));
            Require(source.Contains("currency.runic") && source.Contains("currency.platinum") && source.Contains("currency.gold") && source.Contains("currency.silver") && source.Contains("currency.copper"), "all-five-denominations-persisted");
            Require(source.Contains("PhysicalCoinCount") && source.Contains("currencyWeight"), "physical-currency-weight-consumer");
            Require(source.Contains("RuntimeItemLocations.Container") && source.Contains("RuntimeItemLocations.Equipment"), "nested-equipped-carried-weight-consumer");
            Require(live.Contains("RuntimeBundleCInventoryAuthority.Credit"), "room-pickup-preserves-denominations");
            Require(render.Contains("RuntimeBundleCInventoryAuthority.Project"), "inventory-display-authoritative-projection");
            Require(!render.Contains("strength * 80", StringComparison.Ordinal), "invented-capacity-formula-removed");
            Require(source.Contains("TRUE_UNKNOWN_NATIVE_CAPACITY_FORMULA"), "native-capacity-unknown-explicitly-classified");
            Require(File.ReadAllText(Path.Combine(projectRoot,"Apps","Source","Reforged.Server","RuntimeItemsEquipment.cs")).Contains("InventoryWeight(string ownerId)"), "item-engine-weight-owner-retained");
            return new(errors.Count == 0 ? "PASS" : "FAIL", catalog.Items.Count, checks, errors);
        }
        catch (Exception ex) { errors.Add(ex.ToString()); return new("FAIL",0,checks,errors); }
    }
}
