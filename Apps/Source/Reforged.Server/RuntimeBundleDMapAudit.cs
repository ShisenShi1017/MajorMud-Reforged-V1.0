using System.Text.Json;

namespace Reforged.Server;

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
        using var topology = JsonDocument.Parse(File.ReadAllText(Path.Combine(dataRoot, "map_topology.json")));
        using var exitsDoc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dataRoot, "exits.json")));
        var root = topology.RootElement;
        var world = new RuntimeWorldLoader().Load(projectRoot, definition);
        var rooms = world.AllRooms();
        var runtimeExitCount = rooms.Sum(room => room.Exits.Count);
        var mapCount = root.GetProperty("map_count").GetInt32();
        var roomCount = root.GetProperty("room_count").GetInt32();
        var exitCount = root.GetProperty("exit_count").GetInt32();
        var crossMapCount = root.GetProperty("cross_map_transition_count").GetInt32();
        var missingDestinations = root.GetProperty("missing_destination_rooms").EnumerateArray().Select(x => x.GetString() ?? string.Empty).Where(x => x.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var blockedMissing = exitsDoc.RootElement.GetProperty("records").EnumerateArray().Count(exit =>
            missingDestinations.Contains(exit.GetProperty("id").GetString() ?? string.Empty) &&
            exit.TryGetProperty("runtime_disposition", out var disposition) &&
            string.Equals(disposition.GetString(), "invalid_destination_blocked", StringComparison.OrdinalIgnoreCase));

        Check(mapCount == rooms.Select(room => room.MapId).Distinct().Count(), "canonical-map-count-matches-runtime");
        Check(roomCount == rooms.Count, "canonical-room-count-matches-runtime");
        Check(exitCount == runtimeExitCount, "canonical-exit-count-matches-runtime");
        Check(root.GetProperty("duplicate_rooms").GetArrayLength() == 0, "no-duplicate-map-qualified-rooms");
        Check(root.GetProperty("duplicate_exits").GetArrayLength() == 0, "no-duplicate-exit-identities");
        Check(root.GetProperty("missing_source_rooms").GetArrayLength() == 0, "every-exit-source-room-resolves");
        Check(blockedMissing == missingDestinations.Count, "all-missing-destinations-are-explicitly-blocked");
        Check(crossMapCount == rooms.SelectMany(room => room.Exits).Count(exit => exit.FromMapId != exit.TargetMapId), "cross-map-transition-count-matches-runtime");
        Check(rooms.All(room => room.Id.Equals($"room.map{room.MapId}.{room.RoomNumber}", StringComparison.OrdinalIgnoreCase)), "canonical-room-id-is-authoritative");
        Check(root.GetProperty("coordinate_policy").GetProperty("status").GetString() == "not_present_in_decoded_room_records", "coordinates-not-invented");
        Check(root.GetProperty("region_policy").GetProperty("status").GetString() == "catalog_only_no_per_room_region_key_in_generated_room_records", "per-room-regions-not-invented");

        return new RuntimeBundleDMapAuditReport(failures.Count == 0, checks, failures, mapCount, roomCount, exitCount, crossMapCount, blockedMissing);

        void Check(bool condition, string name)
        {
            checks.Add(name);
            if (!condition) failures.Add(name);
        }
    }
}
