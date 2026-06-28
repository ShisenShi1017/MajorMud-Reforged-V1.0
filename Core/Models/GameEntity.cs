namespace MajorMud.Reforged.Core.Models;

/// <summary>
/// Base identity contract for all editable MajorMud Reforged data records.
/// LegacyId preserves original MajorMUD numbering while StableId gives Reforged a durable internal reference.
/// </summary>
public sealed class GameEntity
{
    public string EntityType { get; set; } = string.Empty;
    public int? LegacyId { get; set; }
    public string StableId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, object?> UnknownFields { get; set; } = new();
    public List<string> SourceFiles { get; set; } = new();
}
