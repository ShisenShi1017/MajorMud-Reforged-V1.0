using System.Text.Json;
using RealmGate.Core.Validation;

namespace RealmGate.Server;

/// <summary>
/// Builds a read-only Server projection over Data/world.json without starting world simulation.
/// </summary>
public sealed class ServerWorldProjectionAudit
{
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip
    };

    public ServerWorldProjectionAuditReport Run(string projectRoot, ValidationResult validation, string? requestedWorldId = null)
    {
        var root = Path.GetFullPath(projectRoot);
        var dataRoot = string.IsNullOrWhiteSpace(requestedWorldId)
            ? ResolveDataRoot(root)
            : ServerDataRootResolver.Resolve(root, requestedWorldId).DataRoot;
        var worldPath = Path.Combine(dataRoot, File.Exists(Path.Combine(dataRoot, "world.json")) ? "world.json" : "worlds.json");
        var roomsPath = Path.Combine(dataRoot, "rooms.json");
        var report = new ServerWorldProjectionAuditReport
        {
            ProjectRoot = root,
            WorldPath = worldPath,
            RoomsPath = roomsPath
        };

        if (!File.Exists(worldPath))
        {
            validation.Add(ValidationSeverity.Error, "SERVER_WORLD_FILE_MISSING", "Data/world.json was not found.", worldPath, "world");
            return report;
        }

        if (!File.Exists(roomsPath))
        {
            validation.Add(ValidationSeverity.Error, "SERVER_ROOMS_FILE_MISSING", "Data/rooms.json was not found.", roomsPath, "rooms");
            return report;
        }

        try
        {
            var canonicalRoomIds = ReadCanonicalRoomIds(roomsPath);
            using var worldDocument = JsonDocument.Parse(File.ReadAllText(worldPath), DocumentOptions);
            var worldRoomIds = ReadWorldRoomIds(worldDocument.RootElement);
            if (worldRoomIds.Count == 0)
            {
                worldRoomIds = canonicalRoomIds.OrderBy(roomId => roomId, StringComparer.OrdinalIgnoreCase).ToArray();
            }
            var uniqueWorldRoomIds = new HashSet<string>(worldRoomIds, StringComparer.OrdinalIgnoreCase);
            var missingRoomIds = uniqueWorldRoomIds
                .Where(roomId => !canonicalRoomIds.Contains(roomId))
                .OrderBy(roomId => roomId, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var duplicateWorldRoomIds = worldRoomIds
                .GroupBy(roomId => roomId, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .OrderBy(roomId => roomId, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var startRoom = ResolveGeneratedStartRoom(dataRoot, worldDocument.RootElement);
            var startRoomExists = !string.IsNullOrWhiteSpace(startRoom) && canonicalRoomIds.Contains(startRoom);

            report.StartRoom = startRoom;
            report.WorldRoomCount = worldRoomIds.Count;
            report.UniqueWorldRoomCount = uniqueWorldRoomIds.Count;
            report.CanonicalRoomCount = canonicalRoomIds.Count;
            report.MissingRoomIds = missingRoomIds;
            report.DuplicateWorldRoomIds = duplicateWorldRoomIds;
            report.StartRoomExists = startRoomExists;

            if (worldRoomIds.Count == 0)
            {
                validation.Add(ValidationSeverity.Warning, "SERVER_WORLD_ROOMS_EMPTY", "Data/world.json contains no projected room ids.", worldPath, "world_rooms");
            }

            if (!string.IsNullOrWhiteSpace(startRoom) && !startRoomExists)
            {
                validation.Add(ValidationSeverity.Warning, "SERVER_WORLD_START_ROOM_MISSING", $"World start room '{startRoom}' is not present in Data/rooms.json.", worldPath, "world_rooms");
            }

            foreach (var missingRoomId in missingRoomIds.Take(20))
            {
                validation.Add(ValidationSeverity.Warning, "SERVER_WORLD_ROOM_MISSING", $"World room '{missingRoomId}' is not present in Data/rooms.json.", worldPath, "world_rooms");
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or ArgumentException)
        {
            validation.Add(ValidationSeverity.Error, "SERVER_WORLD_PROJECTION_READ_ERROR", ex.Message, worldPath, "world_rooms");
        }

        return report;
    }

    private static IReadOnlyList<string> ReadWorldRoomIds(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("rooms", out var rooms) ||
            rooms.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return rooms.EnumerateArray()
            .Where(room => room.ValueKind == JsonValueKind.String)
            .Select(room => room.GetString())
            .Where(roomId => !string.IsNullOrWhiteSpace(roomId))
            .Cast<string>()
            .ToArray();
    }

    private static HashSet<string> ReadCanonicalRoomIds(string path)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var room in ReadRecords(path))
        {
            var id = TryGetString(room, "id");
            if (!string.IsNullOrWhiteSpace(id))
            {
                ids.Add(id);
            }
        }

        return ids;
    }

    private static IReadOnlyList<JsonElement> ReadRecords(string path)
    {
        var output = new List<JsonElement>();
        using var document = JsonDocument.Parse(File.ReadAllText(path), DocumentOptions);
        if (document.RootElement.ValueKind == JsonValueKind.Object &&
            document.RootElement.TryGetProperty("record_parts", out var parts) &&
            parts.ValueKind == JsonValueKind.Array)
        {
            foreach (var part in parts.EnumerateArray())
            {
                if (part.ValueKind != JsonValueKind.String) continue;
                var relative = part.GetString();
                if (string.IsNullOrWhiteSpace(relative)) continue;
                var partPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, relative.Replace('/', Path.DirectorySeparatorChar)));
                output.AddRange(ReadRecords(partPath));
            }

            return output;
        }

        var root = document.RootElement;
        var rooms = root.ValueKind switch
        {
            JsonValueKind.Array => root.EnumerateArray(),
            JsonValueKind.Object when root.TryGetProperty("records", out var records) && records.ValueKind == JsonValueKind.Array => records.EnumerateArray(),
            _ => default
        };

        if (rooms.Equals(default(JsonElement.ArrayEnumerator)))
        {
            return output;
        }

        output.AddRange(rooms.Select(room => room.Clone()));
        return output;
    }

    private static string ResolveDataRoot(string projectRoot)
    {
        var flatRoot = Path.Combine(projectRoot, "Data");
        return File.Exists(Path.Combine(flatRoot, "rooms.json"))
            ? flatRoot
            : Path.Combine(flatRoot, "Classic");
    }

    private static string ResolveGeneratedStartRoom(string dataRoot, JsonElement worldRoot)
    {
        var configured = TryGetString(worldRoot, "startRoom");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return NormalizeRoomId(configured, 1);
        }

        var manifestPath = Path.Combine(dataRoot, "world_manifest.json");
        if (File.Exists(manifestPath))
        {
            using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath), DocumentOptions);
            var root = manifest.RootElement;
            var direct = TryGetString(root, "character_start_room");
            if (!string.IsNullOrWhiteSpace(direct))
            {
                var mapId = 1;
                if (root.TryGetProperty("character_spawning", out var spawning) && spawning.ValueKind == JsonValueKind.Object)
                {
                    mapId = TryGetInt(spawning, "map_id") ?? mapId;
                }
                return NormalizeRoomId(direct, mapId);
            }
        }

        return "room.map1.2140";
    }

    private static string NormalizeRoomId(string roomId, int mapId)
    {
        if (roomId.StartsWith("room.map", StringComparison.OrdinalIgnoreCase))
        {
            return roomId;
        }

        var prefix = "room.";
        if (roomId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(roomId[prefix.Length..], out var roomNumber) && roomNumber > 0)
        {
            return $"room.map{Math.Max(1, mapId)}.{roomNumber}";
        }

        return roomId;
    }

    private static int? TryGetInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }
        return value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsed) ? parsed : null;
    }

    private static string? TryGetString(JsonElement element, string name)
    {
        return element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(name, out var property) &&
            property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }
}
