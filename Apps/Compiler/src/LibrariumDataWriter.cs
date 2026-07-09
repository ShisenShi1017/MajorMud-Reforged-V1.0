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
            var output = new JsonObject
            {
                ["generated_by"] = "MajorMud.Reforged.Compiler",
                ["compiler_code_update"] = "0127",
                ["world_id"] = result.WorldId,
                ["family"] = family.Name,
                ["source_file"] = Path.GetRelativePath(options.RepositoryRoot, family.SourceFile).Replace('\\', '/'),
                ["record_count"] = family.Records.Count,
                ["records"] = new JsonArray(family.Records.Select(r => r.DeepClone()).ToArray())
            };

            var fileName = family.Name.Replace('-', '_').ToLowerInvariant() + ".json";
            WriteJson(Path.Combine(options.OutputRoot, fileName), output);
            result.FilesWritten++;
            result.ObjectFamiliesCompiled++;
            result.ObjectsCompiled += family.Records.Count;
        }

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
        => new()
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

    private static void WriteJson(string path, JsonNode node)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, node.ToJsonString(JsonOptions));
    }
}
