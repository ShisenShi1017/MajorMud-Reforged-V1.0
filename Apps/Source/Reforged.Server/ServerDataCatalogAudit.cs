using Reforged.Core.Loading;
using Reforged.Core.Validation;

namespace Reforged.Server;

/// <summary>
/// Loads canonical Reforged Data/ categories through Core loaders for server startup readiness.
/// This is not gameplay startup; it verifies the live data catalog is readable by the server.
/// </summary>
public sealed class ServerDataCatalogAudit
{
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
        var dataRoot = Path.Combine(root, "Data");
        var report = new ServerDataCatalogAuditReport
        {
            ProjectRoot = root,
            DataRoot = dataRoot
        };

        var catalog = new CoreDataRepositoryLoader().LoadCatalog(root, validation);
        foreach (var descriptor in CoreDataCategoryRegistry.Current)
        {
            var path = Path.Combine(dataRoot, descriptor.FileName);
            var exists = File.Exists(path);
            var size = exists ? new FileInfo(path).Length : 0;
            var recordCount = catalog.TryGetCategory(descriptor.Category, out var loadedCategory)
                ? loadedCategory.Records.Count
                : 0;
            var isPlaceholder = PlaceholderCategories.ContainsKey(descriptor.Category);
            var runtimeReady = exists && recordCount > 0;

            if (!exists)
            {
                validation.Add(
                    ValidationSeverity.Error,
                    "SERVER_DATA_FILE_MISSING",
                    $"Required canonical server data file is missing: {descriptor.RelativePath}",
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
