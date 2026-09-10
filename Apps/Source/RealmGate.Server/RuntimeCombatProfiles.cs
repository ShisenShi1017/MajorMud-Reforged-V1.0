using System.Globalization;
using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeWeaponOnHitSpell(int ChancePercent, int LegacySpellId);

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
    bool SupportsBackstab,
    int MagicalRank,
    int HitMagicRank,
    int RequiredToHitCode,
    IReadOnlyList<RuntimeWeaponOnHitSpell> OnHitSpells);

public sealed class RuntimeItemCombatCatalog
{
    private readonly Dictionary<string, RuntimeItemCombatProfile> _profiles = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyCollection<RuntimeItemCombatProfile> Profiles => _profiles.Values;

    public static RuntimeItemCombatCatalog Load(string projectRoot) =>
        Load(projectRoot, ServerRuntimeOptions.Load(projectRoot).DefaultWorldId);

    public static RuntimeItemCombatCatalog Load(string projectRoot, string worldId)
    {
        var resolver = ServerDataRootResolver.Resolve(projectRoot, worldId);
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
                HasAbility(item, 116),
                ReadAbilityValue(item, 28),
                ReadAbilityValue(item, 142),
                ReadAbilityValue(item, 158),
                ReadOnHitSpells(item));
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

    private static IReadOnlyList<RuntimeWeaponOnHitSpell> ReadOnHitSpells(JsonElement item)
    {
        if (!item.TryGetProperty("public_reference_0139", out var reference) || reference.ValueKind != JsonValueKind.Object ||
            !reference.TryGetProperty("fields", out var fields) || fields.ValueKind != JsonValueKind.Object ||
            !fields.TryGetProperty("abilities_named", out var abilities) || abilities.ValueKind != JsonValueKind.Array)
            return Array.Empty<RuntimeWeaponOnHitSpell>();

        var ordered = abilities.EnumerateArray().ToArray();
        var effects = new List<RuntimeWeaponOnHitSpell>();
        for (var index = 0; index < ordered.Length; index++)
        {
            if (JsonValueReader.ReadInt(ordered[index], "ability_id", 0) != 114) continue;
            var chance = JsonValueReader.ReadInt(ordered[index], "value", 0);
            var nextCastSpell = ordered.Skip(index + 1)
                .FirstOrDefault(ability => JsonValueReader.ReadInt(ability, "ability_id", 0) == 43);
            var spell = nextCastSpell.ValueKind == JsonValueKind.Object
                ? JsonValueReader.ReadInt(nextCastSpell, "value", 0)
                : 0;
            var effect = new RuntimeWeaponOnHitSpell(chance, spell);
            if (chance is >= 1 and <= 100 && spell > 0 && !effects.Contains(effect)) effects.Add(effect);
        }
        return effects;
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
            Require(itemCatalog.Profiles.Count(x => x.SupportsBackstab) == 239, "item-backstab-accuracy-owner-count"); report.Checks.Add("item-backstab-accuracy-owner-count-239");
            Require(itemCatalog.Profiles.Count(x => x.BackstabAccuracyBonus < 0) == 114
                && itemCatalog.Profiles.Count(x => x.SupportsBackstab && x.BackstabAccuracyBonus == 0) == 68
                && itemCatalog.Profiles.Count(x => x.BackstabAccuracyBonus > 0) == 57,
                "item-backstab-accuracy-signed-distribution"); report.Checks.Add("item-backstab-accuracy-signed-distribution-114-68-57");
            Require(itemCatalog.Profiles.Count(x => x.MagicalRank != 0) == 636, "item-magical-rank-count"); report.Checks.Add("item-magical-rank-nonzero-count-636");
            Require(itemCatalog.Profiles.Count(x => x.HitMagicRank != 0) == 31, "item-hitmagic-rank-count"); report.Checks.Add("item-hitmagic-rank-count-31");
            Require(itemCatalog.Profiles.Count(x => x.RequiredToHitCode != 0) == 5, "item-required-to-hit-count"); report.Checks.Add("item-required-to-hit-count-5");
            Require(itemCatalog.Profiles.Sum(x => x.OnHitSpells.Count) == 127, "item-on-hit-spell-pair-count"); report.Checks.Add("item-on-hit-spell-pair-count-127");
            Require(itemCatalog.Profiles.Single(x => x.ItemId == "item.1334").OnHitSpells.Single() == new RuntimeWeaponOnHitSpell(100, 1138), "item-on-hit-intervening-attribute-scan"); report.Checks.Add("item-on-hit-next-castspell-skips-intervening-attributes");
            Require(itemCatalog.Profiles.Single(x => x.ItemId == "item.1530").OnHitSpells.Count == 1, "item-on-hit-duplicate-pair-deduplication"); report.Checks.Add("item-on-hit-duplicate-identical-pair-deduplicated");
            Require(new[] { "item.1619", "item.1617", "item.1254" }.All(id => itemCatalog.Profiles.Single(x => x.ItemId == id).OnHitSpells.Count == 0), "item-on-hit-unpaired-fail-closed"); report.Checks.Add("item-on-hit-three-unpaired-records-fail-closed");
            var spellCatalog = RuntimeSpellCatalog.Load(projectRoot, "Classic");
            var onHitSpellIds = itemCatalog.Profiles.SelectMany(x => x.OnHitSpells).Select(x => x.LegacySpellId).ToHashSet();
            var resolvedOnHitSpells = spellCatalog.Spells.Where(x => int.TryParse(x.LegacyId, out var legacyId) && onHitSpellIds.Contains(legacyId)).ToArray();
            Require(resolvedOnHitSpells.Length == onHitSpellIds.Count, "item-on-hit-spell-resolution"); report.Checks.Add("item-on-hit-spells-resolve");
            Require(resolvedOnHitSpells.All(x => x.TargetType == "8"), "item-on-hit-spell-hostile-target-contract"); report.Checks.Add("item-on-hit-spells-single-hostile-target");
            var npcProfiles = npcRecords.Select(RuntimeNpcDefinition.FromJson).ToArray();
            Require(npcProfiles.Count(x => x.MagicalRequirement != 0) == 386, "npc-magical-requirement-count"); report.Checks.Add("npc-magical-requirement-count-386");
            Require(npcProfiles.Count(x => x.SpellImmunityLevel > 0) == 387, "npc-spell-immunity-count"); report.Checks.Add("npc-spell-immunity-count-387");
            Require(npcRecords.Count(record => record.GetRawText().Contains("\"ability_id\": 146", StringComparison.Ordinal)) == 112, "npc-monster-guards-owner-count"); report.Checks.Add("npc-monster-guards-owner-count-112");
            Require(npcProfiles.Count(profile => profile.GuardedNpcLegacyIds.Count > 0) == 112
                && npcProfiles.SelectMany(profile => profile.GuardedNpcLegacyIds).All(id => npcProfiles.Any(target => target.LegacyId == id)),
                "npc-monster-guards-target-resolution"); report.Checks.Add("npc-monster-guards-all-targets-resolve");
            Require(npcRecords.Count(record => record.GetRawText().Contains("\"ability_id\": 147", StringComparison.Ordinal)) == 9, "npc-resist-water-owner-count"); report.Checks.Add("npc-resist-water-owner-count-9");
            Require(npcProfiles.Count(profile => profile.WaterResistance > 0) == 9, "npc-resist-water-runtime-projection"); report.Checks.Add("npc-resist-water-nine-positive-profiles");
            Require(npcProfiles.Single(x => x.Id == "npc.975").SpellImmunityLevel == 10, "npc-spell-immunity-threshold-value"); report.Checks.Add("npc-975-spell-immunity-level-10");
            Require(npcProfiles.Count(x => x.RequiredToHitCode != 0) == 8, "npc-required-to-hit-nonzero-count"); report.Checks.Add("npc-required-to-hit-nonzero-count-8");
            var nonAttackProfiles = npcProfiles.Where(x => x.NonAttackItemLegacyId > 0).ToArray();
            Require(nonAttackProfiles.Length == 18, "npc-bad-attack-owner-count"); report.Checks.Add("npc-bad-attack-owner-count-18");
            var runtimeItems = RuntimeItemCatalog.Load(projectRoot, "Classic");
            Require(nonAttackProfiles.All(profile => runtimeItems.Items.Any(item => item.Id.Equals($"item.{profile.NonAttackItemLegacyId}", StringComparison.OrdinalIgnoreCase))),
                "npc-bad-attack-item-resolution"); report.Checks.Add("npc-bad-attack-all-item-targets-resolve");
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
