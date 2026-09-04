namespace RealmGate.Core.Models;

/// <summary>
/// Base contract for editable Reforged data records.
/// LegacyId preserves the original legacy identity. StableId is for future internal references.
/// </summary>
public abstract class DataRecord
{
    public int LegacyId { get; init; }
    public string StableId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public Dictionary<string, object?> UnknownFields { get; init; } = new();
    public List<SourceReference> Sources { get; init; } = new();
}
