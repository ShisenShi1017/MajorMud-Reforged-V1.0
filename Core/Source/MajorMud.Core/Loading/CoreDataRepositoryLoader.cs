using System.Text.Json;
using MajorMud.Core.Repositories;
using MajorMud.Core.Validation;

namespace MajorMud.Core.Loading;

/// <summary>
/// Loads canonical Data/ JSON files into Core repositories without app-specific importer, editor, or server dependencies.
/// </summary>
public sealed class CoreDataRepositoryLoader
{
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip
    };

    public CoreDataCatalog LoadCatalog(string projectRootOrDataFolder, ValidationResult? result = null)
    {
        var catalog = new CoreDataCatalog();
        foreach (var descriptor in CoreDataCategoryRegistry.Current)
        {
            catalog.Add(LoadCategory(projectRootOrDataFolder, descriptor, result));
        }

        return catalog;
    }

    public LoadedDataCategory LoadCategory(string projectRootOrDataFolder, DataCategoryDescriptor descriptor, ValidationResult? result = null)
    {
        var repository = new InMemoryDataRepository<CanonicalDataRecord>();
        var filePath = ResolveDataFile(projectRootOrDataFolder, descriptor.FileName);

        if (!File.Exists(filePath))
        {
            result?.Add(ValidationSeverity.Warning, "DATA_LOADER_FILE_MISSING", $"Data file '{descriptor.FileName}' was not found.", filePath, descriptor.Category);
            return new LoadedDataCategory(descriptor, repository);
        }

        try
        {
            using var stream = File.OpenRead(filePath);
            using var document = JsonDocument.Parse(stream, DocumentOptions);
            foreach (var record in ReadRecords(document.RootElement, descriptor, filePath))
            {
                repository.Upsert(record);
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or ArgumentException)
        {
            result?.Add(ValidationSeverity.Error, "DATA_LOADER_READ_ERROR", ex.Message, filePath, descriptor.Category);
        }

        return new LoadedDataCategory(descriptor, repository);
    }

    private static string ResolveDataFile(string projectRootOrDataFolder, string fileName)
    {
        var direct = Path.Combine(projectRootOrDataFolder, fileName);
        if (File.Exists(direct) || Path.GetFileName(projectRootOrDataFolder).Equals("Data", StringComparison.OrdinalIgnoreCase))
        {
            return direct;
        }

        return Path.Combine(projectRootOrDataFolder, "Data", fileName);
    }

    private static IEnumerable<CanonicalDataRecord> ReadRecords(JsonElement root, DataCategoryDescriptor descriptor, string sourceFile)
    {
        var collection = SelectCollection(root, descriptor.CollectionProperty);
        if (collection.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var element in collection.EnumerateArray())
            {
                if (element.ValueKind == JsonValueKind.Object)
                {
                    yield return CreateRecord(element, descriptor, sourceFile, index);
                }

                index++;
            }
        }
        else if (collection.ValueKind == JsonValueKind.Object)
        {
            var index = 0;
            foreach (var property in collection.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Object)
                {
                    yield return CreateRecord(property.Value, descriptor, sourceFile, index, property.Name);
                }

                index++;
            }
        }
    }

    private static JsonElement SelectCollection(JsonElement root, string? collectionProperty)
    {
        if (!string.IsNullOrWhiteSpace(collectionProperty)
            && root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty(collectionProperty, out var nested)
            && nested.ValueKind is JsonValueKind.Array or JsonValueKind.Object)
        {
            return nested;
        }

        return root;
    }

    private static CanonicalDataRecord CreateRecord(JsonElement element, DataCategoryDescriptor descriptor, string sourceFile, int index, string? fallbackId = null)
    {
        var rawFields = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in element.EnumerateObject())
        {
            rawFields[property.Name] = property.Value.Clone();
        }

        var stableId = TryGetText(element, "id")
            ?? TryGetText(element, "stableId")
            ?? TryGetText(element, "StableId")
            ?? fallbackId
            ?? $"{descriptor.Category}_{index + 1}";

        var legacyId = TryGetInt(element, "legacyId")
            ?? TryGetInt(element, "LegacyId")
            ?? TryGetInt(element, "sourceId")
            ?? TryGetInt(element, "id")
            ?? index + 1;

        var unknownFields = rawFields.ToDictionary(p => p.Key, p => (object?)p.Value, StringComparer.OrdinalIgnoreCase);

        return new CanonicalDataRecord
        {
            Category = descriptor.Category,
            SourceFile = sourceFile,
            Index = index,
            StableId = stableId,
            LegacyId = legacyId,
            Name = TryGetText(element, "name") ?? stableId,
            UnknownFields = unknownFields,
            RawFields = rawFields
        };
    }

    private static string? TryGetText(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private static int? TryGetInt(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var value))
        {
            return value;
        }

        return property.ValueKind == JsonValueKind.String && int.TryParse(property.GetString(), out var parsed) ? parsed : null;
    }
}
