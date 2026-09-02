namespace Reforged.Server;

/// <summary>
/// Read-only inventory summary for generated Classic runtime data.
/// </summary>
public sealed class ClassicParityAuditReport
{
    private readonly List<ClassicParitySourceSummary> sources = new();
    private readonly List<ClassicParityCategorySummary> categories = new();
    private readonly List<string> exactNameMatches = new();

    public string? ProjectRoot { get; internal set; }

    public string? CanonicalDataPath { get; internal set; }

    public string? ImportedDatabasesPath { get; internal set; }

    public IReadOnlyList<ClassicParitySourceSummary> Sources => sources;

    public IReadOnlyList<ClassicParityCategorySummary> Categories => categories;

    public IReadOnlyList<string> ExactNameMatches => exactNameMatches;

    public int CanonicalFileCount { get; internal set; }

    public int ImportedReferenceFileCount { get; internal set; }

    public int ExactNameMatchCount => exactNameMatches.Count;

    public string Status
    {
        get
        {
            if (CanonicalFileCount == 0)
            {
                return "canonical-data-missing";
            }
            return "runtime-data-ready";
        }
    }

    internal void AddSource(ClassicParitySourceSummary source) => sources.Add(source);

    internal void AddCategory(ClassicParityCategorySummary category) => categories.Add(category);

    internal void AddExactNameMatch(string fileName) => exactNameMatches.Add(fileName);
}

public sealed record ClassicParitySourceSummary(
    string Name,
    string Path,
    string Role,
    int FileCount,
    IReadOnlyDictionary<string, int> ExtensionCounts);

public sealed record ClassicParityCategorySummary(
    string Category,
    string Description,
    int CanonicalFileCount,
    int ImportedReferenceFileCount,
    int ExactNameMatchCount,
    string Status);
