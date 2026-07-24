using System.Globalization;
using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeItemCombatProfile(
    string ItemId,
    string ItemName,
    int MinimumDamage,
    int MaximumDamage,
    int ArmorClassBonus,
    int DamageResistanceBonus,
    int Accuracy,
    int CriticalChanceBonus,
    int BackstabAccuracyBonus,
    int Speed,
    int WeaponType,
    int ArmorType,
    bool IsArmor,
    bool IsTwoHanded,
    bool SupportsBackstab);

public sealed class RuntimeItemCombatCatalog
{
    private readonly Dictionary<string, RuntimeItemCombatProfile> _profiles = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyCollection<RuntimeItemCombatProfile> Profiles => _profiles.Values;

    public static RuntimeItemCombatCatalog Load(string projectRoot)
    {
        var resolver = ServerDataRootResolver.ResolveDefault(projectRoot);
        var path = Path.Combine(resolver.DataRoot, "items.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var catalog = new RuntimeItemCombatCatalog();
        foreach (var item in document.RootElement.GetProperty("records").EnumerateArray())
        {
            if (!item.TryGetProperty("combat", out var combat) || combat.ValueKind != JsonValueKind.Object) continue;
            var id = JsonValueReader.ReadString(item, "id", string.Empty);
            if (id.Length == 0) continue;
            var name = JsonValueReader.ReadString(item, "name", string.Empty);
            var weaponType = JsonValueReader.ReadInt(combat, "weapon_type", 0);
            catalog._profiles[id] = new RuntimeItemCombatProfile(
                id,
                name,
                JsonValueReader.ReadInt(combat, "minimum_damage", 0),
                JsonValueReader.ReadInt(combat, "maximum_damage", 0),
                JsonValueReader.ReadInt(combat, "armor_class_bonus", 0),
                JsonValueReader.ReadInt(combat, "damage_resistance_bonus", 0),
                JsonValueReader.ReadInt(combat, "accuracy", 0),
                ReadAbilityValue(item, 58),
                ReadAbilityValue(item, 116),
                JsonValueReader.ReadInt(combat, "speed", 0),
                weaponType,
                JsonValueReader.ReadInt(combat, "armor_type", 0),
                combat.TryGetProperty("is_armor", out var armor) && armor.ValueKind == JsonValueKind.True,
                IsClassicTwoHanded(name, weaponType),
                HasAbility(item, 116));
        }
        return catalog;
    }


    private static bool IsClassicTwoHanded(string name, int weaponType)
    {
        if (weaponType == 3) return true;
        return name.Contains("two-handed", StringComparison.OrdinalIgnoreCase)
            || name.Contains("two handed", StringComparison.OrdinalIgnoreCase)
            || name.Contains("greatsword", StringComparison.OrdinalIgnoreCase)
            || name.Contains("quarterstaff", StringComparison.OrdinalIgnoreCase)
            || name.Contains("halberd", StringComparison.OrdinalIgnoreCase)
            || name.Contains("pike", StringComparison.OrdinalIgnoreCase)
            || name.Contains("longbow", StringComparison.OrdinalIgnoreCase)
            || name.Contains("crossbow", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasAbility(JsonElement item, int abilityId)
    {
        if (!item.TryGetProperty("public_reference_0139", out var reference) || reference.ValueKind != JsonValueKind.Object ||
            !reference.TryGetProperty("fields", out var fields) || fields.ValueKind != JsonValueKind.Object ||
            !fields.TryGetProperty("abilities_named", out var abilities) || abilities.ValueKind != JsonValueKind.Array)
            return false;
        foreach (var ability in abilities.EnumerateArray())
            if (JsonValueReader.ReadInt(ability, "ability_id", 0) == abilityId) return true;
        return false;
    }

    private static int ReadAbilityValue(JsonElement item, int abilityId)
    {
        if (!item.TryGetProperty("public_reference_0139", out var reference) || reference.ValueKind != JsonValueKind.Object ||
            !reference.TryGetProperty("fields", out var fields) || fields.ValueKind != JsonValueKind.Object ||
            !fields.TryGetProperty("abilities_named", out var abilities) || abilities.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        var total = 0;
        foreach (var ability in abilities.EnumerateArray())
        {
            if (JsonValueReader.ReadInt(ability, "ability_id", 0) != abilityId) continue;
            var raw = JsonValueReader.ReadString(ability, "value", "0");
            if (int.TryParse(raw, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var value))
            {
                total = checked(total + value);
            }
        }
        return total;
    }

    public bool TryGet(string itemId, out RuntimeItemCombatProfile profile) => _profiles.TryGetValue(itemId, out profile!);
}

public sealed class RuntimeCombatProfilePromotionSmokeReport
{
    public bool Passed => Failures.Count == 0;
    public int NpcCount { get; private set; }
    public int NpcAttackCount { get; private set; }
    public int ItemCombatProfileCount { get; private set; }
    public List<string> Checks { get; } = new();
    public List<string> Failures { get; } = new();

    public static RuntimeCombatProfilePromotionSmokeReport Run(string projectRoot)
    {
        var report = new RuntimeCombatProfilePromotionSmokeReport();
        try
        {
            var resolver = ServerDataRootResolver.ResolveDefault(projectRoot);
            using var npcDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(resolver.DataRoot, "npcs.json")));
            var npcRecords = npcDocument.RootElement.GetProperty("records").EnumerateArray().ToArray();
            report.NpcCount = npcRecords.Length;
            report.NpcAttackCount = npcRecords.Sum(npc => npc.TryGetProperty("attacks", out var attacks) && attacks.ValueKind == JsonValueKind.Array ? attacks.GetArrayLength() : 0);
            Require(report.NpcCount == 1100, "npc-count"); report.Checks.Add("npc-count");
            Require(report.NpcAttackCount > 0, "npc-attacks-promoted"); report.Checks.Add("npc-attacks-promoted");
            Require(npcRecords.All(npc => npc.TryGetProperty("attack_profile_authority", out _)), "npc-attack-authority"); report.Checks.Add("npc-attack-authority");

            var itemCatalog = RuntimeItemCombatCatalog.Load(projectRoot);
            report.ItemCombatProfileCount = itemCatalog.Profiles.Count;
            Require(report.ItemCombatProfileCount == 1950, "item-combat-profiles-promoted"); report.Checks.Add("item-combat-profiles-promoted");
            Require(itemCatalog.Profiles.Any(x => x.MaximumDamage > 0), "weapon-damage-available"); report.Checks.Add("weapon-damage-available");
            Require(itemCatalog.Profiles.Any(x => x.ArmorClassBonus != 0 || x.DamageResistanceBonus != 0), "armor-defense-available"); report.Checks.Add("armor-defense-available");
            Require(itemCatalog.Profiles.Any(x => x.CriticalChanceBonus != 0), "item-critical-bonus-available"); report.Checks.Add("item-critical-bonus-available");
            Require(itemCatalog.Profiles.Any(x => x.BackstabAccuracyBonus != 0), "item-backstab-accuracy-bonus-available"); report.Checks.Add("item-backstab-accuracy-bonus-available");
        }
        catch (Exception ex) { report.Failures.Add(ex.Message); }
        return report;
    }

    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    private static void Require(bool value, string code) { if (!value) throw new InvalidOperationException(code); }
}

// Consolidated from RuntimeClassicStealthResolution.cs during Bundle B runtime closure.
public sealed record RuntimeClassicStealthCheck(
    int ModifiedStealthRating,
    int ModifiedPerceptionValue,
    int RequiredClearance,
    int SuccessChance,
    int Roll,
    bool Succeeded,
    string EvidenceStatus);

/// <summary>
/// Authoritative behavioral boundary supplied by Classic gameplay evidence:
/// ordinary Hide/Sneak compares modified Stealth against modified Perception;
/// success requires Stealth to clear Perception by the decoded clearance value.
/// The numeric rating builders and clearance remain data-owned and fail closed
/// when they have not been supplied by an authoritative source.
/// </summary>
public static class RuntimeClassicStealthResolver
{
    public const string FormulaId = "combat.stealth_vs_perception";
    public const string ModifiedStealthFormulaId = "combat.modified_stealth_rating";
    public const string SeeHiddenFormulaId = "combat.see_hidden_detection";
    public const string PercentileRollFormulaId = "combat.stealth_percentile_roll";
    public const string CommandAdmissionFormulaId = "combat.stealth_command_admission";

    public static bool HasSeeHidden(RuntimeLivingNpcState npc)
    {
        ArgumentNullException.ThrowIfNull(npc);
        return npc.Metadata.TryGetValue("see_hidden", out var raw)
            && (raw.Equals("1", StringComparison.OrdinalIgnoreCase)
                || raw.Equals("true", StringComparison.OrdinalIgnoreCase)
                || raw.Equals("yes", StringComparison.OrdinalIgnoreCase));
    }

    public static RuntimeClassicStealthCheck ResolveSeeHidden(int modifiedStealthRating)
        => new(modifiedStealthRating, 9999, 0, 0, 1, false, "verified decoded Ability 57 See Hidden detection");

    public static RuntimeClassicStealthCheck Resolve(int modifiedStealthRating, int modifiedPerceptionValue, int requiredClearance)
    {
        if (requiredClearance < 0) throw new ArgumentOutOfRangeException(nameof(requiredClearance));
        var chance = CalculateSuccessChance(modifiedStealthRating, modifiedPerceptionValue, requiredClearance);
        return new RuntimeClassicStealthCheck(
            modifiedStealthRating,
            modifiedPerceptionValue,
            requiredClearance,
            chance,
            chance,
            chance > 0,
            "compatibility deterministic boundary; live Hide/Sneak uses the decoded percentile roll");
    }

    public static int CalculateSuccessChance(int modifiedStealthRating, int modifiedPerceptionValue, int requiredClearance = 0)
    {
        if (modifiedStealthRating < 0) throw new ArgumentOutOfRangeException(nameof(modifiedStealthRating));
        if (modifiedPerceptionValue < 0) throw new ArgumentOutOfRangeException(nameof(modifiedPerceptionValue));
        if (requiredClearance < 0) throw new ArgumentOutOfRangeException(nameof(requiredClearance));
        return Math.Clamp(checked(modifiedStealthRating - modifiedPerceptionValue - requiredClearance), 0, 100);
    }

    public static RuntimeClassicStealthCheck ResolvePercentile(
        int modifiedStealthRating,
        int modifiedPerceptionValue,
        int roll,
        int requiredClearance = 0)
    {
        if (roll is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(roll));
        var chance = CalculateSuccessChance(modifiedStealthRating, modifiedPerceptionValue, requiredClearance);
        return new RuntimeClassicStealthCheck(
            modifiedStealthRating,
            modifiedPerceptionValue,
            requiredClearance,
            chance,
            roll,
            roll <= chance,
            "strongly corroborated Classic percent roll: every Hide/Sneak attempt rerolls modified Stealth minus modified Perception");
    }

    public static int CalculateModifiedStealth(
        int level,
        int agility,
        int intellect,
        int charm,
        bool classStealth,
        bool raceStealth,
        int plusStealth)
    {
        if (level < 0) throw new ArgumentOutOfRangeException(nameof(level));
        if (!classStealth && !raceStealth) return 0;

        var stealth = level <= 15 ? checked(level * 2) : checked(level + 15);
        stealth = checked(stealth + 20);
        stealth = checked(stealth + (agility / 4));
        stealth = checked(stealth + (intellect / 8));
        stealth = checked(stealth + (charm / 6));

        if (classStealth && raceStealth) stealth = checked(stealth + 10);
        else if (raceStealth) stealth = checked(stealth - 15);

        return checked(stealth + plusStealth);
    }

    public static bool TryReadModifiedStealth(RuntimeCharacterRecord character, out int value)
    {
        ArgumentNullException.ThrowIfNull(character);
        if (TryRead(character.Conditions, out value, "combat.modified_stealth", "stealth.modified_rating", "stealth.rating")) return true;

        var abilities = RuntimeCharacterAbilityValues.Restore(character.Conditions);
        var plusStealth = RuntimeCharacterAbilityValues.Read(abilities, RuntimeCharacterQuestAbilityCatalog.StealthAbilityId);
        var classStealth = ReadBool(character.Conditions, "class.stealth_capable", "combat.class_stealth", "stealth.class");
        var raceStealth = ReadBool(character.Conditions, "race.stealth_capable", "combat.race_stealth", "stealth.race");
        if (!classStealth && !raceStealth && plusStealth > 0) classStealth = true;
        if (!classStealth && !raceStealth) { value = 0; return false; }

        value = CalculateModifiedStealth(
            character.Level,
            ReadStat(character.BaseStats, "agility", "agi", "agl"),
            ReadStat(character.BaseStats, "intellect", "int"),
            ReadStat(character.BaseStats, "charm", "cha"),
            classStealth,
            raceStealth,
            plusStealth);
        return true;
    }

    public static bool TryReadRequiredClearance(RuntimeCharacterRecord character, out int value)
        => TryRead(character.Conditions, out value, "combat.stealth_required_clearance", "stealth.required_clearance");

    public static bool TryReadModifiedPerception(RuntimeLivingNpcState npc, out int value)
        => TryRead(npc.Metadata, out value, "combat.modified_perception", "perception.modified_value", "perception.value", "perception");

    private static bool ReadBool(IReadOnlyDictionary<string,string> values, params string[] keys)
    {
        foreach (var key in keys)
            if (values.TryGetValue(key, out var raw)
                && (raw.Equals("1", StringComparison.OrdinalIgnoreCase)
                    || raw.Equals("true", StringComparison.OrdinalIgnoreCase)
                    || raw.Equals("yes", StringComparison.OrdinalIgnoreCase)))
                return true;
        return false;
    }

    private static int ReadStat(IReadOnlyDictionary<string,int> values, params string[] keys)
    {
        foreach (var key in keys) if (values.TryGetValue(key, out var value)) return value;
        return 0;
    }

    private static bool TryRead(IReadOnlyDictionary<string,string> values, out int value, params string[] keys)
    {
        foreach (var key in keys)
            if (values.TryGetValue(key, out var raw)
                && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
                && value >= 0)
                return true;
        value = 0;
        return false;
    }
}
