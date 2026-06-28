namespace Reforged.Core.Repositories;

/// <summary>
/// Describes the repositories available to an application without tying Core to application projects.
/// </summary>
public interface IRepositoryCatalog
{
    IReadOnlyCollection<string> EntityTypes { get; }
    bool ContainsEntityType(string entityType);
    object? GetRepository(string entityType);
}
