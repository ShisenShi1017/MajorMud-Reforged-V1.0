using System.Text.Json;
using System.Text.Json.Nodes;

namespace MajorMud.Reforged.Compiler;

public sealed class LibrariumCatalogReader
{
    private static readonly string[] PreferredRecordNames =
    [
        "*_records_*.json",
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
            var selected = SelectSourceFile(familyDirectory, familyName);
            if (selected is null)
            {
                diagnostics.Add(new(DiagnosticSeverity.Warning, "LIB002", $"No catalog file found for family '{familyName}'.", familyDirectory));
                continue;
            }

            try
            {
                var json = JsonNode.Parse(File.ReadAllText(selected));
                var records = ExtractRecords(json).ToList();
                if (familyName.Equals("quests", StringComparison.OrdinalIgnoreCase))
                {
                    AppendDecodedQuestFlows(librariumRoot, records, diagnostics);
                }
                families.Add(new LibrariumFamily(familyName, selected, records));
            }
            catch (Exception ex)
            {
                diagnostics.Add(new(DiagnosticSeverity.Error, "LIB003", $"Could not read family '{familyName}': {ex.Message}", selected));
            }
        }

        return families;
    }

    private static void AppendDecodedQuestFlows(string librariumRoot, List<JsonObject> records, List<CompilerDiagnostic> diagnostics)
    {
        var decodedRoot = Path.Combine(librariumRoot, "SourceImports", "MMExplorerSQLData");
        var sourceDirectory = Directory.Exists(decodedRoot)
            ? Directory.GetDirectories(decodedRoot, "TextblocksDecoded*")
                .OrderByDescending(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(directory => File.Exists(Path.Combine(directory, "quest_flows_decoded.json")))
            : null;
        if (sourceDirectory is null) return;

        var path = Path.Combine(sourceDirectory, "quest_flows_decoded.json");
        try
        {
            var decoded = JsonNode.Parse(File.ReadAllText(path)) as JsonArray;
            if (decoded is null) return;

            var recordsById = records
                .Where(record => !string.IsNullOrWhiteSpace(record["id"]?.GetValue<string>()))
                .ToDictionary(record => record["id"]!.GetValue<string>(), StringComparer.OrdinalIgnoreCase);

            foreach (var entry in decoded.OfType<JsonObject>())
            {
                var id = entry["id"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(id)) continue;

                JsonObject promoted;
                if (recordsById.TryGetValue(id, out var existing))
                {
                    promoted = existing;
                    foreach (var property in entry)
                        promoted[property.Key] = property.Value?.DeepClone();
                }
                else
                {
                    promoted = (JsonObject)entry.DeepClone();
                    records.Add(promoted);
                    recordsById[id] = promoted;
                }

                promoted["name"] ??= promoted["quest_flag_name"]?.GetValue<string>() ?? id;
                promoted["object_type"] = "quest";
                promoted["classification"] = "npc_textblock_quest_flow";
                promoted["world_scope"] ??= new JsonArray("classic");

                if (promoted["script_flow"] is JsonObject scriptFlow)
                    scriptFlow["execution_status"] = "authoritative_script_graph";

                promoted["runtime_mapping"] = new JsonObject
                {
                    ["status"] = "executable",
                    ["exports_to"] = "/Data/<world>/quests.json",
                    ["source"] = Path.GetRelativePath(librariumRoot, path).Replace('\\', '/'),
                    ["generated_only"] = true,
                    ["runtime_reads_generated_data"] = true
                };
            }
        }
        catch (Exception ex)
        {
            diagnostics.Add(new(DiagnosticSeverity.Error, "LIBQUEST", $"Could not promote decoded quest flows: {ex.Message}", path));
        }
    }

    private static string? SelectSourceFile(string familyDirectory, string familyName)
    {
        // Shops have both raw SQL/MDB imports and the executable Librarium projection.
        // The raw import is newer on disk, but it is evidence input rather than runtime authority.
        if (familyName.Equals("shops", StringComparison.OrdinalIgnoreCase))
        {
            var executable = Directory.GetFiles(familyDirectory, "shop_records_*.json")
                .OrderByDescending(Path.GetFileName)
                .FirstOrDefault();
            if (executable is not null) return executable;
        }

        // MMExplorer movement evidence is promoted through Librarium/Objects before generation.
        // Raw SourceImports are retained as provenance, but the compiler must prefer the
        // canonical object records so runtime data never depends on imported decoder output.
        if (familyName.Equals("rooms", StringComparison.OrdinalIgnoreCase) ||
            familyName.Equals("exits", StringComparison.OrdinalIgnoreCase))
        {
            var promotedPattern = familyName.Equals("rooms", StringComparison.OrdinalIgnoreCase)
                ? "mmexplorer_room_records_*.json"
                : "mmexplorer_exit_records_*.json";
            var promoted = Directory.GetFiles(familyDirectory, promotedPattern)
                .OrderByDescending(Path.GetFileName)
                .FirstOrDefault();
            if (promoted is not null) return promoted;

            var librariumRoot = Directory.GetParent(Directory.GetParent(familyDirectory)!.FullName)!.FullName;
            var decodedRoot = Path.Combine(librariumRoot, "SourceImports", "MMExplorerSQLData", "DecodedRooms0138");
            var decoded = Path.Combine(decodedRoot,
                familyName.Equals("rooms", StringComparison.OrdinalIgnoreCase) ? "rooms_decoded.json" : "exits_decoded.json");
            if (File.Exists(decoded)) return decoded;
        }

        // Room interactions are the executable MMExplorer-backed trigger slice. Prefer them over
        // older broad trigger catalogs when present so movement gates stay map-qualified.
        if (familyName.Equals("triggers", StringComparison.OrdinalIgnoreCase))
        {
            var interactions = Directory.GetFiles(familyDirectory, "mmexplorer_room_interaction_records_*.json")
                .OrderByDescending(Path.GetFileName)
                .FirstOrDefault();
            if (interactions is not null) return interactions;

            var executable = Directory.GetFiles(familyDirectory, "trigger_records_*.json")
                .OrderByDescending(Path.GetFileName)
                .FirstOrDefault();
            if (executable is not null) return executable;
        }

        foreach (var pattern in PreferredRecordNames)
        {
            var match = Directory.GetFiles(familyDirectory, pattern).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
            if (match is not null) return match;
        }
        return null;
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

public sealed record LibrariumFamily(string Name, string SourceFile, IReadOnlyList<JsonObject> Records);
