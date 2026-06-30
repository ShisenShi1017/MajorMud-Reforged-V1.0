using Reforged.Core.Loading;
using Reforged.Core.Validation;

namespace Reforged.Server;

/// <summary>
/// Loads canonical Reforged Data/ categories through Core loaders for server startup readiness.
/// This is not gameplay startup; it verifies the live data catalog is readable by the server.
/// </summary>
public sealed class ServerDataCatalogAudit
{
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
                DetermineStatus(exists, recordCount)));
        }

        return report;
    }

    private static string DetermineStatus(bool exists, int recordCount)
    {
        if (!exists)
        {
            return "missing";
        }

        return recordCount == 0 ? "empty" : "loaded";
    }
}
