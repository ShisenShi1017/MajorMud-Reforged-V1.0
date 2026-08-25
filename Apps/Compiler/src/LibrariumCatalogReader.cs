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
                    EnrichRoomsWithDecodedEnvironment(librariumRoot, records, recordsById, diagnostics);
                    AppendImportedClassicRoomAnchors(librariumRoot, records, recordsById, diagnostics);
                }
                if (familyName.Equals("race_descriptions", StringComparison.OrdinalIgnoreCase))
                {
                    AttachNativeRaceDescriptionPayloads(librariumRoot, records, diagnostics);
                }
                if (familyName.Equals("gang_houses", StringComparison.OrdinalIgnoreCase))
                {
                    AttachNativeGangHouseInteriorLocations(librariumRoot, records, diagnostics);
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

    private static void AttachNativeGangHouseInteriorLocations(
        string librariumRoot,
        IReadOnlyList<JsonObject> records,
        List<CompilerDiagnostic> diagnostics)
    {
        var sourceRoot = Path.Combine(Directory.GetParent(librariumRoot)!.FullName,
            "ImportedDatabases", "WCCDefaultInstall", "wcc default");
        var paths = Directory.Exists(sourceRoot)
            ? Directory.EnumerateFiles(sourceRoot, "WCC*.HSE", SearchOption.TopDirectoryOnly).OrderBy(Path.GetFileName).ToArray()
            : [];
        if (paths.Length == 0)
        {
            var existingLocations = records
                .SelectMany(record => (record["locations"] as JsonArray)?.OfType<JsonObject>() ?? [])
                .Count(location => location["is_gang_house"]?.GetValue<bool>() == true);
            if (existingLocations > 0)
            {
                diagnostics.Add(new(DiagnosticSeverity.Info, "LIBGANGHOUSE",
                    $"Native WCC gang-house HSE files are archived locally; using {existingLocations:N0} promoted Librarium locations.", sourceRoot));
                return;
            }

            diagnostics.Add(new(DiagnosticSeverity.Error, "LIBGANGHOUSE",
                "Native WCC gang-house interior description files were not found and no promoted Librarium locations exist.", sourceRoot));
            return;
        }

        var byColor = records.Where(record => record["color"] is not null)
            .ToDictionary(record => record["color"]!.GetValue<string>(), StringComparer.OrdinalIgnoreCase);
        var occupied = records.SelectMany(record => (record["locations"] as JsonArray)?.OfType<JsonObject>() ?? [])
            .Select(location => location["location_id"]?.GetValue<string>() ?? string.Empty)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var attached = 0;
        foreach (var path in paths)
        {
            var stem = Path.GetFileNameWithoutExtension(path);
            if (stem.Length < 7 || !int.TryParse(stem[3..^2], out var roomNumber) || !int.TryParse(stem[^2..], out var mapId))
            {
                diagnostics.Add(new(DiagnosticSeverity.Warning, "LIBGANGHOUSE",
                    $"Native gang-house filename could not be decoded: {Path.GetFileName(path)}", path));
                continue;
            }
            var title = File.ReadLines(path).FirstOrDefault()?.Trim() ?? string.Empty;
            var words = title.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var houseIndex = Array.FindIndex(words, word => word.Equals("House", StringComparison.OrdinalIgnoreCase));
            if (houseIndex < 1 || !byColor.TryGetValue(words[houseIndex - 1], out var owner))
            {
                diagnostics.Add(new(DiagnosticSeverity.Warning, "LIBGANGHOUSE",
                    $"Native gang-house title could not be assigned: {title}", path));
                continue;
            }
            var locationId = $"room.map{mapId}.{roomNumber}";
            if (!occupied.Add(locationId)) continue;
            var kind = words.Length == 0 ? "interior" : words[0].ToLowerInvariant() switch
            {
                "entrance" => "entrance",
                "normal" => "interior",
                "rest" => "rest_room",
                "vault" => "vault",
                "shop" => "supply_shop",
                _ => "interior"
            };
            var locations = owner["locations"] as JsonArray ?? new JsonArray();
            owner["locations"] = locations;
            locations.Add(new JsonObject
            {
                ["location_id"] = locationId,
                ["room_id"] = locationId,
                ["map_id"] = mapId,
                ["room_number"] = roomNumber,
                ["room_name"] = title,
                ["location_kind"] = kind,
                ["shop_id"] = string.Empty,
                ["legacy_shop_number"] = 0,
                ["is_gang_house"] = true,
                ["is_vault"] = kind == "vault",
                ["is_gang_house_shop"] = kind == "supply_shop",
                ["evidence"] = $"ImportedDatabases/WCCDefaultInstall/wcc default/{Path.GetFileName(path)}"
            });
            attached++;
        }
        if (attached == 0)
            diagnostics.Add(new(DiagnosticSeverity.Error, "LIBGANGHOUSE",
                "No native WCC gang-house interior locations were attached.", sourceRoot));
    }

    private static void AttachNativeRaceDescriptionPayloads(
        string librariumRoot,
        IReadOnlyList<JsonObject> records,
        List<CompilerDiagnostic> diagnostics)
    {
        var evidencePath = Path.Combine(librariumRoot, "Evidence", "native_message_catalog_correlation_0022.json");
        if (!File.Exists(evidencePath))
        {
            diagnostics.Add(new(DiagnosticSeverity.Error, "LIBRACEDESC",
                "Required native race-description evidence was not found.", evidencePath));
            return;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(evidencePath));
        var entries = document.RootElement.GetProperty("catalogs").EnumerateArray()
            .Where(catalog => string.Equals(catalog.GetProperty("mcv_file").GetString(), "WCCMMHLP.mcv", StringComparison.OrdinalIgnoreCase))
            .SelectMany(catalog => catalog.GetProperty("entries").EnumerateArray())
            .ToDictionary(entry => entry.GetProperty("symbol").GetString() ?? string.Empty, entry => entry.Clone(), StringComparer.OrdinalIgnoreCase);

        foreach (var record in records)
        {
            var symbol = record["native_symbol"]?.GetValue<string>() ?? string.Empty;
            if (!entries.TryGetValue(symbol, out var entry))
            {
                diagnostics.Add(new(DiagnosticSeverity.Error, "LIBRACEDESC",
                    $"Native race-description symbol '{symbol}' was not found.", evidencePath));
                continue;
            }

            var expectedNumber = record["native_message_number"]?.GetValue<int>() ?? -1;
            var actualNumber = entry.GetProperty("message_number").GetInt32();
            var expectedHash = record["native_sha256"]?.GetValue<string>() ?? string.Empty;
            var actualHash = entry.GetProperty("sha256").GetString() ?? string.Empty;
            if (expectedNumber != actualNumber ||
                (expectedHash.Length > 0 && !string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase)))
            {
                diagnostics.Add(new(DiagnosticSeverity.Error, "LIBRACEDESC",
                    $"Native race-description evidence mismatch for '{symbol}'.", evidencePath));
                continue;
            }

            record["description"] = entry.GetProperty("decoded_cp437").GetString() ?? string.Empty;
            record["source_file"] = "WCCMMHLP.mcv";
            record["native_sha256"] = actualHash;
        }
    }

    private static void EnrichRoomsWithDecodedEnvironment(
        string librariumRoot,
        List<JsonObject> records,
        Dictionary<string, JsonObject> recordsById,
        List<CompilerDiagnostic> diagnostics)
    {
        var path = Path.Combine(
            librariumRoot, "SourceImports", "MMExplorerSQLData", "DecodedRooms0138", "rooms_decoded.json");
        if (!File.Exists(path))
        {
            diagnostics.Add(new(DiagnosticSeverity.Error, "LIBROOMENV",
                "Required decoded Classic room environment source was not found.", path));
            return;
        }

        var enriched = 0;
        foreach (var source in ReadJsonCatalogParts(path).SelectMany(ExtractRecords))
        {
            var id = source["id"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(id) || !recordsById.TryGetValue(id, out var room)) continue;

            room["environment"] = new JsonObject
            {
                ["light_level"] = source["light"]?.DeepClone() ?? 0,
                ["shop_id"] = source["shop_id"]?.DeepClone() ?? 0,
                ["room_spell_id"] = source["spell_id"]?.DeepClone() ?? 0,
                ["room_lair_id"] = source["lair_id"]?.DeepClone() ?? 0,
                ["room_delay"] = source["delay"]?.DeepClone() ?? 0,
                ["source_classification"] = "decoded_mmexplorer_room_record"
            };
            enriched++;
        }

        var promotedPath = Path.Combine(
            librariumRoot, "Objects", "rooms", "wcc_room_environment_records_0139.json");
        if (!File.Exists(promotedPath))
        {
            diagnostics.Add(new(DiagnosticSeverity.Error, "LIBWCCROOMENV",
                "Promoted decoded WCC room environment records were not found.", promotedPath));
            return;
        }

        foreach (var source in ReadJsonCatalogParts(promotedPath).SelectMany(ExtractRecords))
        {
            var id = source["id"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(id) || !recordsById.TryGetValue(id, out var room)) continue;
            var environment = room["environment"] as JsonObject ?? new JsonObject();
            room["environment"] = environment;
            foreach (var field in new[]
                     {
                         "room_type", "light_level", "room_lair_id", "attributes", "death_room_id",
                         "gang_house", "respawn_minutes", "max_area_cap", "linked_spawn_room",
                         "source_classification", "evidence_path"
                     })
            {
                if (source[field] is not null) environment[field] = source[field]!.DeepClone();
            }
        }

        if (enriched != recordsById.Count)
        {
            diagnostics.Add(new(DiagnosticSeverity.Warning, "LIBROOMENVCOVERAGE",
                $"Decoded room environment enriched {enriched:N0}/{recordsById.Count:N0} canonical rooms; imported WCC anchors are handled separately.",
                path));
        }
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
        var promotedAnchorPath = Path.Combine(librariumRoot, "Objects", "rooms", "imported_room_anchor_records.json");
        if (File.Exists(promotedAnchorPath))
        {
            MergeRecords(records, recordsById,
                ReadJsonCatalogParts(promotedAnchorPath).SelectMany(ExtractRecords));
        }

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
                ["environment"] = new JsonObject
                {
                    ["light_level"] = raw["light_level"]?.DeepClone() ?? 0,
                    ["shop_id"] = raw["shop_number"]?.DeepClone() ?? 0,
                    ["room_spell_id"] = 0,
                    ["room_lair_id"] = raw["monster_type"]?.DeepClone() ?? 0,
                    ["room_delay"] = 0,
                    ["room_type"] = raw["room_type"]?.DeepClone() ?? 0,
                    ["attributes"] = raw["attributes"]?.DeepClone() ?? 0,
                    ["death_room_id"] = raw["death_room_id"]?.DeepClone() ?? 0,
                    ["gang_house"] = raw["gang_house"]?.DeepClone() ?? 0,
                    ["respawn_minutes"] = raw["respawn_minutes"]?.DeepClone() ?? 0,
                    ["max_area_cap"] = raw["max_area_cap"]?.DeepClone() ?? 0,
                    ["linked_spawn_room"] = raw["linked_spawn_room"]?.DeepClone() ?? 0,
                    ["source_classification"] = "decoded_imported_wcc_room_record_anchor"
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
        // NPCs retain MMExplorer SQL/MDB rows as reference evidence beside the
        // executable canonical projection. Worktree checkout timestamps are not
        // a valid authority signal, so always select the canonical NPC records.
        if (familyName.Equals("npcs", StringComparison.OrdinalIgnoreCase))
        {
            var executable = Directory.GetFiles(familyDirectory, "npc_records_*.json")
                .OrderByDescending(Path.GetFileName)
                .FirstOrDefault();
            if (executable is not null) return [executable];
        }

        // Shops have both raw SQL/MDB imports and the executable Librarium projection.
        // The raw import is newer on disk, but it is evidence input rather than runtime authority.
        if (familyName.Equals("shops", StringComparison.OrdinalIgnoreCase))
        {
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
            var selected = new[]
                {
                    Path.Combine(familyDirectory, "loot_rule_catalog_0121.json"),
                    Path.Combine(familyDirectory, "classic_drop_roll_rule_0136.json")
                }
                .Where(File.Exists)
                .ToArray();
            if (selected.Length > 0) return selected;
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
