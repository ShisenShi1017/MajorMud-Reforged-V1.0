using System.Text.Json;

namespace RealmGate.Server;

/// <summary>
/// Retains every generated gameplay record as live world-scoped runtime data.
/// Generated WCC and gameplay-required Worldgroup data is executable authority;
/// confidence/provenance metadata may be audited but never suppresses a decoded record.
/// </summary>
public sealed class RuntimeAuthoritativeDecodedDataCatalog
{
    private readonly IReadOnlyDictionary<string, RuntimeDecodedFamily> _families;

    private RuntimeAuthoritativeDecodedDataCatalog(IReadOnlyDictionary<string, RuntimeDecodedFamily> families)
    {
        _families = families;
    }

    public IReadOnlyDictionary<string, RuntimeDecodedFamily> Families => _families;
    public int FamilyCount => _families.Count;
    public int RecordCount => _families.Values.Sum(x => x.Records.Count);

    public bool TryGetFamily(string family, out RuntimeDecodedFamily value) =>
        _families.TryGetValue(family, out value!);

    public IReadOnlyList<JsonElement> Records(string family) =>
        _families.TryGetValue(family, out var value) ? value.Records : Array.Empty<JsonElement>();

    public JsonElement? Find(string family, string id)
    {
        if (!_families.TryGetValue(family, out var value)) return null;
        return value.ById.TryGetValue(id, out var record) ? record : null;
    }

    public JsonElement? FindLegacy(string family, int legacyId)
    {
        if (!_families.TryGetValue(family, out var value)) return null;
        return value.ByLegacyId.TryGetValue(legacyId, out var record) ? record : null;
    }

    public static RuntimeAuthoritativeDecodedDataCatalog Load(string dataRoot)
    {
        if (!Directory.Exists(dataRoot)) throw new DirectoryNotFoundException(dataRoot);
        var families = new Dictionary<string, RuntimeDecodedFamily>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in Directory.EnumerateFiles(dataRoot, "*.json", SearchOption.TopDirectoryOnly)
                     .OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            var family = Path.GetFileNameWithoutExtension(path);
            if (family.EndsWith("_manifest", StringComparison.OrdinalIgnoreCase)) continue;
            var records = LoadRecords(path).ToArray();
            var byId = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            var byLegacy = new Dictionary<int, JsonElement>();
            foreach (var record in records)
            {
                var id = ReadIdentity(record);
                if (!string.IsNullOrWhiteSpace(id)) byId.TryAdd(id, record);
                var legacy = ReadLegacyId(record);
                if (legacy > 0) byLegacy.TryAdd(legacy, record);
            }
            families[family] = new RuntimeDecodedFamily(family, path, records, byId, byLegacy);
        }
        return new RuntimeAuthoritativeDecodedDataCatalog(families);
    }

    private static IEnumerable<JsonElement> LoadRecords(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        if (root.ValueKind == JsonValueKind.Array)
            return root.EnumerateArray().Select(x => x.Clone()).ToArray();
        if (root.ValueKind != JsonValueKind.Object) return Array.Empty<JsonElement>();
        if (root.TryGetProperty("records", out var records) && records.ValueKind == JsonValueKind.Array)
            return records.EnumerateArray().Select(x => x.Clone()).ToArray();
        if (root.TryGetProperty("record_parts", out var parts) && parts.ValueKind == JsonValueKind.Array)
        {
            var baseDirectory = Path.GetDirectoryName(path)!;
            return parts.EnumerateArray()
                .Where(x => x.ValueKind == JsonValueKind.String)
                .SelectMany(x => LoadRecords(Path.GetFullPath(Path.Combine(baseDirectory, x.GetString()!.Replace('/', Path.DirectorySeparatorChar)))))
                .ToArray();
        }
        return new[] { root.Clone() };
    }

    private static string ReadIdentity(JsonElement record)
    {
        foreach (var field in new[] { "id", "key", "name" })
            if (record.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()))
                return value.GetString()!;
        return string.Empty;
    }

    private static int ReadLegacyId(JsonElement record)
    {
        foreach (var field in new[] { "legacy_id", "legacyId", "number", "record_number" })
        {
            if (!record.TryGetProperty(field, out var value)) continue;
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) return number;
            if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out number)) return number;
        }
        return 0;
    }
}

public sealed record RuntimeDecodedFamily(
    string Name,
    string Path,
    IReadOnlyList<JsonElement> Records,
    IReadOnlyDictionary<string, JsonElement> ById,
    IReadOnlyDictionary<int, JsonElement> ByLegacyId);
