using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Reforged.App.Models;

namespace Reforged.App.Services;

/// <summary>
/// Effective game-data projection used by the MudPlay-inspired Character Workshop.
/// Reforged sessions read their server-selected world projection. External BBS sessions
/// read Classic with their local field overlay applied.
/// </summary>
public sealed class MudPlayWorkshopDataCatalog
{
    private readonly Dictionary<string, MudPlayWorkshopItem> _itemsByNormalizedName;

    private MudPlayWorkshopDataCatalog(string worldName, string dataRoot, string authorityDescription, IReadOnlyList<MudPlayWorkshopItem> items)
    {
        WorldName = worldName;
        DataRoot = dataRoot;
        AuthorityDescription = authorityDescription;
        Items = items;
        _itemsByNormalizedName = items
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .GroupBy(item => NormalizeItemName(item.Name), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Key.Length > 0)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
    }

    public string WorldName { get; }
    public string DataRoot { get; }
    public string AuthorityDescription { get; }
    public IReadOnlyList<MudPlayWorkshopItem> Items { get; }

    public static MudPlayWorkshopDataCatalog LoadClassic()
    {
        var overlayService = new MudPlayClientGameDataOverlayService();
        var equipmentRecords = overlayService.LoadClassicFamily("Equipment")
            .Select(record => record.EffectiveRecord)
            .ToArray();
        var itemRecords = overlayService.LoadClassicFamily("Items")
            .Select(record => record.EffectiveRecord)
            .ToArray();

        var equipmentRules = LoadEquipmentRules(equipmentRecords);
        var items = LoadItems(itemRecords, equipmentRules)
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new MudPlayWorkshopDataCatalog("Classic", "Data/Classic", "Classic base data", items);
    }

    public static MudPlayWorkshopDataCatalog Load(ClientSessionConnection session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var overlayService = new MudPlayClientGameDataOverlayService();
        var context = overlayService.ResolveContext(session);
        var equipmentRecords = overlayService.LoadFamily(session, "Equipment")
            .Select(record => record.EffectiveRecord)
            .ToArray();
        var itemRecords = overlayService.LoadFamily(session, "Items")
            .Select(record => record.EffectiveRecord)
            .ToArray();

        var equipmentRules = LoadEquipmentRules(equipmentRecords);
        var items = LoadItems(itemRecords, equipmentRules)
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new MudPlayWorkshopDataCatalog(context.BaseWorld, context.BaseDataRoot, context.Description, items);
    }

    public MudPlayWorkshopItem? FindByDisplayName(string? displayName)
    {
        var normalized = NormalizeItemName(displayName ?? string.Empty);
        if (normalized.Length == 0)
            return null;

        if (_itemsByNormalizedName.TryGetValue(normalized, out var exact))
            return exact;

        // Captured terminal inventory/equipment can contain quantities or short annotations.
        // Prefer a deterministic longest-name containment match rather than guessing identity.
        return _itemsByNormalizedName
            .Where(pair => normalized.Contains(pair.Key, StringComparison.OrdinalIgnoreCase) ||
                           pair.Key.Contains(normalized, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(pair => pair.Key.Length)
            .Select(pair => pair.Value)
            .FirstOrDefault();
    }

    private static IEnumerable<MudPlayWorkshopItem> LoadItems(
        IEnumerable<JsonObject> records,
        IReadOnlyDictionary<string, MudPlayWorkshopEquipmentRule> equipmentRules)
    {
        foreach (var objectRecord in records)
        {
            var record = ToElement(objectRecord);
            var id = FirstText(record, ["id"]);
            if (id.Length == 0)
            {
                var legacy = FirstText(record, ["legacy_id"]);
                if (legacy.Length > 0)
                    id = "item." + legacy;
            }

            var name = FirstText(record,
                ["name"],
                ["public_reference_0139", "fields", "name"]);
            if (name.Length == 0)
                continue;

            equipmentRules.TryGetValue(id, out var rule);

            var itemType = FirstText(record,
                ["classification", "type"],
                ["public_reference_0139", "fields", "item_type"]);

            var weight = FirstLong(record,
                ["economy", "weight"],
                ["public_reference_0139", "fields", "weight"]);
            var cost = FirstLong(record,
                ["economy", "cost"],
                ["public_reference_0139", "fields", "cost"]);

            var minDamage = FirstInt(record,
                ["combat", "minimum_damage"],
                ["public_reference_0139", "fields", "min_hit"]);
            var maxDamage = FirstInt(record,
                ["combat", "maximum_damage"],
                ["public_reference_0139", "fields", "max_hit"]);
            var armorClass = FirstInt(record,
                ["combat", "armor_class_bonus"],
                ["public_reference_0139", "fields", "ac_bonus"]);
            var damageResistance = FirstInt(record,
                ["combat", "damage_resistance_bonus"],
                ["public_reference_0139", "fields", "dr_bonus"]);
            var accuracy = FirstInt(record,
                ["combat", "accuracy"],
                ["public_reference_0139", "fields", "accuracy"]);
            var speed = FirstInt(record,
                ["combat", "speed"],
                ["public_reference_0139", "fields", "speed"]);

            yield return new MudPlayWorkshopItem(
                id,
                name,
                itemType,
                rule?.Slot ?? string.Empty,
                weight,
                cost,
                minDamage,
                maxDamage,
                armorClass,
                damageResistance,
                accuracy,
                speed,
                rule?.MinimumLevel ?? 0,
                rule?.MaximumLevel ?? 0,
                rule?.MinimumStrength ?? 0,
                rule?.TwoHanded ?? false,
                rule?.ClassRestricted ?? false,
                rule?.RaceRestricted ?? false);
        }
    }

    private static IReadOnlyDictionary<string, MudPlayWorkshopEquipmentRule> LoadEquipmentRules(IEnumerable<JsonObject> records)
    {
        var result = new Dictionary<string, MudPlayWorkshopEquipmentRule>(StringComparer.OrdinalIgnoreCase);
        foreach (var objectRecord in records)
        {
            var record = ToElement(objectRecord);
            var itemId = FirstText(record, ["item_id"]);
            if (itemId.Length == 0)
                continue;

            result[itemId] = new MudPlayWorkshopEquipmentRule(
                itemId,
                FirstText(record, ["slot"]),
                FirstBool(record, ["two_handed"]),
                FirstBool(record, ["class_restricted"]),
                FirstBool(record, ["race_restricted"]),
                FirstInt(record, ["minimum_level"]),
                FirstInt(record, ["maximum_level"]),
                FirstInt(record, ["minimum_strength"]));
        }
        return result;
    }

    private static JsonElement ToElement(JsonObject record)
    {
        using var document = JsonDocument.Parse(record.ToJsonString());
        return document.RootElement.Clone();
    }

    private static string NormalizeItemName(string value)
    {
        var text = value.Trim().ToLowerInvariant();
        text = System.Text.RegularExpressions.Regex.Replace(text, @"^\s*\d+\s*[xX]\s+", string.Empty);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"^\s*\[[^\]]+\]\s*", string.Empty);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+\(.*?\)\s*$", string.Empty);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"^(a|an|the)\s+", string.Empty);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
        return text.Trim();
    }

    private static string FirstText(JsonElement root, params string[][] paths)
    {
        foreach (var path in paths)
        {
            if (!TryReadPath(root, path, out var value))
                continue;
            if (value.ValueKind == JsonValueKind.String)
                return value.GetString()?.Trim() ?? string.Empty;
            if (value.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
                return value.ToString();
        }
        return string.Empty;
    }

    private static int FirstInt(JsonElement root, params string[][] paths)
    {
        foreach (var path in paths)
        {
            if (!TryReadPath(root, path, out var value))
                continue;
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
                return number;
            if (int.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
                return number;
        }
        return 0;
    }

    private static long FirstLong(JsonElement root, params string[][] paths)
    {
        foreach (var path in paths)
        {
            if (!TryReadPath(root, path, out var value))
                continue;
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number))
                return number;
            if (long.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
                return number;
        }
        return 0;
    }

    private static bool FirstBool(JsonElement root, params string[][] paths)
    {
        foreach (var path in paths)
        {
            if (!TryReadPath(root, path, out var value))
                continue;
            if (value.ValueKind == JsonValueKind.True) return true;
            if (value.ValueKind == JsonValueKind.False) return false;
            if (bool.TryParse(value.ToString(), out var flag)) return flag;
        }
        return false;
    }

    private static bool TryReadPath(JsonElement root, IReadOnlyList<string> path, out JsonElement value)
    {
        value = root;
        foreach (var part in path)
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(part, out value))
                return false;
        }
        return true;
    }
}

public sealed record MudPlayWorkshopEquipmentRule(
    string ItemId,
    string Slot,
    bool TwoHanded,
    bool ClassRestricted,
    bool RaceRestricted,
    int MinimumLevel,
    int MaximumLevel,
    int MinimumStrength);

public sealed record MudPlayWorkshopItem(
    string Id,
    string Name,
    string ItemType,
    string Slot,
    long Weight,
    long Cost,
    int MinimumDamage,
    int MaximumDamage,
    int ArmorClassBonus,
    int DamageResistanceBonus,
    int Accuracy,
    int Speed,
    int MinimumLevel,
    int MaximumLevel,
    int MinimumStrength,
    bool TwoHanded,
    bool ClassRestricted,
    bool RaceRestricted)
{
    public string Damage => MaximumDamage > 0 ? $"{MinimumDamage}-{MaximumDamage}" : "—";
    public string Restrictions
    {
        get
        {
            var parts = new List<string>();
            if (MinimumLevel > 0) parts.Add($"Lvl {MinimumLevel}+");
            if (MaximumLevel > 0) parts.Add($"≤ Lvl {MaximumLevel}");
            if (MinimumStrength > 0) parts.Add($"STR {MinimumStrength}+");
            if (ClassRestricted) parts.Add("Class restricted");
            if (RaceRestricted) parts.Add("Race restricted");
            if (TwoHanded) parts.Add("Two-handed");
            return parts.Count == 0 ? "None shown" : string.Join(" · ", parts);
        }
    }
}
