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

        var id = JsonValueReader.ReadString(element, "id", string.Empty);
        var mapId = JsonValueReader.ReadInt(
            element,
            "mapId",
            JsonValueReader.ReadInt(element, "map_id", JsonValueReader.ReadInt(element, "map_number", 0)));
        var roomNumber = JsonValueReader.ReadInt(
            element,
            "roomNumber",
            JsonValueReader.ReadInt(element, "room_number", JsonValueReader.ReadInt(element, "legacy_id", 0)));

        if ((mapId <= 0 || roomNumber <= 0) && TryParseCanonicalRoomId(id, out var parsedMapId, out var parsedRoomNumber))
        {
            mapId = mapId > 0 ? mapId : parsedMapId;
            roomNumber = roomNumber > 0 ? roomNumber : parsedRoomNumber;
        }

        return new RuntimeRoom
        {
            Id = id,
            MapId = mapId,
            RoomNumber = roomNumber,
            Name = JsonValueReader.ReadString(element, "name", "Unknown Room"),
            Description = JsonValueReader.ReadString(element, "description", string.Empty),
            Exits = exits
        };
    }

    private static bool TryParseCanonicalRoomId(string id, out int mapId, out int roomNumber)
    {
        mapId = 0;
        roomNumber = 0;
        if (string.IsNullOrWhiteSpace(id) || !id.StartsWith("room.map", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var parts = id.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 3 &&
               parts[1].StartsWith("map", StringComparison.OrdinalIgnoreCase) &&
               int.TryParse(parts[1][3..], out mapId) &&
               int.TryParse(parts[2], out roomNumber) &&
               mapId > 0 && roomNumber > 0;
    }
}

public readonly record struct RuntimeRoomKey(int MapId, int RoomNumber)
{
    public bool IsValid => MapId > 0 && RoomNumber > 0;
    public override string ToString() => $"room.map{MapId}.{RoomNumber}";
}

public sealed class RuntimeExit
{
    public string Id { get; init; } = string.Empty;
    public string Direction { get; init; } = string.Empty;
    public int FromMapId { get; init; }
    public int FromRoomNumber { get; init; }
    public int TargetMapId { get; init; }
    public int TargetRoomNumber { get; init; }
    public string TargetRoomId { get; init; } = string.Empty;
    public int? ExitType { get; init; }
    public string GateKind { get; init; } = string.Empty;
    public string Qualifier { get; init; } = string.Empty;
    public string RuntimeDisposition { get; init; } = string.Empty;
    public string DestinationStatus { get; init; } = string.Empty;
    public string ValidationStatus { get; init; } = string.Empty;
    public bool SourceVerified { get; init; }

    public RuntimeRoomKey SourceKey => new(FromMapId, FromRoomNumber);
    public RuntimeRoomKey TargetKey => new(TargetMapId, TargetRoomNumber);
    public bool HasBlockedDestination =>
        RuntimeDisposition.Equals("invalid_destination_blocked", StringComparison.OrdinalIgnoreCase) ||
        DestinationStatus.Equals("missing_in_source", StringComparison.OrdinalIgnoreCase) ||
        DestinationStatus.Equals("missing_from_decoded_room_catalog", StringComparison.OrdinalIgnoreCase) ||
        ValidationStatus.Equals("dangling_destination", StringComparison.OrdinalIgnoreCase);

    public static RuntimeExit FromJson(JsonElement element)
    {
        var fromMap = JsonValueReader.ReadInt(element, "from_map_number", JsonValueReader.ReadInt(element, "fromMapId", 0));
        var fromRoom = JsonValueReader.ReadInt(element, "from_room_number", JsonValueReader.ReadInt(element, "fromRoomNumber", 0));
        var targetMap = JsonValueReader.ReadInt(element, "to_map_number", JsonValueReader.ReadInt(element, "targetMapId", fromMap));
        var targetRoom = JsonValueReader.ReadInt(element, "to_room_number", JsonValueReader.ReadInt(element, "targetRoomNumber", JsonValueReader.ReadInt(element, "to_room", 0)));
        return new RuntimeExit
        {
            Id = JsonValueReader.ReadString(element, "id", string.Empty),
            Direction = JsonValueReader.ReadString(element, "dir", JsonValueReader.ReadString(element, "direction", string.Empty)).ToUpperInvariant(),
            FromMapId = fromMap,
            FromRoomNumber = fromRoom,
            TargetMapId = targetMap,
            TargetRoomNumber = targetRoom,
            TargetRoomId = JsonValueReader.ReadString(element, "to_room_id", targetMap > 0 && targetRoom > 0 ? $"room.map{targetMap}.{targetRoom}" : string.Empty),
            ExitType = JsonValueReader.ReadNullableInt(element, "exit_type"),
            GateKind = JsonValueReader.ReadString(element, "gate_kind", string.Empty),
            Qualifier = JsonValueReader.ReadString(element, "qualifier", string.Empty),
            RuntimeDisposition = JsonValueReader.ReadString(element, "runtime_disposition", string.Empty),
            DestinationStatus = JsonValueReader.ReadString(element, "destination_status", string.Empty),
            ValidationStatus = JsonValueReader.ReadString(element, "validation_status", string.Empty),
            SourceVerified = JsonValueReader.ReadBool(element, "source_verified", false)
        };
    }

    public static RuntimeExit FromGeneratedRecord(JsonElement element) => FromJson(element);

    public static RuntimeRoomKey ReadSourceKey(JsonElement element)
    {
        var map = JsonValueReader.ReadInt(element, "from_map_number", JsonValueReader.ReadInt(element, "fromMapId", 0));
        var room = JsonValueReader.ReadInt(element, "from_room_number", JsonValueReader.ReadInt(element, "fromRoomNumber", JsonValueReader.ReadInt(element, "from_room", 0)));
        return new RuntimeRoomKey(map, room);
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

    public static bool ReadBool(JsonElement element, string propertyName, bool fallback)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return fallback;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(value.GetString(), out var parsed) => parsed,
            _ => fallback
        };
    }
}
