using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeNpcLootDefinition(
    int ItemLegacyId,
    string CanonicalItemId,
    string Name,
    int DropPercent);

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
    int DodgeMessageLegacyId,
    string AttackVerb = "",
    string MissVerb = "",
    string WeaponName = "",
    bool NativeEmptyMissArgumentsVerified = false)
{
    public int EffectiveSpellLegacyId => Type == 2 ? Accuracy : HitSpellLegacyId;
    public bool HasExactNativeMissArguments => NativeEmptyMissArgumentsVerified ||
        (!string.IsNullOrWhiteSpace(MissVerb) && !string.IsNullOrWhiteSpace(WeaponName));
}

public sealed class RuntimeNpcAttackArgumentRecoveryCatalog
{
    private readonly IReadOnlyDictionary<string, string> _verbs;
    private readonly IReadOnlyDictionary<string, string> _weapons;

    private RuntimeNpcAttackArgumentRecoveryCatalog(IReadOnlyDictionary<string, string> verbs, IReadOnlyDictionary<string, string> weapons)
    {
        _verbs = verbs;
        _weapons = weapons;
    }

    public static RuntimeNpcAttackArgumentRecoveryCatalog Load(string projectRoot) =>
        Load(projectRoot, ServerRuntimeOptions.Load(projectRoot).DefaultWorldId);

    public static RuntimeNpcAttackArgumentRecoveryCatalog Load(string projectRoot, string worldId)
    {
        var dataRoot = ServerDataRootResolver.Resolve(projectRoot, worldId).DataRoot;
        var path = Path.Combine(dataRoot, "npcs.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var npcs = document.RootElement.GetProperty("records").EnumerateArray().Select(RuntimeNpcDefinition.FromJson).ToArray();
        var rows = npcs.SelectMany(npc => npc.Attacks.Select(attack => (npc.Name, Attack: attack))).ToArray();
        static string Key(string name, RuntimeNpcAttackDefinition attack) =>
            $"{name.Trim().ToUpperInvariant()}|{attack.Slot}|{attack.HitMessageLegacyId}";
        static Dictionary<string, string> Consensus(
            IEnumerable<(string Name, RuntimeNpcAttackDefinition Attack)> source,
            Func<RuntimeNpcAttackDefinition, string> selector) => source
            .Where(row => !string.IsNullOrWhiteSpace(selector(row.Attack)))
            .GroupBy(row => Key(row.Name, row.Attack), StringComparer.Ordinal)
            .Select(group => (group.Key, Values: group.Select(row => selector(row.Attack).Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()))
            .Where(group => group.Values.Length == 1)
            .ToDictionary(group => group.Key, group => group.Values[0], StringComparer.Ordinal);
        return new(Consensus(rows, attack => attack.MissVerb), Consensus(rows, attack => attack.WeaponName));
    }

    public RuntimeNpcAttackDefinition Complete(string npcName, RuntimeNpcAttackDefinition attack)
    {
        var key = $"{npcName.Trim().ToUpperInvariant()}|{attack.Slot}|{attack.HitMessageLegacyId}";
        var verb = attack.MissVerb;
        var weapon = attack.WeaponName;
        if (string.IsNullOrWhiteSpace(verb) && _verbs.TryGetValue(key, out var recoveredVerb)) verb = recoveredVerb;
        if (string.IsNullOrWhiteSpace(weapon) && _weapons.TryGetValue(key, out var recoveredWeapon)) weapon = recoveredWeapon;
        return attack with { MissVerb = verb, WeaponName = weapon };
    }
}

public sealed class RuntimeNpcDefinition
{
    public string Id { get; init; } = string.Empty;
    public int LegacyId { get; init; }
    public int GroupId { get; init; }
    public string Name { get; init; } = string.Empty;
    public int Level { get; init; }
    public int HitPoints { get; init; }
    public int Energy { get; init; }
    public string Alignment { get; init; } = string.Empty;
    public int ArmorClass { get; init; }
    public int DamageResistance { get; init; }
    public int MagicResistance { get; init; }
    public int CharmResistance { get; init; }
    public int CharmLevel { get; init; }
    public int FollowValue { get; init; }
    public int RegenTime { get; init; }
    public int RespawnHours { get; init; }
    public bool Undead { get; init; }
    public bool NonLiving { get; init; }
    public int HitPointRegen { get; init; }
    public int GameLimit { get; init; }
    public int Gender { get; init; }
    public int MonsterType { get; init; }
    public decimal ExperienceMultiplier { get; init; } = 1m;
    public int BackstabDefense { get; init; }
    public bool SeeHidden { get; init; }
    public bool Animal { get; init; }
    public IReadOnlyList<int> NegatedAbilityIds { get; init; } = Array.Empty<int>();
    public IReadOnlyList<int> GuardedNpcLegacyIds { get; init; } = Array.Empty<int>();
    public int WaterResistance { get; init; }
    public int SpellImmunityLevel { get; init; }
    public int MagicalRequirement { get; init; }
    public int RequiredToHitCode { get; init; }
    public int NonAttackItemLegacyId { get; init; }
    public int AttackCount { get; init; }
    public long ExperienceReward { get; init; }
    public long CopperReward { get; init; }
    public long SilverReward { get; init; }
    public long GoldReward { get; init; }
    public long PlatinumReward { get; init; }
    public long RunicReward { get; init; }
    public bool LootKnown { get; init; }
    public int LootTableEntries { get; init; }
    public IReadOnlyList<RuntimeNpcLootDefinition> Loot { get; init; } = Array.Empty<RuntimeNpcLootDefinition>();
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
        var publicReference = element.TryGetProperty("public_reference_0139", out var publicReferenceElement) && publicReferenceElement.ValueKind == JsonValueKind.Object
            ? publicReferenceElement
            : default;
        var publicFields = publicReference.ValueKind == JsonValueKind.Object && publicReference.TryGetProperty("fields", out var publicFieldsElement) && publicFieldsElement.ValueKind == JsonValueKind.Object
            ? publicFieldsElement
            : default;

        var definition = new RuntimeNpcDefinition
        {
            Id = JsonValueReader.ReadString(element, "id", string.Empty),
            LegacyId = JsonValueReader.ReadInt(element, "legacy_id", 0),
            GroupId = behavior.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadInt(behavior, "group_id", 0) : 0,
            Name = JsonValueReader.ReadString(element, "name", "Unknown NPC"),
            Level = combat.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadInt(combat, "level", 0) : 0,
            HitPoints = combat.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadInt(combat, "hp", 0) : 0,
            Energy = combat.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadInt(combat, "energy", 0) : 0,
            Alignment = behavior.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadString(behavior, "alignment", string.Empty) : string.Empty,
            ArmorClass = combat.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadInt(combat, "armor_class", 0) : 0,
            DamageResistance = combat.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadInt(combat, "damage_resistance", 0) : 0,
            MagicResistance = combat.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadInt(combat, "magic_resistance", 0) : 0,
            CharmResistance = behavior.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadInt(behavior, "charm_resistance", 0) : 0,
            CharmLevel = behavior.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadInt(behavior, "charm_level", 0) : 0,
            FollowValue = behavior.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadInt(behavior, "follow", 0) : 0,
            RegenTime = behavior.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadInt(behavior, "regen_time", 0) : 0,
            RespawnHours = behavior.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadInt(behavior, "respawn_hours", 0) : 0,
            Undead = behavior.ValueKind == JsonValueKind.Object && JsonValueReader.ReadBool(behavior, "undead", false),
            NonLiving = HasAbility(element, 109),
            HitPointRegen = publicFields.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadInt(publicFields, "hp_regen", 0) : 0,
            GameLimit = publicFields.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadInt(publicFields, "game_limit", 0) : 0,
            Gender = publicFields.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadInt(publicFields, "gender", 0) : 0,
            MonsterType = publicFields.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadInt(publicFields, "monster_type", 0) : 0,
            ExperienceMultiplier = ReadDecimal(publicFields, "experience_multiplier", 1m),
            BackstabDefense = publicFields.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadInt(publicFields, "backstab_defense", 0) : 0,
            SeeHidden = HasAbility(element, 57),
            Animal = HasAbility(element, 78),
            NegatedAbilityIds = ReadAbilityValues(element, 124).Where(value => value > 0).Distinct().OrderBy(value => value).ToArray(),
            GuardedNpcLegacyIds = ReadAbilityValues(element, 146).Where(value => value > 0).ToArray(),
            WaterResistance = ReadAbilityValues(element, 147).Where(value => value > 0).DefaultIfEmpty(0).Max(),
            SpellImmunityLevel = ReadAbilityValues(element, 139).Where(value => value > 0).DefaultIfEmpty(0).Max(),
            MagicalRequirement = ReadAbilityValue(element, 28),
            RequiredToHitCode = ReadAbilityValue(element, 158),
            NonAttackItemLegacyId = ReadAbilityValue(element, 185),
            AttackCount = combat.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadInt(combat, "attack_count", 0) : 0,
            ExperienceReward = rewards.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadInt(rewards, "xp_base", 0) : 0,
            CopperReward = rewards.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadInt(rewards, "copper", 0) : 0,
            SilverReward = rewards.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadInt(rewards, "silver", 0) : 0,
            GoldReward = rewards.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadInt(rewards, "gold", 0) : 0,
            PlatinumReward = rewards.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadInt(rewards, "platinum", 0) : 0,
            RunicReward = rewards.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadInt(rewards, "runic", 0) : 0,
            LootKnown = rewards.ValueKind == JsonValueKind.Object && bool.TryParse(JsonValueReader.ReadString(rewards, "loot_known", "false"), out var lootKnown) && lootKnown,
            LootTableEntries = rewards.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadInt(rewards, "loot_table_entries", 0) : 0,
            Loot = ReadLoot(publicFields),
            Attacks = ReadAttacks(element, publicFields, combat.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadInt(combat, "attack_count", 0) : 0)
        };
        return RuntimeClassicNpcNativeFieldContract.Validate(definition);
    }

    private static IReadOnlyList<int> ReadAbilityValues(JsonElement element, int abilityId)
    {
        if (!element.TryGetProperty("public_reference_0139", out var reference)
            || !reference.TryGetProperty("fields", out var fields)
            || !fields.TryGetProperty("abilities_named", out var abilities)
            || abilities.ValueKind != JsonValueKind.Array) return Array.Empty<int>();
        return abilities.EnumerateArray()
            .Where(ability => JsonValueReader.ReadInt(ability, "ability_id", 0) == abilityId)
            .Select(ability => JsonValueReader.ReadInt(ability, "value", 0))
            .ToArray();
    }


    public bool ResistsCharm(int controlPower)
    {
        if (controlPower < 0) throw new ArgumentOutOfRangeException(nameof(controlPower));
        return CharmResistance > controlPower;
    }

    private static decimal ReadDecimal(JsonElement element, string property, decimal fallback)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(property, out var value)) return fallback;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number)) return number;
        return decimal.TryParse(value.ToString(), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    }

    private static bool HasAbility(JsonElement element, int legacyAbilityId)
    {
        if (!element.TryGetProperty("abilities", out var abilities) || abilities.ValueKind != JsonValueKind.Array) return false;
        foreach (var ability in abilities.EnumerateArray())
        {
            if (JsonValueReader.ReadInt(ability, "ability_id", 0) == legacyAbilityId) return true;
        }
        return false;
    }

    private static int ReadAbilityValue(JsonElement element, int legacyAbilityId)
    {
        if (!element.TryGetProperty("abilities", out var abilities) || abilities.ValueKind != JsonValueKind.Array) return 0;
        var total = 0;
        foreach (var ability in abilities.EnumerateArray())
        {
            if (JsonValueReader.ReadInt(ability, "ability_id", 0) != legacyAbilityId) continue;
            total = checked(total + JsonValueReader.ReadInt(ability, "value", 0));
        }
        return total;
    }

    private static IReadOnlyList<RuntimeNpcLootDefinition> ReadLoot(JsonElement publicFields)
    {
        if (publicFields.ValueKind != JsonValueKind.Object
            || !publicFields.TryGetProperty("loot_named", out var loot)
            || loot.ValueKind != JsonValueKind.Array)
            return Array.Empty<RuntimeNpcLootDefinition>();

        return loot.EnumerateArray().Select(entry =>
        {
            var itemId = JsonValueReader.ReadInt(entry, "item_id", 0);
            var percent = JsonValueReader.ReadInt(entry, "drop_percent", 0);
            if (itemId <= 0) throw new InvalidDataException($"NPC_LOOT_ITEM_ID_INVALID:{itemId}");
            if (percent is < 0 or > 100) throw new InvalidDataException($"NPC_LOOT_PERCENT_INVALID:{itemId}:{percent}");
            return new RuntimeNpcLootDefinition(
                itemId,
                $"item.{itemId}",
                JsonValueReader.ReadString(entry, "name", $"item {itemId}"),
                percent);
        }).ToArray();
    }

    private static IReadOnlyList<RuntimeNpcAttackDefinition> ReadAttacks(JsonElement element, JsonElement publicFields, int declaredAttackCount)
    {
        if (declaredAttackCount <= 0 || !element.TryGetProperty("attacks", out var attacks) || attacks.ValueKind != JsonValueKind.Array)
            return Array.Empty<RuntimeNpcAttackDefinition>();

        // attack_count is the decoded executable row count. WCC records can retain fixed-width
        // trailing rows (including type 229 sentinels) and can expose more physical rows than the
        // authored count. Those rows are evidence, not additional live attacks.
        var publicAttacks = publicFields.ValueKind == JsonValueKind.Object &&
            publicFields.TryGetProperty("attacks", out var publicAttackArray) && publicAttackArray.ValueKind == JsonValueKind.Array
            ? publicAttackArray.EnumerateArray().ToArray()
            : Array.Empty<JsonElement>();
        return attacks.EnumerateArray()
            .Select(attack =>
            {
                var slot = JsonValueReader.ReadInt(attack, "slot", 0);
                var publicAttack = slot > 0 && slot <= publicAttacks.Length ? publicAttacks[slot - 1] : default;
                return new RuntimeNpcAttackDefinition(
                slot,
                JsonValueReader.ReadInt(attack, "type", 0),
                JsonValueReader.ReadInt(attack, "minimum_damage", 0),
                JsonValueReader.ReadInt(attack, "maximum_damage", 0),
                JsonValueReader.ReadInt(attack, "selection_percent", 0),
                JsonValueReader.ReadInt(attack, "accuracy", 0),
                JsonValueReader.ReadInt(attack, "hit_spell_legacy_id", 0),
                JsonValueReader.ReadInt(attack, "energy_cost", 0),
                JsonValueReader.ReadInt(attack, "hit_message_legacy_id", 0),
                JsonValueReader.ReadInt(attack, "miss_message_legacy_id", 0),
                JsonValueReader.ReadInt(attack, "dodge_message_legacy_id", 0),
                publicAttack.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadString(publicAttack, "atk_verb3", string.Empty) : string.Empty,
                JsonValueReader.ReadString(attack, "miss_verb3",
                    publicAttack.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadString(publicAttack, "miss_verb3", string.Empty) : string.Empty),
                JsonValueReader.ReadString(attack, "weapon_name",
                    publicAttack.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadString(publicAttack, "weapon_name", string.Empty) : string.Empty),
                JsonValueReader.ReadBool(attack, "native_empty_miss_arguments_verified", false));
            })
            .Where(IsExecutableAttackRow)
            .OrderBy(attack => attack.Slot)
            .Take(declaredAttackCount)
            .ToArray();
    }

    private static bool IsExecutableAttackRow(RuntimeNpcAttackDefinition attack) =>
        attack.Slot > 0 &&
        (attack.MinimumDamage != 0 || attack.MaximumDamage != 0 || attack.SelectionPercent != 0 ||
         attack.Accuracy != 0 || attack.HitSpellLegacyId != 0 || attack.EnergyCost != 0 ||
         attack.HitMessageLegacyId != 0 || attack.MissMessageLegacyId != 0 || attack.DodgeMessageLegacyId != 0);
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
    public int MaximumAreaPopulation { get; init; }
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
            MaximumAreaPopulation = model.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadInt(model, "max_area_cap", 0) : 0,
            InteractionModel = model.ValueKind == JsonValueKind.Object ? JsonValueReader.ReadString(model, "interaction_model", string.Empty) : string.Empty,
            GroupDefinitions = groupDefinitions ?? Array.Empty<RuntimeNpcDefinition>(),
            BossDefinition = bossDefinition
        };
    }
}

// Consolidated from RuntimeNpcAttackSelection.cs during Bundle B runtime closure.
public sealed record RuntimeNpcAttackSelectionResult(
    RuntimeNpcAttackDefinition? Attack,
    bool Affordable,
    int Roll,
    IReadOnlyList<RuntimeNpcAttackDefinition> OrderedAttacks);

/// <summary>
/// Classic NPC attack-row selection recovered directly from WCCMMUD.DLL. One native [0,100)
/// roll is compared against authored Att%-x byte thresholds in slot order using roll < threshold.
/// The first matching row is selected even when its energy cost cannot currently be paid; native
/// combat does not remove unaffordable rows and re-normalize the remaining rows.
/// </summary>
public static class RuntimeNpcAttackSelector
{
    public const string FormulaId = "combat.npc_attack_selection_threshold";

    public static RuntimeNpcAttackSelectionResult Select(
        IReadOnlyList<RuntimeNpcAttackDefinition> attacks,
        int currentEnergy,
        int roll)
    {
        ArgumentNullException.ThrowIfNull(attacks);
        if (currentEnergy < 0) throw new ArgumentOutOfRangeException(nameof(currentEnergy));
        if (roll is < 0 or >= 100)
            throw new ArgumentOutOfRangeException(nameof(roll), "Native roll must be within 0..99.");
        if (attacks.Any(x => x.EnergyCost < 0))
            throw new InvalidDataException("NPC_COMBAT_NEGATIVE_ATTACK_ENERGY_COST");
        if (attacks.Any(x => x.SelectionPercent < 0))
            throw new InvalidDataException("NPC_COMBAT_NEGATIVE_ATTACK_SELECTION_THRESHOLD");

        var ordered = attacks
            .Where(x => x.Type > 0 && x.SelectionPercent > 0)
            .OrderBy(x => x.Slot)
            .ToArray();
        var selected = ordered.FirstOrDefault(x => roll < x.SelectionPercent);
        return new RuntimeNpcAttackSelectionResult(
            selected,
            selected is not null && selected.EnergyCost <= currentEnergy,
            roll,
            ordered);
    }

    public static RuntimeNpcAttackSelectionResult Select(
        IReadOnlyList<RuntimeNpcAttackDefinition> attacks,
        int currentEnergy,
        Random random)
    {
        ArgumentNullException.ThrowIfNull(random);
        return Select(attacks, currentEnergy, random.Next(0, 100));
    }
}

// Consolidated from RuntimeNpcDefensiveProfiles.cs during Bundle B runtime closure.
public sealed record RuntimeNpcDefensiveProfile(
    string NpcId,
    int ArmorClass,
    int DamageResistance,
    int MagicResistance,
    int BackstabDefense,
    int DefenseValue,
    int DodgeBonus,
    string DefenseAuthority,
    string DodgeAuthority)
{
    public ClassicCombatantStats ToCombatantStats(int attackValue = 0, int criticalChance = 0)
        => new(attackValue, DefenseValue, ArmorClass, DamageResistance, DodgeBonus, 0, criticalChance);
}

public static class RuntimeNpcDefensiveProfileResolver
{
    public static RuntimeNpcDefensiveProfile Resolve(RuntimeNpcDefinition npc)
    {
        ArgumentNullException.ThrowIfNull(npc);
        if (string.IsNullOrWhiteSpace(npc.Id))
            throw new InvalidOperationException("NPC_DEFENSE_ID_MISSING");
        if (npc.HitPoints <= 0)
            throw new InvalidOperationException($"NPC_DEFENSE_HP_INVALID:{npc.Id}");

        // Classic NPC defense is represented directly by the generated monster AC and DR fields.
        // The retained attack simulator's dodge input is explicitly user-side (nUserDodge), and
        // the generated NPC schema contains no separate NPC dodge field. Therefore no additional
        // defense or dodge value is synthesized here.
        return new RuntimeNpcDefensiveProfile(
            npc.Id,
            npc.ArmorClass,
            npc.DamageResistance,
            npc.MagicResistance,
            npc.BackstabDefense,
            DefenseValue: 0,
            DodgeBonus: 0,
            DefenseAuthority: "generated-npc-armor-class-and-damage-resistance",
            DodgeAuthority: "not-applicable-to-npc-defender; retained-simulator-input-is-player-side");
    }
}

public sealed class RuntimeNpcDefensiveProfileSmokeReport
{
    public bool Passed => Failures.Count == 0;
    public int NpcCount { get; private set; }
    public int ReadyProfileCount { get; private set; }
    public List<string> Checks { get; } = new();
    public List<string> Failures { get; } = new();

    public static RuntimeNpcDefensiveProfileSmokeReport Run(string projectRoot)
    {
        var report = new RuntimeNpcDefensiveProfileSmokeReport();
        try
        {
            var dataRoot = ServerDataRootResolver.ResolveDefault(projectRoot).DataRoot;
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(dataRoot, "npcs.json")));
            var npcs = document.RootElement.GetProperty("records")
                .EnumerateArray()
                .Select(RuntimeNpcDefinition.FromJson)
                .ToArray();

            report.NpcCount = npcs.Length;
            Require(npcs.Length == 1100, "npc-count");
            report.Checks.Add("npc-count");

            var profiles = npcs.Select(RuntimeNpcDefensiveProfileResolver.Resolve).ToArray();
            report.ReadyProfileCount = profiles.Length;
            Require(profiles.Length == npcs.Length, "all-npc-defensive-profiles-ready");
            report.Checks.Add("all-npc-defensive-profiles-ready");

            Require(profiles.All(x => x.DefenseValue == 0), "no-synthetic-npc-defense-value");
            report.Checks.Add("no-synthetic-npc-defense-value");

            Require(profiles.All(x => x.DodgeBonus == 0), "no-synthetic-npc-dodge-value");
            report.Checks.Add("no-synthetic-npc-dodge-value");

            Require(profiles.Any(x => x.ArmorClass != 0), "generated-npc-armor-class");
            report.Checks.Add("generated-npc-armor-class");

            Require(profiles.Any(x => x.DamageResistance > 0), "generated-npc-damage-resistance");
            report.Checks.Add("generated-npc-damage-resistance");

            Require(profiles.Any(x => x.BackstabDefense > 0), "generated-npc-backstab-defense");
            report.Checks.Add("generated-npc-backstab-defense");

            var backstabSample = profiles.First(x => x.BackstabDefense > 0);
            var backstabHitChance = ClassicCombatResolver.CalculateHitChance(
                ClassicAttackType.Backstab,
                new ClassicCombatantStats(backstabSample.BackstabDefense + 50, 0, 0, 0, 0),
                new ClassicCombatantStats(0, backstabSample.BackstabDefense, 999, 0, 0));
            Require(backstabHitChance == 50, "backstab-defense-av-minus-dv-ignores-ac");
            report.Checks.Add("backstab-defense-av-minus-dv-ignores-ac");

            var sample = profiles.First(x => x.ArmorClass != 0 || x.DamageResistance > 0);
            var combatant = sample.ToCombatantStats();
            Require(combatant.ArmorClass == sample.ArmorClass && combatant.DamageResistance == sample.DamageResistance,
                "classic-combatant-mapping");
            report.Checks.Add("classic-combatant-mapping");
        }
        catch (Exception ex)
        {
            report.Failures.Add(ex.Message);
        }

        return report;
    }

    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

    private static void Require(bool condition, string code)
    {
        if (!condition) throw new InvalidOperationException(code);
    }
}
