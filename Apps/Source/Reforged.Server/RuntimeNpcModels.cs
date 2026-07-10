using System.Text.Json;

namespace Reforged.Server;

public sealed class RuntimeNpcDefinition
{
    public string Id { get; init; } = string.Empty;
    public int LegacyId { get; init; }
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

        return new RuntimeNpcDefinition
        {
            Id = JsonValueReader.ReadString(element, "id", string.Empty),
            LegacyId = JsonValueReader.ReadInt(element, "legacy_id", 0),
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
    public int NpcLegacyId { get; init; }
    public string DeclaredNpcName { get; init; } = string.Empty;
    public int RespawnMinutes { get; init; }
    public RuntimeNpcDefinition? Definition { get; init; }
    public bool DefinitionResolved => Definition is not null;
    public string DisplayName => Definition?.Name ?? DeclaredNpcName;

    public static RuntimeNpcSpawn FromJson(JsonElement element, IReadOnlyDictionary<int, RuntimeNpcDefinition> npcsByLegacyId)
    {
        var model = element.TryGetProperty("spawn_model", out var modelElement) && modelElement.ValueKind == JsonValueKind.Object
            ? modelElement
            : default;
        var monsterType = model.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadInt(model, "monster_type", 0) : 0;
        npcsByLegacyId.TryGetValue(monsterType, out var definition);

        return new RuntimeNpcSpawn
        {
            Id = JsonValueReader.ReadString(element, "id", string.Empty),
            RoomId = JsonValueReader.ReadString(element, "room_ref", string.Empty),
            NpcLegacyId = monsterType,
            DeclaredNpcName = JsonValueReader.ReadString(element, "npc_name", "Unknown NPC"),
            RespawnMinutes = model.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadInt(model, "respawn_minutes", 0) : 0,
            Definition = definition
        };
    }
}
