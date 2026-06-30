namespace Reforged.Server;

/// <summary>
/// Read-only summary of canonical Data/ categories loaded for server startup readiness.
/// </summary>
public sealed class ServerDataCatalogAuditReport
{
    private readonly List<ServerDataCategorySummary> categories = new();

    public string? ProjectRoot { get; internal set; }

    public string? DataRoot { get; internal set; }

    public IReadOnlyList<ServerDataCategorySummary> Categories => categories;

    public int CategoryCount => categories.Count;

    public int PresentCategoryCount => categories.Count(category => category.FileExists);

    public int MissingCategoryCount => categories.Count(category => !category.FileExists);

    public int EmptyCategoryCount => categories.Count(category => category.FileExists && category.RecordCount == 0);

    public int EmptyPlaceholderCategoryCount => categories.Count(category => category.IsPlaceholder && category.RecordCount == 0);

    public int RuntimeReadyCategoryCount => categories.Count(category => category.RuntimeReady);

    public int RuntimeBlockedCategoryCount => categories.Count(category => !category.RuntimeReady);

    public int LoadedRecordCount => categories.Sum(category => category.RecordCount);

    public string Status
    {
        get
        {
            if (MissingCategoryCount > 0)
            {
                return "missing-data-files";
            }

            if (CategoryCount == 0)
            {
                return "no-categories";
            }

            if (LoadedRecordCount == 0)
            {
                return "no-records-loaded";
            }

            return RuntimeBlockedCategoryCount > 0 ? "loaded-with-runtime-gaps" : "runtime-ready";
        }
    }

    internal void AddCategory(ServerDataCategorySummary category) => categories.Add(category);
}

public sealed record ServerDataCategorySummary(
    string Category,
    string RelativePath,
    string Path,
    bool FileExists,
    long FileSizeBytes,
    int RecordCount,
    bool IsPlaceholder,
    bool RuntimeReady,
    string Status,
    string ReadinessNote);
