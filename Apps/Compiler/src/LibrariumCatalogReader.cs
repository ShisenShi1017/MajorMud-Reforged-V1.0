using System.Text.Json;
using System.Text.Json.Nodes;

namespace RealmGate.Compiler;

public sealed class LibrariumCatalogReader
{
    private static readonly string[] PreferredRecordNames =
    [
        "*_records_*.json",
        "*_records.json",
        "*_catalog_*.json",
        "catalog.json"
    ];

    public IReadOnlyList<LibrariumFamily> ReadFamilies(string librariumRoot, List<CompilerDiagnostic> diagnostics)
    {
        var objectsRoot = Path.Combine(librariumRoot, "Objects");
        if (!Directory.Exists(objectsRoot))
        {
            diagnostics.Add(new(DiagnosticSeverity.Error, "LIB001", "Librarium/Objects was not found.", objectsRoot));
            return [];
        }

        var families = new List<LibrariumFamily>();
        foreach (var familyDirectory in Directory.GetDirectories(objectsRoot).OrderBy(Path.GetFileName))
        {
            var familyName = Path.GetFileName(familyDirectory);
            if (familyName.Equals("room_descriptions", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var selectedFiles = SelectSourceFiles(familyDirectory, familyName);
            if (selectedFiles.Count == 0)
            {
                diagnostics.Add(new(DiagnosticSeverity.Warning, "LIB002", $"No catalog file found for family '{familyName}'.", familyDirectory));
                continue;
            }

            try
            {
                var records = new List<JsonObject>();
                var recordsById = new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);
                foreach (var selected in selectedFiles)
                {
                    foreach (var json in ReadJsonCatalogParts(selected))
                    {
                        MergeRecords(records, recordsById, ExtractRecords(json));
                    }
                }

                if (familyName.Equals("gang_houses", StringComparison.OrdinalIgnoreCase))
                {
                    ValidateCanonicalGangHouseInteriorLocations(records, diagnostics);
                }
                families.Add(new LibrariumFamily(familyName, selectedFiles[0], records, selectedFiles));
            }
            catch (Exception ex)
            {
                diagnostics.Add(new(DiagnosticSeverity.Error, "LIB003", $"Could not read family '{familyName}': {ex.Message}", selectedFiles[0]));
            }
        }

        return families;
    }

    private static void ValidateCanonicalGangHouseInteriorLocations(
        IReadOnlyList<JsonObject> records,
        List<CompilerDiagnostic> diagnostics)
    {
        var locations = records
            .SelectMany(record => (record["locations"] as JsonArray)?.OfType<JsonObject>() ?? [])
            .Where(location => location["is_gang_house"]?.GetValue<bool>() == true)
            .ToArray();
        if (locations.Length == 0)
            diagnostics.Add(new(DiagnosticSeverity.Error, "LIBGANGHOUSE",
                "Canonical gang-house records contain no promoted interior locations.",
                "Librarium/Objects/gang_houses"));
    }

    private static void MergeRecords(
        List<JsonObject> records,
        Dictionary<string, JsonObject> recordsById,
        IEnumerable<JsonObject> incoming)
    {
        foreach (var record in incoming)
        {
            var id = record["id"]?.GetValue<string>() ??
                     record["mechanic_id"]?.GetValue<string>() ??
                     record["legacy_id"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(id))
            {
                records.Add(record);
                continue;
            }

            if (!recordsById.TryGetValue(id, out var existing))
            {
                records.Add(record);
                recordsById[id] = record;
                continue;
            }

            var index = records.IndexOf(existing);
            if (index >= 0) records[index] = record;
            recordsById[id] = record;
        }
    }

    private static IReadOnlyList<string> SelectSourceFiles(string familyDirectory, string familyName)
    {
        if (familyName.Equals("quests", StringComparison.OrdinalIgnoreCase))
        {
            var canonical = Path.Combine(familyDirectory, "quest_records.json");
            if (File.Exists(canonical)) return [canonical];
        }

        // NPCs retain MMExplorer SQL/MDB rows as reference evidence beside the
        // executable canonical projection. Worktree checkout timestamps are not
        // a valid authority signal, so always select the canonical NPC records.
        if (familyName.Equals("npcs", StringComparison.OrdinalIgnoreCase))
        {
            var canonical = Path.Combine(familyDirectory, "npc_records.json");
            if (File.Exists(canonical)) return [canonical];
            var executable = Directory.GetFiles(familyDirectory, "npc_records_*.json")
                .OrderByDescending(Path.GetFileName)
                .FirstOrDefault();
            if (executable is not null) return [executable];
        }

        // Items also retain MMExplorer SQL/MDB reference records beside the executable
        // canonical item catalog. Checkout timestamps are not authority; always select the
        // promoted item_records source so native flags, ordered ability slots, and uses reach
        // Data/Classic/items.json deterministically.
        if (familyName.Equals("items", StringComparison.OrdinalIgnoreCase))
        {
            var canonical = Path.Combine(familyDirectory, "item_records.json");
            if (File.Exists(canonical)) return [canonical];
            var executable = Directory.GetFiles(familyDirectory, "item_records_*.json")
                .OrderByDescending(Path.GetFileName)
                .FirstOrDefault();
            if (executable is not null) return [executable];
        }

        // Spells retain raw MMExplorer SQL/MDB evidence beside the executable canonical
        // spell projection. Clean checkouts flatten/replace authoring timestamps, so the
        // generic timestamp fallback can incorrectly select sql_mdb_records_* and export
        // mmud_sql_spell_* evidence identities instead of canonical spell.* runtime ids.
        if (familyName.Equals("spells", StringComparison.OrdinalIgnoreCase))
        {
            var canonical = Path.Combine(familyDirectory, "spell_records.json");
            if (File.Exists(canonical)) return [canonical];
            var executable = Directory.GetFiles(familyDirectory, "spell_records_*.json")
                .OrderByDescending(Path.GetFileName)
                .FirstOrDefault();
            if (executable is not null) return [executable];
        }

        // Shops have both raw SQL/MDB imports and the executable Librarium projection.
        // The raw import is newer on disk, but it is evidence input rather than runtime authority.
        if (familyName.Equals("shops", StringComparison.OrdinalIgnoreCase))
        {
            var canonical = Path.Combine(familyDirectory, "shop_records.json");
            if (File.Exists(canonical)) return [canonical];
            var executable = Directory.GetFiles(familyDirectory, "shop_records_*.json")
                .OrderByDescending(Path.GetFileName)
                .FirstOrDefault();
            if (executable is not null) return [executable];
        }

        if (familyName.Equals("loot_rules", StringComparison.OrdinalIgnoreCase))
        {
            // Preserve the broad evidence catalog and the later verified Classic
            // drop-roll contract. Selecting only the timestamp-preferred catalog
            // silently omitted the executable 1..100 rule from generated data.
            var selected = Directory.GetFiles(familyDirectory, "*.json")
                .Where(path =>
                {
                    var name = Path.GetFileName(path);
                    return name.StartsWith("loot_rule_catalog_", StringComparison.OrdinalIgnoreCase) ||
                           name.StartsWith("classic_drop_roll_rule_", StringComparison.OrdinalIgnoreCase);
                })
                .OrderBy(Path.GetFileName)
                .ToArray();
            if (selected.Length > 0) return selected;
        }

        // MMExplorer movement evidence is promoted through Librarium/Objects before generation.
        // Raw SourceImports are retained as provenance, but the compiler must prefer the
        // canonical object records so runtime data never depends on imported decoder output.
        if (familyName.Equals("rooms", StringComparison.OrdinalIgnoreCase) ||
            familyName.Equals("exits", StringComparison.OrdinalIgnoreCase))
        {
            var canonicalName = familyName.Equals("rooms", StringComparison.OrdinalIgnoreCase)
                ? "room_records.json"
                : "exit_records.json";
            var canonical = Path.Combine(familyDirectory, canonicalName);
            if (File.Exists(canonical)) return [canonical];

            var promotedPattern = familyName.Equals("rooms", StringComparison.OrdinalIgnoreCase)
                ? "mmexplorer_room_records_*.json"
                : "mmexplorer_exit_records_*.json";
            var promoted = Directory.GetFiles(familyDirectory, promotedPattern)
                .OrderByDescending(Path.GetFileName)
                .FirstOrDefault();
            if (promoted is not null) return [promoted];

            var promotedParts = Directory.GetDirectories(familyDirectory, promotedPattern.Replace("*.json", "*.parts"))
                .SelectMany(directory => Directory.GetFiles(directory, "*.json"))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (promotedParts.Length > 0) return promotedParts;

        }

        if (familyName.Equals("room_interactions", StringComparison.OrdinalIgnoreCase))
        {
            var selected = new List<string>();
            var currentPrimary = Path.Combine(familyDirectory, "room_interaction_records.json");
            var primary = File.Exists(currentPrimary)
                ? currentPrimary
                : Directory.GetFiles(familyDirectory, "room_interaction_records_*.json")
                .OrderByDescending(Path.GetFileName)
                .FirstOrDefault();
            if (primary is not null) selected.Add(primary);

            selected.AddRange(Directory.GetFiles(familyDirectory, "strongly_correlated_*_records.json")
                .OrderBy(Path.GetFileName));
            selected.AddRange(Directory.GetFiles(familyDirectory, "decoded_*_action_records.json")
                .OrderBy(Path.GetFileName));
            selected.AddRange(Directory.GetFiles(familyDirectory, "decoded_*_action_records_*.json")
                .OrderBy(Path.GetFileName));
            selected.AddRange(Directory.GetFiles(familyDirectory, "wccmmud_*_records.json")
                .OrderBy(Path.GetFileName));
            selected.AddRange(Directory.GetFiles(familyDirectory, "wccmmud_*_records_*.json")
                .OrderBy(Path.GetFileName));

            if (selected.Count > 0) return selected.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        // Room interactions are the executable MMExplorer-backed trigger slice. Prefer them over
        // older broad trigger catalogs when present so movement gates stay map-qualified.
        if (familyName.Equals("triggers", StringComparison.OrdinalIgnoreCase))
        {
            var currentInteractions = Path.Combine(familyDirectory, "mmexplorer_room_interaction_records.json");
            if (File.Exists(currentInteractions)) return [currentInteractions];

            var interactions = Directory.GetFiles(familyDirectory, "mmexplorer_room_interaction_records_*.json")
                .OrderByDescending(Path.GetFileName)
                .FirstOrDefault();
            if (interactions is not null) return [interactions];

            var executable = Directory.GetFiles(familyDirectory, "trigger_records_*.json")
                .OrderByDescending(Path.GetFileName)
                .FirstOrDefault();
            if (executable is not null) return [executable];
        }

        if (familyName.Equals("scripts", StringComparison.OrdinalIgnoreCase))
        {
            var selected = new List<string>();
            var questStages = Directory.GetFiles(familyDirectory, "mmexplorer_textblock_stage_records*.json")
                .OrderByDescending(Path.GetFileName)
                .FirstOrDefault();
            if (questStages is not null) selected.Add(questStages);
            selected.AddRange(Directory.GetFiles(familyDirectory, "npc_hit_spell_textblock_records_*.json")
                .OrderBy(Path.GetFileName));
            if (selected.Count > 0) return selected;
        }

        foreach (var pattern in PreferredRecordNames)
        {
            var match = Directory.GetFiles(familyDirectory, pattern).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
            if (match is not null) return [match];
        }
        return [];
    }

    private static IEnumerable<JsonNode?> ReadJsonCatalogParts(string path)
    {
        var json = JsonNode.Parse(File.ReadAllText(path));
        if (json is JsonObject obj && obj["record_parts"] is JsonArray parts)
        {
            foreach (var part in parts)
            {
                var relative = part?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(relative)) continue;
                var partPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, relative.Replace('/', Path.DirectorySeparatorChar)));
                yield return JsonNode.Parse(File.ReadAllText(partPath));
            }
            yield break;
        }

        yield return json;
    }

    private static IEnumerable<JsonObject> ExtractRecords(JsonNode? node)
    {
        if (node is JsonArray arr)
        {
            foreach (var entry in arr.OfType<JsonObject>()) yield return entry;
            yield break;
        }

        if (node is not JsonObject obj) yield break;

        foreach (var key in new[] { "records", "items", "objects", "catalog", "entries", "formulas", "worlds" })
        {
            if (obj[key] is JsonArray recordArray)
            {
                foreach (var entry in recordArray.OfType<JsonObject>()) yield return entry;
                yield break;
            }
        }

        if (LooksLikeRecord(obj)) yield return obj;
    }

    private static bool LooksLikeRecord(JsonObject obj)
        => obj.ContainsKey("id") || obj.ContainsKey("name") || obj.ContainsKey("object_type") || obj.ContainsKey("mechanic_id");
}

public sealed record LibrariumFamily(
    string Name,
    string SourceFile,
    IReadOnlyList<JsonObject> Records,
    IReadOnlyList<string>? SourceFiles = null);
