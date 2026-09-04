using System.Text.Json;
using System.Text.Json.Nodes;

namespace RealmGate.Compiler;

public sealed class GeneratedProjectionValidator
{
    private static readonly string[] RequiredFiles =
    [
        "world_manifest.json",
        "compile_manifest.json",
        "exits.json"
    ];

    public ProjectionValidationResult Validate(string repositoryRoot, string worldId)
    {
        var normalizedWorld = LibrariumCompilerOptions.NormalizeWorld(worldId);
        var outputRoot = Path.Combine(repositoryRoot, "Data", normalizedWorld);
        var errors = new List<string>();
        var filesValidated = 0;

        if (!Directory.Exists(outputRoot))
        {
            errors.Add($"Generated world data folder does not exist: {outputRoot}");
            return new ProjectionValidationResult(normalizedWorld, outputRoot, filesValidated, errors);
        }

        foreach (var requiredFile in RequiredFiles)
        {
            if (!File.Exists(Path.Combine(outputRoot, requiredFile)))
            {
                errors.Add($"Missing required generated projection file: {requiredFile}");
            }
        }

        foreach (var path in Directory.EnumerateFiles(outputRoot, "*.json", SearchOption.TopDirectoryOnly)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                // Validation is intentionally read-only. FileShare.ReadWrite | FileShare.Delete
                // allows verification while Windows Search, antivirus, or another reader has a
                // temporary mapped section open. The validator never rewrites generated data.
                using var stream = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                _ = JsonNode.Parse(stream, documentOptions: new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow
                });
                filesValidated++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                errors.Add($"{Path.GetFileName(path)}: {ex.Message}");
            }
        }

        ValidateManifestWorldId(Path.Combine(outputRoot, "world_manifest.json"), normalizedWorld, errors);
        return new ProjectionValidationResult(normalizedWorld, outputRoot, filesValidated, errors);
    }

    private static void ValidateManifestWorldId(string path, string expectedWorldId, List<string> errors)
    {
        if (!File.Exists(path)) return;

        try
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            var manifest = JsonNode.Parse(stream) as JsonObject;
            var actualWorldId = manifest?["world_id"]?.GetValue<string>();
            if (!string.Equals(actualWorldId, expectedWorldId, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"world_manifest.json world_id is '{actualWorldId ?? "<missing>"}', expected '{expectedWorldId}'.");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            errors.Add($"world_manifest.json manifest validation failed: {ex.Message}");
        }
    }
}

public sealed record ProjectionValidationResult(
    string WorldId,
    string OutputRoot,
    int FilesValidated,
    IReadOnlyList<string> Errors)
{
    public bool Success => Errors.Count == 0;
}
