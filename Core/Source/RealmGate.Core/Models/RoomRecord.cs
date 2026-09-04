namespace RealmGate.Core.Models;

public sealed class RoomRecord : DataRecord
{
    public string Area { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public Dictionary<string, int> Exits { get; init; } = new();
}
