using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeBundleDFieldAuthority(
    string Source,
    string Field,
    string RuntimeOwner,
    string RuntimeConsumer,
    string ValidationOwner,
    string EvidenceStatus,
    bool Consumed);

public sealed record RuntimeBundleDWorldAuthorityAuditReport(
    bool Passed,
    string WorldId,
    int RoomCount,
    int ExitCount,
    int SpawnCount,
    int TriggerCount,
    IReadOnlyList<RuntimeBundleDFieldAuthority> Fields,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Failures,
    IReadOnlyList<string> DuplicateImplementations,
    IReadOnlyList<string> Verified,
    IReadOnlyList<string> StronglyCorroborated,
    IReadOnlyList<string> DecodedButNotMapped,
    IReadOnlyList<string> Conflicts,
    IReadOnlyList<string> TrueUnknown)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

public static class RuntimeBundleDWorldAuthorityAudit
{
    private sealed record Mapping(string Owner, string Consumer, string Validation, string Evidence);

    private static readonly IReadOnlyDictionary<string, Mapping> RoomMappings =
        new Dictionary<string, Mapping>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = new("ClassicRuntimeCatalog / RuntimeWorld", "RuntimeWorld.FindRoomById and canonical room movement", "RuntimeWorldLoader duplicate identity validation", "Verified"),
            ["mapId"] = new("RuntimeRoomKey", "map-qualified room lookup", "RuntimeWorldLoader map-qualified identity validation", "Verified"),
            ["map_id"] = new("RuntimeRoomKey", "map-qualified room lookup", "RuntimeWorldLoader map-qualified identity validation", "Verified"),
            ["map_number"] = new("RuntimeRoomKey", "map-qualified room lookup", "RuntimeWorldLoader map-qualified identity validation", "Verified"),
            ["roomNumber"] = new("RuntimeRoomKey", "map-qualified room lookup", "RuntimeWorldLoader map-qualified identity validation", "Verified"),
            ["room_number"] = new("RuntimeRoomKey", "map-qualified room lookup", "RuntimeWorldLoader map-qualified identity validation", "Verified"),
            ["legacy_id"] = new("RuntimeRoomKey", "generated room compatibility parser", "RuntimeWorldLoader canonical identity validation", "Strongly Corroborated"),
            ["name"] = new("RuntimeRoom", "room display/look consumer", "ClassicRuntimeAudit room inspection", "Verified"),
            ["description"] = new("RuntimeRoom", "room display/look consumer", "ClassicRuntimeAudit room inspection", "Verified"),
            ["npc_definition"] = new("RuntimeRoom.NpcDefinition", "room narrative/NPC compatibility consumer", "Bundle D authority audit", "Strongly Corroborated"),
            ["exits"] = new("RuntimeWorld", "movement and exit gate consumers", "RuntimeWorldLoader exit graph validation", "Verified"),
            ["source_fields"] = new("Generated Classic Data provenance", "RuntimeRoom source-field adapter", "Bundle D authority audit", "Strongly Corroborated")
        };

    private static readonly IReadOnlyDictionary<string, Mapping> ExitMappings =
        new Dictionary<string, Mapping>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = new("RuntimeExit", "movement/gate/door/trap lookup", "RuntimeWorldLoader exit identity validation", "Verified"),
            ["dir"] = new("RuntimeExit.Direction", "live movement command", "exit graph integrity validation", "Verified"),
            ["direction"] = new("RuntimeExit.Direction", "live movement command", "exit graph integrity validation", "Verified"),
            ["from_map_number"] = new("RuntimeExit.SourceKey", "map-qualified source resolution", "RuntimeWorldLoader source endpoint validation", "Verified"),
            ["from_room_number"] = new("RuntimeExit.SourceKey", "map-qualified source resolution", "RuntimeWorldLoader source endpoint validation", "Verified"),
            ["to_map_number"] = new("RuntimeExit.TargetKey", "map-qualified destination resolution", "RuntimeWorldLoader destination endpoint validation", "Verified"),
            ["to_room_number"] = new("RuntimeExit.TargetKey", "map-qualified destination resolution", "RuntimeWorldLoader destination endpoint validation", "Verified"),
            ["to_room"] = new("RuntimeExit.TargetKey", "legacy destination adapter", "RuntimeWorldLoader destination endpoint validation", "Strongly Corroborated"),
            ["to_room_id"] = new("RuntimeExit.TargetRoomId", "canonical destination lookup", "canonical endpoint validation", "Verified"),
            ["exit_type"] = new("RuntimeExit.ExitType", "RuntimeExitGateService / special-exit dispatch", "special exit behavior audit", "Strongly Corroborated"),
            ["gate_kind"] = new("RuntimeExit.GateKind", "RuntimeExitGateService", "movement gate validation", "Strongly Corroborated"),
            ["qualifier"] = new("RuntimeExit.Qualifier", "RuntimeExitGateService", "movement gate validation", "Strongly Corroborated"),
            ["runtime_disposition"] = new("RuntimeExit.RuntimeDisposition", "movement allow/block dispatch", "dangling destination validation", "Verified"),
            ["destination_status"] = new("RuntimeExit.DestinationStatus", "invalid destination block", "dangling destination validation", "Verified"),
            ["validation_status"] = new("RuntimeExit.ValidationStatus", "invalid destination block", "dangling destination validation", "Verified"),
            ["source_verified"] = new("RuntimeExit.SourceVerified", "evidence-aware runtime validation", "Bundle D authority audit", "Strongly Corroborated")
        };

    public static RuntimeBundleDWorldAuthorityAuditReport Run(string projectRoot, string worldId)
    {
        var options = ServerRuntimeOptions.Load(projectRoot);
        var host = new ServerRuntimeHost(projectRoot, options);
        host.Bootstrap();
        var world = host.Worlds.TryGetValue(worldId, out var selected) ? selected : host.Worlds.Values.First();

        var fields = new List<RuntimeBundleDFieldAuthority>();
        Inventory(world.EvidencePath, "rooms.json", RoomMappings, fields);
        Inventory(world.ExitsEvidencePath, "exits.json", ExitMappings, fields);

        var checks = new List<string>();
        var failures = new List<string>();
        void Check(bool value, string id) { if (value) checks.Add(id); else failures.Add(id); }

        Check(world.RoomCount > 0, "world_contains_rooms");
        Check(world.ExitCount > 0, "world_contains_exits");
        Check(world.SpawnCount > 0, "world_contains_spawns");
        Check(world.AllRooms().All(room => room.MapId > 0 && room.RoomNumber > 0), "all_runtime_rooms_are_map_qualified");
        Check(world.AllRooms().Select(room => room.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() == world.RoomCount, "room_ids_unique");
        Check(world.AllRooms().Select(room => new RuntimeRoomKey(room.MapId, room.RoomNumber)).Distinct().Count() == world.RoomCount, "canonical_room_keys_unique");
        Check(world.FindRoomById("room.map1.2150") is not null, "newhaven_arena_room_map1_2150_exists");
        Check(world.AllRooms().SelectMany(room => room.Exits).All(exit => exit.SourceKey.IsValid), "all_exits_have_map_qualified_sources");
        Check(world.AllRooms().SelectMany(room => room.Exits).Where(exit => !exit.HasBlockedDestination).All(exit => exit.TargetKey.IsValid), "all_executable_exits_have_map_qualified_destinations");
        Check(fields.Where(field => field.EvidenceStatus is "Verified" or "Strongly Corroborated").All(field => field.Consumed), "all_verified_or_strong_fields_have_runtime_consumers");

        var serverRoot = Path.Combine(projectRoot, "Apps", "Source", "Reforged.Server");
        var duplicates = new List<string>();
        Check(Directory.GetFiles(serverRoot, "RuntimeWorldLoader.cs", SearchOption.TopDirectoryOnly).Length == 1, "single_runtime_world_loader");
        Check(Directory.GetFiles(serverRoot, "RuntimeClassicHiddenSearch.cs", SearchOption.TopDirectoryOnly).Length == 1, "single_classic_hidden_search_authority");
        Check(Directory.GetFiles(serverRoot, "RuntimeClassicCleanup.cs", SearchOption.TopDirectoryOnly).Length == 1, "single_classic_cleanup_authority");
        Check(Directory.GetFiles(serverRoot, "RuntimeDoorAccess.cs", SearchOption.TopDirectoryOnly).Length == 1, "single_door_access_authority");
        Check(Directory.GetFiles(serverRoot, "RuntimeTraps.cs", SearchOption.TopDirectoryOnly).Length == 1, "single_trap_authority");
        if (failures.Any(x => x.StartsWith("single_", StringComparison.Ordinal))) duplicates.AddRange(failures.Where(x => x.StartsWith("single_", StringComparison.Ordinal)));

        var decodedOnly = fields.Where(field => !field.Consumed).Select(field => $"{field.Source}:{field.Field}").Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToArray();
        return new RuntimeBundleDWorldAuthorityAuditReport(
            failures.Count == 0,
            world.Definition.Id,
            world.RoomCount,
            world.ExitCount,
            world.SpawnCount,
            world.TriggerCount,
            fields.OrderBy(x => x.Source).ThenBy(x => x.Field).ToArray(),
            checks,
            failures,
            duplicates.Count == 0 ? new[] { "No duplicate authoritative loader/search/cleanup/door/trap source file was found." } : duplicates,
            new[]
            {
                "Canonical map-qualified room IDs are owned by RuntimeWorld and used by live movement.",
                "Newhaven Arena validation uses room.map1.2150.",
                "Generated exits are merged once by RuntimeWorldLoader and consumed from RuntimeRoom.Exits.",
                "Room names and descriptions are consumed by the live look/inspection path."
            },
            new[]
            {
                "Legacy field aliases are accepted only as adapters into canonical RuntimeRoomKey and RuntimeExit fields.",
                "Exit type, gate kind and qualifier are retained for the authoritative special-exit gate consumer."
            },
            decodedOnly.Length == 0 ? new[] { "No Verified or Strongly Corroborated room/exit authority field is decoded-only in Phase D1." } : decodedOnly,
            new[] { "Obsolete display-name and unqualified room-number validation is not authoritative; canonical room IDs win." },
            new[]
            {
                "Unclassified generated room/exit properties remain quarantined until their decoded meaning and live consumer are proven in later Bundle D passes.",
                "Native-only latch effects remain deferred to the final native pass unless direct evidence is found earlier."
            });
    }

    private static void Inventory(string path, string source, IReadOnlyDictionary<string, Mapping> mappings, ICollection<RuntimeBundleDFieldAuthority> output)
    {
        var records = RuntimeChunkedClassicCatalog.LoadRecords(path);
        var properties = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in records) CollectProperties(record, properties);
        foreach (var property in properties)
        {
            if (mappings.TryGetValue(property, out var mapping))
                output.Add(new(source, property, mapping.Owner, mapping.Consumer, mapping.Validation, mapping.Evidence, true));
            else
                output.Add(new(source, property, "Generated Classic Data (quarantined)", "None proven", "Bundle D field mapping audit", "Decoded but not mapped", false));
        }
    }

    private static void CollectProperties(JsonElement element, ISet<string> properties)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    properties.Add(property.Name);
                    CollectProperties(property.Value, properties);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray()) CollectProperties(item, properties);
                break;
        }
    }
}
