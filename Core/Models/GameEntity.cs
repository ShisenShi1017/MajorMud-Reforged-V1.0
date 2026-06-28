namespace Reforged.Core.Models;

/// <summary>
/// Base identity record for all editable Reforged data.
/// LegacyId preserves original legacy MUD numbering while StableId gives Reforged a durable internal reference.
/// </summary>
public class GameEntity : IReforgedDataRecord
{
    public string EntityType { get; set; } = string.Empty;
    public int? LegacyId { get; set; }
    public string StableId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, object?> UnknownFields { get; set; } = new();
    public List<string> SourceFiles { get; set; } = new();

    IDictionary<string, object?> IReforgedDataRecord.UnknownFields => UnknownFields;
    IReadOnlyCollection<string> IReforgedDataRecord.SourceFiles => SourceFiles;
}
