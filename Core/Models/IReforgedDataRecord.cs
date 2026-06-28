namespace Reforged.Core.Models;

/// <summary>
/// Shared identity contract for every editable Reforged data record.
/// Applications must use this Core contract instead of defining duplicate local models.
/// </summary>
public interface IReforgedDataRecord
{
    string EntityType { get; }
    int? LegacyId { get; }
    string StableId { get; }
    string Name { get; }
    IDictionary<string, object?> UnknownFields { get; }
    IReadOnlyCollection<string> SourceFiles { get; }
}
