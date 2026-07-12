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


    private static JsonObject CreateRuntimeFamilyProjection(
        LibrariumCompilerOptions options,
        CompileResult result,
        LibrariumFamily family)
    {
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

        return new JsonObject
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
        File.WriteAllText(path, node.ToJsonString(JsonOptions));
    }
}
