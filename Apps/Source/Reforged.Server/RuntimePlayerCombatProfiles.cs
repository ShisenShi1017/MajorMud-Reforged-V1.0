using System.Globalization;
using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimePlayerCombatProfile(
    string CharacterId,
    int AttackValue,
    int BackstabAttackValue,
    int DefenseValue,
    int ArmorClass,
    int DamageResistance,
    int DodgeBonus,
    int CriticalChance,
    int NaturalCriticalChance,
    int MinimumDamage,
    int MaximumDamage,
    int WeaponAccuracyModifier,
    int ClassCombatAccuracy,
    int AbilityAccuracyModifier,
    int QuestMaximumDamageBonus,
    int QuestArmorClassBonus,
    int QuestDodgeBonus,
    int QuestCriticalBonus,
    int StrengthMaximumDamageBonus,
    int WeaponSpeed,
    int AttackEnergyCost,
    int QuickAndDeadlyCriticalBonus,
    int EncumbrancePercent,
    int BackstabMinimumBonus,
    int BackstabMaximumBonus,
    int EquippedBackstabAccuracyBonus,
    bool SmashUnlocked,
    bool Barehanded,
    bool WeaponIsTwoHanded,
    bool WeaponSupportsBackstab,
    int EquippedWeaponType,
    IReadOnlyList<string> EquippedCombatItems,
    IReadOnlyList<string> Blockers)
{
    public bool Ready => Blockers.Count == 0;
}

public sealed class RuntimePlayerCombatProfileResolver
{
    private readonly RuntimeItemCombatCatalog _items;
    private readonly string _projectRoot;

    public RuntimePlayerCombatProfileResolver(RuntimeItemCombatCatalog items, string? projectRoot = null)
    {
        _items = items;
        _projectRoot = projectRoot ?? string.Empty;
    }

    public RuntimePlayerCombatProfile Resolve(RuntimeCharacterRecord character)
    {
        var blockers = new List<string>();
        var attackValue = checked(CombatValue(character, "combat.attack_value", DerivedAttackValue(character)) + RuntimeClassicStatusProjection.Numeric(character, "accuracy"));
        var conditionBackstabAccuracyBonus = Optional(character.Conditions, "combat.backstab_accuracy_bonus");
        var defenseValue = CombatValue(character, "combat.defense_value", DerivedDefenseValue(character));
        var dodgeBonus = checked(CombatValue(character, "combat.dodge_bonus", DerivedDodgeBonus(character)) + RuntimeClassicStatusProjection.Numeric(character, "dodge"));
        var naturalCriticalChance = RuntimeClassicPhysicalFormulaCalculator.CalculateNaturalCriticalChance(
            character.Level,
            AdjustedStat(character, "intellect", "int"),
            AdjustedStat(character, "agility", "agi", "agl"),
            AdjustedStat(character, "charm", "cha"));
        var criticalChance = checked(naturalCriticalChance + Optional(character.Conditions, "combat.critical_chance") + RuntimeClassicStatusProjection.Numeric(character, "critical"));
        var armorClass = checked(Optional(character.Conditions, "combat.armor_class") + RuntimeClassicStatusProjection.Numeric(character, "armor_class"));
        var damageResistance = checked(Optional(character.Conditions, "combat.damage_resistance") + RuntimeClassicStatusProjection.Numeric(character, "damage_resistance"));
        var minimumDamage = Optional(character.Conditions, "combat.minimum_damage");
        var maximumDamage = Optional(character.Conditions, "combat.maximum_damage");
        var weaponAccuracy = 0;
        var equippedBackstabAccuracyBonus = 0;
        var barehanded = true;
        var weaponIsTwoHanded = false;
        var weaponSupportsBackstab = false;
        var equippedWeaponType = 0;
        var weaponSpeed = 0;
        var classCombatAccuracy = ResolveClassCombatAccuracy(character);
        var questBonuses = ResolveQuestCombatBonuses(character);
        var abilityAccuracy = questBonuses.AccuracyPercentBonus;
        armorClass = checked(armorClass + questBonuses.ArmourClassBonus);
        dodgeBonus = checked(dodgeBonus + questBonuses.DodgeBonus);
        criticalChance = checked(criticalChance + questBonuses.CriticalBonus);
        var incapacitated = IsAvoidanceSuppressed(character);
        if (incapacitated) dodgeBonus = 0;
        var equipped = new List<string>();
        var weaponProfiles = new List<RuntimeItemCombatProfile>();

        foreach (var item in character.ItemState?.Items ?? Array.Empty<RuntimeItemInstance>())
        {
            if (!string.Equals(item.OwnerId, character.CharacterId, StringComparison.OrdinalIgnoreCase) || item.EquippedSlot is null) continue;
            if (!_items.TryGet(item.CanonicalItemId, out var profile)) continue;
            equipped.Add(item.CanonicalItemId);
            armorClass = checked(armorClass + profile.ArmorClassBonus);
            damageResistance = checked(damageResistance + profile.DamageResistanceBonus);
            criticalChance = checked(criticalChance + profile.CriticalChanceBonus);
            equippedBackstabAccuracyBonus = checked(equippedBackstabAccuracyBonus + profile.BackstabAccuracyBonus);
            if (profile.MaximumDamage > 0) weaponProfiles.Add(profile);
        }

        if (weaponProfiles.Count > 1)
        {
            blockers.Add("PLAYER_COMBAT_MULTIPLE_WEAPON_PROFILES");
        }
        else if (weaponProfiles.Count == 1)
        {
            var weapon = weaponProfiles[0];
            minimumDamage = weapon.MinimumDamage;
            maximumDamage = weapon.MaximumDamage;
            weaponAccuracy = weapon.Accuracy;
            barehanded = false;
            weaponIsTwoHanded = weapon.IsTwoHanded;
            weaponSupportsBackstab = weapon.SupportsBackstab;
            equippedWeaponType = weapon.WeaponType;
            weaponSpeed = weapon.Speed;
        }
        else if (maximumDamage <= 0 || minimumDamage < 0 || maximumDamage < minimumDamage)
        {
            // Classic characters can always fight unarmed. Equipment replaces this range when wielded.
            minimumDamage = 1;
            maximumDamage = Math.Max(2, 2 + AdjustedStat(character, "strength", "str") / 25);
        }

        var backstabAttackValue = RuntimeClassicPhysicalFormulaCalculator.CalculateBackstabAccuracy(
            ResolveModifiedStealth(character),
            AdjustedStat(character, "agility", "agi", "agl"),
            checked(conditionBackstabAccuracyBonus + equippedBackstabAccuracyBonus),
            HasClassStealth(character),
            checked(abilityAccuracy + weaponAccuracy));

        var strengthMaximumDamageBonus = RuntimeClassicPhysicalFormulaCalculator.CalculateStrengthMaximumDamageBonus(AdjustedStat(character, "strength", "str"));
        maximumDamage = checked(maximumDamage + strengthMaximumDamageBonus + questBonuses.MaximumDamageBonus + RuntimeClassicStatusProjection.Numeric(character, "maximum_damage"));
        if (maximumDamage < minimumDamage) maximumDamage = minimumDamage;
        var attackEnergyCost = barehanded || weaponSpeed <= 0
            ? 0
            : RuntimeClassicPhysicalFormulaCalculator.CalculateAttackEnergyCost(
                ResolveClassCombatCode(character), character.Level, weaponSpeed, AdjustedStat(character, "agility", "agi", "agl"));
        var encumbrancePercent = Math.Max(0, Optional(character.Conditions, "combat.encumbrance_percent"));
        var quickAndDeadlyCriticalBonus = barehanded || weaponSpeed <= 0
            ? 0
            : RuntimeClassicPhysicalFormulaCalculator.CalculateQuickAndDeadlyCriticalBonus(
                AdjustedStat(character, "agility", "agi", "agl"), attackEnergyCost, encumbrancePercent);
        criticalChance = checked(criticalChance + quickAndDeadlyCriticalBonus);
        criticalChance = RuntimeClassicPhysicalFormulaCalculator.CalculateFinalCriticalChance(criticalChance);

        return new RuntimePlayerCombatProfile(
            character.CharacterId,
            attackValue,
            backstabAttackValue,
            defenseValue,
            armorClass,
            damageResistance,
            dodgeBonus,
            criticalChance,
            naturalCriticalChance,
            minimumDamage,
            maximumDamage,
            weaponAccuracy,
            classCombatAccuracy,
            abilityAccuracy,
            questBonuses.MaximumDamageBonus,
            questBonuses.ArmourClassBonus,
            questBonuses.DodgeBonus,
            questBonuses.CriticalBonus,
            strengthMaximumDamageBonus,
            weaponSpeed,
            attackEnergyCost,
            quickAndDeadlyCriticalBonus,
            encumbrancePercent,
            questBonuses.BackstabMinimumBonus,
            questBonuses.BackstabMaximumBonus,
            equippedBackstabAccuracyBonus,
            RuntimeCharacterAbilityValues.Read(RuntimeCharacterAbilityValues.Restore(character.Conditions), RuntimeCharacterQuestAbilityCatalog.SmashAbilityId) > 0,
            barehanded,
            weaponIsTwoHanded,
            weaponSupportsBackstab,
            equippedWeaponType,
            equipped,
            blockers.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    internal static int ResolveModifiedStealth(RuntimeCharacterRecord character)
    {
        ArgumentNullException.ThrowIfNull(character);
        if (RuntimeClassicStealthResolver.TryReadModifiedStealth(character, out var decoded)) return checked(decoded + RuntimeClassicStatusProjection.Numeric(character, "stealth"));
        return checked(CombatValue(character, "combat.stealth_rating", AdjustedStat(character, "stealth", "stl")) + RuntimeClassicStatusProjection.Numeric(character, "stealth"));
    }

    internal static bool HasClassStealth(RuntimeCharacterRecord character)
    {
        ArgumentNullException.ThrowIfNull(character);
        return ReadConditionBool(character.Conditions, "class.stealth_capable", "combat.class_stealth", "stealth.class");
    }

    private static bool ReadConditionBool(IReadOnlyDictionary<string, string> values, params string[] keys)
    {
        foreach (var key in keys)
            if (values.TryGetValue(key, out var raw) &&
                ((bool.TryParse(raw, out var flag) && flag) || (int.TryParse(raw, out var number) && number > 0)))
                return true;
        return false;
    }

    internal static bool IsAvoidanceSuppressed(RuntimeCharacterRecord character)
    {
        ArgumentNullException.ThrowIfNull(character);
        if (RuntimeClassicStatusProjection.Flag(character, "paralyzed")) return true;
        foreach (var key in new[] { "combat.stunned", "state.stunned", "stunned", "combat.paralyzed", "state.paralyzed", "paralyzed", "combat.immobilized", "state.immobilized", "immobilized", "combat.sleeping", "state.sleeping", "sleeping" })
        {
            if (!character.Conditions.TryGetValue(key, out var raw)) continue;
            if (raw.Equals("1", StringComparison.OrdinalIgnoreCase) || raw.Equals("true", StringComparison.OrdinalIgnoreCase) || raw.Equals("yes", StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    internal static bool IsPhysicalAttackSuppressed(RuntimeCharacterRecord character)
    {
        ArgumentNullException.ThrowIfNull(character);
        if (RuntimeClassicStatusProjection.IsPhysicalAttackBlocked(character)) return true;
        // Rooted/immobilized characters retain physical attacks. Only states that remove
        // conscious or voluntary action suppress the attack turn.
        foreach (var key in new[] { "combat.stunned", "state.stunned", "stunned", "combat.paralyzed", "state.paralyzed", "paralyzed", "combat.sleeping", "state.sleeping", "sleeping" })
        {
            if (!character.Conditions.TryGetValue(key, out var raw)) continue;
            if (raw.Equals("1", StringComparison.OrdinalIgnoreCase) || raw.Equals("true", StringComparison.OrdinalIgnoreCase) || raw.Equals("yes", StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private int ResolveClassCombatAccuracy(RuntimeCharacterRecord character)
        => checked(ResolveClassCombatCode(character) * character.Level);

    private int ResolveClassCombatCode(RuntimeCharacterRecord character)
    {
        if (string.IsNullOrWhiteSpace(_projectRoot)) return 0;
        var catalog = RuntimeCharacterClassCapabilityCatalog.Load(_projectRoot, character.WorldId);
        return catalog.RequireClass(character.ClassId).CombatLevelCode;
    }

    private static RuntimeCharacterQuestDerivedBonuses ResolveQuestCombatBonuses(RuntimeCharacterRecord character)
    {
        var values = RuntimeCharacterAbilityValues.Restore(character.Conditions);
        return new RuntimeCharacterQuestDerivedBonuses(
            ArmourClassBonus: Math.Max(0, RuntimeCharacterAbilityValues.Read(values, RuntimeCharacterQuestAbilityCatalog.ArmourClassAbilityId)),
            MaximumDamageBonus: Math.Max(0, RuntimeCharacterAbilityValues.Read(values, RuntimeCharacterQuestAbilityCatalog.MaximumDamageAbilityId)),
            AccuracyPercentBonus: Math.Max(0, RuntimeCharacterAbilityValues.Read(values, RuntimeCharacterQuestAbilityCatalog.AccuracyAbilityId)),
            StealthBonus: Math.Max(0, RuntimeCharacterAbilityValues.Read(values, RuntimeCharacterQuestAbilityCatalog.StealthAbilityId)),
            DodgeBonus: Math.Max(0, RuntimeCharacterAbilityValues.Read(values, RuntimeCharacterQuestAbilityCatalog.DodgeAbilityId)),
            CriticalBonus: Math.Max(0, RuntimeCharacterAbilityValues.Read(values, RuntimeCharacterQuestAbilityCatalog.CriticalAbilityId)),
            MaximumManaBonus: 0,
            SpellcastingBonus: 0,
            BackstabMinimumBonus: Math.Max(0, RuntimeCharacterAbilityValues.Read(values, RuntimeCharacterQuestAbilityCatalog.BackstabMinimumAbilityId)),
            BackstabMaximumBonus: Math.Max(0, RuntimeCharacterAbilityValues.Read(values, RuntimeCharacterQuestAbilityCatalog.BackstabMaximumAbilityId)),
            SeeHiddenRank: 0,
            EvidenceStatus: "verified decoded ability semantics",
            RuntimeOwner: "Bundle B live physical combat profile");
    }

    private static int CombatValue(RuntimeCharacterRecord character, string key, int fallback)
        => character.Conditions.TryGetValue(key, out var raw)
           && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;

    private static int DerivedAttackValue(RuntimeCharacterRecord character)
        => Math.Max(1, (AdjustedStat(character, "strength", "str") + AdjustedStat(character, "agility", "agi", "agl")) / 2 + character.Level);

    private static int DerivedDefenseValue(RuntimeCharacterRecord character)
        => Math.Max(1, (AdjustedStat(character, "agility", "agi", "agl") + AdjustedStat(character, "health", "hea")) / 2 + character.Level);

    private static int DerivedDodgeBonus(RuntimeCharacterRecord character)
        => Math.Max(0, AdjustedStat(character, "agility", "agi", "agl") / 10);

    private static int AdjustedStat(RuntimeCharacterRecord character, params string[] keys)
    {
        var value = Stat(character.BaseStats, keys);
        var canonical = keys.FirstOrDefault()?.ToLowerInvariant() switch
        {
            "int" or "intelligence" => "intellect",
            "wil" or "will" => "wisdom",
            "agi" or "agl" => "agility",
            "str" => "strength",
            "cha" => "charm",
            var key => key ?? string.Empty
        };
        return checked(value + RuntimeClassicStatusProjection.Numeric(character, "stat_" + canonical));
    }

    private static int Stat(IReadOnlyDictionary<string, int> values, params string[] keys)
    {
        foreach (var key in keys)
            if (values.TryGetValue(key, out var value)) return value;
        return 0;
    }

    private static int Required(IReadOnlyDictionary<string, string> values, string key, List<string> blockers)
    {
        if (!values.TryGetValue(key, out var raw) || !int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            blockers.Add($"PLAYER_COMBAT_FIELD_MISSING:{key}");
            return 0;
        }
        return value;
    }

    private static int Optional(IReadOnlyDictionary<string, string> values, string key)
        => values.TryGetValue(key, out var raw) && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;
}

public sealed class RuntimePlayerCombatProfileSmokeReport
{
    public bool Passed => Failures.Count == 0;
    public List<string> Checks { get; } = new();
    public List<string> Failures { get; } = new();

    public static RuntimePlayerCombatProfileSmokeReport Run(string projectRoot)
    {
        var report = new RuntimePlayerCombatProfileSmokeReport();
        try
        {
            var catalog = RuntimeItemCombatCatalog.Load(projectRoot);
            var weapon = catalog.Profiles.First(x => x.MaximumDamage > 0);
            var character = new RuntimeCharacterRecord(
                "character.test", "account.test", "Test", "Classic", "race.test", "Test", "class.test", "Test", 1, 0,
                10, 10, 0, 0, "room.1", 1,
                new Dictionary<string, int>(), new Dictionary<string, int>(), Array.Empty<string>(), Array.Empty<string>(), new Dictionary<string, string?>(),
                new Dictionary<string, string>
                {
                    ["combat.attack_value"] = "50",
                    ["combat.defense_value"] = "40",
                    ["combat.dodge_bonus"] = "3",
                    ["quest.ability.2"] = "2",
                    ["quest.ability.4"] = "3",
                    ["quest.ability.22"] = "4",
                    ["quest.ability.34"] = "5",
                    ["quest.ability.58"] = "6"
                }, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
                ItemState: new RuntimeItemState(new[]
                {
                    new RuntimeItemInstance("instance.weapon", weapon.ItemId, "character.test", RuntimeItemLocations.Equipment, "character.test", "MAIN_HAND", 0, 1, null)
            }, 0, new Dictionary<string, string>()));
            var profile = new RuntimePlayerCombatProfileResolver(catalog).Resolve(character);
            Require(profile.Ready, "profile-ready"); report.Checks.Add("profile-ready");
            Require(profile.AttackValue == 50 && profile.DefenseValue == 40, "authoritative-character-fields"); report.Checks.Add("authoritative-character-fields");
            var expectedBackstabAttackValue = RuntimeClassicPhysicalFormulaCalculator.CalculateBackstabAccuracy(
                RuntimePlayerCombatProfileResolver.ResolveModifiedStealth(character),
                0,
                profile.EquippedBackstabAccuracyBonus,
                RuntimePlayerCombatProfileResolver.HasClassStealth(character),
                profile.AbilityAccuracyModifier + profile.WeaponAccuracyModifier);
            Require(profile.BackstabAttackValue == expectedBackstabAttackValue, "backstab-accuracy-formula-consumer"); report.Checks.Add("backstab-accuracy-formula-consumer");
            Require(profile.MinimumDamage == weapon.MinimumDamage && profile.MaximumDamage == weapon.MaximumDamage - 5 + 3, "generated-weapon-damage-plus-strength-plus-ability-4"); report.Checks.Add("generated-weapon-damage");
            Require(profile.StrengthMaximumDamageBonus == -5, "classic-strength-maximum-damage-bonus"); report.Checks.Add("classic-strength-maximum-damage-bonus");
            Require(profile.WeaponSpeed == weapon.Speed, "weapon-speed-consumer"); report.Checks.Add("weapon-speed-consumer");
            Require(profile.WeaponAccuracyModifier == weapon.Accuracy, "generated-weapon-accuracy"); report.Checks.Add("generated-weapon-accuracy");
            Require(profile.ClassCombatAccuracy == 0 && profile.AbilityAccuracyModifier == 4, "ability-22-live-consumer"); report.Checks.Add("ability-22-live-consumer");
            Require(profile.NaturalCriticalChance == 1, "natural-critical-live-consumer"); report.Checks.Add("natural-critical-live-consumer");
            Require(profile.QuestMaximumDamageBonus == 3 && profile.QuestArmorClassBonus == 2 && profile.QuestDodgeBonus == 5 && profile.QuestCriticalBonus == 6, "quest-combat-bonus-consumers"); report.Checks.Add("quest-combat-bonus-consumers");
            Require(!profile.Barehanded && profile.WeaponIsTwoHanded == weapon.IsTwoHanded && profile.WeaponSupportsBackstab == weapon.SupportsBackstab && profile.EquippedWeaponType == weapon.WeaponType, "weapon-backstab-metadata-consumer"); report.Checks.Add("weapon-backstab-metadata-consumer");
            var stunnedConditions = new Dictionary<string, string>(character.Conditions, StringComparer.OrdinalIgnoreCase) { ["combat.stunned"] = "true" };
            var stunned = new RuntimePlayerCombatProfileResolver(catalog).Resolve(character with { Conditions = stunnedConditions });
            Require(stunned.DodgeBonus == 0, "stunned-dodge-suppression"); report.Checks.Add("stunned-dodge-suppression");
            var paralyzedConditions = new Dictionary<string, string>(character.Conditions, StringComparer.OrdinalIgnoreCase) { ["state.paralyzed"] = "1" };
            var paralyzedCharacter = character with { Conditions = paralyzedConditions };
            var paralyzed = new RuntimePlayerCombatProfileResolver(catalog).Resolve(paralyzedCharacter);
            Require(paralyzed.DodgeBonus == 0, "paralyzed-dodge-suppression"); report.Checks.Add("paralyzed-dodge-suppression");
            Require(RuntimePlayerCombatProfileResolver.IsPhysicalAttackSuppressed(paralyzedCharacter), "paralyzed-attack-suppression"); report.Checks.Add("paralyzed-attack-suppression");
            Require(RuntimePlayerCombatProfileResolver.IsPhysicalAttackSuppressed(character with { Conditions = stunnedConditions }), "stunned-attack-suppression"); report.Checks.Add("stunned-attack-suppression");
            var sleepingConditions = new Dictionary<string, string>(character.Conditions, StringComparer.OrdinalIgnoreCase) { ["combat.sleeping"] = "yes" };
            Require(RuntimePlayerCombatProfileResolver.IsPhysicalAttackSuppressed(character with { Conditions = sleepingConditions }), "sleeping-attack-suppression"); report.Checks.Add("sleeping-attack-suppression");
            var immobilizedConditions = new Dictionary<string, string>(character.Conditions, StringComparer.OrdinalIgnoreCase) { ["combat.immobilized"] = "true" };
            Require(!RuntimePlayerCombatProfileResolver.IsPhysicalAttackSuppressed(character with { Conditions = immobilizedConditions }), "immobilized-retains-attack"); report.Checks.Add("immobilized-retains-attack");
            Require(typeof(RuntimePlayerCombatProfileResolver).GetMethod(nameof(RuntimePlayerCombatProfileResolver.Resolve)) is not null, "runtime-resolver"); report.Checks.Add("runtime-resolver");
        }
        catch (Exception ex) { report.Failures.Add(ex.Message); }
        return report;
    }

    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    private static void Require(bool value, string code) { if (!value) throw new InvalidOperationException(code); }
}
