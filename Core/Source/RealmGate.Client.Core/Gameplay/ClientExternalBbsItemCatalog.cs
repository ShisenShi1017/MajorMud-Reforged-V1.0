using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace RealmGate.Client.Gameplay;

/// <summary>
/// Read-only effective item projection for an external BBS profile.
/// Precedence is manual override, imported MDB delta, then generated Classic.
/// </summary>
public sealed class ClientExternalBbsItemCatalog
{
    private const long MaximumSourceFileBytes = 64L * 1024 * 1024;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly Dictionary<string, ClientWorkshopItem> _itemsByNormalizedName;

    private ClientExternalBbsItemCatalog(
        string classicDataRoot,
        string? overlayPath,
        IReadOnlyList<ClientWorkshopItem> items)
    {
        ClassicDataRoot = classicDataRoot;
        OverlayPath = overlayPath ?? string.Empty;
        Items = items;
        _itemsByNormalizedName = items
            .GroupBy(item => NormalizeItemName(item.Name), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Key.Length > 0)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
    }

    public string ClassicDataRoot { get; }
    public string OverlayPath { get; }
    public bool HasOverlay => OverlayPath.Length > 0 && File.Exists(OverlayPath);
    public string AuthorityDescription => HasOverlay
        ? "Classic base + external MDB delta + manual overrides"
        : "Classic base (no external overlay found)";
    public IReadOnlyList<ClientWorkshopItem> Items { get; }

    public static ClientExternalBbsItemCatalog Load(string classicDataRoot, string? overlayPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(classicDataRoot);
        var root = Path.GetFullPath(classicDataRoot);
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException($"Generated Classic data was not found at '{root}'.");

        var overlay = LoadOverlay(overlayPath);
        var itemRecords = LoadEffectiveFamily(root, "items.json", "Items", overlay);
        var equipmentRecords = LoadEffectiveFamily(root, "equipment.json", "Equipment", overlay);
        var equipment = LoadEquipment(equipmentRecords);
        var items = LoadItems(itemRecords, equipment)
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new ClientExternalBbsItemCatalog(root, NormalizeOptionalPath(overlayPath), items);
    }

    public IReadOnlyList<ClientWorkshopItem> Search(string query, int limit, out int totalMatches)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        if (limit is < 1 or > 1000)
            throw new ArgumentOutOfRangeException(nameof(limit));

        var text = query.Trim();
        var matches = Items.Where(item =>
                item.Name.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                item.Id.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                item.Slot.Contains(text, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        totalMatches = matches.Length;
        return matches.Take(limit).ToArray();
    }

    public ClientWorkshopItem? FindByDisplayName(string? displayName)
    {
        var normalized = NormalizeItemName(displayName ?? string.Empty);
        if (normalized.Length == 0)
            return null;
        if (_itemsByNormalizedName.TryGetValue(normalized, out var exact))
            return exact;

        return _itemsByNormalizedName
            .Where(pair => normalized.Contains(pair.Key, StringComparison.OrdinalIgnoreCase) ||
                           pair.Key.Contains(normalized, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(pair => pair.Key.Length)
            .Select(pair => pair.Value)
            .FirstOrDefault();
    }

    private static ClientExternalBbsOverlay? LoadOverlay(string? overlayPath)
    {
        var path = NormalizeOptionalPath(overlayPath);
        if (path.Length == 0 || !File.Exists(path))
            return null;
        EnsureSafeFileSize(path);
        try
        {
            var overlay = JsonSerializer.Deserialize<ClientExternalBbsOverlay>(File.ReadAllText(path), Json)
                ?? throw new InvalidDataException("External BBS overlay was empty.");
            overlay.Families ??= new(StringComparer.OrdinalIgnoreCase);
            return overlay;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("External BBS overlay is not valid JSON.", exception);
        }
    }

    private static IReadOnlyList<EffectiveRecord> LoadEffectiveFamily(
        string root,
        string fileName,
        string family,
        ClientExternalBbsOverlay? overlay)
    {
        var sourcePath = Path.Combine(root, fileName);
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException($"Generated Classic {family} data was not found.", sourcePath);

        Dictionary<string, ClientExternalBbsRecordOverlay>? familyOverlay = null;
        if (overlay?.Families is { } families && families.TryGetValue(family, out var records))
            familyOverlay = records;
        var result = new List<EffectiveRecord>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in EnumerateRecords(sourcePath, root, new HashSet<string>(StringComparer.OrdinalIgnoreCase)))
        {
            var id = ResolveRecordId(family, source);
            if (id.Length == 0)
                continue;
            seen.Add(id);
            var effective = (JsonObject)source.DeepClone();
            if (familyOverlay?.TryGetValue(id, out var recordOverlay) == true)
                ApplyOverlay(effective, recordOverlay, applyImportedRecordFields: true);
            result.Add(new EffectiveRecord(id, effective));
        }

        if (familyOverlay is not null)
        {
            foreach (var pair in familyOverlay.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (seen.Contains(pair.Key) || pair.Value?.ImportedRecord is null)
                    continue;
                var effective = (JsonObject)pair.Value.ImportedRecord.DeepClone();
                ApplyOverlay(effective, pair.Value, applyImportedRecordFields: false);
                result.Add(new EffectiveRecord(pair.Key, effective));
            }
        }

        return result;
    }

    private static IEnumerable<JsonObject> EnumerateRecords(
        string path,
        string dataRoot,
        HashSet<string> visited)
    {
        var fullPath = Path.GetFullPath(path);
        if (!visited.Add(fullPath))
            throw new InvalidDataException($"Generated data manifest contains a cycle at '{fullPath}'.");
        EnsureSafeFileSize(fullPath);

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(File.ReadAllText(fullPath));
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"Generated data file '{fullPath}' is not valid JSON.", exception);
        }

        if (root is JsonArray direct)
        {
            foreach (var record in direct.OfType<JsonObject>())
                yield return record;
            yield break;
        }
        if (root is not JsonObject document)
            yield break;
        if (document["records"] is JsonArray records)
        {
            foreach (var record in records.OfType<JsonObject>())
                yield return record;
            yield break;
        }
        if (document["record_parts"] is not JsonArray parts)
            yield break;

        foreach (var part in parts)
        {
            var relative = part?.GetValue<string>()?.Trim() ?? string.Empty;
            if (relative.Length == 0)
                continue;
            var partPath = Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(fullPath) ?? dataRoot,
                relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!IsWithinRoot(dataRoot, partPath))
                throw new InvalidDataException("Generated data manifest references a file outside its world root.");
            if (!File.Exists(partPath))
                throw new FileNotFoundException("Generated data manifest part was not found.", partPath);
            foreach (var record in EnumerateRecords(partPath, dataRoot, visited))
                yield return record;
        }
    }

    private static IReadOnlyDictionary<string, EquipmentRule> LoadEquipment(IEnumerable<EffectiveRecord> records)
    {
        var result = new Dictionary<string, EquipmentRule>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in records)
        {
            var itemId = FirstText(entry.Record, ["item_id"]);
            if (itemId.Length == 0)
                itemId = entry.Id.StartsWith("equipment.", StringComparison.OrdinalIgnoreCase)
                    ? "item." + entry.Id["equipment.".Length..]
                    : entry.Id;
            if (itemId.Length == 0)
                continue;
            result[itemId] = new EquipmentRule(
                FirstText(entry.Record, ["slot"]),
                FirstBool(entry.Record, ["two_handed"]),
                FirstBool(entry.Record, ["class_restricted"]),
                FirstBool(entry.Record, ["race_restricted"]),
                FirstInt(entry.Record, ["minimum_level"]),
                FirstInt(entry.Record, ["maximum_level"]),
                FirstInt(entry.Record, ["minimum_strength"]));
        }
        return result;
    }

    private static IEnumerable<ClientWorkshopItem> LoadItems(
        IEnumerable<EffectiveRecord> records,
        IReadOnlyDictionary<string, EquipmentRule> equipment)
    {
        foreach (var entry in records)
        {
            var id = FirstText(entry.Record, ["id"]);
            if (id.Length == 0)
                id = entry.Id;
            var name = FirstText(entry.Record, ["name"], ["public_reference_0139", "fields", "name"]);
            if (id.Length == 0 || name.Length == 0)
                continue;

            equipment.TryGetValue(id, out var rule);
            yield return new ClientWorkshopItem(
                id,
                name,
                FirstText(entry.Record, ["classification", "type"], ["public_reference_0139", "fields", "item_type"]),
                rule?.Slot ?? string.Empty,
                FirstInt(entry.Record, ["economy", "weight"], ["public_reference_0139", "fields", "weight"]),
                FirstLong(entry.Record, ["economy", "cost"], ["public_reference_0139", "fields", "cost"]),
                FirstInt(entry.Record, ["combat", "minimum_damage"], ["public_reference_0139", "fields", "min_hit"]),
                FirstInt(entry.Record, ["combat", "maximum_damage"], ["public_reference_0139", "fields", "max_hit"]),
                FirstInt(entry.Record, ["combat", "armor_class_bonus"], ["public_reference_0139", "fields", "ac_bonus"]),
                FirstInt(entry.Record, ["combat", "damage_resistance_bonus"], ["public_reference_0139", "fields", "dr_bonus"]),
                FirstInt(entry.Record, ["combat", "accuracy"], ["public_reference_0139", "fields", "accuracy"]),
                FirstInt(entry.Record, ["combat", "speed"], ["public_reference_0139", "fields", "speed"]),
                rule?.MinimumLevel ?? 0,
                rule?.MaximumLevel ?? 0,
                rule?.MinimumStrength ?? 0,
                rule?.TwoHanded ?? false,
                rule?.ClassRestricted ?? false,
                rule?.RaceRestricted ?? false,
                ReadAbilities(entry.Record));
        }
    }

    private static IReadOnlyList<ClientWorkshopAbility> ReadAbilities(JsonObject record)
    {
        if (ReadPath(record, ["public_reference_0139", "fields", "abilities_named"]) is not JsonArray abilities)
            return [];
        var result = new List<ClientWorkshopAbility>();
        foreach (var entry in abilities.OfType<JsonObject>())
        {
            var name = ScalarText(entry["name"]);
            if (name.Length == 0)
                continue;
            result.Add(new ClientWorkshopAbility(name, ScalarInt(entry["value"])));
        }
        return result;
    }

    private static void ApplyOverlay(
        JsonObject target,
        ClientExternalBbsRecordOverlay overlay,
        bool applyImportedRecordFields)
    {
        if (applyImportedRecordFields)
            foreach (var pair in overlay.ImportedFields ?? [])
                SetPath(target, pair.Key, pair.Value?.DeepClone());
        foreach (var pair in overlay.Fields ?? [])
            SetPath(target, pair.Key, pair.Value?.DeepClone());
    }

    private static void SetPath(JsonObject root, string path, JsonNode? value)
    {
        var parts = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return;
        var current = root;
        for (var index = 0; index < parts.Length - 1; index++)
        {
            if (current[parts[index]] is not JsonObject next)
            {
                next = [];
                current[parts[index]] = next;
            }
            current = next;
        }
        current[parts[^1]] = value;
    }

    private static string ResolveRecordId(string family, JsonObject record)
    {
        var id = ScalarText(record["id"]);
        if (id.Length > 0)
            return id;
        var itemId = ScalarText(record["item_id"]);
        if (itemId.Length > 0)
            return itemId;
        var legacy = ScalarText(record["legacy_id"]);
        return legacy.Length == 0 ? string.Empty : "item." + legacy;
    }

    private static string FirstText(JsonObject root, params string[][] paths)
    {
        foreach (var path in paths)
        {
            var text = ScalarText(ReadPath(root, path));
            if (text.Length > 0)
                return text;
        }
        return string.Empty;
    }

    private static int FirstInt(JsonObject root, params string[][] paths)
    {
        foreach (var path in paths)
        {
            var node = ReadPath(root, path);
            if (node is not null && TryInt(node, out var value))
                return value;
        }
        return 0;
    }

    private static long FirstLong(JsonObject root, params string[][] paths)
    {
        foreach (var path in paths)
        {
            var node = ReadPath(root, path);
            if (node is JsonValue value)
            {
                if (value.TryGetValue<long>(out var number))
                    return number;
                if (long.TryParse(ScalarText(value), NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
                    return number;
            }
        }
        return 0;
    }

    private static bool FirstBool(JsonObject root, params string[][] paths)
    {
        foreach (var path in paths)
        {
            var node = ReadPath(root, path);
            if (node is not JsonValue value)
                continue;
            if (value.TryGetValue<bool>(out var flag))
                return flag;
            if (bool.TryParse(ScalarText(value), out flag))
                return flag;
        }
        return false;
    }

    private static JsonNode? ReadPath(JsonObject root, IReadOnlyList<string> path)
    {
        JsonNode? current = root;
        foreach (var part in path)
        {
            if (current is not JsonObject obj || !obj.TryGetPropertyValue(part, out current))
                return null;
        }
        return current;
    }

    private static bool TryInt(JsonNode node, out int result)
    {
        if (node is JsonValue value && value.TryGetValue<int>(out result))
            return true;
        return int.TryParse(ScalarText(node), NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
    }

    private static int ScalarInt(JsonNode? node) =>
        node is not null && TryInt(node, out var result) ? result : 0;

    private static string ScalarText(JsonNode? node)
    {
        if (node is not JsonValue value)
            return string.Empty;
        if (value.TryGetValue<string>(out var text))
            return text?.Trim() ?? string.Empty;
        return value.ToJsonString().Trim('"');
    }

    private static string NormalizeItemName(string value)
    {
        var text = value.Trim().ToLowerInvariant();
        text = Regex.Replace(text, @"^\s*\d+\s*[xX]\s+", string.Empty);
        text = Regex.Replace(text, @"^\s*\[[^\]]+\]\s*", string.Empty);
        text = Regex.Replace(text, @"\s+\(.*?\)\s*$", string.Empty);
        text = Regex.Replace(text, @"^(a|an|the)\s+", string.Empty);
        return Regex.Replace(text, @"\s+", " ").Trim();
    }

    private static string NormalizeOptionalPath(string? path) =>
        string.IsNullOrWhiteSpace(path) ? string.Empty : Path.GetFullPath(path);

    private static bool IsWithinRoot(string root, string candidate)
    {
        var relative = Path.GetRelativePath(Path.GetFullPath(root), Path.GetFullPath(candidate));
        return !relative.Equals("..", StringComparison.Ordinal) &&
               !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) &&
               !Path.IsPathRooted(relative);
    }

    private static void EnsureSafeFileSize(string path)
    {
        var length = new FileInfo(path).Length;
        if (length > MaximumSourceFileBytes)
            throw new InvalidDataException($"Game-data file '{path}' exceeds the client safety limit.");
    }

    private sealed record EffectiveRecord(string Id, JsonObject Record);
    private sealed record EquipmentRule(
        string Slot,
        bool TwoHanded,
        bool ClassRestricted,
        bool RaceRestricted,
        int MinimumLevel,
        int MaximumLevel,
        int MinimumStrength);

    private sealed class ClientExternalBbsOverlay
    {
        public Dictionary<string, Dictionary<string, ClientExternalBbsRecordOverlay>>? Families { get; set; }
    }

    private sealed class ClientExternalBbsRecordOverlay
    {
        public Dictionary<string, JsonNode?>? Fields { get; set; }
        public Dictionary<string, JsonNode?>? ImportedFields { get; set; }
        public JsonObject? ImportedRecord { get; set; }
    }
}
