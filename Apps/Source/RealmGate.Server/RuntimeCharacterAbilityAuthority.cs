using System.Text.Json;

namespace RealmGate.Server;

// Consolidated from RuntimeCharacterAbilityValues.cs
public static class RuntimeCharacterAbilityValues
{
    public const string ConditionPrefix = "quest.ability.";

    public static int Read(IReadOnlyDictionary<int, int> values, int abilityId) =>
        values.TryGetValue(RequireAbilityId(abilityId), out var value) ? value : 0;

    public static void Give(IDictionary<int, int> values, int abilityId, int value) =>
        values[RequireAbilityId(abilityId)] = value;

    public static int Add(IDictionary<int, int> values, int abilityId, int delta)
    {
        abilityId = RequireAbilityId(abilityId);
        var value = checked(Read(new Dictionary<int, int>(values), abilityId) + delta);
        values[abilityId] = value;
        return value;
    }

    public static bool Check(IReadOnlyDictionary<int, int> values, int abilityId, int expected) => Read(values, abilityId) == expected;
    public static bool Test(IReadOnlyDictionary<int, int> values, int abilityId, int minimum) => Read(values, abilityId) >= minimum;
    public static bool Fail(IReadOnlyDictionary<int, int> values, int abilityId) => Read(values, abilityId) == 0;

    public static Dictionary<int, int> Restore(IReadOnlyDictionary<string, string> conditions)
    {
        var values = new Dictionary<int, int>();
        foreach (var pair in conditions)
        {
            if (!pair.Key.StartsWith(ConditionPrefix, StringComparison.OrdinalIgnoreCase)) continue;
            var suffix = pair.Key[ConditionPrefix.Length..];
            // Provenance keys such as quest.ability.source.<id> share the namespace but are not values.
            if (!int.TryParse(suffix, out var abilityId)) continue;
            if (abilityId <= 0)
                throw new InvalidDataException($"Invalid persisted ability key '{pair.Key}'.");
            if (!int.TryParse(pair.Value, out var value))
                throw new InvalidDataException($"Invalid persisted ability value for '{pair.Key}'.");
            values.Add(abilityId, value);
        }
        return values;
    }

    public static void Persist(IReadOnlyDictionary<int, int> values, IDictionary<string, string> conditions)
    {
        foreach (var key in conditions.Keys.Where(IsPersistedAbilityValueKey).ToArray()) conditions.Remove(key);
        foreach (var pair in values.OrderBy(x => x.Key)) conditions[ConditionPrefix + pair.Key] = pair.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    public static IReadOnlyList<string> Validate(IReadOnlyDictionary<int, int> values, RuntimeAbilityCatalog catalog)
    {
        var errors = new List<string>();
        foreach (var abilityId in values.Keys)
            if (!catalog.TryGet($"ability.{abilityId}", out _)) errors.Add($"unknown-ability-value:ability.{abilityId}");
        return errors;
    }

    private static bool IsPersistedAbilityValueKey(string key)
    {
        if (!key.StartsWith(ConditionPrefix, StringComparison.OrdinalIgnoreCase)) return false;
        return int.TryParse(key[ConditionPrefix.Length..], out var abilityId) && abilityId > 0;
    }

    private static int RequireAbilityId(int abilityId) => abilityId > 0 ? abilityId : throw new ArgumentOutOfRangeException(nameof(abilityId));
}

// Consolidated from RuntimeCharacterQuestAbilityCatalog.cs
public sealed record RuntimeCharacterQuestAbilityOperation(
    string QuestId,
    string QuestName,
    int TextblockId,
    int Line,
    int Sequence,
    string Opcode,
    int AbilityId,
    int AbilityValue,
    string AbilityName,
    bool MutatesCharacterState,
    string EvidenceStatus);


public sealed record RuntimeCharacterSearchCapabilityProjection(
    int SeeHiddenRank,
    IReadOnlyDictionary<string, int> Modifiers,
    string EvidenceStatus,
    string RuntimeOwner);

public sealed record RuntimeCharacterQuestDerivedBonuses(
    int ArmourClassBonus,
    int MaximumDamageBonus,
    int AccuracyPercentBonus,
    int StealthBonus,
    int DodgeBonus,
    int CriticalBonus,
    int MaximumManaBonus,
    int SpellcastingBonus,
    int BackstabMinimumBonus,
    int BackstabMaximumBonus,
    int SeeHiddenRank,
    string EvidenceStatus,
    string RuntimeOwner);

/// <summary>
/// Authoritative projection for decoded quest ability operations. Quest progression and rewards
/// are stored as persistent ability values; downstream stat, spell, item, and command consumers
/// must key from these values rather than inventing direct quest rewards.
/// </summary>
public sealed class RuntimeCharacterQuestAbilityCatalog
{
    public const int ArmourClassAbilityId = 2;
    public const int MaximumDamageAbilityId = 4;
    public const int AccuracyAbilityId = 22;
    public const int StealthAbilityId = 27;
    public const int SmashAbilityId = 32;
    public const int DodgeAbilityId = 34;
    public const int SeeHiddenAbilityId = 57;
    public const int CriticalAbilityId = 58;
    public const int MaximumManaAbilityId = 69;
    public const int SpellcastingAbilityId = 70;
    public const int BackstabMinimumAbilityId = 117;
    public const int BackstabMaximumAbilityId = 118;
    public const int IceSorceressQuestAbilityId = 125;
    public const int GoodQuestAbilityId = 126;
    public const int NeutralQuestAbilityId = 127;
    public const int EvilQuestAbilityId = 128;
    public const int DarkDruidQuestAbilityId = 129;
    public const int BloodChampionQuestAbilityId = 130;
    public const int SheDragonQuestAbilityId = 131;
    public const int WereratQuestAbilityId = 132;
    public const int PhoenixQuestAbilityId = 133;
    public const int DaoLordQuestAbilityId = 134;
    public const int PerfectStealthAbilityId = 186;
    public const int MeditateAbilityId = 187;

    public static IReadOnlyDictionary<int, string> QuestProgressionAbilities { get; } =
        new Dictionary<int, string>
        {
            [IceSorceressQuestAbilityId] = "IceSorcQuest",
            [GoodQuestAbilityId] = "GoodQuest",
            [NeutralQuestAbilityId] = "NeutralQuest",
            [EvilQuestAbilityId] = "EvilQuest",
            [DarkDruidQuestAbilityId] = "DarkDruidQuest",
            [BloodChampionQuestAbilityId] = "BloodChampQuest",
            [SheDragonQuestAbilityId] = "SheDragonQuest",
            [WereratQuestAbilityId] = "WereratQuest",
            [PhoenixQuestAbilityId] = "PhoenixQuest",
            [DaoLordQuestAbilityId] = "DaoLordQuest"
        };

    private static readonly HashSet<int> ExecutableQuestCapabilityIds = new()
    {
        SmashAbilityId,
        PerfectStealthAbilityId,
        MeditateAbilityId
    };

    private readonly Dictionary<int, string> _abilityNames = new();
    private readonly List<RuntimeCharacterQuestAbilityOperation> _operations = new();

    public IReadOnlyList<RuntimeCharacterQuestAbilityOperation> Operations => _operations
        .OrderBy(x => x.QuestId).ThenBy(x => x.TextblockId).ThenBy(x => x.Line).ThenBy(x => x.Sequence).ToArray();
    public IReadOnlyList<RuntimeCharacterQuestAbilityOperation> Grants => Operations.Where(x => x.MutatesCharacterState).ToArray();

    public static RuntimeCharacterQuestAbilityCatalog Load(string projectRoot, string worldId)
    {
        var catalog = new RuntimeCharacterQuestAbilityCatalog();
        catalog.LoadAbilities(Path.Combine(projectRoot, "Data", worldId, "abilities.json"));
        catalog.LoadQuests(Path.Combine(projectRoot, "Data", worldId, "quests.json"));
        return catalog;
    }

    public RuntimeCharacterRecord ApplyGrant(RuntimeCharacterRecord character, int abilityId, int value, string sourceId)
    {
        if (!_abilityNames.ContainsKey(abilityId)) throw new InvalidOperationException($"Unknown decoded quest ability ability.{abilityId}.");
        if (string.IsNullOrWhiteSpace(sourceId)) throw new InvalidOperationException("Quest ability grant requires provenance.");
        var values = RuntimeCharacterAbilityValues.Restore(character.Conditions);
        RuntimeCharacterAbilityValues.Give(values, abilityId, value);
        var conditions = new Dictionary<string, string>(character.Conditions, StringComparer.OrdinalIgnoreCase);
        RuntimeCharacterAbilityValues.Persist(values, conditions);
        conditions[$"quest.ability.source.{abilityId}"] = sourceId.Trim();
        return character with { Conditions = conditions, UpdatedAtUtc = DateTimeOffset.UtcNow };
    }

    public bool HasAtLeast(RuntimeCharacterRecord character, int abilityId, int minimumValue) =>
        RuntimeCharacterAbilityValues.Test(RuntimeCharacterAbilityValues.Restore(character.Conditions), abilityId, minimumValue);

    public bool HasUnlockedCapability(RuntimeCharacterRecord character, int abilityId) =>
        RuntimeCharacterAbilityValues.Read(RuntimeCharacterAbilityValues.Restore(character.Conditions), abilityId) > 0;

    public bool CanUseCapability(
        RuntimeCharacterRecord character,
        int abilityId,
        RuntimeCharacterWorldRewardCatalog gates)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(gates);
        return HasUnlockedCapability(character, abilityId)
            && gates.MeetsClassAbilityGate(abilityId, character.ClassName, character.Level);
    }

    public static bool IsQuestProgressionAbility(int abilityId) => QuestProgressionAbilities.ContainsKey(abilityId);

    public bool TryGetAbilityName(int abilityId, out string abilityName) =>
        _abilityNames.TryGetValue(abilityId, out abilityName!);

    public RuntimeCharacterQuestDerivedBonuses ResolveDerivedBonuses(RuntimeCharacterRecord character)
    {
        var values = RuntimeCharacterAbilityValues.Restore(character.Conditions);
        return new RuntimeCharacterQuestDerivedBonuses(
            ArmourClassBonus: RuntimeCharacterAbilityValues.Read(values, ArmourClassAbilityId),
            MaximumDamageBonus: RuntimeCharacterAbilityValues.Read(values, MaximumDamageAbilityId),
            AccuracyPercentBonus: RuntimeCharacterAbilityValues.Read(values, AccuracyAbilityId),
            StealthBonus: RuntimeCharacterAbilityValues.Read(values, StealthAbilityId),
            DodgeBonus: RuntimeCharacterAbilityValues.Read(values, DodgeAbilityId),
            CriticalBonus: RuntimeCharacterAbilityValues.Read(values, CriticalAbilityId),
            MaximumManaBonus: RuntimeCharacterAbilityValues.Read(values, MaximumManaAbilityId),
            SpellcastingBonus: RuntimeCharacterAbilityValues.Read(values, SpellcastingAbilityId),
            BackstabMinimumBonus: RuntimeCharacterAbilityValues.Read(values, BackstabMinimumAbilityId),
            BackstabMaximumBonus: RuntimeCharacterAbilityValues.Read(values, BackstabMaximumAbilityId),
            SeeHiddenRank: RuntimeCharacterAbilityValues.Read(values, SeeHiddenAbilityId),
            EvidenceStatus: "verified decoded ability semantics",
            RuntimeOwner: "Bundle A projection; Bundle B/D consumers");
    }

    public bool HasSeeHidden(RuntimeCharacterRecord character) =>
        ResolveDerivedBonuses(character).SeeHiddenRank > 0;

    /// <summary>
    /// Character-side handoff for Bundle D. The decoded See Hidden rank is exposed as an explicit
    /// search modifier without guessing its numeric weight inside the Perception formula.
    /// </summary>
    public RuntimeCharacterSearchCapabilityProjection ResolveSearchCapability(RuntimeCharacterRecord character)
    {
        var rank = ResolveDerivedBonuses(character).SeeHiddenRank;
        return new RuntimeCharacterSearchCapabilityProjection(
            rank,
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["quest.ability.57.see_hidden_rank"] = rank
            },
            "verified decoded ability identity; formula weight unresolved",
            "Bundle A character projection; Bundle D search formula consumer");
    }

    public bool TryResolveAbilityIdByName(string abilityName, out int abilityId)
    {
        var normalized = NormalizeName(abilityName);
        var matches = _abilityNames.Where(pair => NormalizeName(pair.Value) == normalized).Select(pair => pair.Key).Distinct().ToArray();
        abilityId = matches.Length == 1 ? matches[0] : 0;
        return abilityId > 0;
    }

    public RuntimeCharacterRecord ApplyNamedGrant(RuntimeCharacterRecord character, string abilityName, int value, string sourceId)
    {
        if (!TryResolveAbilityIdByName(abilityName, out var abilityId))
            throw new InvalidOperationException($"Decoded ability name '{abilityName}' does not resolve uniquely.");
        return ApplyGrant(character, abilityId, value, sourceId);
    }

    /// <summary>
    /// Resolves persisted quest ability flags that are proven to unlock live command abilities.
    /// Quest progression flags (GoodQuest, NeutralQuest, EvilQuest, etc.) remain state only and
    /// are never exposed as executable powers.
    /// </summary>
    public IReadOnlyList<string> ResolveExecutablePowerIds(
        RuntimeCharacterRecord character,
        RuntimeAbilityCatalog abilityCatalog,
        RuntimeCharacterWorldRewardCatalog? gateCatalog = null)
    {
        var values = RuntimeCharacterAbilityValues.Restore(character.Conditions);
        var resolved = new List<string>();
        foreach (var abilityId in ExecutableQuestCapabilityIds.OrderBy(x => x))
        {
            if (RuntimeCharacterAbilityValues.Read(values, abilityId) <= 0) continue;
            if (gateCatalog is not null && !gateCatalog.MeetsClassAbilityGate(abilityId, character.ClassName, character.Level))
                continue;
            var canonicalId = $"ability.{abilityId}";
            if (!abilityCatalog.TryGet(canonicalId, out _))
                throw new InvalidDataException($"Persisted quest capability {canonicalId} has no generated ability record.");
            resolved.Add(canonicalId);
        }
        return resolved;
    }

    private void LoadAbilities(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var record in document.RootElement.GetProperty("records").EnumerateArray())
        {
            var id = ReadInt(record, "legacy_id");
            if (id <= 0) continue;
            _abilityNames[id] = ReadString(record, "name");
        }
    }

    private void LoadQuests(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var quest in document.RootElement.GetProperty("records").EnumerateArray())
        {
            var questId = ReadString(quest, "id");
            var questName = ReadString(quest, "name");
            Scan(quest, questId, questName, 0);
        }
    }

    private void Scan(JsonElement element, string questId, string questName, int inheritedTextblock)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray()) Scan(child, questId, questName, inheritedTextblock);
            return;
        }
        if (element.ValueKind != JsonValueKind.Object) return;
        var textblock = ReadInt(element, "textblock_id");
        if (textblock <= 0) textblock = inheritedTextblock;
        var opcode = ReadString(element, "opcode");
        var abilityId = ReadInt(element, "ability_id");
        if (abilityId > 0 && IsAbilityOpcode(opcode))
        {
            if (!_abilityNames.TryGetValue(abilityId, out var abilityName))
                throw new InvalidDataException($"Quest '{questId}' references missing ability.{abilityId}.");
            var mutates = opcode.Equals("giveability", StringComparison.OrdinalIgnoreCase) ||
                          opcode.Equals("addability", StringComparison.OrdinalIgnoreCase);
            _operations.Add(new RuntimeCharacterQuestAbilityOperation(
                questId, questName, textblock, ReadInt(element, "line"), ReadInt(element, "sequence"), opcode,
                abilityId, ReadInt(element, "ability_value"), abilityName, mutates,
                mutates ? "verified decoded quest ability mutation" : "verified decoded quest ability condition"));
        }
        foreach (var property in element.EnumerateObject()) Scan(property.Value, questId, questName, textblock);
    }

    private static bool IsAbilityOpcode(string opcode) => opcode.Equals("giveability", StringComparison.OrdinalIgnoreCase) ||
        opcode.Equals("addability", StringComparison.OrdinalIgnoreCase) || opcode.Equals("checkability", StringComparison.OrdinalIgnoreCase) ||
        opcode.Equals("testability", StringComparison.OrdinalIgnoreCase) || opcode.Equals("failability", StringComparison.OrdinalIgnoreCase);

    private static string ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;
    private static string NormalizeName(string value) => new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static int ReadInt(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value)) return 0;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) return number;
        return value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out number) ? number : 0;
    }
}

// Consolidated from RuntimeCharacterWorldRewardCatalog.cs
public sealed record RuntimeCharacterWorldRewardAnchor(
    string Id,
    string TriggerKind,
    string SourceName,
    string? RaceRequirement,
    string RewardKind,
    string RewardName,
    int? MinimumLevel,
    int? MaximumLevel,
    string LevelGateEvidenceStatus,
    string EvidenceStatus,
    bool DecodedActivationResolved,
    string DecodedSource,
    string RuntimeOwner)
{
    public bool LevelGateResolved => MinimumLevel.HasValue;
}

public sealed record RuntimeCharacterClassAbilityGate(
    int AbilityId,
    string AbilityName,
    int ClassLegacyNumber,
    string ClassName,
    int MinimumLevel,
    string SourceTextblock,
    string EvidenceStatus);

public sealed record RuntimeCharacterRewardEligibility(bool Eligible, string Reason, RuntimeCharacterWorldRewardAnchor Reward);

/// <summary>
/// Evidence-backed character reward and class capability gates decoded from Classic textblock scripts.
/// Fixed world gates and variable class gates are stored separately so a generic level can never replace
/// the class-specific minlevel operations used by Smash, Meditate, and Perfect Stealth.
/// </summary>
public sealed class RuntimeCharacterWorldRewardCatalog
{
    private static readonly RuntimeCharacterWorldRewardAnchor[] KnownAnchors =
    {
        new("classic.reward.high_druid_gem", "room_interaction", "gem in the High Druid Lair", null, "stat_ability", "Spellcasting +1", 15, null, "verified", "verified decoded script", true, "textblock script: touch gem; minlevel 15; addability 70 1; giveability 129 2", "Bundle A character state; Bundle D/F activation"),
        new("classic.reward.ice_sorceress_throne", "room_interaction", "Ice Sorceress throne", null, "stat_ability", "Armour Class +1", 15, null, "verified", "verified decoded script", true, "textblock script: sit throne; minlevel 15; addability 2 1; giveability 125 2", "Bundle A character state; Bundle D/F activation"),
        new("classic.reward.adult_red_dragon", "room_interaction", "adult red dragon ruby", null, "stat_ability", "Critical +1 / Spellcasting +2", 35, null, "verified", "verified decoded script", true, "textblock script: touch/move/get ruby; minlevel 35; addability 58 1; addability 70 2; giveability 131 3", "Bundle A character state; Bundle D/E/F activation"),
        new("classic.reward.alignment_quest_1", "quest_completion", "first alignment quest", null, "alignment_reward", "alignment branch reward", 10, null, "verified", "verified decoded quest stage gate", true, "quest flows: abilities 126/127/128 with minlevel 10", "Bundle A character state; Bundle F activation"),
        new("classic.reward.alignment_quest_2", "quest_completion", "second alignment quest", null, "stat_ability", "alignment stat reward", 20, null, "verified", "verified decoded quest stage gate", true, "quest flows: abilities 126/127/128 with minlevel 20", "Bundle A character state; Bundle F activation"),
        new("classic.reward.alignment_quest_3", "quest_completion", "third alignment quest", null, "stat_ability", "alignment stat reward", 30, null, "verified", "verified decoded quest stage gate", true, "quest flows: abilities 126/127/128 with minlevel 30", "Bundle A character state; Bundle F activation"),
        new("classic.reward.alignment_quest_4", "quest_completion", "fourth alignment quest", null, "item", "alignment cloak", 40, null, "verified", "verified decoded quest stage gate; item identity pending", true, "quest flows: abilities 126/127/128 with minlevel 40", "Bundle A entitlement; Bundle C/F item delivery"),
        new("classic.reward.master_assassin", "npc_dialogue", "dying master assassin", null, "capability", "Perfect Stealth", null, null, "class-specific decoded gates", "verified class-specific decoded scripts", true, "textblock.9029: class branches grant ability 186", "Bundle A capability state; Bundle E/F activation"),
        new("classic.reward.gaunt_one", "npc_dialogue", "Gaunt One temple master", "Gaunt", "capability", "See Hidden", 15, null, "verified", "verified decoded script", true, "textblock.2698: race 13; minlevel 15; giveability 57 1", "Bundle A perception/search state; Bundle F dialogue and Bundle D search consumer")
    };

    private static readonly RuntimeCharacterClassAbilityGate[] ClassAbilityGates =
    {
        // Ability 32 / Smash, decoded from textblock 2942.
        new(32, "Smash", 1, "Warrior", 22, "textblock.2942", "verified"),
        new(32, "Smash", 2, "Witchunter", 20, "textblock.2942", "verified"),
        new(32, "Smash", 3, "Paladin", 25, "textblock.2942", "verified"),
        new(32, "Smash", 4, "Cleric", 27, "textblock.2942", "verified"),
        new(32, "Smash", 11, "Warlock", 27, "textblock.2942", "verified"),
        new(32, "Smash", 14, "Ranger", 22, "textblock.2942", "verified"),

        // Ability 187 / Meditate, decoded from textblock 9046.
        new(187, "Meditate", 3, "Paladin", 27, "textblock.9046", "verified"),
        new(187, "Meditate", 4, "Cleric", 23, "textblock.9046", "verified"),
        new(187, "Meditate", 5, "Priest", 20, "textblock.9046", "verified"),
        new(187, "Meditate", 6, "Missionary", 23, "textblock.9046", "verified"),
        new(187, "Meditate", 9, "Bard", 27, "textblock.9046", "verified"),
        new(187, "Meditate", 10, "Gypsy", 23, "textblock.9046", "verified"),
        new(187, "Meditate", 11, "Warlock", 23, "textblock.9046", "verified"),
        new(187, "Meditate", 12, "Mage", 20, "textblock.9046", "verified"),
        new(187, "Meditate", 13, "Druid", 20, "textblock.9046", "verified"),
        new(187, "Meditate", 14, "Ranger", 27, "textblock.9046", "verified"),

        // Ability 186 / Perfect Stealth, decoded from textblock 9029.
        new(186, "Perfect Stealth", 6, "Missionary", 25, "textblock.9029", "verified"),
        new(186, "Perfect Stealth", 7, "Ninja", 20, "textblock.9029", "verified"),
        new(186, "Perfect Stealth", 8, "Thief", 20, "textblock.9029", "verified"),
        new(186, "Perfect Stealth", 9, "Bard", 25, "textblock.9029", "verified"),
        new(186, "Perfect Stealth", 10, "Gypsy", 25, "textblock.9029", "verified"),
        new(186, "Perfect Stealth", 14, "Ranger", 20, "textblock.9029", "verified"),
        new(186, "Perfect Stealth", 15, "Mystic", 27, "textblock.9029", "verified")
    };

    public IReadOnlyList<RuntimeCharacterWorldRewardAnchor> Rewards => KnownAnchors;
    public IReadOnlyList<RuntimeCharacterClassAbilityGate> AbilityGates => ClassAbilityGates;

    public bool TryGetClassAbilityGate(int abilityId, int classLegacyNumber, out RuntimeCharacterClassAbilityGate gate)
    {
        gate = ClassAbilityGates.SingleOrDefault(x => x.AbilityId == abilityId && x.ClassLegacyNumber == classLegacyNumber)!;
        return gate is not null;
    }

    public bool MeetsClassAbilityGate(int abilityId, int classLegacyNumber, int characterLevel) =>
        TryGetClassAbilityGate(abilityId, classLegacyNumber, out var gate) && characterLevel >= gate.MinimumLevel;

    public bool TryGetClassAbilityGate(int abilityId, string className, out RuntimeCharacterClassAbilityGate gate)
    {
        gate = ClassAbilityGates.SingleOrDefault(x => x.AbilityId == abilityId && x.ClassName.Equals(className, StringComparison.OrdinalIgnoreCase))!;
        return gate is not null;
    }

    public bool MeetsClassAbilityGate(int abilityId, string className, int characterLevel) =>
        TryGetClassAbilityGate(abilityId, className, out var gate) && characterLevel >= gate.MinimumLevel;

    public RuntimeCharacterRewardEligibility EvaluateEligibility(RuntimeCharacterRecord character, string rewardId)
    {
        var reward = KnownAnchors.SingleOrDefault(x => x.Id.Equals(rewardId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Unknown Classic reward anchor '{rewardId}'.");

        if (!reward.DecodedActivationResolved)
            return new(false, "Decoded activation trigger is unresolved.", reward);
        if (!reward.LevelGateResolved)
            return new(false, "Reward uses a class-specific gate and requires class-gate evaluation.", reward);
        if (character.Level < reward.MinimumLevel!.Value)
            return new(false, $"Requires level {reward.MinimumLevel.Value}.", reward);
        if (reward.MaximumLevel.HasValue && character.Level > reward.MaximumLevel.Value)
            return new(false, $"Maximum eligible level is {reward.MaximumLevel.Value}.", reward);
        if (!string.IsNullOrWhiteSpace(reward.RaceRequirement) &&
            !character.RaceName.Equals(reward.RaceRequirement, StringComparison.OrdinalIgnoreCase))
            return new(false, $"Requires race '{reward.RaceRequirement}'.", reward);

        return new(true, "Decoded activation, level, and race gates passed.", reward);
    }

    public RuntimeCharacterRecord ApplyResolvedAbilityReward(RuntimeCharacterRecord character, RuntimeCharacterQuestAbilityCatalog abilities, string rewardId, int abilityId, int value, string decodedSourceId)
    {
        var eligibility = EvaluateEligibility(character, rewardId);
        if (!eligibility.Eligible)
            throw new InvalidOperationException($"Reward '{eligibility.Reward.Id}' is not eligible: {eligibility.Reason}");
        if (!abilities.TryGetAbilityName(abilityId, out _))
            throw new InvalidOperationException($"Reward '{eligibility.Reward.Id}' references unknown ability.{abilityId}.");
        if (string.IsNullOrWhiteSpace(decodedSourceId))
            throw new InvalidOperationException($"Reward '{eligibility.Reward.Id}' requires decoded provenance.");
        return abilities.ApplyGrant(character, abilityId, value, $"{eligibility.Reward.Id}:{decodedSourceId}");
    }
}

// Consolidated from RuntimeCharacterWitchunterQuestCatalog.cs
public sealed record RuntimeCharacterWitchunterQuestStep(
    int StageValue,
    int SourceTextblockId,
    string NpcId,
    int? MinimumLevel,
    IReadOnlyList<string> RequiredItemIds,
    IReadOnlyList<string> RemovedItemIds,
    IReadOnlyList<string> GrantedItemIds,
    int AbilityDelta,
    string EvidenceStatus,
    string DecodedSource);

public sealed record RuntimeCharacterWitchunterEligibility(bool Eligible, string Reason, RuntimeCharacterWitchunterQuestStep Step);

public sealed record RuntimeCharacterWitchunterWeaponEntitlement(
    string ItemId,
    string ItemName,
    int RequiredQuestAbilityId,
    int RequiredQuestStage,
    int MinimumUseLevel,
    IReadOnlyList<int> ClassRestrictions,
    string RewardScriptId,
    int RewardTextblockId,
    string EvidenceStatus,
    string DecodedSource);

public sealed record RuntimeCharacterWitchunterWeaponUseEligibility(bool Eligible, string Reason, RuntimeCharacterWitchunterWeaponEntitlement Entitlement);

/// <summary>
/// Projects the decoded MageBaneQuest (Ability 50) for the single-class Witchunter path.
/// Bundle A owns class, level, quest-stage and entitlement semantics. Bundle F executes
/// the decoded dialogue operations; Bundle C owns item transfer and equipment use.
/// </summary>
public sealed class RuntimeCharacterWitchunterQuestCatalog
{
    public const int WitchunterClassLegacyNumber = 2;
    public const int MageBaneQuestAbilityId = 50;

    private readonly List<RuntimeCharacterWitchunterQuestStep> _steps = new();
    private RuntimeCharacterWitchunterWeaponEntitlement? _drakaBlade;
    public RuntimeCharacterWitchunterWeaponEntitlement? DrakaBladeEntitlement => _drakaBlade;

    public IReadOnlyList<RuntimeCharacterWitchunterQuestStep> Steps => _steps
        .OrderBy(x => x.StageValue)
        .ThenBy(x => x.SourceTextblockId)
        .ToArray();

    public static RuntimeCharacterWitchunterQuestCatalog Load(string projectRoot, string worldId)
    {
        var catalog = new RuntimeCharacterWitchunterQuestCatalog();
        var path = Path.Combine(projectRoot, "Data", worldId, "quests.json");
        if (!File.Exists(path)) return catalog;
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        catalog.Scan(document.RootElement, 0, string.Empty);
        catalog.LoadDrakaBlade(projectRoot, worldId);
        var unique = catalog._steps
            .GroupBy(x => (x.StageValue, x.SourceTextblockId, x.NpcId, string.Join(',', x.GrantedItemIds), string.Join(',', x.RequiredItemIds)))
            .Select(x => x.First())
            .ToArray();
        catalog._steps.Clear();
        catalog._steps.AddRange(unique);
        return catalog;
    }

    public RuntimeCharacterWitchunterEligibility EvaluateStart(RuntimeCharacterRecord character)
    {
        var step = Steps.FirstOrDefault(x => x.StageValue == 1 && x.MinimumLevel.HasValue)
            ?? throw new InvalidOperationException("Decoded Witchunter first-half start step is missing.");
        if (!Normalize(character.ClassName).Equals("Witchunter", StringComparison.OrdinalIgnoreCase))
            return new(false, "Requires the Witchunter class.", step);
        if (character.Level < step.MinimumLevel!.Value)
            return new(false, $"Requires level {step.MinimumLevel.Value}.", step);
        return new(true, "Decoded Witchunter class and first-half level gate passed.", step);
    }

    public bool HasDecodedLevel50WeaponUseRelationship => _drakaBlade is not null && _drakaBlade.MinimumUseLevel == 50;

    public RuntimeCharacterWitchunterWeaponUseEligibility EvaluateDrakaBladeUse(RuntimeCharacterRecord character, RuntimeCharacterQuestAbilityCatalog questAbilities)
    {
        var entitlement = _drakaBlade ?? throw new InvalidOperationException("Decoded Draka Blade entitlement is missing.");
        if (!Normalize(character.ClassName).Equals("Witchunter", StringComparison.OrdinalIgnoreCase))
            return new(false, "Draka's Blade is restricted to the Witchunter class.", entitlement);
        if (!questAbilities.HasAtLeast(character, entitlement.RequiredQuestAbilityId, entitlement.RequiredQuestStage))
            return new(false, $"Requires MageBaneQuest stage {entitlement.RequiredQuestStage}.", entitlement);
        if (character.Level < entitlement.MinimumUseLevel)
            return new(false, $"Requires level {entitlement.MinimumUseLevel} to use.", entitlement);
        return new(true, "Decoded Witchunter quest entitlement and item minimum-level gate passed.", entitlement);
    }

    private void LoadDrakaBlade(string projectRoot, string worldId)
    {
        var itemPath = Path.Combine(projectRoot, "Data", worldId, "items.json");
        var scriptPath = Path.Combine(projectRoot, "Data", worldId, "scripts.json");
        if (!File.Exists(itemPath) || !File.Exists(scriptPath)) return;

        using var itemDocument = JsonDocument.Parse(File.ReadAllText(itemPath));
        var item = itemDocument.RootElement.GetProperty("records").EnumerateArray()
            .FirstOrDefault(x => ReadString(x, "id").Equals("item.1334", StringComparison.OrdinalIgnoreCase));
        if (item.ValueKind == JsonValueKind.Undefined) return;

        var minimumLevel = 0;
        var classRestrictions = new List<int>();
        if (item.TryGetProperty("public_reference_0139", out var reference) && reference.ValueKind == JsonValueKind.Object &&
            reference.TryGetProperty("fields", out var fields) && fields.ValueKind == JsonValueKind.Object)
        {
            if (fields.TryGetProperty("class_restrictions", out var classes) && classes.ValueKind == JsonValueKind.Array)
                classRestrictions.AddRange(classes.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Number).Select(x => x.GetInt32()));
            if (fields.TryGetProperty("abilities_named", out var abilities) && abilities.ValueKind == JsonValueKind.Array)
            {
                foreach (var ability in abilities.EnumerateArray())
                {
                    if (ReadInt(ability, "ability_id") == 135 || ReadString(ability, "name").Equals("MinLevel", StringComparison.OrdinalIgnoreCase))
                    {
                        var raw = ReadString(ability, "value");
                        if (int.TryParse(raw, out var parsed)) minimumLevel = parsed;
                    }
                }
            }
        }

        using var scriptDocument = JsonDocument.Parse(File.ReadAllText(scriptPath));
        string rewardScriptId = string.Empty;
        var rewardTextblock = 0;
        var questStage = 0;
        foreach (var record in scriptDocument.RootElement.GetProperty("records").EnumerateArray())
        {
            var id = ReadString(record, "id");
            if (!id.Contains("MageBaneQuest", StringComparison.OrdinalIgnoreCase)) continue;
            var serialized = record.GetRawText();
            if (!serialized.Contains("giveitem 1334", StringComparison.OrdinalIgnoreCase)) continue;
            rewardScriptId = id;
            rewardTextblock = ReadInt(record, "textblock_id");
            if (rewardTextblock <= 0 && record.TryGetProperty("relationships", out var relationships))
            {
                var stageRef = ReadString(relationships, "textblock");
                if (stageRef.StartsWith("textblock.", StringComparison.OrdinalIgnoreCase)) int.TryParse(stageRef[10..], out rewardTextblock);
            }
            questStage = 4;
            break;
        }

        if (minimumLevel <= 0 || classRestrictions.Count == 0 || string.IsNullOrWhiteSpace(rewardScriptId)) return;
        _drakaBlade = new RuntimeCharacterWitchunterWeaponEntitlement(
            "item.1334", ReadString(item, "name"), MageBaneQuestAbilityId, questStage, minimumLevel, classRestrictions,
            rewardScriptId, rewardTextblock, "verified",
            "items.json item.1334 Ability 135 MinLevel=50 + class restriction 2; scripts.json MageBaneQuest random reward giveitem 1334");
    }

    private void Scan(JsonElement element, int textblockId, string questId)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray()) Scan(child, textblockId, questId);
            return;
        }
        if (element.ValueKind != JsonValueKind.Object) return;

        var localTextblock = ReadInt(element, "textblock_id");
        if (localTextblock <= 0) localTextblock = textblockId;
        var localQuestId = ReadString(element, "id");
        if (string.IsNullOrWhiteSpace(localQuestId)) localQuestId = questId;

        if (localQuestId.Contains("MageBaneQuest", StringComparison.OrdinalIgnoreCase) &&
            element.TryGetProperty("operations", out var operations) && operations.ValueKind == JsonValueKind.Array)
            Project(localTextblock, localQuestId, operations);

        foreach (var property in element.EnumerateObject())
            Scan(property.Value, localTextblock, localQuestId);
    }

    private void Project(int textblockId, string questId, JsonElement operations)
    {
        foreach (var lineGroup in operations.EnumerateArray().GroupBy(x => ReadInt(x, "line")))
        {
            var line = lineGroup.OrderBy(x => ReadInt(x, "sequence")).ToArray();
            var classOp = line.FirstOrDefault(x => Opcode(x, "class"));
            if (classOp.ValueKind == JsonValueKind.Undefined || FirstArg(classOp) != WitchunterClassLegacyNumber) continue;

            var questOps = line.Where(x =>
                Opcode(x, "checkability") || Opcode(x, "giveability") || Opcode(x, "addability") ||
                Opcode(x, "testability") || Opcode(x, "failability")).ToArray();
            var stageValues = questOps
                .Where(x => AbilityId(x) == MageBaneQuestAbilityId)
                .Select(AbilityValue)
                .Where(x => x > 0)
                .ToArray();
            if (stageValues.Length == 0) continue;

            var stage = stageValues.Min();
            var minLevelOp = line.FirstOrDefault(x => Opcode(x, "minlevel"));
            int? minimumLevel = minLevelOp.ValueKind == JsonValueKind.Undefined ? null : FirstArg(minLevelOp);
            var required = line.Where(x => Opcode(x, "checkitem")).Select(x => $"item.{FirstArg(x)}").Where(x => x != "item.0").Distinct().ToArray();
            var removed = line.Where(x => Opcode(x, "takeitem")).Select(x => $"item.{FirstArg(x)}").Where(x => x != "item.0").Distinct().ToArray();
            var granted = line.Where(x => Opcode(x, "giveitem")).Select(x => $"item.{FirstArg(x)}").Where(x => x != "item.0").Distinct().ToArray();
            var delta = line.Where(x => Opcode(x, "addability") && AbilityId(x) == MageBaneQuestAbilityId).Select(AbilityValue).DefaultIfEmpty(0).Max();

            var npcId = ExtractNpcId(questId);
            _steps.Add(new RuntimeCharacterWitchunterQuestStep(
                stage, textblockId, npcId, minimumLevel, required, removed, granted, delta,
                "verified",
                $"decoded {questId} textblock.{textblockId} line {ReadInt(classOp, "line")}: class 2 / ability 50 operations"));
        }
    }

    private static string ExtractNpcId(string questId)
    {
        var marker = ".npc";
        var index = questId.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0) return string.Empty;
        index += marker.Length;
        var end = questId.IndexOf('.', index);
        var number = end < 0 ? questId[index..] : questId[index..end];
        return int.TryParse(number, out var npc) ? $"npc.{npc}" : string.Empty;
    }

    private static int AbilityId(JsonElement operation)
    {
        var value = ReadInt(operation, "ability_id");
        return value > 0 ? value : FirstArg(operation);
    }

    private static int AbilityValue(JsonElement operation)
    {
        var value = ReadInt(operation, "ability_value");
        return value > 0 ? value : Arg(operation, 1);
    }

    private static bool Opcode(JsonElement operation, string expected) =>
        ReadString(operation, "opcode").Equals(expected, StringComparison.OrdinalIgnoreCase);

    private static int FirstArg(JsonElement operation) => Arg(operation, 0);

    private static int Arg(JsonElement operation, int index)
    {
        if (!operation.TryGetProperty("numeric_arguments", out var values) || values.ValueKind != JsonValueKind.Array || values.GetArrayLength() <= index) return 0;
        var value = values[index];
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : 0;
    }

    private static int ReadInt(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value)) return 0;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) return number;
        return value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out number) ? number : 0;
    }

    private static string ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    private static string Normalize(string value)
    {
        var normalized = value.Trim();
        while (normalized.Length > 0 && !char.IsLetterOrDigit(normalized[^1])) normalized = normalized[..^1];
        return normalized;
    }
}

// Consolidated from RuntimeCharacterQuestSpellTurnIn.cs
public sealed record RuntimeCharacterQuestSpellTurnInResult(
    bool Completed,
    string Message,
    RuntimeCharacterRecord Character,
    IReadOnlyList<string> RemovedItemIds,
    string? GrantedItemId,
    string? CompletionFlag);

/// <summary>
/// Single atomic character-side consumer for permanent quest spell rewards. The resolver is
/// deliberately evidence-gated: candidate relationships execute only after the decoded NPC dialogue/turn-in root is resolved.
/// The gnome merchant root is decoded at textblock 2798; the transaction remains atomic.
/// </summary>
public static class RuntimeCharacterQuestSpellTurnInResolver
{
    public static RuntimeCharacterQuestSpellTurnInResult TryCompleteLevel40(
        RuntimeCharacterRecord character,
        string npcId,
        RuntimeCharacterLearningCatalog learning)
    {
        var candidate = learning.RequireLevel40Quest(character.ClassName);
        var completionFlag = $"quest_complete:level40:{candidate.ClassName.ToLowerInvariant()}";

        if (!candidate.TurnInScriptResolved)
            return new(false, "The decoded gnome-merchant turn-in authority is unavailable.", character, Array.Empty<string>(), null, null);
        if (!npcId.Equals(candidate.NpcId, StringComparison.OrdinalIgnoreCase))
            return new(false, "This NPC does not complete your class spell quest.", character, Array.Empty<string>(), null, null);
        if (character.Conditions.ContainsKey(completionFlag))
            return new(false, "You have already completed this class spell quest.", character, Array.Empty<string>(), null, completionFlag);

        var inventory = character.Inventory.ToList();
        foreach (var requiredItemId in candidate.RequiredItemIds)
        {
            var index = inventory.FindIndex(x => x.Equals(requiredItemId, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
                return new(false, $"You do not have every required component. Missing {requiredItemId}.", character, Array.Empty<string>(), null, null);
            inventory.RemoveAt(index);
        }

        inventory.Add(candidate.RewardItemId);
        var conditions = new Dictionary<string, string>(character.Conditions, StringComparer.OrdinalIgnoreCase)
        {
            [completionFlag] = candidate.RewardItemId,
            [$"quest_reward_item:{candidate.RewardItemId}"] = $"level40:{candidate.NpcId}:{candidate.ClassName.ToLowerInvariant()}"
        };
        var updated = character with { Inventory = inventory, Conditions = conditions, UpdatedAtUtc = DateTimeOffset.UtcNow };

        return new(true, $"The gnome merchant gives you {candidate.RewardItemName}.", updated, candidate.RequiredItemIds, candidate.RewardItemId, completionFlag);
    }
}
