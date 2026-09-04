using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeBundleCShopStockDefinition(
    string ItemId,
    int NormalQuantity,
    int MaximumQuantity,
    int RegenPercent,
    int RegenQuantity,
    int RegenTime,
    long Price,
    string? CurrencyType,
    bool GangStocked);

public sealed record RuntimeBundleCShopDefinition(
    string Id,
    string Name,
    string RoomId,
    string ServiceKind,
    int ClassLimit,
    int MinimumLevel,
    int MaximumLevel,
    int MarkupPercent,
    IReadOnlyList<RuntimeBundleCShopStockDefinition> Inventory);

public sealed record RuntimeBundleCBankDefinition(
    string Id,
    string Name,
    int RoomNumber,
    string ServiceKind,
    int ClassLimit,
    int MinimumLevel,
    int MaximumLevel);

public sealed record RuntimeBundleCEconomyRule(
    string Id,
    string Name,
    string Scope,
    int MinimumPercent,
    IReadOnlyList<string> ShopIds,
    IReadOnlyList<string> ItemIds);

public sealed record RuntimeBundleCLootRuleDefinition(
    string Id,
    string Name,
    string Status,
    string Confidence,
    string Description);

public sealed class RuntimeBundleCDecodedServiceAuthority
{
    private readonly Dictionary<string, RuntimeBundleCShopDefinition> _shops;
    private readonly Dictionary<string, RuntimeBundleCBankDefinition> _banks;
    private readonly Dictionary<string, RuntimeBundleCEconomyRule> _economyRules;
    private readonly Dictionary<string, RuntimeBundleCLootRuleDefinition> _lootRules;

    private RuntimeBundleCDecodedServiceAuthority(
        IEnumerable<RuntimeBundleCShopDefinition> shops,
        IEnumerable<RuntimeBundleCBankDefinition> banks,
        IEnumerable<RuntimeBundleCEconomyRule> economyRules,
        IEnumerable<RuntimeBundleCLootRuleDefinition> lootRules)
    {
        _shops = shops.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        _banks = banks.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        _economyRules = economyRules.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        _lootRules = lootRules.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<RuntimeBundleCShopDefinition> Shops => _shops.Values;
    public IReadOnlyCollection<RuntimeBundleCBankDefinition> Banks => _banks.Values;
    public IReadOnlyCollection<RuntimeBundleCEconomyRule> EconomyRules => _economyRules.Values;
    public IReadOnlyCollection<RuntimeBundleCLootRuleDefinition> LootRules => _lootRules.Values;

    public bool TryGetShop(string id, out RuntimeBundleCShopDefinition definition) => _shops.TryGetValue(id, out definition!);
    public bool TryGetBank(string id, out RuntimeBundleCBankDefinition definition) => _banks.TryGetValue(id, out definition!);
    public bool TryGetEconomyRule(string id, out RuntimeBundleCEconomyRule definition) => _economyRules.TryGetValue(id, out definition!);

    public static RuntimeBundleCDecodedServiceAuthority Load(string projectRoot, string worldId)
    {
        var root = Path.Combine(projectRoot, "Data", worldId);
        return new(
            ReadShops(Path.Combine(root, "shops.json")),
            ReadBanks(Path.Combine(root, "banks.json")),
            ReadEconomyRules(Path.Combine(root, "economy_rules.json")),
            ReadLootRules(Path.Combine(root, "loot_rules.json")));
    }

    private static IEnumerable<RuntimeBundleCShopDefinition> ReadShops(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var record in Records(document))
        {
            var publicFields = PublicFields(record);
            var inventory = new List<RuntimeBundleCShopStockDefinition>();
            if (record.TryGetProperty("inventory", out var generatedInventory) && generatedInventory.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in generatedInventory.EnumerateArray())
                {
                    var itemId = S(entry, "item_ref");
                    if (string.IsNullOrWhiteSpace(itemId)) continue;
                    var publicEntry = FindPublicInventory(publicFields, itemId);
                    inventory.Add(new(
                        itemId,
                        I(entry, "normal_qty"),
                        I(entry, "max_qty"),
                        I(entry, "regen_percent"),
                        I(entry, "regen_qty"),
                        I(entry, "regen_time"),
                        publicEntry.ValueKind == JsonValueKind.Object ? L(publicEntry, "price") : 0,
                        publicEntry.ValueKind == JsonValueKind.Object ? S(publicEntry, "currency_type") : null,
                        publicEntry.ValueKind == JsonValueKind.Object && B(publicEntry, "gang_stocked")));
                }
            }
            yield return new(
                S(record, "id") ?? throw new InvalidOperationException("shop-id-required"),
                S(record, "name") ?? string.Empty,
                S(record, "room_ref") ?? string.Empty,
                S(record, "service_kind") ?? S(record, "service_type") ?? "shop",
                I(record, "class_limit"),
                record.TryGetProperty("level_range", out var range) ? I(range, "min") : 0,
                record.TryGetProperty("level_range", out range) ? I(range, "max") : 0,
                publicFields.ValueKind == JsonValueKind.Object ? I(publicFields, "markup_percent") : 0,
                inventory);
        }
    }

    private static IEnumerable<RuntimeBundleCBankDefinition> ReadBanks(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var record in Records(document))
        {
            var evidenceRow = FirstEvidenceRow(record);
            yield return new(
                S(record, "id") ?? throw new InvalidOperationException("bank-id-required"),
                S(record, "name") ?? string.Empty,
                I(record, "room_number"),
                S(record, "service_kind") ?? "bank",
                evidenceRow.ValueKind == JsonValueKind.Object ? I(evidenceRow, "class_limit") : 0,
                evidenceRow.ValueKind == JsonValueKind.Object ? I(evidenceRow, "min_level") : 0,
                evidenceRow.ValueKind == JsonValueKind.Object ? I(evidenceRow, "max_level") : 0);
        }
    }

    private static IEnumerable<RuntimeBundleCEconomyRule> ReadEconomyRules(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var record in Records(document))
        {
            var relationships = record.TryGetProperty("relationships", out var r) ? r : default;
            yield return new(
                S(record, "id") ?? throw new InvalidOperationException("economy-rule-id-required"),
                S(record, "name") ?? string.Empty,
                S(record, "rule_scope") ?? string.Empty,
                I(record, "minimum_percent"),
                ReadStrings(relationships, "shops"),
                ReadStrings(relationships, "items"));
        }
    }

    private static IEnumerable<RuntimeBundleCLootRuleDefinition> ReadLootRules(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var record in Records(document))
            yield return new(
                S(record, "id") ?? throw new InvalidOperationException("loot-rule-id-required"),
                S(record, "name") ?? string.Empty,
                S(record, "status") ?? string.Empty,
                S(record, "confidence") ?? string.Empty,
                S(record, "description") ?? string.Empty);
    }

    private static IEnumerable<JsonElement> Records(JsonDocument document) => document.RootElement.GetProperty("records").EnumerateArray();
    private static JsonElement PublicFields(JsonElement record) => record.TryGetProperty("public_reference_0139", out var pr) && pr.ValueKind == JsonValueKind.Object && pr.TryGetProperty("fields", out var f) ? f : default;
    private static JsonElement FirstEvidenceRow(JsonElement record)
    {
        if (!record.TryGetProperty("evidence", out var e) || e.ValueKind != JsonValueKind.Array) return default;
        foreach (var item in e.EnumerateArray()) if (item.TryGetProperty("row", out var row) && row.ValueKind == JsonValueKind.Object) return row;
        return default;
    }
    private static JsonElement FindPublicInventory(JsonElement fields, string itemId)
    {
        if (fields.ValueKind != JsonValueKind.Object || !fields.TryGetProperty("inventory", out var inventory) || inventory.ValueKind != JsonValueKind.Array) return default;
        foreach (var entry in inventory.EnumerateArray()) if (string.Equals(S(entry, "item_id"), itemId, StringComparison.OrdinalIgnoreCase)) return entry;
        return default;
    }
    private static IReadOnlyList<string> ReadStrings(JsonElement parent, string name)
    {
        if (parent.ValueKind != JsonValueKind.Object || !parent.TryGetProperty(name, out var values) || values.ValueKind != JsonValueKind.Array) return Array.Empty<string>();
        return values.EnumerateArray().Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() : null).Where(x => !string.IsNullOrWhiteSpace(x)).Cast<string>().ToArray();
    }
    private static string? S(JsonElement e, string n) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    private static bool B(JsonElement e, string n) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.True;
    private static int I(JsonElement e, string n)
    {
        if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(n, out var v)) return 0;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i)) return i;
        return v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out i) ? i : 0;
    }
    private static long L(JsonElement e, string n)
    {
        if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(n, out var v)) return 0;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var i)) return i;
        return v.ValueKind == JsonValueKind.String && long.TryParse(v.GetString(), out i) ? i : 0;
    }
}
