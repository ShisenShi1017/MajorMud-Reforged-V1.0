using Reforged.Core.Loading;
using Reforged.Core.Validation;

namespace Reforged.Server;

/// <summary>
/// Loads canonical Reforged Data/ categories through Core loaders for server startup readiness.
/// This is not gameplay startup; it verifies the live data catalog is readable by the server.
/// </summary>
public sealed class ServerDataCatalogAudit
{
    private static readonly IReadOnlyDictionary<string, string> GeneratedCategoryFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["loot_tables"] = "loot_rules.json",
        ["monsters"] = "npcs.json",
        ["world"] = "worlds.json"
    };

    private static readonly IReadOnlyDictionary<string, string> PlaceholderCategories = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["loot_tables"] = "Present but empty; loot population rules are not runtime-ready yet.",
        ["quests"] = "Present but empty; quest content is not runtime-ready yet.",
        ["rooms"] = "Present but empty; current room content is loaded through world_rooms until room data is normalized.",
        ["shops"] = "Present but empty; shop inventories are not runtime-ready yet.",
        ["trainers"] = "Present but empty; trainer data is not runtime-ready yet."
    };

    public ServerDataCatalogAuditReport Run(string projectRoot, ValidationResult validation)
    {
        var root = Path.GetFullPath(projectRoot);
        var dataRoot = ResolveDataRoot(root);
        var generatedDataMode = dataRoot.EndsWith(Path.Combine("Data", "Classic"), StringComparison.OrdinalIgnoreCase);
        var report = new ServerDataCatalogAuditReport
        {
            ProjectRoot = root,
            DataRoot = dataRoot
        };

        var catalog = generatedDataMode
            ? null
            : new CoreDataRepositoryLoader().LoadCatalog(root, validation);
        foreach (var descriptor in CoreDataCategoryRegistry.Current)
        {
            var fileName = GeneratedCategoryFiles.TryGetValue(descriptor.Category, out var generatedFile)
                ? generatedFile
                : descriptor.FileName;
            var path = Path.Combine(dataRoot, fileName);
            var exists = File.Exists(path);
            var size = exists ? new FileInfo(path).Length : 0;
            var recordCount = catalog is not null && catalog.TryGetCategory(descriptor.Category, out var loadedCategory)
                ? loadedCategory.Records.Count
                : CountGeneratedRecords(path);
            var isPlaceholder = recordCount == 0 && PlaceholderCategories.ContainsKey(descriptor.Category);
            var runtimeReady = exists && recordCount > 0;

            if (!exists)
            {
                validation.Add(
                    generatedDataMode ? ValidationSeverity.Warning : ValidationSeverity.Error,
                    "SERVER_DATA_FILE_MISSING",
                    generatedDataMode
                        ? $"Legacy canonical server data file is absent from generated Classic output: {descriptor.RelativePath}"
                        : $"Required canonical server data file is missing: {descriptor.RelativePath}",
                    path,
                    descriptor.Category);
            }

            report.AddCategory(new ServerDataCategorySummary(
                descriptor.Category,
                descriptor.RelativePath,
                path,
                exists,
                size,
                recordCount,
                isPlaceholder,
                runtimeReady,
                DetermineStatus(exists, recordCount, isPlaceholder),
                DetermineReadinessNote(descriptor.Category, exists, recordCount, isPlaceholder)));
        }

        return report;
    }

    private static string ResolveDataRoot(string projectRoot)
    {
        var flatRoot = Path.Combine(projectRoot, "Data");
        return File.Exists(Path.Combine(flatRoot, "rooms.json"))
            ? flatRoot
            : Path.Combine(flatRoot, "Classic");
    }

    private static int CountGeneratedRecords(string path)
    {
        if (!File.Exists(path))
        {
            return 0;
        }

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
            return document.RootElement.ValueKind switch
            {
                System.Text.Json.JsonValueKind.Array => document.RootElement.GetArrayLength(),
                System.Text.Json.JsonValueKind.Object when document.RootElement.TryGetProperty("records", out var records) && records.ValueKind == System.Text.Json.JsonValueKind.Array => records.GetArrayLength(),
                _ => 0
            };
        }
        catch (System.Text.Json.JsonException)
        {
            return 0;
        }
    }

    private static string DetermineStatus(bool exists, int recordCount, bool isPlaceholder)
    {
        if (!exists)
        {
            return "missing";
        }

        if (recordCount > 0)
        {
            return "runtime-ready";
        }

        return isPlaceholder ? "placeholder-empty-needs-data" : "empty-needs-data";
    }

    private static string DetermineReadinessNote(string category, bool exists, int recordCount, bool isPlaceholder)
    {
        if (!exists)
        {
            return "Required canonical data file is missing.";
        }

        if (recordCount > 0)
        {
            return "Canonical data file is present and records loaded.";
        }

        if (isPlaceholder && PlaceholderCategories.TryGetValue(category, out var note))
        {
            return note;
        }

        return "Canonical data file is present but no records loaded.";
    }
}
