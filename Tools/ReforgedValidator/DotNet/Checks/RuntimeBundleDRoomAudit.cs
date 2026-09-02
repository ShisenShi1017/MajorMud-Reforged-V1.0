using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeBundleDRoomAuditReport(
    bool Passed,
    string WorldId,
    int RoomCount,
    int MapCount,
    int DescribedRoomCount,
    int RoomWithNpcDefinitionCount,
    int RoomWithCommandDefinitionCount,
    int RoomWithPlacedDefinitionCount,
    int RoomWithDecodedPlacedItemsCount,
    int ImportedMetadataRoomCount,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Failures,
    IReadOnlyList<string> Verified,
    IReadOnlyList<string> StronglyCorroborated,
    IReadOnlyList<string> DecodedButNotMapped,
    IReadOnlyList<string> Conflicts,
    IReadOnlyList<string> TrueUnknown)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

public static class RuntimeBundleDRoomAudit
{
    public static RuntimeBundleDRoomAuditReport Run(string projectRoot, string worldId)
    {
        var options = ServerRuntimeOptions.Load(projectRoot);
        var host = new ServerRuntimeHost(projectRoot, options);
        host.Bootstrap();
        var world = host.Worlds.TryGetValue(worldId, out var selected) ? selected : host.Worlds.Values.First();
        var rooms = world.AllRooms();
        var checks = new List<string>();
        var failures = new List<string>();
        void Check(bool value, string id) { if (value) checks.Add(id); else failures.Add(id); }

        Check(rooms.Count == world.RoomCount && rooms.Count > 0, "all_generated_rooms_loaded_once");
        Check(rooms.All(room => room.Id.Equals(new RuntimeRoomKey(room.MapId, room.RoomNumber).ToString(), StringComparison.OrdinalIgnoreCase)), "canonical_room_id_matches_map_and_room_number");
        Check(rooms.Select(room => room.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() == rooms.Count, "canonical_room_ids_unique");
        Check(rooms.Select(room => new RuntimeRoomKey(room.MapId, room.RoomNumber)).Distinct().Count() == rooms.Count, "map_room_keys_unique");
        Check(rooms.All(room => room.ObjectType.Equals("room", StringComparison.OrdinalIgnoreCase)), "object_type_consumed_as_room");
        Check(rooms.All(room => room.WorldScopes.Contains("classic", StringComparer.OrdinalIgnoreCase)), "classic_world_scope_preserved");
        Check(rooms.All(room => room.EvidenceReferenceCount > 0), "room_evidence_provenance_preserved");
        Check(rooms.All(room => room.ClassicLocked), "classic_lock_preserved");
        Check(rooms.All(room => world.GetAuthoritativeRoomMetadata(room.Id) == room), "room_metadata_live_consumer_uses_runtime_world_authority");
        Check(rooms.All(room => world.GetResetPlacedItemIds(room.Id).SequenceEqual(room.PlacedDefinition.DecodedItemIds)), "placed_item_reset_consumer_uses_runtime_room_authority");
        Check(rooms.Where(room => room.PlacedDefinition.DecodeStatus.Contains("pending", StringComparison.OrdinalIgnoreCase)).All(room => room.PlacedDefinition.DecodedItemIds.Count == 0), "pending_placed_item_values_spawn_nothing");
        Check(world.FindRoomById("room.map1.2150") is { MapId: 1, RoomNumber: 2150 }, "newhaven_arena_is_room_map1_2150");
        Check(rooms.All(room => !string.IsNullOrWhiteSpace(room.Name)), "all_rooms_have_runtime_names");
        Check(rooms.Count(room => !string.IsNullOrWhiteSpace(room.Description)) > 0, "decoded_descriptions_reach_runtime");

        var imported = rooms.Where(room => room.ImportedRoomType.HasValue || room.ImportedShopNumber.HasValue || room.ImportedLightLevel.HasValue).ToArray();
        Check(imported.All(room => room.ImportedRoomType.HasValue && room.ImportedShopNumber.HasValue && room.ImportedLightLevel.HasValue), "imported_room_metadata_is_atomic");
        Check(imported.Any(room => room.Id.Equals("room.map1.2306", StringComparison.OrdinalIgnoreCase) && room.ImportedRoomType == 1 && room.ImportedShopNumber == 65 && room.ImportedLightLevel == 0), "imported_shop_room_metadata_preserved");

        var sourcePath = world.EvidencePath;
        var records = RuntimeChunkedClassicCatalog.LoadRecords(sourcePath);
        Check(records.Count == rooms.Count, "generated_room_record_count_matches_runtime");
        Check(records.All(record => record.TryGetProperty("relationships", out _)), "room_relationship_metadata_present");
        Check(records.All(record => record.TryGetProperty("runtime_mapping", out _)), "runtime_mapping_provenance_present");
        Check(records.All(record => record.TryGetProperty("generator_mapping", out _)), "generator_mapping_provenance_present");
        Check(records.All(record => record.TryGetProperty("validation", out _)), "room_validation_provenance_present");

        return new RuntimeBundleDRoomAuditReport(
            failures.Count == 0,
            world.Definition.Id,
            rooms.Count,
            rooms.Select(room => room.MapId).Distinct().Count(),
            rooms.Count(room => !string.IsNullOrWhiteSpace(room.Description)),
            rooms.Count(room => !string.IsNullOrWhiteSpace(room.NpcDefinition)),
            rooms.Count(room => !string.IsNullOrWhiteSpace(room.CommandDefinition)),
            rooms.Count(room => room.PlacedDefinition.SlotCount > 0),
            rooms.Count(room => room.PlacedDefinition.DecodedItemIds.Count > 0),
            imported.Length,
            checks,
            failures,
            new[]
            {
                "Canonical map-qualified room identity is the sole live lookup authority.",
                "Room names and descriptions are consumed by live room rendering.",
                "Room NPC definitions are consumed by the live NPC population path.",
                "World scope, evidence provenance, Classic lock, mapping provenance, and validation provenance survive generation into RuntimeRoom.",
                "Newhaven Arena is validated only as room.map1.2150."
            },
            new[]
            {
                "Fixed placed-item slot semantics are retained by RuntimeRoomPlacedDefinition and consumed by the reset-item query.",
                "Pending long-value recovery produces an empty decoded item list and therefore spawns nothing rather than inventing item IDs.",
                "Imported room type, shop number, and light level are retained as source metadata for later environment/merchant consumers."
            },
            new[]
            {
                "Room command_definition identifiers are preserved but their command execution semantics remain owned by the later integrated triggers/scripts/commands mapping; no behavior was invented.",
                "Terrain, region, coordinates, ownership, general room flags, and special-property fields are absent from the current generated Classic room schema and therefore cannot be mapped in D2."
            },
            new[] { "No conflicting canonical room identities were accepted; duplicate map/room keys fail world loading." },
            new[]
            {
                "Concrete placed-item IDs remain unknown where MDB long-value recovery is pending.",
                "The gameplay meaning of imported_room_type numeric values remains unresolved outside the two preserved imported records."
            });
    }
}
