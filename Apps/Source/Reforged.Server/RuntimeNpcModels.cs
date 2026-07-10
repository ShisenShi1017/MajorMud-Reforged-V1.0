using System.Text.Json;

namespace Reforged.Server;

public sealed class RuntimeNpcDefinition
{
    public string Id { get; init; } = string.Empty;
    public int LegacyId { get; init; }
    public int GroupId { get; init; }
    public string Name { get; init; } = string.Empty;
    public int Level { get; init; }
    public int HitPoints { get; init; }
    public int Energy { get; init; }
    public int ArmorClass { get; init; }
    public int DamageResistance { get; init; }
    public int MagicResistance { get; init; }
    public int AttackCount { get; init; }

    public static RuntimeNpcDefinition FromJson(JsonElement element)
    {
        var combat = element.TryGetProperty("combat", out var combatElement) && combatElement.ValueKind == JsonValueKind.Object
            ? combatElement
            : default;
        var behavior = element.TryGetProperty("behavior", out var behaviorElement) && behaviorElement.ValueKind == JsonValueKind.Object
            ? behaviorElement
            : default;

        return new RuntimeNpcDefinition
        {
            Id = JsonValueReader.ReadString(element, "id", string.Empty),
            LegacyId = JsonValueReader.ReadInt(element, "legacy_id", 0),
            GroupId = behavior.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadInt(behavior, "group_id", 0) : 0,
            Name = JsonValueReader.ReadString(element, "name", "Unknown NPC"),
            Level = combat.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadInt(combat, "level", 0) : 0,
            HitPoints = combat.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadInt(combat, "hp", 0) : 0,
            Energy = combat.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadInt(combat, "energy", 0) : 0,
            ArmorClass = combat.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadInt(combat, "armor_class", 0) : 0,
            DamageResistance = combat.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadInt(combat, "damage_resistance", 0) : 0,
            MagicResistance = combat.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadInt(combat, "magic_resistance", 0) : 0,
            AttackCount = combat.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadInt(combat, "attack_count", 0) : 0
        };
    }
}

public sealed class RuntimeNpcSpawn
{
    public string Id { get; init; } = string.Empty;
    public string RoomId { get; init; } = string.Empty;
    public string QualifiedRoomRef { get; init; } = string.Empty;
    public int SourceRoomNumber { get; init; }
    public int SourceMapId { get; init; }
    public string ResolvedRuntimeRoomId { get; set; } = string.Empty;
    public bool PrimaryRoomAvailable => !string.IsNullOrWhiteSpace(ResolvedRuntimeRoomId);
    public int MonsterGroupId { get; init; }
    public int BossLegacyId { get; init; }
    public int LinkedSpawnRoomNumber { get; init; }
    public bool HasLinkedPopulationArea => LinkedSpawnRoomNumber > 0;
    public bool HasMapQualifiedPopulationArea => HasLinkedPopulationArea && SourceMapId > 0;
    public string PopulationAreaKey => !HasLinkedPopulationArea
        ? string.Empty
        : HasMapQualifiedPopulationArea
            ? $"map{SourceMapId}-room{LinkedSpawnRoomNumber}"
            : $"unqualified-room{LinkedSpawnRoomNumber}";
    public int RespawnMinutes { get; init; }
    public IReadOnlyList<RuntimeNpcDefinition> GroupDefinitions { get; init; } = Array.Empty<RuntimeNpcDefinition>();
    public RuntimeNpcDefinition? BossDefinition { get; init; }
    public bool GroupResolved => MonsterGroupId <= 0 || GroupDefinitions.Count > 0;
    public bool BossResolved => BossLegacyId <= 0 || BossDefinition is not null;
    public bool FullyResolved => GroupResolved && BossResolved;
    public string DisplayName => BossDefinition is not null
        ? BossDefinition.Name
        : MonsterGroupId > 0
            ? $"monster group {MonsterGroupId} ({GroupDefinitions.Count} eligible NPC types)"
            : "no configured encounter";

    public static RuntimeNpcSpawn FromJson(
        JsonElement element,
        IReadOnlyDictionary<int, RuntimeNpcDefinition> npcsByLegacyId,
        IReadOnlyDictionary<int, IReadOnlyList<RuntimeNpcDefinition>> npcsByGroupId)
    {
        var model = element.TryGetProperty("spawn_model", out var modelElement) && modelElement.ValueKind == JsonValueKind.Object
            ? modelElement
            : default;
        var monsterGroupId = model.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadInt(model, "monster_type", 0) : 0;
        var bossLegacyId = JsonValueReader.ReadInt(element, "npc_legacy_id", 0);
        var sourceMapId = JsonValueReader.ReadInt(element, "map_id", 0);
        var sourceRoomNumber = JsonValueReader.ReadInt(element, "room_number", 0);
        if (sourceMapId <= 0 && model.ValueKind == JsonValueKind.Object)
        {
            sourceMapId = JsonValueReader.ReadInt(model, "map_id", 0);
        }
        npcsByLegacyId.TryGetValue(bossLegacyId, out var bossDefinition);
        npcsByGroupId.TryGetValue(monsterGroupId, out var groupDefinitions);

        return new RuntimeNpcSpawn
        {
            Id = JsonValueReader.ReadString(element, "id", string.Empty),
            RoomId = JsonValueReader.ReadString(element, "room_ref", string.Empty),
            QualifiedRoomRef = JsonValueReader.ReadString(element, "qualified_room_ref", string.Empty),
            SourceRoomNumber = sourceRoomNumber,
            SourceMapId = sourceMapId,
            MonsterGroupId = monsterGroupId,
            BossLegacyId = bossLegacyId,
            LinkedSpawnRoomNumber = model.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadInt(model, "linked_spawn_room", 0) : 0,
            RespawnMinutes = model.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadInt(model, "respawn_minutes", 0) : 0,
            GroupDefinitions = groupDefinitions ?? Array.Empty<RuntimeNpcDefinition>(),
            BossDefinition = bossDefinition
        };
    }
}
