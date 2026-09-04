using System.Text.Json;
using System.Text.Json.Nodes;

namespace RealmGate.Compiler;

/// <summary>
/// Applies host-authored, world-scoped differences on top of the canonical Librarium projection.
/// Classic never consumes these files. Generated /Data remains disposable output.
/// </summary>
public sealed class WorldOverrideEngine
{
    public IReadOnlyList<LibrariumFamily> Apply(
        string repositoryRoot,
        string worldId,
        IReadOnlyList<LibrariumFamily> families,
        List<CompilerDiagnostic> diagnostics)
    {
        if (worldId.Equals("Classic", StringComparison.OrdinalIgnoreCase)) return families;

        var path = ResolveOverridePath(repositoryRoot, worldId);
        if (!File.Exists(path)) return families;

        JsonObject root;
        try
        {
            root = JsonNode.Parse(File.ReadAllText(path)) as JsonObject
                   ?? throw new JsonException("Override root must be a JSON object.");
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            diagnostics.Add(new(DiagnosticSeverity.Error, "WORLDOVR001", $"Could not read world overrides: {ex.Message}", path));
            return families;
        }

        var declaredWorld = root["world_id"]?.GetValue<string>()?.Trim() ?? string.Empty;
        if (declaredWorld.Length > 0 && !declaredWorld.Equals(worldId, StringComparison.OrdinalIgnoreCase))
        {
            diagnostics.Add(new(DiagnosticSeverity.Error, "WORLDOVR002", $"Override file world_id '{declaredWorld}' does not match compiler world '{worldId}'.", path));
            return families;
        }

        if (root["overrides"] is not JsonArray overrides || overrides.Count == 0)
        {
            diagnostics.Add(new(DiagnosticSeverity.Info, "WORLDOVR000", $"World '{worldId}' has no authored object overrides yet.", path));
            return families;
        }

        var mutable = families.ToDictionary(
            family => family.Name,
            family => new MutableFamily(family, family.Records.Select(record => (JsonObject)record.DeepClone()).ToList()),
            StringComparer.OrdinalIgnoreCase);

        var applied = 0;
        foreach (var node in overrides.OfType<JsonObject>())
        {
            var familyName = node["family"]?.GetValue<string>()?.Trim() ?? string.Empty;
            var targetId = node["target_id"]?.GetValue<string>()?.Trim() ?? string.Empty;
            var operation = (node["operation"]?.GetValue<string>()?.Trim() ?? "patch").ToLowerInvariant();

            if (familyName.Length == 0 || targetId.Length == 0)
            {
                diagnostics.Add(new(DiagnosticSeverity.Error, "WORLDOVR003", "Every override requires family and target_id.", path));
                continue;
            }

            if (operation is not ("patch" or "upsert" or "create" or "delete"))
            {
                diagnostics.Add(new(DiagnosticSeverity.Error, "WORLDOVR008", $"Override '{familyName}/{targetId}' uses unsupported operation '{operation}'.", path));
                continue;
            }

            if (!mutable.TryGetValue(familyName, out var family))
            {
                diagnostics.Add(new(DiagnosticSeverity.Error, "WORLDOVR004", $"Override family '{familyName}' does not exist in the Librarium runtime projection.", path));
                continue;
            }

            var index = family.Records.FindIndex(record => RecordMatches(record, targetId));
            if (operation.Equals("delete", StringComparison.OrdinalIgnoreCase))
            {
                if (index < 0)
                {
                    diagnostics.Add(new(DiagnosticSeverity.Warning, "WORLDOVR005", $"Delete override target '{familyName}/{targetId}' was not found.", path));
                    continue;
                }

                family.Records.RemoveAt(index);
                applied++;
                continue;
            }

            if (node["fields"] is not JsonObject fields)
            {
                diagnostics.Add(new(DiagnosticSeverity.Error, "WORLDOVR006", $"Override '{familyName}/{targetId}' requires a fields object for operation '{operation}'.", path));
                continue;
            }

            if (index < 0)
            {
                if (operation == "patch")
                {
                    diagnostics.Add(new(DiagnosticSeverity.Error, "WORLDOVR007", $"Patch target '{familyName}/{targetId}' was not found. Use upsert to create a custom record.", path));
                    continue;
                }

                var created = (JsonObject)fields.DeepClone();
                if (!created.ContainsKey("id")) created["id"] = targetId;
                family.Records.Add(created);
                applied++;
                continue;
            }

            if (operation == "create")
            {
                diagnostics.Add(new(DiagnosticSeverity.Error, "WORLDOVR009", $"Create target '{familyName}/{targetId}' already exists. Use patch or upsert instead.", path));
                continue;
            }

            MergeObject(family.Records[index], fields);
            applied++;
        }

        diagnostics.Add(new(DiagnosticSeverity.Info, "WORLDOVR100", $"Applied {applied:N0} authored override(s) for world '{worldId}'.", path));
        return families.Select(family =>
        {
            var edited = mutable[family.Name];
            return new LibrariumFamily(family.Name, family.SourceFile, edited.Records, family.SourceFiles);
        }).ToArray();
    }

    public static string ResolveOverridePath(string repositoryRoot, string worldId) =>
        Path.Combine(repositoryRoot, "Librarium", "WorldOverrides", LibrariumCompilerOptions.NormalizeWorld(worldId), "overrides.json");

    private static bool RecordMatches(JsonObject record, string targetId)
    {
        foreach (var key in new[] { "id", "mechanic_id", "object_id" })
        {
            var value = record[key]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(value) && value.Equals(targetId, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static void MergeObject(JsonObject target, JsonObject patch)
    {
        foreach (var property in patch)
        {
            if (property.Value is JsonObject patchObject && target[property.Key] is JsonObject targetObject)
            {
                MergeObject(targetObject, patchObject);
                continue;
            }

            target[property.Key] = property.Value?.DeepClone();
        }
    }

    private sealed record MutableFamily(LibrariumFamily Source, List<JsonObject> Records);
}
