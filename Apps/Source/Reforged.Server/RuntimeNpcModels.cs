using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeNpcAttackDefinition(
    int Slot,
    int Type,
    int MinimumDamage,
    int MaximumDamage,
    int SelectionPercent,
    int Accuracy,
    int HitSpellLegacyId,
    int EnergyCost,
    int HitMessageLegacyId,
    int MissMessageLegacyId,
    int DodgeMessageLegacyId);

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
    public long ExperienceReward { get; init; }
    public long GoldReward { get; init; }
    public bool LootKnown { get; init; }
    public int LootTableEntries { get; init; }
    public IReadOnlyList<RuntimeNpcAttackDefinition> Attacks { get; init; } = Array.Empty<RuntimeNpcAttackDefinition>();

    public static RuntimeNpcDefinition FromJson(JsonElement element)
    {
        var combat = element.TryGetProperty("combat", out var combatElement) && combatElement.ValueKind == JsonValueKind.Object
            ? combatElement
            : default;
        var behavior = element.TryGetProperty("behavior", out var behaviorElement) && behaviorElement.ValueKind == JsonValueKind.Object
            ? behaviorElement
            : default;
        var rewards = element.TryGetProperty("rewards", out var rewardsElement) && rewardsElement.ValueKind == JsonValueKind.Object
            ? rewardsElement
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
            AttackCount = combat.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadInt(combat, "attack_count", 0) : 0,
            ExperienceReward = rewards.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadInt(rewards, "xp_base", 0) : 0,
            GoldReward = rewards.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadInt(rewards, "gold", 0) : 0,
            LootKnown = rewards.ValueKind == JsonValueKind.Object && bool.TryParse(JsonValueReader.ReadString(rewards, "loot_known", "false"), out var lootKnown) && lootKnown,
            LootTableEntries = rewards.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadInt(rewards, "loot_table_entries", 0) : 0,
            Attacks = ReadAttacks(element)
        };
    }

    private static IReadOnlyList<RuntimeNpcAttackDefinition> ReadAttacks(JsonElement element)
    {
        if (!element.TryGetProperty("attacks", out var attacks) || attacks.ValueKind != JsonValueKind.Array)
            return Array.Empty<RuntimeNpcAttackDefinition>();

        return attacks.EnumerateArray().Select(attack => new RuntimeNpcAttackDefinition(
            JsonValueReader.ReadInt(attack, "slot", 0),
            JsonValueReader.ReadInt(attack, "type", 0),
            JsonValueReader.ReadInt(attack, "minimum_damage", 0),
            JsonValueReader.ReadInt(attack, "maximum_damage", 0),
            JsonValueReader.ReadInt(attack, "selection_percent", 0),
            JsonValueReader.ReadInt(attack, "accuracy", 0),
            JsonValueReader.ReadInt(attack, "hit_spell_legacy_id", 0),
            JsonValueReader.ReadInt(attack, "energy_cost", 0),
            JsonValueReader.ReadInt(attack, "hit_message_legacy_id", 0),
            JsonValueReader.ReadInt(attack, "miss_message_legacy_id", 0),
            JsonValueReader.ReadInt(attack, "dodge_message_legacy_id", 0))).ToArray();
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
    public string InteractionModel { get; init; } = string.Empty;
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
            InteractionModel = model.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadString(model, "interaction_model", string.Empty) : string.Empty,
            GroupDefinitions = groupDefinitions ?? Array.Empty<RuntimeNpcDefinition>(),
            BossDefinition = bossDefinition
        };
    }
}
