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

    public RuntimeRoom WithExits(IEnumerable<RuntimeExit> exits)
    {
        return new RuntimeRoom
        {
            Id = Id,
            MapId = MapId,
            RoomNumber = RoomNumber,
            Name = Name,
            Description = Description,
            Exits = exits.OrderBy(exit => exit.Direction, StringComparer.OrdinalIgnoreCase).ToArray()
        };
    }

    public static RuntimeRoom FromJson(JsonElement element)
    {
        var exits = new List<RuntimeExit>();
        if (element.TryGetProperty("exits", out var exitsElement) && exitsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var exit in exitsElement.EnumerateArray())
            {
                var runtimeExit = RuntimeExit.FromJson(exit);
                if (!string.IsNullOrWhiteSpace(runtimeExit.Direction) && runtimeExit.TargetRoomNumber > 0)
                {
                    exits.Add(runtimeExit);
                }
            }
        }

        return new RuntimeRoom
        {
            Id = JsonValueReader.ReadString(element, "id", string.Empty),
            MapId = JsonValueReader.ReadInt(element, "mapId", JsonValueReader.ReadInt(element, "map_id", 0)),
            RoomNumber = JsonValueReader.ReadInt(element, "roomNumber", JsonValueReader.ReadInt(element, "room_number", JsonValueReader.ReadInt(element, "legacy_id", 0))),
            Name = JsonValueReader.ReadString(element, "name", "Unknown Room"),
            Description = JsonValueReader.ReadString(element, "description", string.Empty),
            Exits = exits
        };
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
            Direction = JsonValueReader.ReadString(element, "dir", JsonValueReader.ReadString(element, "direction", string.Empty)).ToUpperInvariant(),
            TargetRoomNumber = JsonValueReader.ReadInt(element, "to_room", JsonValueReader.ReadInt(element, "targetRoomNumber", JsonValueReader.ReadInt(element, "to_room_number", 0))),
            ExitType = JsonValueReader.ReadNullableInt(element, "exit_type")
        };
    }

    public static RuntimeExit FromGeneratedRecord(JsonElement element)
    {
        return new RuntimeExit
        {
            Direction = JsonValueReader.ReadString(element, "direction", JsonValueReader.ReadString(element, "dir", string.Empty)).ToUpperInvariant(),
            TargetRoomNumber = JsonValueReader.ReadInt(element, "to_room_number", JsonValueReader.ReadInt(element, "targetRoomNumber", JsonValueReader.ReadInt(element, "to_room", 0))),
            ExitType = JsonValueReader.ReadNullableInt(element, "exit_type")
        };
    }

    public static int ReadFromRoomNumber(JsonElement element)
    {
        return JsonValueReader.ReadInt(element, "from_room_number", JsonValueReader.ReadInt(element, "fromRoomNumber", JsonValueReader.ReadInt(element, "from_room", 0)));
    }
}

internal static class JsonValueReader
{
    public static string ReadString(JsonElement element, string propertyName, string fallback)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return fallback;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? fallback,
            JsonValueKind.Number => value.GetRawText(),
            _ => fallback
        };
    }

    public static int ReadInt(JsonElement element, string propertyName, int fallback)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return fallback;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        return value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsed)
            ? parsed
            : fallback;
    }

    public static int? ReadNullableInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        return value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsed)
            ? parsed
            : null;
    }
}
