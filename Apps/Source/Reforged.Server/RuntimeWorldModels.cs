using System.Text.Json;

namespace Reforged.Server;

public sealed class RuntimeRoom
{
    public string Id { get; init; } = string.Empty;
    public int MapId { get; init; }
    public int RoomNumber { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public IReadOnlyList<RuntimeExit> Exits { get; init; } = Array.Empty<RuntimeExit>();

    public static RuntimeRoom FromJson(JsonElement element)
    {
        var exits = new List<RuntimeExit>();
        if (element.TryGetProperty("exits", out var exitsElement) && exitsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var exit in exitsElement.EnumerateArray())
            {
                exits.Add(RuntimeExit.FromJson(exit));
            }
        }

        return new RuntimeRoom
        {
            Id = ReadString(element, "id", string.Empty),
            MapId = ReadInt(element, "mapId", 0),
            RoomNumber = ReadInt(element, "roomNumber", 0),
            Name = ReadString(element, "name", "Unknown Room"),
            Description = ReadString(element, "description", string.Empty),
            Exits = exits
        };
    }

    private static string ReadString(JsonElement element, string propertyName, string fallback)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? fallback
            : fallback;
    }

    private static int ReadInt(JsonElement element, string propertyName, int fallback)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var result)
            ? result
            : fallback;
    }
}

public sealed class RuntimeExit
{
    public string Direction { get; init; } = string.Empty;
    public int TargetRoomNumber { get; init; }
    public int? ExitType { get; init; }

    public static RuntimeExit FromJson(JsonElement element)
    {
        return new RuntimeExit
        {
            Direction = ReadString(element, "dir", string.Empty).ToUpperInvariant(),
            TargetRoomNumber = ReadInt(element, "to_room", 0),
            ExitType = ReadNullableInt(element, "exit_type")
        };
    }

    private static string ReadString(JsonElement element, string propertyName, string fallback)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? fallback
            : fallback;
    }

    private static int ReadInt(JsonElement element, string propertyName, int fallback)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var result)
            ? result
            : fallback;
    }

    private static int? ReadNullableInt(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var result)
            ? result
            : null;
    }
}
