using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeBundleDEnvironmentAuditReport(
    bool Passed,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Failures,
    int EnvironmentRecordCount,
    int RuntimeRoomCount,
    int RoomsWithLight,
    int RoomsWithSpells,
    int RoomsWithLairs,
    int RoomsWithAttributes,
    int RoomsWithDeathDestinations,
    int GangHouseRooms)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

public static class RuntimeBundleDEnvironmentAudit
{
    public static RuntimeBundleDEnvironmentAuditReport Run(string projectRoot, string worldId)
    {
        var checks = new List<string>();
        var failures = new List<string>();
        var options = ServerRuntimeOptions.Load(projectRoot);
        var definition = options.Worlds.First(x => x.Id.Equals(worldId, StringComparison.OrdinalIgnoreCase));
        var world = new RuntimeWorldLoader().Load(projectRoot, definition);
        var rooms = world.AllRooms();
        var decoded = rooms.ToDictionary(
            room => new RuntimeRoomKey(room.MapId, room.RoomNumber),
            room => new RuntimeRoomEnvironmentRecord(
                room.Id, room.MapId, room.RoomNumber, room.EnvironmentLightLevel,
                room.EnvironmentRoomSpellId, room.EnvironmentLairId, room.EnvironmentDelay,
                room.EnvironmentRoomType, room.EnvironmentAttributes, room.EnvironmentDeathRoomId,
                room.EnvironmentGangHouseId, room.EnvironmentRespawnMinutes, room.EnvironmentMaxAreaCap,
                room.EnvironmentLinkedSpawnRoom, room.EnvironmentEvidence));

        Check(decoded.Count > 0, "decoded-room-environment-loaded");
        var roomKeys = rooms.Select(r => new RuntimeRoomKey(r.MapId, r.RoomNumber)).ToHashSet();
        var roomsWithDecodedEnvironment = rooms
            .Where(r => decoded.ContainsKey(new RuntimeRoomKey(r.MapId, r.RoomNumber)))
            .ToArray();
        var decodedGangHouseRooms = decoded.Values.Count(r => r.GangHouseId != 0);

        Check(decoded.Count == rooms.Count, "embedded-environment-record-per-runtime-room");
        Check(decoded.Keys.All(roomKeys.Contains), "decoded-environment-records-resolve-to-runtime-rooms");
        Check(rooms.All(r => decoded.ContainsKey(new RuntimeRoomKey(r.MapId, r.RoomNumber))), "all-runtime-rooms-map-qualified-in-environment-catalog");
        Check(roomsWithDecodedEnvironment.All(r => !string.IsNullOrWhiteSpace(r.EnvironmentEvidence)), "decoded-environment-provenance-consumed-by-runtime-room");
        Check(rooms.Any(r => r.EnvironmentLightLevel != 0), "light-level-consumer-active");
        Check(rooms.Any(r => r.EnvironmentRoomSpellId != 0), "room-spell-consumer-active");
        if (definition.Type.Equals("Classic", StringComparison.OrdinalIgnoreCase))
        {
            var checkedRooms = rooms.Where(room => room.EnvironmentRoomSpellNativeVerified).ToArray();
            Check(checkedRooms.Length > 0, "classic-native-room-spell-evidence-loaded");
            Check(world.FindRoom(1, 288) is { EnvironmentRoomSpellNativeVerified: true, EnvironmentRoomSpellId: 753 },
                "classic-silver-river-288-spell-753");
            Check(world.FindRoom(1, 2342) is { EnvironmentRoomSpellNativeVerified: true, EnvironmentRoomSpellId: 753 },
                "classic-silver-river-2342-spell-753");
            var knownIds = RuntimeSpellCatalog.Load(projectRoot, worldId).Spells
                .Select(spell => int.TryParse(spell.LegacyId, out var id) ? id : 0).ToHashSet();
            Check(checkedRooms.Where(room => room.EnvironmentRoomSpellId > 0)
                .All(room => knownIds.Contains(room.EnvironmentRoomSpellId)),
                "classic-native-room-spell-references-resolve");
        }
        Check(rooms.Any(r => r.EnvironmentLairId != 0), "lair-consumer-active");
        Check(rooms.Any(r => r.EnvironmentAttributes != 0), "room-attributes-consumer-active");
        Check(rooms.Any(r => r.EnvironmentDeathRoomId != 0), "death-room-consumer-active");
        Check(rooms.Count(r => r.EnvironmentGangHouseId != 0) == decodedGangHouseRooms, "gang-house-environment-consumer-matches-decoded-catalog");
        Check(decoded.TryGetValue(new RuntimeRoomKey(1, 288), out var silverRiver) &&
              silverRiver.RoomType == 0 &&
              silverRiver.LightLevel == 0 &&
              silverRiver.GangHouseId == 0 &&
              silverRiver.Attributes == 0 &&
              silverRiver.DeathRoomId == 0 &&
              silverRiver.RespawnMinutes == 5 &&
              silverRiver.MaxAreaCap == 0 &&
              silverRiver.LinkedSpawnRoom == 2342 &&
              silverRiver.LairId == 21 &&
              silverRiver.SourceClassification.Equals("decoded_imported_wcc_room_record_anchor", StringComparison.OrdinalIgnoreCase),
            "imported-wcc-anchor:room.map1.288.environment-values");
        Check(decoded.TryGetValue(new RuntimeRoomKey(1, 2306), out var smallHouseAttic) &&
              smallHouseAttic.RoomType == 1 &&
              smallHouseAttic.LightLevel == 0 &&
              smallHouseAttic.GangHouseId == 0 &&
              smallHouseAttic.Attributes == 0 &&
              smallHouseAttic.DeathRoomId == 0 &&
              smallHouseAttic.RespawnMinutes == 5 &&
              smallHouseAttic.MaxAreaCap == 0 &&
              smallHouseAttic.LinkedSpawnRoom == 0 &&
              smallHouseAttic.LairId == 10 &&
              smallHouseAttic.SourceClassification.Equals("decoded_imported_wcc_room_record_anchor", StringComparison.OrdinalIgnoreCase),
            "imported-wcc-anchor:room.map1.2306.environment-values");
        Check(rooms.Where(r => r.EnvironmentDeathRoomId > 0).All(r => world.FindRoom(r.MapId, r.EnvironmentDeathRoomId) is not null), "death-room-destinations-resolve-on-owning-map");
        Check(rooms.All(r => r.EnvironmentRoomType >= 0 && r.EnvironmentDelay >= 0 && r.EnvironmentRespawnMinutes >= 0 && r.EnvironmentMaxAreaCap >= 0), "decoded-environment-values-preserved-without-negative-invention");

        return new RuntimeBundleDEnvironmentAuditReport(
            failures.Count == 0, checks, failures, decoded.Count, rooms.Count,
            rooms.Count(r => r.EnvironmentLightLevel != 0),
            rooms.Count(r => r.EnvironmentRoomSpellId != 0),
            rooms.Count(r => r.EnvironmentLairId != 0),
            rooms.Count(r => r.EnvironmentAttributes != 0),
            rooms.Count(r => r.EnvironmentDeathRoomId != 0),
            rooms.Count(r => r.EnvironmentGangHouseId != 0));

        void Check(bool condition, string name)
        {
            checks.Add(name);
            if (!condition) failures.Add(name);
        }
    }
}
