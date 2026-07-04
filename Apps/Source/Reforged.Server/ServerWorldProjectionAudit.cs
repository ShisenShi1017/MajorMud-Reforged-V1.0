using System.Text.Json;
using Reforged.Core.Validation;

namespace Reforged.Server;

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

    public ServerWorldProjectionAuditReport Run(string projectRoot, ValidationResult validation)
    {
        var root = Path.GetFullPath(projectRoot);
        var dataRoot = Path.Combine(root, "Data");
        var worldPath = Path.Combine(dataRoot, "world.json");
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
            using var worldDocument = JsonDocument.Parse(File.ReadAllText(worldPath), DocumentOptions);
            using var roomsDocument = JsonDocument.Parse(File.ReadAllText(roomsPath), DocumentOptions);
            var worldRoomIds = ReadWorldRoomIds(worldDocument.RootElement);
            var canonicalRoomIds = ReadCanonicalRoomIds(roomsDocument.RootElement);
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
            var startRoom = TryGetString(worldDocument.RootElement, "startRoom");
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

    private static HashSet<string> ReadCanonicalRoomIds(JsonElement root)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (root.ValueKind != JsonValueKind.Array)
        {
            return ids;
        }

        foreach (var room in root.EnumerateArray())
        {
            var id = TryGetString(room, "id");
            if (!string.IsNullOrWhiteSpace(id))
            {
                ids.Add(id);
            }
        }

        return ids;
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
