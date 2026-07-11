using System.Text.Json;
using Reforged.Core.Validation;

namespace Reforged.Core.Health;

/// <summary>
/// Validates current Data/*.json files without requiring importer/runtime dependencies.
/// This prepares Milestone 1 for the Native Importer milestone by making JSON health checks shared in Core.
/// </summary>
public sealed class DataJsonHealthCheck
{
    public ValidationResult ValidateDataFolder(string dataFolder, bool validateLegacyRepositoryContracts = true)
    {
        var result = new ValidationResult();

        if (!Directory.Exists(dataFolder))
        {
            result.Add(ValidationSeverity.Fatal, "DATA_FOLDER_MISSING", "Data folder was not found.", dataFolder);
            return result;
        }

        foreach (var file in Directory.EnumerateFiles(dataFolder, "*.json", SearchOption.TopDirectoryOnly).OrderBy(p => p))
        {
            ValidateJsonFile(file, result);
        }

        if (!result.HasErrors && validateLegacyRepositoryContracts)
        {
            result.Merge(new DataCrossReferenceHealthCheck().ValidateDataFolder(dataFolder));
            result.Merge(new CoreDataRepositoryHealthCheck().ValidateDataFolder(dataFolder));
        }

        return result;
    }

    private static void ValidateJsonFile(string file, ValidationResult result)
    {
        try
        {
            using var stream = File.OpenRead(file);
            using var document = JsonDocument.Parse(stream);
            if (document.RootElement.ValueKind is not (JsonValueKind.Array or JsonValueKind.Object))
            {
                result.Add(ValidationSeverity.Error, "DATA_JSON_ROOT_INVALID", "Data JSON root must be an object or array.", file);
            }

            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                ValidateDuplicateIds(file, document.RootElement, result);
            }
        }
        catch (JsonException ex)
        {
            result.Add(ValidationSeverity.Fatal, "DATA_JSON_PARSE_ERROR", ex.Message, file);
        }
        catch (IOException ex)
        {
            result.Add(ValidationSeverity.Fatal, "DATA_JSON_READ_ERROR", ex.Message, file);
        }
    }

    private static void ValidateDuplicateIds(string file, JsonElement array, ValidationResult result)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var duplicates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var element in array.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var id = TryGetPropertyText(element, "id")
                ?? TryGetPropertyText(element, "legacyId")
                ?? TryGetPropertyText(element, "StableId")
                ?? TryGetPropertyText(element, "LegacyId");

            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            if (!ids.Add(id))
            {
                duplicates.Add(id);
            }
        }

        foreach (var duplicate in duplicates.OrderBy(v => v))
        {
            result.Add(ValidationSeverity.Error, "DATA_JSON_DUPLICATE_ID", $"Duplicate record id found: {duplicate}", file, duplicate);
        }
    }

    private static string? TryGetPropertyText(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.ToString(),
            _ => null
        };
    }
}
