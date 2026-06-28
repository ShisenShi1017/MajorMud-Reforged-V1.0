using MajorMud.Core.Repositories;

namespace MajorMud.Core.Loading;

/// <summary>
/// Holds the repository and descriptor for one loaded Data/ category.
/// </summary>
public sealed class LoadedDataCategory
{
    public LoadedDataCategory(DataCategoryDescriptor descriptor, InMemoryDataRepository<CanonicalDataRecord> repository)
    {
        Descriptor = descriptor;
        Repository = repository;
    }

    public DataCategoryDescriptor Descriptor { get; }
    public InMemoryDataRepository<CanonicalDataRecord> Repository { get; }
    public IReadOnlyList<CanonicalDataRecord> Records => Repository.All();
}
