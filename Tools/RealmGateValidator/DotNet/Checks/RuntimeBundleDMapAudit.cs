using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeBundleDMapAuditReport(
    bool Passed,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Failures,
    int MapCount,
    int RoomCount,
    int ExitCount,
    int CrossMapTransitionCount,
    int BlockedMissingDestinationCount)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

public static class RuntimeBundleDMapAudit
{
    public static RuntimeBundleDMapAuditReport Run(string projectRoot, string worldId)
    {
        var checks = new List<string>();
        var failures = new List<string>();
        var options = ServerRuntimeOptions.Load(projectRoot);
        var definition = options.Worlds.First(x => x.Id.Equals(worldId, StringComparison.OrdinalIgnoreCase));
        var dataRoot = Path.Combine(projectRoot, definition.DataRoot);
        var exitRecords = RuntimeChunkedClassicCatalog.LoadRecords(Path.Combine(dataRoot, "exits.json"));
        var world = new RuntimeWorldLoader().Load(projectRoot, definition);
        var rooms = world.AllRooms();
        var runtimeExitCount = rooms.Sum(room => room.Exits.Count);
        var roomIds = rooms.Select(room => room.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var mapCount = rooms.Select(room => room.MapId).Distinct().Count();
        var roomCount = rooms.Count;
        var exitIds = exitRecords.Select(exit => JsonValueReader.ReadString(exit, "id", string.Empty)).Where(id => id.Length > 0).ToArray();
        var missingSourceCount = exitRecords.Count(exit => !roomIds.Contains(ReadRoomId(exit, "from")));
        var missingDestinations = exitRecords
            .Where(exit => !roomIds.Contains(ReadRoomId(exit, "to")))
            .Select(exit => JsonValueReader.ReadString(exit, "id", string.Empty))
            .Where(id => id.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var blockedMissing = exitRecords.Count(exit =>
            missingDestinations.Contains(JsonValueReader.ReadString(exit, "id", string.Empty)) &&
            exit.TryGetProperty("runtime_disposition", out var disposition) &&
            string.Equals(disposition.GetString(), "invalid_destination_blocked", StringComparison.OrdinalIgnoreCase));
        var crossMapCount = exitRecords.Count(exit =>
            JsonValueReader.ReadInt(exit, "from_map_number", 0) != JsonValueReader.ReadInt(exit, "to_map_number", 0));

        Check(mapCount == rooms.Select(room => room.MapId).Distinct().Count(), "canonical-map-count-matches-runtime");
        Check(roomCount == rooms.Count, "canonical-room-count-matches-runtime");
        Check(exitRecords.Count == runtimeExitCount, "canonical-exit-count-matches-runtime");
        Check(roomIds.Count == rooms.Count, "no-duplicate-map-qualified-rooms");
        Check(exitIds.Distinct(StringComparer.OrdinalIgnoreCase).Count() == exitIds.Length, "no-duplicate-exit-identities");
        Check(missingSourceCount == 0, "every-exit-source-room-resolves");
        Check(blockedMissing == missingDestinations.Count, "all-missing-destinations-are-explicitly-blocked");
        Check(crossMapCount == rooms.SelectMany(room => room.Exits).Count(exit => exit.FromMapId != exit.TargetMapId), "cross-map-transition-count-matches-runtime");
        Check(rooms.All(room => room.Id.Equals($"room.map{room.MapId}.{room.RoomNumber}", StringComparison.OrdinalIgnoreCase)), "canonical-room-id-is-authoritative");

        return new RuntimeBundleDMapAuditReport(failures.Count == 0, checks, failures, mapCount, roomCount, exitRecords.Count, crossMapCount, blockedMissing);

        static string ReadRoomId(JsonElement exit, string side)
        {
            var map = JsonValueReader.ReadInt(exit, $"{side}_map_number", 0);
            var room = JsonValueReader.ReadInt(exit, $"{side}_room_number", 0);
            return map > 0 && room > 0 ? $"room.map{map}.{room}" : string.Empty;
        }

        void Check(bool condition, string name)
        {
            checks.Add(name);
            if (!condition) failures.Add(name);
        }
    }
}
