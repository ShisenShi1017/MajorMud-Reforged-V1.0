namespace MajorMud.Core.Loading;

/// <summary>
/// In-memory catalog of all currently supported canonical Data/ repositories.
/// </summary>
public sealed class CoreDataCatalog
{
    private readonly Dictionary<string, LoadedDataCategory> categories = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<LoadedDataCategory> Categories => categories.Values.OrderBy(c => c.Descriptor.Category).ToList();

    public void Add(LoadedDataCategory category) => categories[category.Descriptor.Category] = category;

    public bool TryGetCategory(string category, out LoadedDataCategory loadedCategory)
        => categories.TryGetValue(category, out loadedCategory!);

    public LoadedDataCategory GetCategory(string category)
    {
        if (!TryGetCategory(category, out var loadedCategory))
        {
            throw new KeyNotFoundException($"Data category '{category}' was not loaded.");
        }

        return loadedCategory;
    }
}
