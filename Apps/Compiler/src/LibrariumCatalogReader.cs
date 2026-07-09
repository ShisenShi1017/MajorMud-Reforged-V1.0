using System.Text.Json;
using System.Text.Json.Nodes;

namespace MajorMud.Reforged.Compiler;

public sealed class LibrariumCatalogReader
{
    private static readonly string[] PreferredRecordNames =
    [
        "*_records_*.json",
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
            var selected = SelectSourceFile(familyDirectory);
            if (selected is null)
            {
                diagnostics.Add(new(DiagnosticSeverity.Warning, "LIB002", $"No catalog file found for family '{familyName}'.", familyDirectory));
                continue;
            }

            try
            {
                var json = JsonNode.Parse(File.ReadAllText(selected));
                var records = ExtractRecords(json).ToList();
                families.Add(new LibrariumFamily(familyName, selected, records));
            }
            catch (Exception ex)
            {
                diagnostics.Add(new(DiagnosticSeverity.Error, "LIB003", $"Could not read family '{familyName}': {ex.Message}", selected));
            }
        }

        return families;
    }

    private static string? SelectSourceFile(string familyDirectory)
    {
        foreach (var pattern in PreferredRecordNames)
        {
            var match = Directory.GetFiles(familyDirectory, pattern).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
            if (match is not null) return match;
        }
        return null;
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

public sealed record LibrariumFamily(string Name, string SourceFile, IReadOnlyList<JsonObject> Records);
