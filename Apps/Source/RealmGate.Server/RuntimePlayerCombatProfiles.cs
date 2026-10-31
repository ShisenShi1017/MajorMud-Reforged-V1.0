using System.Globalization;
using System.Text.Json;

namespace RealmGate.Server;

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
    int WeaponMagicalRank,
    int WeaponRequiredToHitCode,
    int HitMagicBypassRank,
    IReadOnlyList<RuntimeWeaponOnHitSpell> WeaponOnHitSpells,
    IReadOnlyList<string> EquippedCombatItems,
    IReadOnlyList<string> Blockers)
{
    public bool Ready => Blockers.Count == 0;
    // Reduced before weapon-only Quick and Deadly is added. Martial attacks
    // must use this value rather than subtracting an unreduced bonus from a
    // final reduced chance.
    public int CriticalChanceWithoutQuickAndDeadly { get; init; }
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
        var attackValue = CombatValue(character, "combat.attack_value", 0);
        var conditionBackstabAccuracyBonus = Optional(character.Conditions, "combat.backstab_accuracy_bonus");
        var defenseValue = CombatValue(character, "combat.defense_value", DerivedDefenseValue(character));
        var dodgeBonus = 0;
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
        var equippedAccuracy = 0;
        var equippedBackstabAccuracyBonus = 0;
        var barehanded = true;
        var weaponIsTwoHanded = false;
        var weaponSupportsBackstab = false;
        var equippedWeaponType = 0;
        var weaponMagicalRank = 0;
        var weaponRequiredToHitCode = 0;
        var equipmentHitMagicRank = 0;
        var weaponSpeed = 0;
        var weaponItemId = string.Empty;
        var weaponStrengthRequirement = 0;
        IReadOnlyList<RuntimeWeaponOnHitSpell> weaponOnHitSpells = Array.Empty<RuntimeWeaponOnHitSpell>();
        var questBonuses = ResolveQuestCombatBonuses(character);
        var abilityAccuracy = checked(questBonuses.AccuracyPercentBonus + RuntimeClassicStatusProjection.Numeric(character, "accuracy"));
        armorClass = checked(armorClass + questBonuses.ArmourClassBonus);
        var encumbrancePercent = ResolveCombatEncumbrancePercent(character);
        dodgeBonus = character.Conditions.TryGetValue("combat.raw_dodge_override", out var rawDodgeOverride)
            && int.TryParse(rawDodgeOverride, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedRawDodgeOverride)
            ? parsedRawDodgeOverride
            : RuntimeClassicCharacterFormulaCalculator.ResolveRawDodge(character, encumbrancePercent);
        criticalChance = checked(criticalChance + questBonuses.CriticalBonus);
        var incapacitated = IsAvoidanceSuppressed(character);
        if (incapacitated) dodgeBonus = 0;
        var equipped = new List<string>();
        var weaponProfiles = new List<RuntimeItemCombatProfile>();
        var readiedWeaponId = RuntimeEquipmentRuntimeAuthority.ResolveReadiedWeaponInstanceId(
            character.ItemState, character.CharacterId);
        var readiedLightId = RuntimeEquipmentRuntimeAuthority.ResolveReadiedLightInstanceId(
            character.ItemState, character.CharacterId);

        foreach (var item in character.ItemState?.Items ?? Array.Empty<RuntimeItemInstance>())
        {
            if (!string.Equals(item.OwnerId, character.CharacterId, StringComparison.OrdinalIgnoreCase)) continue;
            var worn = item.LocationKind.Equals(RuntimeItemLocations.Equipment, StringComparison.OrdinalIgnoreCase)
                && item.EquippedSlot is not null;
            var carriedReadyItem = item.LocationKind.Equals(RuntimeItemLocations.Character, StringComparison.OrdinalIgnoreCase)
                && item.EquippedSlot is null
                && (item.InstanceId.Equals(readiedWeaponId, StringComparison.OrdinalIgnoreCase)
                    || item.InstanceId.Equals(readiedLightId, StringComparison.OrdinalIgnoreCase));
            if (!worn && !carriedReadyItem) continue;
            if (!_items.TryGet(item.CanonicalItemId, out var profile)) continue;
            equipped.Add(item.CanonicalItemId);
            armorClass = checked(armorClass + profile.ArmorClassBonus);
            damageResistance = checked(damageResistance + profile.DamageResistanceBonus);
            criticalChance = checked(criticalChance + profile.CriticalChanceBonus);
            equippedAccuracy = checked(equippedAccuracy + profile.Accuracy);
            equippedBackstabAccuracyBonus = checked(equippedBackstabAccuracyBonus + profile.BackstabAccuracyBonus);
            equipmentHitMagicRank = checked(equipmentHitMagicRank + profile.HitMagicRank);
            // Only the readied instance supplies the weapon damage profile.
            // Old saves without an explicit readiness key still resolve their
            // WIELD instance through the compatibility reader.
            if (profile.MaximumDamage > 0 && readiedWeaponId is not null
                && item.InstanceId.Equals(readiedWeaponId, StringComparison.OrdinalIgnoreCase))
                weaponProfiles.Add(profile);
        }
        damageResistance = ApplyDamageResistancePercent(
            damageResistance, RuntimeClassicStatusProjection.Numeric(character, "damage_resistance_percent"));

        // CodeUpdate0357: stock fighter Accuracy is constructed once from the native
        // _move_player_to_fighter owner. Direct equipped Accuracy is already folded here,
        // so normal live combat must not add the wielded weapon Accuracy a second time.
        var classCombatAccuracy = character.Conditions.ContainsKey("combat.attack_value")
            ? 0
            : RuntimeClassicPhysicalFormulaCalculator.CalculateStockPlayerAccuracyBase(
                ResolveClassCombatCode(character),
                character.Level,
                AdjustedStat(character, "strength", "str"),
                AdjustedStat(character, "agility", "agi", "agl"),
                equippedAccuracy,
                encumbrancePercent,
                character.HitPoints > 0);

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
            weaponMagicalRank = weapon.MagicalRank;
            weaponRequiredToHitCode = weapon.RequiredToHitCode;
            weaponSpeed = weapon.Speed;
            weaponItemId = weapon.ItemId;
            weaponStrengthRequirement = ResolveWeaponStrengthRequirement(character, weaponItemId);
            weaponOnHitSpells = weapon.OnHitSpells;
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
            checked(conditionBackstabAccuracyBonus + equippedBackstabAccuracyBonus + RuntimeClassicStatusProjection.Numeric(character, "backstab_accuracy")),
            HasClassStealth(character),
            checked(abilityAccuracy + weaponAccuracy));

        var strengthMaximumDamageBonus = RuntimeClassicPhysicalFormulaCalculator.CalculateStrengthMaximumDamageBonus(AdjustedStat(character, "strength", "str"));
        maximumDamage = checked(maximumDamage + strengthMaximumDamageBonus + questBonuses.MaximumDamageBonus + RuntimeClassicStatusProjection.Numeric(character, "maximum_damage") + RuntimeClassicCharacterFormulaCalculator.ResolveProjectedEquipmentContribution(character, "maximum_damage_bonus", questBonuses.MaximumDamageBonus));
        if (maximumDamage < minimumDamage) maximumDamage = minimumDamage;
        var attackEnergyCost = barehanded || weaponSpeed <= 0
            ? 0
            : RuntimeClassicPhysicalFormulaCalculator.CalculateClassicLiveAttackEnergyCost(
                ResolveClassCombatCode(character),
                character.Level,
                weaponSpeed,
                AdjustedStat(character, "agility", "agi", "agl"),
                AdjustedStat(character, "strength", "str"),
                encumbrancePercent,
                weaponStrengthRequirement,
                false);
        var quickAndDeadlyCriticalBonus = barehanded || weaponSpeed <= 0
            ? 0
            : RuntimeClassicPhysicalFormulaCalculator.CalculateQuickAndDeadlyCriticalBonus(
                AdjustedStat(character, "agility", "agi", "agl"), attackEnergyCost, encumbrancePercent);
        var criticalChanceWithoutQuickAndDeadly =
            RuntimeClassicPhysicalFormulaCalculator.CalculateFinalCriticalChance(criticalChance);
        criticalChance = RuntimeClassicPhysicalFormulaCalculator.CalculateFinalCriticalChance(
            checked(criticalChance + quickAndDeadlyCriticalBonus));

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
            checked(questBonuses.BackstabMinimumBonus + RuntimeClassicCharacterFormulaCalculator.ResolveProjectedEquipmentContribution(character, "backstab_minimum_damage_bonus", questBonuses.BackstabMinimumBonus) + RuntimeClassicStatusProjection.Numeric(character, "backstab_minimum_damage")),
            checked(questBonuses.BackstabMaximumBonus + RuntimeClassicCharacterFormulaCalculator.ResolveProjectedEquipmentContribution(character, "backstab_maximum_damage_bonus", questBonuses.BackstabMaximumBonus) + RuntimeClassicStatusProjection.Numeric(character, "backstab_maximum_damage")),
            equippedBackstabAccuracyBonus,
            RuntimeCharacterAbilityValues.Read(RuntimeCharacterAbilityValues.Restore(character.Conditions), RuntimeCharacterQuestAbilityCatalog.SmashAbilityId) > 0,
            barehanded,
            weaponIsTwoHanded,
            weaponSupportsBackstab,
            equippedWeaponType,
            weaponMagicalRank,
            weaponRequiredToHitCode,
            checked(RuntimeCharacterAbilityValues.Read(RuntimeCharacterAbilityValues.Restore(character.Conditions), 142)
                + equipmentHitMagicRank + RuntimeClassicStatusProjection.Numeric(character, "hit_magic")),
            weaponOnHitSpells,
            equipped,
            blockers.Distinct(StringComparer.OrdinalIgnoreCase).ToArray())
        {
            CriticalChanceWithoutQuickAndDeadly = criticalChanceWithoutQuickAndDeadly
        };
    }

    internal static int ResolveModifiedStealth(RuntimeCharacterRecord character)
    {
        ArgumentNullException.ThrowIfNull(character);
        if (RuntimeClassicStealthResolver.TryReadModifiedStealth(character, out var decoded))
        {
            var questStealth = ResolveQuestCombatBonuses(character).StealthBonus;
            return checked(decoded + RuntimeClassicStatusProjection.Numeric(character, "stealth") + RuntimeClassicCharacterFormulaCalculator.ResolveProjectedEquipmentContribution(character, "stealth_bonus", questStealth));
        }
        return checked(CombatValue(character, "combat.stealth_rating", AdjustedStat(character, "stealth", "stl")) + RuntimeClassicStatusProjection.Numeric(character, "stealth") + Derived(character, "stealth_bonus"));
    }

    internal static bool HasClassStealth(RuntimeCharacterRecord character)
    {
        ArgumentNullException.ThrowIfNull(character);
        return ReadConditionBool(character.Conditions, "class.stealth_capable", "combat.class_stealth", "stealth.class");
    }

    internal static int ApplyDamageResistancePercent(int damageResistance, int percent) =>
        Math.Max(0, checked(damageResistance + checked((damageResistance * percent) / 100)));

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
        if (RuntimeClassicStatusProjection.Flag(character, "paralyzed")
            || RuntimeClassicStatusProjection.Flag(character, "immobilized")) return true;
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

    private int ResolveWeaponStrengthRequirement(RuntimeCharacterRecord character, string weaponItemId)
    {
        if (string.IsNullOrWhiteSpace(_projectRoot) || string.IsNullOrWhiteSpace(weaponItemId)) return 0;
        var catalog = RuntimeItemCatalog.Load(_projectRoot, character.WorldId);
        return Math.Max(0, catalog.Equipment(weaponItemId)?.MinimumStrength ?? 0);
    }

    private int ResolveCombatEncumbrancePercent(RuntimeCharacterRecord character)
    {
        // Explicit combat.encumbrance_percent is retained as a deterministic harness/admin
        // override. Live characters derive the native percentage from actual carried weight.
        if (character.Conditions.TryGetValue("combat.encumbrance_percent", out var raw)
            && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var explicitPercent))
            return Math.Clamp(explicitPercent, 0, 100);
        if (string.IsNullOrWhiteSpace(_projectRoot)) return 0;

        var itemCatalog = RuntimeItemCatalog.Load(_projectRoot, character.WorldId);
        var carried = RuntimeBundleCInventoryAuthority.Project(character, itemCatalog).TotalCarriedWeight;
        var strength = Math.Max(0, AdjustedStat(character, "strength", "str"));
        var baseCapacity = RuntimeMajorMudCompatibility.CalculateBaseMaximumEncumberance(strength);
        var characterCatalog = RuntimeCharacterCatalog.Load(_projectRoot, character.WorldId);
        var permanentCapacityBonus = characterCatalog.RequireRace(character.RaceId).AbilitySlots
            .Where(grant => grant.AbilityId == 96).Sum(grant => grant.Value)
            + characterCatalog.RequireClass(character.ClassId).AbilitySlots
                .Where(grant => grant.AbilityId == 96).Sum(grant => grant.Value);
        var equipmentCapacityBonus = character.SemanticState?.DerivedStats.GetValueOrDefault("encumbrance_modifier_percent") ?? 0;
        var maximum = RuntimeMajorMudCompatibility.ApplyEncumberanceModifier(
            baseCapacity, Math.Max(0, checked(permanentCapacityBonus + equipmentCapacityBonus)));
        return RuntimeMajorMudCompatibility.CalculateEncumberancePercent(carried, maximum);
    }


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


    private static int DerivedDefenseValue(RuntimeCharacterRecord character)
        => Math.Max(1, (AdjustedStat(character, "agility", "agi", "agl") + AdjustedStat(character, "health", "hea")) / 2 + character.Level);

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

    private static int Derived(RuntimeCharacterRecord character, string key)
        => character.SemanticState?.DerivedStats.TryGetValue(key, out var value) == true ? value : 0;
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
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    ["str"] = 50, ["agi"] = 50, ["int"] = 50, ["hea"] = 50, ["cha"] = 50, ["wisdom"] = 50
                }, new Dictionary<string, int>(), Array.Empty<string>(), Array.Empty<string>(), new Dictionary<string, string?>(),
                new Dictionary<string, string>
                {
                    ["combat.attack_value"] = "50",
                    ["combat.defense_value"] = "40",
                    ["quest.ability.2"] = "2",
                    ["quest.ability.4"] = "3",
                    ["quest.ability.22"] = "4",
                    ["quest.ability.34"] = "5",
                    ["quest.ability.58"] = "6",
                    ["combat.status.backstab_accuracy"] = "10"
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
                profile.EquippedBackstabAccuracyBonus + 10,
                RuntimePlayerCombatProfileResolver.HasClassStealth(character),
                profile.AbilityAccuracyModifier + profile.WeaponAccuracyModifier);
            Require(profile.BackstabAttackValue == expectedBackstabAttackValue, "backstab-accuracy-formula-consumer"); report.Checks.Add("backstab-accuracy-formula-consumer");
            Require(profile.MinimumDamage == weapon.MinimumDamage && profile.MaximumDamage == weapon.MaximumDamage - 5 + 3, "generated-weapon-damage-plus-strength-plus-ability-4"); report.Checks.Add("generated-weapon-damage");
            Require(profile.StrengthMaximumDamageBonus == -5, "classic-strength-maximum-damage-bonus"); report.Checks.Add("classic-strength-maximum-damage-bonus");
            Require(profile.WeaponSpeed == weapon.Speed, "weapon-speed-consumer"); report.Checks.Add("weapon-speed-consumer");
            Require(profile.WeaponAccuracyModifier == weapon.Accuracy, "generated-weapon-accuracy"); report.Checks.Add("generated-weapon-accuracy");

            // Verify a live-ready state transition using two *carried* weapons,
            // not the old synthetic WIELD or MAIN_HAND equipment position.
            // Selecting the second weapon must replace, never stack, the first.
            var otherWeapon = catalog.Profiles.FirstOrDefault(candidate =>
                candidate.MaximumDamage > 0 && candidate.ItemId != weapon.ItemId);
            Require(otherWeapon is not null, "weapon-switch-second-weapon-available");
            var carriedWeapons = new[]
            {
                new RuntimeItemInstance("instance.weapon.a", weapon.ItemId, character.CharacterId,
                    RuntimeItemLocations.Character, character.CharacterId, null, 0, 1, null),
                new RuntimeItemInstance("instance.weapon.b", otherWeapon!.ItemId, character.CharacterId,
                    RuntimeItemLocations.Character, character.CharacterId, null, 0, 1, null)
            };
            RuntimeCharacterRecord ReadyCharacter(string? instanceId) => character with
            {
                ItemState = new RuntimeItemState(carriedWeapons, 0,
                    instanceId is null
                        ? new Dictionary<string, string>()
                        : new Dictionary<string, string>
                        {
                            [RuntimeEquipmentRuntimeAuthority.ReadyWeaponKey] = instanceId
                        })
            };
            var combatResolver = new RuntimePlayerCombatProfileResolver(catalog);
            var firstReadied = combatResolver.Resolve(ReadyCharacter(carriedWeapons[0].InstanceId));
            var switched = combatResolver.Resolve(ReadyCharacter(carriedWeapons[1].InstanceId));
            var unready = combatResolver.Resolve(ReadyCharacter(null));
            Require(!firstReadied.Barehanded && firstReadied.WeaponSpeed == weapon.Speed
                && firstReadied.WeaponAccuracyModifier == weapon.Accuracy
                && firstReadied.MinimumDamage == weapon.MinimumDamage
                && firstReadied.MaximumDamage == weapon.MaximumDamage - 5 + 3
                && firstReadied.EquippedCombatItems.Count(x => x == weapon.ItemId) == 1,
                "first-carried-weapon-combat-profile");
            Require(!switched.Barehanded && switched.WeaponSpeed == otherWeapon.Speed
                && switched.WeaponAccuracyModifier == otherWeapon.Accuracy
                && switched.MinimumDamage == otherWeapon.MinimumDamage
                && switched.MaximumDamage == Math.Max(otherWeapon.MinimumDamage, otherWeapon.MaximumDamage - 5 + 3)
                && switched.EquippedCombatItems.Count == 1
                && switched.EquippedCombatItems[0] == otherWeapon.ItemId,
                "weapon-switch-replaces-damage-speed-and-accuracy");
            Require(unready.Barehanded && unready.WeaponSpeed == 0
                && unready.WeaponAccuracyModifier == 0
                && unready.EquippedCombatItems.Count == 0,
                "unready-does-not-retain-previous-weapon");

            // The combat-start gate and round scheduler must consume the
            // *currently selected* weapon, not the previous active item.
            var firstBackstab = ClassicAttackPreparationPolicy.PrepareBackstab(
                true, false, firstReadied.Barehanded,
                firstReadied.WeaponSupportsBackstab, firstReadied.WeaponIsTwoHanded);
            var switchedBackstab = ClassicAttackPreparationPolicy.PrepareBackstab(
                true, false, switched.Barehanded,
                switched.WeaponSupportsBackstab, switched.WeaponIsTwoHanded);
            var unreadyBackstab = ClassicAttackPreparationPolicy.PrepareBackstab(
                true, false, unready.Barehanded,
                unready.WeaponSupportsBackstab, unready.WeaponIsTwoHanded);
            Require(firstBackstab.BackstabQualified ==
                    (!firstReadied.WeaponIsTwoHanded && firstReadied.WeaponSupportsBackstab)
                && switchedBackstab.BackstabQualified ==
                    (!switched.WeaponIsTwoHanded && switched.WeaponSupportsBackstab)
                && unreadyBackstab.BackstabQualified,
                "weapon-switch-backstab-gate-uses-selected-weapon");

            var firstSchedule = RuntimeClassicPhysicalFormulaCalculator.ResolvePhysicalAttackSchedule(
                ClassicAttackType.NormalWeapon, 0, firstReadied.AttackEnergyCost);
            var switchedSchedule = RuntimeClassicPhysicalFormulaCalculator.ResolvePhysicalAttackSchedule(
                ClassicAttackType.NormalWeapon, 0, switched.AttackEnergyCost);
            Require(firstSchedule.Swings ==
                    RuntimeClassicPhysicalFormulaCalculator.ResolveNormalWeaponAttackSchedule(
                        0, firstReadied.AttackEnergyCost).Swings
                && switchedSchedule.Swings ==
                    RuntimeClassicPhysicalFormulaCalculator.ResolveNormalWeaponAttackSchedule(
                        0, switched.AttackEnergyCost).Swings,
                "weapon-switch-energy-schedule-uses-selected-cost");
            Require(RuntimeClassicPhysicalFormulaCalculator.ResolvePhysicalAttackSchedule(
                    ClassicAttackType.Backstab, 275, switched.AttackEnergyCost).Swings == 1
                && RuntimeClassicPhysicalFormulaCalculator.ResolvePhysicalAttackSchedule(
                    ClassicAttackType.Backstab, 275, switched.AttackEnergyCost).RemainingEnergy == 0,
                "switched-weapon-backstab-remains-single-full-round");
            report.Checks.AddRange(new[]
            {
                "weapon-switch-backstab-gate-uses-selected-weapon",
                "weapon-switch-energy-schedule-uses-selected-cost",
                "switched-weapon-backstab-remains-single-full-round"
            });
            report.Checks.AddRange(new[]
            {
                "first-carried-weapon-combat-profile",
                "weapon-switch-replaces-damage-speed-and-accuracy",
                "unready-does-not-retain-previous-weapon"
            });
            Require(profile.ClassCombatAccuracy == 0 && profile.AbilityAccuracyModifier == 4, "ability-22-live-consumer"); report.Checks.Add("ability-22-live-consumer");
            Require(profile.NaturalCriticalChance == 1, "natural-critical-live-consumer"); report.Checks.Add("natural-critical-live-consumer");

            // Critical reduction is nonlinear above 40. Do not subtract the
            // unreduced weapon-only Q&D bonus from the reduced final chance.
            var highCriticalConditions = new Dictionary<string, string>(character.Conditions,
                StringComparer.OrdinalIgnoreCase) { ["combat.critical_chance"] = "90" };
            var highCritical = new RuntimePlayerCombatProfileResolver(catalog).Resolve(
                character with { Conditions = highCriticalConditions });
            var withoutQnd = highCritical.CriticalChanceWithoutQuickAndDeadly;
            var reducedWithQnd = highCritical.CriticalChance;
            Require(withoutQnd >= 0 && withoutQnd <= reducedWithQnd,
                "martial-critical-removes-qnd-before-reduction");
            if (highCritical.QuickAndDeadlyCriticalBonus > 0 && reducedWithQnd < 99)
                Require(withoutQnd != Math.Max(0, reducedWithQnd - highCritical.QuickAndDeadlyCriticalBonus),
                    "martial-critical-nonlinear-qnd-not-subtracted-from-final");
            Require(RuntimeClassicPhysicalFormulaCalculator.CalculateFinalCriticalChance(60) == 46
                && RuntimeClassicPhysicalFormulaCalculator.CalculateFinalCriticalChance(80) == 53
                && RuntimeClassicPhysicalFormulaCalculator.CalculateFinalCriticalChance(80) - 20 == 33,
                "martial-critical-diminishing-returns-known-vector");
            report.Checks.AddRange(new[] {
                "martial-critical-removes-qnd-before-reduction",
                "martial-critical-nonlinear-qnd-not-subtracted-from-final",
                "martial-critical-diminishing-returns-known-vector"
            });
            Require(profile.QuestMaximumDamageBonus == 3 && profile.QuestArmorClassBonus == 2 && profile.QuestDodgeBonus == 5 && profile.QuestCriticalBonus == 6, "quest-combat-bonus-consumers"); report.Checks.Add("quest-combat-bonus-consumers");
            Require(profile.DodgeBonus == 15, "stock-character-raw-dodge-consumer"); report.Checks.Add("stock-character-raw-dodge-consumer");
            var equipmentDerived = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["maximum_damage_bonus"] = 7,
                ["dodge_bonus"] = 11,
                ["stealth_bonus"] = 13,
                ["backstab_minimum_damage_bonus"] = 17,
                ["backstab_maximum_damage_bonus"] = 19
            };
            var equipmentCharacter = character with
            {
                SemanticState = RuntimeCharacterSemanticState.Empty with { DerivedStats = equipmentDerived }
            };
            var equipmentProfile = new RuntimePlayerCombatProfileResolver(catalog).Resolve(equipmentCharacter);
            Require(equipmentProfile.MaximumDamage == profile.MaximumDamage + 7, "equipment-ability-4-maximum-damage-consumer"); report.Checks.Add("equipment-ability-4-maximum-damage-consumer");
            Require(equipmentProfile.DodgeBonus == profile.DodgeBonus + 11, "equipment-ability-34-dodge-consumer"); report.Checks.Add("equipment-ability-34-dodge-consumer");
            Require(RuntimePlayerCombatProfileResolver.ResolveModifiedStealth(equipmentCharacter) == RuntimePlayerCombatProfileResolver.ResolveModifiedStealth(character) + 13, "equipment-ability-27-stealth-consumer"); report.Checks.Add("equipment-ability-27-stealth-consumer");
            var projectedCombinedCharacter = character with
            {
                SemanticState = RuntimeCharacterSemanticState.Empty with
                {
                    DerivedStats = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["maximum_damage_bonus"] = 10,
                        ["dodge_bonus"] = 16
                    },
                    RuntimeMarkers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        [RuntimeCharacterCombatStatisticProjector.ProjectionMarker] = "test|character.statistics.explicit_modifier_projection_0155|instance.weapon"
                    }
                }
            };
            var projectedCombinedProfile = new RuntimePlayerCombatProfileResolver(catalog).Resolve(projectedCombinedCharacter);
            Require(projectedCombinedProfile.MaximumDamage == equipmentProfile.MaximumDamage && projectedCombinedProfile.DodgeBonus == equipmentProfile.DodgeBonus,
                "projected-ability-equipment-no-double-count"); report.Checks.Add("projected-ability-equipment-no-double-count");
            Require(equipmentProfile.BackstabMinimumBonus == profile.BackstabMinimumBonus + 17 && equipmentProfile.BackstabMaximumBonus == profile.BackstabMaximumBonus + 19, "equipment-abilities-117-118-backstab-damage-consumers"); report.Checks.Add("equipment-abilities-117-118-backstab-damage-consumers");
            var hitMagicConditions = new Dictionary<string, string>(character.Conditions, StringComparer.OrdinalIgnoreCase)
            {
                ["combat.status.hit_magic"] = "10"
            };
            var hitMagicProfile = new RuntimePlayerCombatProfileResolver(catalog).Resolve(character with { Conditions = hitMagicConditions });
            Require(hitMagicProfile.HitMagicBypassRank == profile.HitMagicBypassRank + 10, "spell-hitmagic-live-profile-consumer"); report.Checks.Add("spell-hitmagic-live-profile-consumer");
            var statusCatalog = RuntimeClassicLiveStatusCatalog.Load(projectRoot, "Classic");
            Require(statusCatalog.Definitions.Count(definition => definition.Abilities.Any(ability => ability.AbilityId == 142 && ability.Value == 10)) == 2,
                "spell-hitmagic-owner-count"); report.Checks.Add("spell-hitmagic-two-value-10-owners");
            Require(!profile.Barehanded && profile.WeaponIsTwoHanded == weapon.IsTwoHanded && profile.WeaponSupportsBackstab == weapon.SupportsBackstab && profile.EquippedWeaponType == weapon.WeaponType, "weapon-backstab-metadata-consumer"); report.Checks.Add("weapon-backstab-metadata-consumer");
            Require(RuntimePlayerCombatProfileResolver.ApplyDamageResistancePercent(40, -25) == 30
                && RuntimePlayerCombatProfileResolver.ApplyDamageResistancePercent(40, 50) == 60
                && RuntimePlayerCombatProfileResolver.ApplyDamageResistancePercent(40, -500) == 0,
                "alter-dr-percent-live-consumer"); report.Checks.Add("alter-dr-percent-live-consumer");
            var magicalCreature = new RuntimeNpcDefinition { Id = "npc.magical", Name = "magical target", MagicalRequirement = 5 };
            var exactCombined = RuntimeMagicalCreatureWeaponAdmission.Evaluate(
                magicalCreature, profile with { HitMagicBypassRank = 2, WeaponMagicalRank = 3 }, usesWieldedWeapon: true);
            Require(exactCombined.Allowed && exactCombined.RemainingMagicalRequirement == 3, "hitmagic-reduces-before-weapon-magical-check"); report.Checks.Add("hitmagic-reduces-before-weapon-magical-check");
            Require(!RuntimeMagicalCreatureWeaponAdmission.Evaluate(
                magicalCreature, profile with { HitMagicBypassRank = 1, WeaponMagicalRank = 3 }, usesWieldedWeapon: true).Allowed,
                "insufficient-magical-weapon-rejected"); report.Checks.Add("insufficient-magical-weapon-rejected");
            Require(RuntimeMagicalCreatureWeaponAdmission.Evaluate(
                magicalCreature, profile with { HitMagicBypassRank = 5, WeaponMagicalRank = 0 }, usesWieldedWeapon: false).Allowed,
                "hitmagic-full-bypass-allows-unarmed"); report.Checks.Add("hitmagic-full-bypass-allows-unarmed");
            var codedCreature = new RuntimeNpcDefinition
            {
                Id = "npc.coded",
                Name = "coded target",
                MagicalRequirement = magicalCreature.MagicalRequirement,
                RequiredToHitCode = 2
            };
            Require(RuntimeMagicalCreatureWeaponAdmission.Evaluate(
                codedCreature, profile with { HitMagicBypassRank = 5, WeaponRequiredToHitCode = 2 }, usesWieldedWeapon: true).Allowed,
                "required-to-hit-exact-code"); report.Checks.Add("required-to-hit-exact-code");
            Require(!RuntimeMagicalCreatureWeaponAdmission.Evaluate(
                codedCreature, profile with { HitMagicBypassRank = 5, WeaponRequiredToHitCode = 3 }, usesWieldedWeapon: true).Allowed &&
                !RuntimeMagicalCreatureWeaponAdmission.Evaluate(
                    codedCreature, profile with { HitMagicBypassRank = 5, WeaponRequiredToHitCode = 2 }, usesWieldedWeapon: false).Allowed,
                "required-to-hit-mismatch-and-unarmed-rejected"); report.Checks.Add("required-to-hit-mismatch-and-unarmed-rejected");
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
