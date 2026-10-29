using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeRoomPlacedDefinition(
    string SemanticType,
    int SlotCount,
    int NativeSlotWidthBytes,
    string ItemIdType,
    string MdbRepresentation,
    IReadOnlyList<int> DecodedItemIds,
    string DecodeStatus,
    string RuntimeBehavior,
    string Fallback,
    string Confidence)
{
    public static RuntimeRoomPlacedDefinition Empty { get; } = new(
        string.Empty, 0, 0, string.Empty, string.Empty, Array.Empty<int>(), string.Empty, string.Empty, string.Empty, string.Empty);

    public static RuntimeRoomPlacedDefinition FromJson(JsonElement sourceFields)
    {
        if (!sourceFields.TryGetProperty("placed_definition", out var element) || element.ValueKind != JsonValueKind.Object)
            return Empty;

        var ids = new List<int>();
        if (element.TryGetProperty("decoded_items", out var decoded) && decoded.ValueKind == JsonValueKind.Array)
        {
            foreach (var value in decoded.EnumerateArray())
            {
                if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var id) && id > 0) ids.Add(id);
                else if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out id) && id > 0) ids.Add(id);
            }
        }

        return new RuntimeRoomPlacedDefinition(
            JsonValueReader.ReadString(element, "semantic_type", string.Empty),
            JsonValueReader.ReadInt(element, "slot_count", 0),
            JsonValueReader.ReadInt(element, "native_slot_width_bytes", 0),
            JsonValueReader.ReadString(element, "item_id_type", string.Empty),
            JsonValueReader.ReadString(element, "mdb_representation", string.Empty),
            ids.Distinct().ToArray(),
            JsonValueReader.ReadString(element, "decode_status", string.Empty),
            JsonValueReader.ReadString(element, "runtime_behavior", string.Empty),
            JsonValueReader.ReadString(element, "fallback", string.Empty),
            JsonValueReader.ReadString(element, "confidence", string.Empty));
    }
}

public sealed class RuntimeRoom
{
    public string Id { get; init; } = string.Empty;
    public int MapId { get; init; }
    public int RoomNumber { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string ObjectType { get; init; } = string.Empty;
    public IReadOnlyList<string> WorldScopes { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> DeclaredExitIds { get; init; } = Array.Empty<string>();
    public string NpcDefinition { get; init; } = string.Empty;
    public string CommandDefinition { get; init; } = string.Empty;
    public RuntimeRoomPlacedDefinition PlacedDefinition { get; init; } = RuntimeRoomPlacedDefinition.Empty;
    public int? ImportedRoomType { get; init; }
    public int? ImportedShopNumber { get; init; }
    public int? ImportedLightLevel { get; init; }
    public int EnvironmentLightLevel { get; init; }
    public int EnvironmentRoomSpellId { get; init; }
    public bool EnvironmentRoomSpellNativeVerified { get; init; }
    public bool EnvironmentRoomSpellEntryOnChange { get; init; }
    public int EnvironmentRoomPicklockSpellId { get; init; }
    public int EnvironmentLairId { get; init; }
    public int EnvironmentDelay { get; init; }
    public int EnvironmentRoomType { get; init; }
    public int EnvironmentAttributes { get; init; }
    public int EnvironmentDeathRoomId { get; init; }
    public int EnvironmentGangHouseId { get; init; }
    public int EnvironmentRespawnMinutes { get; init; }
    public int EnvironmentMaxAreaCap { get; init; }
    public int EnvironmentLinkedSpawnRoom { get; init; }
    public string EnvironmentEvidence { get; init; } = string.Empty;
    public string Confidence { get; init; } = string.Empty;
    public bool ClassicLocked { get; init; }
    public string RuntimeMappingStatus { get; init; } = string.Empty;
    public string GeneratorMappingStatus { get; init; } = string.Empty;
    public string ValidationStatus { get; init; } = string.Empty;
    public int EvidenceReferenceCount { get; init; }
    public IReadOnlyList<RuntimeExit> Exits { get; init; } = Array.Empty<RuntimeExit>();

    public RuntimeRoom WithExits(IEnumerable<RuntimeExit> exits)
    {
        return new RuntimeRoom
        {
            Id = Id, MapId = MapId, RoomNumber = RoomNumber, Name = Name, Description = Description,
            ObjectType = ObjectType, WorldScopes = WorldScopes, DeclaredExitIds = DeclaredExitIds,
            NpcDefinition = NpcDefinition, CommandDefinition = CommandDefinition, PlacedDefinition = PlacedDefinition,
            ImportedRoomType = ImportedRoomType, ImportedShopNumber = ImportedShopNumber, ImportedLightLevel = ImportedLightLevel,
            EnvironmentLightLevel = EnvironmentLightLevel, EnvironmentRoomSpellId = EnvironmentRoomSpellId, EnvironmentRoomSpellNativeVerified = EnvironmentRoomSpellNativeVerified,
            EnvironmentRoomSpellEntryOnChange = EnvironmentRoomSpellEntryOnChange, EnvironmentRoomPicklockSpellId = EnvironmentRoomPicklockSpellId,
            EnvironmentLairId = EnvironmentLairId,
            EnvironmentDelay = EnvironmentDelay, EnvironmentRoomType = EnvironmentRoomType, EnvironmentAttributes = EnvironmentAttributes,
            EnvironmentDeathRoomId = EnvironmentDeathRoomId, EnvironmentGangHouseId = EnvironmentGangHouseId, EnvironmentRespawnMinutes = EnvironmentRespawnMinutes,
            EnvironmentMaxAreaCap = EnvironmentMaxAreaCap, EnvironmentLinkedSpawnRoom = EnvironmentLinkedSpawnRoom, EnvironmentEvidence = EnvironmentEvidence,
            Confidence = Confidence, ClassicLocked = ClassicLocked, RuntimeMappingStatus = RuntimeMappingStatus,
            GeneratorMappingStatus = GeneratorMappingStatus, ValidationStatus = ValidationStatus,
            EvidenceReferenceCount = EvidenceReferenceCount,
            Exits = exits.OrderBy(exit => exit.Direction, StringComparer.OrdinalIgnoreCase).ToArray()
        };
    }

    public RuntimeRoom WithEnvironment(RuntimeRoomEnvironmentRecord environment)
    {
        return new RuntimeRoom
        {
            Id = Id, MapId = MapId, RoomNumber = RoomNumber, Name = Name, Description = Description, ObjectType = ObjectType,
            WorldScopes = WorldScopes, DeclaredExitIds = DeclaredExitIds, NpcDefinition = NpcDefinition, CommandDefinition = CommandDefinition,
            PlacedDefinition = PlacedDefinition, ImportedRoomType = ImportedRoomType, ImportedShopNumber = ImportedShopNumber, ImportedLightLevel = ImportedLightLevel,
            Confidence = Confidence, ClassicLocked = ClassicLocked, RuntimeMappingStatus = RuntimeMappingStatus, GeneratorMappingStatus = GeneratorMappingStatus,
            ValidationStatus = ValidationStatus, EvidenceReferenceCount = EvidenceReferenceCount, Exits = Exits,
            EnvironmentLightLevel = environment.LightLevel,
            EnvironmentRoomSpellId = EnvironmentRoomSpellNativeVerified ? EnvironmentRoomSpellId : environment.RoomSpellId,
            EnvironmentRoomSpellNativeVerified = EnvironmentRoomSpellNativeVerified,
            EnvironmentRoomSpellEntryOnChange = EnvironmentRoomSpellEntryOnChange,
            EnvironmentRoomPicklockSpellId = EnvironmentRoomPicklockSpellId,
            EnvironmentLairId = environment.LairId,
            EnvironmentDelay = environment.Delay, EnvironmentRoomType = environment.RoomType, EnvironmentAttributes = environment.Attributes,
            EnvironmentDeathRoomId = environment.DeathRoomId, EnvironmentGangHouseId = environment.GangHouseId, EnvironmentRespawnMinutes = environment.RespawnMinutes,
            EnvironmentMaxAreaCap = environment.MaxAreaCap, EnvironmentLinkedSpawnRoom = environment.LinkedSpawnRoom, EnvironmentEvidence = environment.SourceClassification
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
                if (!string.IsNullOrWhiteSpace(runtimeExit.Direction) && runtimeExit.TargetRoomNumber > 0) exits.Add(runtimeExit);
            }
        }

        var id = JsonValueReader.ReadString(element, "id", string.Empty);
        var mapId = JsonValueReader.ReadInt(element, "mapId", JsonValueReader.ReadInt(element, "map_id", JsonValueReader.ReadInt(element, "map_number", 0)));
        var roomNumber = JsonValueReader.ReadInt(element, "roomNumber", JsonValueReader.ReadInt(element, "room_number", JsonValueReader.ReadInt(element, "legacy_id", 0)));
        if ((mapId <= 0 || roomNumber <= 0) && TryParseCanonicalRoomId(id, out var parsedMapId, out var parsedRoomNumber))
        {
            mapId = mapId > 0 ? mapId : parsedMapId;
            roomNumber = roomNumber > 0 ? roomNumber : parsedRoomNumber;
        }

        var sourceFields = element.TryGetProperty("source_fields", out var sf) && sf.ValueKind == JsonValueKind.Object ? sf : default;
        var environment = element.TryGetProperty("environment", out var env) && env.ValueKind == JsonValueKind.Object ? env : default;
        return new RuntimeRoom
        {
            Id = id,
            MapId = mapId,
            RoomNumber = roomNumber,
            Name = JsonValueReader.ReadString(element, "name", "Unknown Room"),
            Description = JsonValueReader.ReadString(element, "description", string.Empty),
            ObjectType = JsonValueReader.ReadString(element, "object_type", string.Empty),
            WorldScopes = ReadStringArray(element, "world_scope"),
            DeclaredExitIds = ReadRelationshipIds(element, "exits"),
            NpcDefinition = sourceFields.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadString(sourceFields, "npc_definition", string.Empty) : string.Empty,
            CommandDefinition = sourceFields.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadString(sourceFields, "command_definition", string.Empty) : string.Empty,
            PlacedDefinition = sourceFields.ValueKind == JsonValueKind.Object ? RuntimeRoomPlacedDefinition.FromJson(sourceFields) : RuntimeRoomPlacedDefinition.Empty,
            ImportedRoomType = sourceFields.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadNullableInt(sourceFields, "imported_room_type") : null,
            ImportedShopNumber = sourceFields.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadNullableInt(sourceFields, "imported_shop_number") : null,
            ImportedLightLevel = sourceFields.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadNullableInt(sourceFields, "imported_light_level") : null,
            EnvironmentLightLevel = environment.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadInt(environment, "light_level", 0) : 0,
            EnvironmentRoomSpellId = environment.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadInt(environment, "room_spell_id", 0) : 0,
            EnvironmentRoomSpellNativeVerified = environment.ValueKind == JsonValueKind.Object
                && JsonValueReader.ReadBool(environment, "native_room_spell_verified", false),
            EnvironmentRoomSpellEntryOnChange = environment.ValueKind == JsonValueKind.Object
                && JsonValueReader.ReadBool(environment, "native_room_spell_entry_on_change", false),
            EnvironmentRoomPicklockSpellId = environment.ValueKind == JsonValueKind.Object
                ? JsonValueReader.ReadInt(environment, "native_room_picklock_spell_id", 0) : 0,
            EnvironmentLairId = environment.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadInt(environment, "room_lair_id", 0) : 0,
            EnvironmentDelay = environment.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadInt(environment, "room_delay", 0) : 0,
            EnvironmentRoomType = environment.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadInt(environment, "room_type", 0) : 0,
            EnvironmentAttributes = environment.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadInt(environment, "attributes", 0) : 0,
            EnvironmentDeathRoomId = environment.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadInt(environment, "death_room_id", 0) : 0,
            EnvironmentGangHouseId = environment.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadInt(environment, "gang_house", 0) : 0,
            EnvironmentRespawnMinutes = environment.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadInt(environment, "respawn_minutes", 0) : 0,
            EnvironmentMaxAreaCap = environment.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadInt(environment, "max_area_cap", 0) : 0,
            EnvironmentLinkedSpawnRoom = environment.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadInt(environment, "linked_spawn_room", 0) : 0,
            EnvironmentEvidence = environment.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadString(environment, "source_classification", string.Empty) : string.Empty,
            Confidence = JsonValueReader.ReadString(element, "confidence", string.Empty),
            ClassicLocked = ReadClassicLock(element),
            RuntimeMappingStatus = ReadObjectStatus(element, "runtime_mapping"),
            GeneratorMappingStatus = ReadObjectStatus(element, "generator_mapping"),
            ValidationStatus = ReadObjectStatus(element, "validation"),
            EvidenceReferenceCount = ReadArrayCount(element, "evidence_refs"),
            Exits = exits
        };
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array) return Array.Empty<string>();
        return value.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString() ?? string.Empty).Where(x => x.Length > 0).ToArray();
    }

    private static IReadOnlyList<string> ReadRelationshipIds(JsonElement element, string name)
    {
        if (!element.TryGetProperty("relationships", out var relationships) || relationships.ValueKind != JsonValueKind.Object) return Array.Empty<string>();
        return ReadStringArray(relationships, name);
    }

    private static int ReadArrayCount(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array ? value.GetArrayLength() : 0;

    private static bool ReadClassicLock(JsonElement element)
    {
        if (!element.TryGetProperty("classic_lock", out var value)) return false;
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.String => !string.IsNullOrWhiteSpace(value.GetString()),
            JsonValueKind.Object => true,
            _ => false
        };
    }

    private static string ReadObjectStatus(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value)) return string.Empty;
        if (value.ValueKind == JsonValueKind.String) return value.GetString() ?? string.Empty;
        if (value.ValueKind != JsonValueKind.Object) return value.GetRawText();
        foreach (var key in new[] { "status", "mapping_status", "validation_status", "classification" })
            if (value.TryGetProperty(key, out var status) && status.ValueKind == JsonValueKind.String) return status.GetString() ?? string.Empty;
        return value.GetRawText();
    }

    private static bool TryParseCanonicalRoomId(string id, out int mapId, out int roomNumber)
    {
        mapId = 0; roomNumber = 0;
        if (string.IsNullOrWhiteSpace(id) || !id.StartsWith("room.map", StringComparison.OrdinalIgnoreCase)) return false;
        var parts = id.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 3 && parts[1].StartsWith("map", StringComparison.OrdinalIgnoreCase) &&
               int.TryParse(parts[1][3..], out mapId) && int.TryParse(parts[2], out roomNumber) && mapId > 0 && roomNumber > 0;
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
