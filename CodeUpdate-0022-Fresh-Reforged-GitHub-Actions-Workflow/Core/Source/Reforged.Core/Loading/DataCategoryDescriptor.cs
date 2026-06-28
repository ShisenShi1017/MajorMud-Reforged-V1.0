namespace Reforged.Core.Loading;

/// <summary>
/// Describes one canonical Data/ JSON file and, when needed, the nested collection property that contains records.
/// </summary>
public sealed record DataCategoryDescriptor(string Category, string FileName, string? CollectionProperty = null)
{
    public string RelativePath => Path.Combine("Data", FileName);
}
