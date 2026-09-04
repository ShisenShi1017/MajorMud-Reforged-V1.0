using System.Text.Json;

namespace RealmGate.Server;

internal static class RuntimeChunkedClassicCatalog
{
    public static IReadOnlyList<JsonElement> LoadRecords(string catalogPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(catalogPath));
        var root = document.RootElement;

        if (root.ValueKind == JsonValueKind.Array)
            return root.EnumerateArray().Select(item => item.Clone()).ToArray();

        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException($"Catalog '{catalogPath}' must be an array or object.");

        if (root.TryGetProperty("records", out var records))
        {
            if (records.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException($"Catalog '{catalogPath}' has a non-array records property.");
            return records.EnumerateArray().Select(item => item.Clone()).ToArray();
        }

        if (root.TryGetProperty("chunked_records", out var chunked) && chunked.ValueKind == JsonValueKind.True &&
            root.TryGetProperty("record_parts", out var parts) && parts.ValueKind == JsonValueKind.Array)
        {
            var baseDirectory = Path.GetDirectoryName(catalogPath) ?? string.Empty;
            var result = new List<JsonElement>();
            foreach (var part in parts.EnumerateArray())
            {
                var relativePath = part.GetString();
                if (string.IsNullOrWhiteSpace(relativePath))
                    throw new InvalidDataException($"Catalog '{catalogPath}' contains an empty record part path.");
                var partPath = Path.GetFullPath(Path.Combine(baseDirectory, relativePath));
                if (!File.Exists(partPath))
                    throw new FileNotFoundException("Chunked catalog part was not found.", partPath);
                result.AddRange(LoadRecords(partPath));
            }

            if (root.TryGetProperty("record_count", out var expectedElement) && expectedElement.TryGetInt32(out var expected) && expected != result.Count)
                throw new InvalidDataException($"Catalog '{catalogPath}' declares {expected} records but its parts contain {result.Count}.");

            return result;
        }

        throw new InvalidDataException($"Catalog '{catalogPath}' does not contain records or chunk metadata.");
    }
}
