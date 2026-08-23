using System.Security.Cryptography;
using System.Text.Json;

namespace Reforged.Server;

public sealed class GeneratedWorldDataCatalog
{
    private static readonly string[] BaselineRequiredFamilies =
    {
        "rooms", "exits", "triggers", "spawns", "npcs", "items", "spells", "formulas", "shops", "quests", "world_manifest", "compile_manifest"
    };

    private static readonly string[] ClassicParityRequiredFamilies =
    {
        "commands", "abilities", "character_foundation", "classes", "races", "skills",
        "equipment", "equipment_rules", "equipment_slots", "status_effects",
        "loot_rules", "economy_rules", "room_interactions", "scripts",
        "gang_houses"
    };

    private static readonly string[] ClassicParityOptionalFamilies =
    {
        "npc_ai_runtime"
    };

    public string WorldId { get; init; } = string.Empty;
    public string DataRoot { get; init; } = string.Empty;
    public string WorldManifestPath { get; init; } = string.Empty;
    public string CompileManifestPath { get; init; } = string.Empty;
    public string WorldManifestHash { get; init; } = string.Empty;
    public string CompileManifestHash { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, GeneratedWorldFamilyStatus> Families { get; init; } = new Dictionary<string, GeneratedWorldFamilyStatus>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<string> Diagnostics { get; init; } = Array.Empty<string>();
    public bool Passed => Diagnostics.Count == 0 && Families.Values.All(family => family.Exists && family.RecordCount >= 0);

    public static GeneratedWorldDataCatalog Validate(string projectRoot, WorldRuntimeDefinition definition)
    {
        var dataRoot = Path.Combine(projectRoot, definition.DataRoot);
        var diagnostics = new List<string>();
        if (!Directory.Exists(dataRoot))
        {
            diagnostics.Add($"Generated data root does not exist: {dataRoot}");
        }

        var families = new Dictionary<string, GeneratedWorldFamilyStatus>(StringComparer.OrdinalIgnoreCase);
        var configuredFamilies = definition.ClassicProtected
            ? BaselineRequiredFamilies.Concat(ClassicParityRequiredFamilies).Distinct(StringComparer.OrdinalIgnoreCase)
            : BaselineRequiredFamilies;
        // Every generated gameplay family in the world data root is authoritative.
        // Discover it automatically so newly decoded WCC/Worldgroup families cannot exist
        // outside runtime validation merely because a hand-maintained allow-list was not updated.
        var discoveredFamilies = Directory.Exists(dataRoot)
            ? Directory.EnumerateFiles(dataRoot, "*.json", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileNameWithoutExtension)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .Select(name => name!)
            : Array.Empty<string>();
        var requiredFamilies = configuredFamilies.Concat(discoveredFamilies)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase);
        foreach (var family in requiredFamilies)
        {
            var fileName = $"{family}.json";
            var path = Path.Combine(dataRoot, fileName);
            var exists = File.Exists(path);
            var recordCount = exists ? CountRecords(path, diagnostics) : -1;
            families[family] = new GeneratedWorldFamilyStatus
            {
                Family = family,
                Path = path,
                Exists = exists,
                RecordCount = recordCount,
                Sha256 = exists ? ComputeSha256(path) : string.Empty
            };

            if (!exists)
            {
                diagnostics.Add($"Required generated data file missing for world '{definition.Id}': {fileName}");
            }
        }

        if (definition.ClassicProtected)
        {
            foreach (var family in ClassicParityOptionalFamilies)
            {
                if (families.ContainsKey(family)) continue;
                var path = Path.Combine(dataRoot, $"{family}.json");
                var exists = File.Exists(path);
                if (!exists) continue;
                families[family] = new GeneratedWorldFamilyStatus
                {
                    Family = family,
                    Path = path,
                    Exists = true,
                    RecordCount = CountRecords(path, diagnostics),
                    Sha256 = ComputeSha256(path)
                };
            }
        }

        var worldManifestPath = Path.Combine(dataRoot, "world_manifest.json");
        var compileManifestPath = Path.Combine(dataRoot, "compile_manifest.json");
        ValidateManifestWorldId(worldManifestPath, definition, diagnostics);
        ValidateManifestWorldId(compileManifestPath, definition, diagnostics);

        return new GeneratedWorldDataCatalog
        {
            WorldId = definition.Id,
            DataRoot = dataRoot,
            WorldManifestPath = worldManifestPath,
            CompileManifestPath = compileManifestPath,
            WorldManifestHash = File.Exists(worldManifestPath) ? ComputeSha256(worldManifestPath) : string.Empty,
            CompileManifestHash = File.Exists(compileManifestPath) ? ComputeSha256(compileManifestPath) : string.Empty,
            Families = families,
            Diagnostics = diagnostics
        };
    }

    private static int CountRecords(string path, List<string> diagnostics)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Array)
            {
                return root.GetArrayLength();
            }

            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("record_count", out var recordCount) && recordCount.ValueKind == JsonValueKind.Number && recordCount.TryGetInt32(out var count))
                {
                    return count;
                }

                if (root.TryGetProperty("record_parts", out var parts) && parts.ValueKind == JsonValueKind.Array)
                {
                    var total = 0;
                    foreach (var part in parts.EnumerateArray())
                    {
                        if (part.ValueKind != JsonValueKind.String) continue;
                        var partPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, part.GetString()!.Replace('/', Path.DirectorySeparatorChar)));
                        total += CountRecords(partPath, diagnostics);
                    }
                    return total;
                }

                if (root.TryGetProperty("records", out var records) && records.ValueKind == JsonValueKind.Array)
                {
                    return records.GetArrayLength();
                }

                return 1;
            }

            diagnostics.Add($"Generated data file has unsupported JSON root: {path}");
            return -1;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            diagnostics.Add($"Generated data file could not be read: {path}: {ex.Message}");
            return -1;
        }
    }

    private static void ValidateManifestWorldId(string path, WorldRuntimeDefinition definition, List<string> diagnostics)
    {
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (!document.RootElement.TryGetProperty("world_id", out var worldId) || worldId.ValueKind != JsonValueKind.String)
            {
                diagnostics.Add($"Manifest missing world_id: {path}");
                return;
            }

            if (!string.Equals(worldId.GetString(), definition.Id, StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add($"Manifest world_id '{worldId.GetString()}' does not match configured world '{definition.Id}': {path}");
            }
        }
        catch (JsonException ex)
        {
            diagnostics.Add($"Manifest could not be parsed: {path}: {ex.Message}");
        }
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

public sealed class GeneratedWorldFamilyStatus
{
    public string Family { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public bool Exists { get; init; }
    public int RecordCount { get; init; }
    public string Sha256 { get; init; } = string.Empty;
}
