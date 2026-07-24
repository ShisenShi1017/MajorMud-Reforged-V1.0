using System.Text.Json;
using System.Text.Json.Nodes;

namespace MajorMud.Reforged.Compiler;

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

                if (familyName.Equals("quests", StringComparison.OrdinalIgnoreCase))
                {
                    AppendDecodedQuestFlows(librariumRoot, records, diagnostics);
                }
                if (familyName.Equals("rooms", StringComparison.OrdinalIgnoreCase))
                {
                    AppendImportedClassicRoomAnchors(librariumRoot, records, recordsById, diagnostics);
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

    private static void AppendImportedClassicRoomAnchors(
        string librariumRoot,
        List<JsonObject> records,
        Dictionary<string, JsonObject> recordsById,
        List<CompilerDiagnostic> diagnostics)
    {
        // Two WCC rooms are present in the uploaded imported database and spawn evidence,
        // but were omitted from the MMExplorer room projection. Promote only those exact
        // map-qualified source records so the Classic spawn graph closes without guessing.
        foreach (var roomNumber in new[] { 288, 2306 })
        {
            var id = $"room.map1.{roomNumber}";
            if (recordsById.ContainsKey(id)) continue;

            var sourcePath = Path.Combine(
                Directory.GetParent(librariumRoot)!.FullName,
                "ImportedDatabases", "OmniVerseReference", "CodeUpdate0046",
                "OmniVerseCodexApi", "wcc", "rooms", $"room-wcc-map1-room{roomNumber}.json");
            if (!File.Exists(sourcePath))
            {
                diagnostics.Add(new(DiagnosticSeverity.Error, "LIBROOMANCHOR",
                    $"Required imported Classic room anchor was not found for {id}.", sourcePath));
                continue;
            }

            var raw = JsonNode.Parse(File.ReadAllText(sourcePath)) as JsonObject
                ?? throw new InvalidDataException($"Imported Classic room anchor is not an object: {sourcePath}");
            var name = raw["name"]?.GetValue<string>() ?? id;
            var exits = new JsonArray();
            if (raw["exits"] is JsonArray rawExits)
            {
                foreach (var exit in rawExits.OfType<JsonObject>())
                {
                    var direction = exit["dir"]?.GetValue<string>()?.Trim().ToLowerInvariant();
                    if (!string.IsNullOrWhiteSpace(direction)) exits.Add($"exit.map1.{roomNumber}.{direction}");
                }
            }

            var record = new JsonObject
            {
                ["id"] = id,
                ["legacy_id"] = roomNumber.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["name"] = name,
                ["object_type"] = "room",
                ["world_scope"] = new JsonArray("classic"),
                ["map_number"] = 1,
                ["room_number"] = roomNumber,
                ["relationships"] = new JsonObject { ["exits"] = exits },
                ["source_fields"] = new JsonObject
                {
                    ["npc_definition"] = string.Empty,
                    ["command_definition"] = string.Empty,
                    ["imported_room_type"] = raw["room_type"]?.DeepClone(),
                    ["imported_shop_number"] = raw["shop_number"]?.DeepClone(),
                    ["imported_light_level"] = raw["light_level"]?.DeepClone()
                },
                ["description"] = string.Join(" ", Enumerable.Range(1, 7)
                    .Select(index => raw[$"desc{index}"]?.GetValue<string>())
                    .Where(value => !string.IsNullOrWhiteSpace(value))),
                ["evidence_refs"] = new JsonArray(new JsonObject
                {
                    ["source"] = "Uploaded ImportedDatabases WCC room record",
                    ["path"] = Path.GetRelativePath(Directory.GetParent(librariumRoot)!.FullName, sourcePath).Replace('\\', '/'),
                    ["classification"] = "direct_imported_database_anchor"
                }),
                ["confidence"] = "verified_imported_room_record",
                ["classic_lock"] = "evidence_backed_immutable",
                ["runtime_mapping"] = new JsonObject
                {
                    ["source"] = "Librarium compiler imported-room closure",
                    ["generated_only"] = true,
                    ["runtime_reads_generated_data"] = true,
                    ["status"] = "canonical_map_qualified_record",
                    ["exports_to"] = "/Data/<world>/rooms.json"
                },
                ["generator_mapping"] = new JsonObject
                {
                    ["compiler_stage"] = "librarium_to_data",
                    ["target_worlds"] = new JsonArray("classic"),
                    ["output_collection"] = "rooms",
                    ["requires_validation"] = true
                },
                ["validation"] = new JsonArray(
                    "unique_map_qualified_id", "map_number_required", "room_number_required",
                    "evidence_required_for_classic", "imported_spawn_room_anchor")
            };
            records.Add(record);
            recordsById[id] = record;
        }
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

    private static IReadOnlyList<string> SelectSourceFiles(string familyDirectory, string familyName)
    {
        // Shops have both raw SQL/MDB imports and the executable Librarium projection.
        // The raw import is newer on disk, but it is evidence input rather than runtime authority.
        if (familyName.Equals("shops", StringComparison.OrdinalIgnoreCase))
        {
            var executable = Directory.GetFiles(familyDirectory, "shop_records_*.json")
                .OrderByDescending(Path.GetFileName)
                .FirstOrDefault();
            if (executable is not null) return [executable];
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
            if (promoted is not null) return [promoted];

            var promotedParts = Directory.GetDirectories(familyDirectory, promotedPattern.Replace("*.json", "*.parts"))
                .SelectMany(directory => Directory.GetFiles(directory, "*.json"))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (promotedParts.Length > 0) return promotedParts;

            var librariumRoot = Directory.GetParent(Directory.GetParent(familyDirectory)!.FullName)!.FullName;
            var decodedRoot = Path.Combine(librariumRoot, "SourceImports", "MMExplorerSQLData", "DecodedRooms0138");
            var decoded = Path.Combine(decodedRoot,
                familyName.Equals("rooms", StringComparison.OrdinalIgnoreCase) ? "rooms_decoded.json" : "exits_decoded.json");
            if (File.Exists(decoded)) return [decoded];
        }

        if (familyName.Equals("room_interactions", StringComparison.OrdinalIgnoreCase))
        {
            var selected = new List<string>();
            var primary = Directory.GetFiles(familyDirectory, "room_interaction_records_*.json")
                .OrderByDescending(Path.GetFileName)
                .FirstOrDefault();
            if (primary is not null) selected.Add(primary);

            selected.AddRange(Directory.GetFiles(familyDirectory, "strongly_correlated_*_records.json")
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
