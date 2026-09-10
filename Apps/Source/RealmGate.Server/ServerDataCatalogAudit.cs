using System.Text.Json;
using RealmGate.Core.Validation;

namespace RealmGate.Server;

/// <summary>
/// Audits the configured generated world catalog using the compiler manifest as authority.
/// </summary>
public sealed class ServerDataCatalogAudit
{
    public ServerDataCatalogAuditReport Run(string projectRoot, ValidationResult validation, string? requestedWorldId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        ArgumentNullException.ThrowIfNull(validation);

        var root = Path.GetFullPath(projectRoot);
        var report = new ServerDataCatalogAuditReport { ProjectRoot = root };

        string worldId;
        string dataRoot;
        try
        {
            (worldId, dataRoot) = string.IsNullOrWhiteSpace(requestedWorldId)
                ? ServerDataRootResolver.ResolveDefault(root)
                : ServerDataRootResolver.Resolve(root, requestedWorldId);
            report.DataRoot = dataRoot;
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or JsonException)
        {
            validation.Add(ValidationSeverity.Error, "SERVER_DATA_ROOT_CONFIGURATION", ex.Message, Path.Combine(root, "Config", "server.worlds.json"), "generated-data");
            return report;
        }

        var manifestPath = Path.Combine(dataRoot, "compile_manifest.json");
        if (!File.Exists(manifestPath))
        {
            validation.Add(ValidationSeverity.Error, "SERVER_COMPILE_MANIFEST_MISSING", $"Configured world '{worldId}' has no compile_manifest.json.", manifestPath, "generated-data");
            return report;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            if (!document.RootElement.TryGetProperty("families", out var families) || families.ValueKind != JsonValueKind.Array)
            {
                validation.Add(ValidationSeverity.Error, "SERVER_COMPILE_MANIFEST_FAMILIES", "compile_manifest.json does not contain a families array.", manifestPath, "generated-data");
                return report;
            }

            foreach (var family in families.EnumerateArray())
            {
                var familyName = family.TryGetProperty("family", out var nameValue) ? nameValue.GetString() : null;
                if (string.IsNullOrWhiteSpace(familyName))
                {
                    validation.Add(ValidationSeverity.Error, "SERVER_COMPILE_MANIFEST_FAMILY_NAME", "compile_manifest.json contains an unnamed family.", manifestPath, "generated-data");
                    continue;
                }

                var expectedCount = family.TryGetProperty("record_count", out var countValue) && countValue.TryGetInt32(out var parsedCount)
                    ? parsedCount
                    : 0;
                var fileName = $"{familyName}.json";
                var path = Path.Combine(dataRoot, fileName);
                var exists = File.Exists(path);
                var actualCount = exists ? CountRecords(path) : 0;
                var runtimeReady = exists && actualCount > 0;

                if (!exists)
                {
                    validation.Add(ValidationSeverity.Error, "SERVER_GENERATED_FAMILY_MISSING", $"Generated family file is missing: {fileName}", path, familyName);
                }
                else if (actualCount == 0 && expectedCount > 0)
                {
                    validation.Add(ValidationSeverity.Error, "SERVER_GENERATED_FAMILY_EMPTY", $"Generated family '{familyName}' declares {expectedCount} records but loaded zero.", path, familyName);
                }

                report.AddCategory(new ServerDataCategorySummary(
                    familyName,
                    Path.GetRelativePath(root, path),
                    path,
                    exists,
                    exists ? new FileInfo(path).Length : 0,
                    actualCount,
                    false,
                    runtimeReady,
                    runtimeReady ? "runtime-ready" : exists ? "empty-or-invalid" : "missing",
                    runtimeReady
                        ? $"Generated family loaded from configured world data; compiler manifest declares {expectedCount} source records."
                        : "Generated family is missing, empty, or invalid."));
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            validation.Add(ValidationSeverity.Error, "SERVER_COMPILE_MANIFEST_READ", ex.Message, manifestPath, "generated-data");
        }

        return report;
    }

    private static int CountRecords(string path)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Array)
            {
                return root.GetArrayLength();
            }

            if (root.ValueKind != JsonValueKind.Object)
            {
                return 0;
            }

            if (root.TryGetProperty("records", out var records) && records.ValueKind == JsonValueKind.Array)
            {
                return records.GetArrayLength();
            }

            if (root.TryGetProperty("record_count", out var declared) && declared.TryGetInt32(out var declaredCount))
            {
                return declaredCount;
            }

            foreach (var property in root.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Array)
                {
                    return property.Value.GetArrayLength();
                }
            }

            return 1;
        }
        catch (JsonException)
        {
            return 0;
        }
    }
}
