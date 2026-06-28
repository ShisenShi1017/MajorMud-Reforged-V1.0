using MajorMud.Core.Loading;
using MajorMud.Core.Validation;

namespace MajorMud.Core.Health;

/// <summary>
/// Validates that every current canonical Data/ category can be loaded into a Core repository.
/// </summary>
public sealed class CoreDataRepositoryHealthCheck
{
    public ValidationResult ValidateDataFolder(string projectRootOrDataFolder)
    {
        var result = new ValidationResult();
        var loader = new CoreDataRepositoryLoader();
        var catalog = loader.LoadCatalog(projectRootOrDataFolder, result);

        foreach (var descriptor in CoreDataCategoryRegistry.Current)
        {
            if (!catalog.TryGetCategory(descriptor.Category, out var category))
            {
                result.Add(ValidationSeverity.Error, "DATA_REPOSITORY_CATEGORY_MISSING", $"Data category '{descriptor.Category}' was not loaded.", descriptor.FileName, descriptor.Category);
                continue;
            }

            if (category.Records.Count == 0 && CategoryUsuallyHasRecords(descriptor.Category))
            {
                result.Add(ValidationSeverity.Warning, "DATA_REPOSITORY_CATEGORY_EMPTY", $"Data category '{descriptor.Category}' loaded zero records.", descriptor.FileName, descriptor.Category);
            }
        }

        return result;
    }

    private static bool CategoryUsuallyHasRecords(string category) => category is not ("loot_tables" or "quests" or "rooms" or "shops" or "trainers");
}
