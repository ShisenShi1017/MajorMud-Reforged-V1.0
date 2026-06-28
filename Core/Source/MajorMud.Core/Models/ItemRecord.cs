namespace MajorMud.Core.Models;

public sealed class ItemRecord : DataRecord
{
    public string ItemType { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}
