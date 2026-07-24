using System.Text.Json;
using System.Text.Json.Nodes;

namespace MajorMud.Reforged.Compiler;

public sealed class LibrariumDataWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public CompileResult Compile(LibrariumCompilerOptions options)
    {
        var result = new CompileResult { WorldId = LibrariumCompilerOptions.NormalizeWorld(options.WorldId), OutputRoot = options.OutputRoot };
        var reader = new LibrariumCatalogReader();
        var families = reader.ReadFamilies(options.LibrariumRoot, result.Diagnostics);
        ApplyCrossFamilyRuntimeGuards(families);
        new LibrariumValidator().Validate(families, result.Diagnostics);

        if (!result.Success) return result;

        if (options.Clean && Directory.Exists(options.OutputRoot)) Directory.Delete(options.OutputRoot, recursive: true);
        Directory.CreateDirectory(options.OutputRoot);

        foreach (var family in families)
        {
            var output = CreateRuntimeFamilyProjection(options, result, family);

            var fileName = family.Name.Replace('-', '_').ToLowerInvariant() + ".json";
            WriteJson(Path.Combine(options.OutputRoot, fileName), output);
            result.FilesWritten++;
            result.ObjectFamiliesCompiled++;
            result.ObjectsCompiled += family.Records.Count;
        }

        result.FilesWritten += RuntimeItemProjectionWriter.Write(options, families, WriteJson);

        if (options.ClientProjection)
        {
            WriteJson(Path.Combine(options.OutputRoot, "client_projection.json"), CreateClientProjection(result, families));
            result.FilesWritten++;
        }

        WriteJson(Path.Combine(options.OutputRoot, "world_manifest.json"), CreateWorldManifest(result, families));
        result.FilesWritten++;

        WriteJson(Path.Combine(options.OutputRoot, "compile_manifest.json"), CreateManifest(result, families));
        result.FilesWritten++;
        return result;
    }

    private static void ApplyCrossFamilyRuntimeGuards(IReadOnlyList<LibrariumFamily> families)
    {
        var rooms = families.FirstOrDefault(f => f.Name.Equals("rooms", StringComparison.OrdinalIgnoreCase));
        var exits = families.FirstOrDefault(f => f.Name.Equals("exits", StringComparison.OrdinalIgnoreCase));
        if (rooms is null || exits is null) return;

        var roomIds = rooms.Records
            .Select(record => record["id"]?.GetValue<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var exit in exits.Records)
        {
            var targetId = ReadExitTargetRoomId(exit);
            if (string.IsNullOrWhiteSpace(targetId) || roomIds.Contains(targetId)) continue;

            exit["runtime_disposition"] = "invalid_destination_blocked";
            exit["semantic_status"] = "blocked_unresolved_destination";
            exit["fallback_behavior"] = "deny traversal because generated target room is not present";
            exit["generator_validation"] = new JsonObject
            {
                ["status"] = "blocked_unresolved_destination",
                ["target_room_id"] = targetId,
                ["policy"] = "compiler cross-family validation prevents unresolved exits from becoming executable traversal"
            };

            if (exit["runtime_mapping"] is JsonObject runtimeMapping)
            {
                runtimeMapping["status"] = "blocked_unresolved_destination";
            }
        }
    }

    private static string? ReadExitTargetRoomId(JsonObject exit)
    {
        if (exit["relationships"] is JsonObject relationships)
        {
            var relationshipTarget = relationships["to_room"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(relationshipTarget)) return relationshipTarget;
        }

        var map = ReadInt(exit, "to_map_number");
        var room = ReadInt(exit, "to_room_number");
        return map > 0 && room > 0 ? $"room.map{map}.{room}" : null;
    }


    private static JsonObject CreateRuntimeFamilyProjection(
        LibrariumCompilerOptions options,
        CompileResult result,
        LibrariumFamily family)
    {
        if (family.Name.Equals("rooms", StringComparison.OrdinalIgnoreCase))
        {
            var audit = new NativeMapRoomTextResolver().ApplyDescriptions(options.RepositoryRoot, family);
            result.Diagnostics.Add(new(
                DiagnosticSeverity.Info,
                "ROOMTEXT001",
                $"Native room text recovery emitted {audit.EmittedNonEmptyDescriptions:N0}/{audit.SourceRooms:N0} non-empty descriptions; unresolved {audit.UnresolvedReferences:N0}; missing native map files {audit.MissingNativeMapFiles.Count:N0}.",
                family.SourceFile));
        }

        if (family.Name.Equals("character_foundation", StringComparison.OrdinalIgnoreCase))
        {
            var promoted = TryReadPromotedCharacterFoundation(family);
            if (promoted is not null)
            {
                promoted["generated_by"] = "MajorMud.Reforged.Compiler";
                promoted["compiler_code_update"] = "0138";
                promoted["world_id"] = result.WorldId;
                promoted["family"] = family.Name;
                promoted["source_catalog_file"] = Path.GetRelativePath(options.RepositoryRoot, family.SourceFile).Replace('\\', '/');
                return promoted;
            }
        }

        var projection = new JsonObject
        {
            ["authority"] = "Librarium",
            ["generated_by"] = "MajorMud.Reforged.Compiler",
            ["compiler_code_update"] = "0127",
            ["world_id"] = result.WorldId,
            ["family"] = family.Name,
            ["source_file"] = Path.GetRelativePath(options.RepositoryRoot, family.SourceFile).Replace('\\', '/'),
            ["record_count"] = family.Records.Count,
            ["records"] = new JsonArray(family.Records.Select(r => r.DeepClone()).ToArray())
        };

        var sourceFiles = family.SourceFiles ?? [family.SourceFile];
        if (sourceFiles.Count > 1)
        {
            projection["source_files"] = new JsonArray(sourceFiles
                .Select(path => JsonValue.Create(Path.GetRelativePath(options.RepositoryRoot, path).Replace('\\', '/')))
                .ToArray());
        }

        return projection;
    }

    private static JsonObject? TryReadPromotedCharacterFoundation(LibrariumFamily family)
    {
        foreach (var record in family.Records)
        {
            var sourceFile = record["source_file"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(sourceFile)) continue;

            var sourcePath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(family.SourceFile)!, sourceFile));
            if (!File.Exists(sourcePath)) continue;

            try
            {
                var source = JsonNode.Parse(File.ReadAllText(sourcePath)) as JsonObject;
                if (source is null) continue;
                if (source["races"] is not JsonArray || source["classes"] is not JsonArray) continue;
                return (JsonObject)source.DeepClone();
            }
            catch (JsonException)
            {
                // Librarium validation owns malformed JSON diagnostics.
            }
        }

        return null;
    }

    private static JsonObject CreateManifest(CompileResult result, IReadOnlyList<LibrariumFamily> families)
        => new()
        {
            ["code_update"] = "0127",
            ["compiler"] = "MajorMud.Reforged.Compiler",
            ["world_id"] = result.WorldId,
            ["librarium_authority"] = "Librarium is source of truth; /Data is generated runtime output.",
            ["object_families_compiled"] = result.ObjectFamiliesCompiled,
            ["objects_compiled"] = result.ObjectsCompiled,
            ["files_written"] = result.FilesWritten + 1,
            ["families"] = new JsonArray(families.Select(f => new JsonObject
            {
                ["family"] = f.Name,
                ["source_file"] = f.SourceFile.Replace('\\', '/'),
                ["source_files"] = new JsonArray((f.SourceFiles ?? [f.SourceFile])
                    .Select(path => JsonValue.Create(path.Replace('\\', '/')))
                    .ToArray()),
                ["record_count"] = f.Records.Count
            }).ToArray()),
            ["diagnostics"] = new JsonArray(result.Diagnostics.Select(d => new JsonObject
            {
                ["severity"] = d.Severity.ToString(),
                ["code"] = d.Code,
                ["message"] = d.Message,
                ["path"] = d.Path
            }).ToArray())
        };

    private static JsonObject CreateClientProjection(CompileResult result, IReadOnlyList<LibrariumFamily> families)
        => new()
        {
            ["world_id"] = result.WorldId,
            ["projection"] = "read_only_client_librarium_projection_v1",
            ["families_available"] = new JsonArray(families.Select(f => JsonValue.Create(f.Name)).ToArray()),
            ["client_rule"] = "Client reads world-scoped Librarium/Data projections only; client never edits gameplay definitions."
        };

    private static JsonObject CreateWorldManifest(CompileResult result, IReadOnlyList<LibrariumFamily> families)
    {
        var manifest = new JsonObject
        {
            ["schema"] = "generated_world_manifest_v1",
            ["code_update"] = "0127",
            ["world_id"] = result.WorldId,
            ["output_root"] = result.OutputRoot.Replace('\\', '/'),
            ["object_families_compiled"] = result.ObjectFamiliesCompiled,
            ["objects_compiled"] = result.ObjectsCompiled,
            ["families"] = new JsonArray(families.Select(f => JsonValue.Create(f.Name)).ToArray()),
            ["runtime_loader_contract"] = "Librarium/RuntimeContracts/generated_world_loader_contract_0127.json",
            ["source_of_truth"] = "Librarium",
            ["generated_output"] = true
        };

        var characterStart = TryReadCharacterStart(families);
        if (characterStart is not null)
        {
            manifest["character_start_room"] = characterStart.RoomId;
            manifest["character_spawning"] = new JsonObject
            {
                ["start_room"] = characterStart.RoomId,
                ["map_id"] = characterStart.MapId,
                ["room_number"] = characterStart.RoomNumber,
                ["name"] = characterStart.Name,
                ["classification"] = "Librarium_promoted_classic_character_start"
            };
        }

        return manifest;
    }

    private static CharacterStart? TryReadCharacterStart(IReadOnlyList<LibrariumFamily> families)
    {
        var family = families.FirstOrDefault(f =>
            f.Name.Equals("character_foundation", StringComparison.OrdinalIgnoreCase));
        if (family is null) return null;

        foreach (var record in family.Records)
        {
            var sourceFile = record["source_file"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(sourceFile)) continue;

            var sourcePath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(family.SourceFile)!, sourceFile));
            if (!File.Exists(sourcePath)) continue;

            try
            {
                var source = JsonNode.Parse(File.ReadAllText(sourcePath)) as JsonObject;
                var start = source?["character_creation_rules"]?["new_character_start"] as JsonObject;
                if (start is null) continue;

                var mapId = ReadInt(start, "map_id");
                var roomNumber = ReadInt(start, "room_number");
                var roomId = start["room_id"]?.GetValue<string>();
                var name = start["name"]?.GetValue<string>() ?? string.Empty;

                if (mapId <= 0 || roomNumber <= 0) continue;
                if (string.IsNullOrWhiteSpace(roomId)) roomId = $"room.map{mapId}.{roomNumber}";

                return new CharacterStart(roomId, mapId, roomNumber, name);
            }
            catch (JsonException)
            {
                // Librarium validation reports malformed source JSON. The manifest writer
                // remains deterministic and simply omits an unresolvable start location.
            }
        }

        return null;
    }

    private static int ReadInt(JsonObject obj, string key)
    {
        var value = obj[key];
        if (value is null) return 0;
        if (value is JsonValue jsonValue)
        {
            if (jsonValue.TryGetValue<int>(out var number)) return number;
            if (jsonValue.TryGetValue<string>(out var text) && int.TryParse(text, out number)) return number;
        }
        return 0;
    }

    private sealed record CharacterStart(string RoomId, int MapId, int RoomNumber, string Name);

    private static void WriteJson(string path, JsonNode node)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var serialized = SerializeJson(node);
        if (serialized.Length < 90_000_000 || node is not JsonObject obj || obj["records"] is not JsonArray records)
        {
            File.WriteAllText(path, serialized);
            return;
        }

        var familyName = Path.GetFileNameWithoutExtension(path);
        var partDirectoryName = familyName + ".parts";
        var partDirectory = Path.Combine(Path.GetDirectoryName(path)!, partDirectoryName);
        if (Directory.Exists(partDirectory)) Directory.Delete(partDirectory, recursive: true);
        Directory.CreateDirectory(partDirectory);

        var partFiles = new JsonArray();
        var partRecords = new JsonArray();
        var partIndex = 1;
        foreach (var record in records.Select(record => record?.DeepClone()).ToArray())
        {
            partRecords.Add(record);
            if (partRecords.Count < 5_000) continue;
            WritePart(path, obj, partDirectoryName, partDirectory, partIndex++, partRecords, partFiles);
            partRecords = new JsonArray();
        }

        if (partRecords.Count > 0)
        {
            WritePart(path, obj, partDirectoryName, partDirectory, partIndex, partRecords, partFiles);
        }

        var manifest = new JsonObject();
        foreach (var property in obj)
        {
            if (property.Key.Equals("records", StringComparison.OrdinalIgnoreCase)) continue;
            manifest[property.Key] = property.Value?.DeepClone();
        }

        manifest["chunked_records"] = true;
        manifest["record_parts"] = partFiles;
        WriteJsonFile(path, manifest);
    }

    private static void WritePart(
        string originalPath,
        JsonObject original,
        string partDirectoryName,
        string partDirectory,
        int partIndex,
        JsonArray partRecords,
        JsonArray partFiles)
    {
        var fileName = $"{Path.GetFileNameWithoutExtension(originalPath)}.part{partIndex:0000}.json";
        var part = new JsonObject
        {
            ["authority"] = original["authority"]?.DeepClone(),
            ["generated_by"] = original["generated_by"]?.DeepClone(),
            ["world_id"] = original["world_id"]?.DeepClone(),
            ["family"] = original["family"]?.DeepClone(),
            ["record_count"] = partRecords.Count,
            ["records"] = partRecords
        };
        WriteJsonFile(Path.Combine(partDirectory, fileName), part);
        partFiles.Add($"{partDirectoryName}/{fileName}");
    }

    private static string SerializeJson(JsonNode node)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = JsonOptions.WriteIndented }))
        {
            node.WriteTo(writer);
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteJsonFile(string path, JsonNode node)
    {
        using var stream = File.Create(path);
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = JsonOptions.WriteIndented });
        node.WriteTo(writer);
    }
}
