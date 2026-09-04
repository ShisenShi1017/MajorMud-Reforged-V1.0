namespace RealmGate.Server;

public sealed record RuntimeCharacterDerivedStatistics(
    int MaximumHitPoints,
    int MaximumMana,
    int ConstitutionAdjustment,
    int AccumulatedHitPointRolls,
    string EvidenceClassification,
    string FormulaId);


public sealed record RuntimeCharacterPromotionGrowth(
    int HitPointRoll,
    int MaximumHitPointDelta,
    int MaximumManaDelta,
    string EvidenceClassification,
    string FormulaId);

public static class RuntimeCharacterDerivedStatisticCalculator
{
    public const string FormulaId = "character.derived.hp_mana.classic_0142";

    public static RuntimeCharacterDerivedStatistics CalculateCreation(
        RuntimeCharacterDefinition race,
        RuntimeCharacterDefinition characterClass,
        int level,
        IReadOnlyDictionary<string, int> baseStats,
        IReadOnlyDictionary<string, int>? abilityValues = null,
        int equipmentManaCache = 0)
    {
        if (level < 1) throw new ArgumentOutOfRangeException(nameof(level));
        if (!baseStats.TryGetValue("hea", out var health))
            throw new InvalidDataException("Character statistics require canonical 'hea'.");

        var hpDefinition = characterClass.HitPoints
            ?? throw new InvalidDataException($"Class '{characterClass.Id}' has no hit-point definition.");
        var abilities = abilityValues ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var maximumHitPointAbility = ReadAbility(abilities, "ability.max_hp", "MaxHP", "max_hp");
        var maximumManaAbility = ReadAbility(abilities, "ability.max_mana", "MaxMana", "max_mana");

        // wccmmud.dll calculate_secondary_stats @0x0041a424.
        // Signed truncation is reproduced by adding 15 before arithmetic shift for negatives.
        var healthAdjustmentProduct = checked((health - 50) * level);
        if (healthAdjustmentProduct < 0) healthAdjustmentProduct = checked(healthAdjustmentProduct + 15);
        var constitutionAdjustment = healthAdjustmentProduct >> 4;

        // Creation deterministically seeds the accumulated roll pool with the class cap.
        var accumulatedHitPointRolls = hpDefinition.MaximumBonusPerLevel;
        var maximumHitPoints = checked(
            (health / 2)
            + checked(level * hpDefinition.MinimumPerLevel)
            + checked(level * race.HitPointsPerLevelAdjustment)
            + constitutionAdjustment
            + accumulatedHitPointRolls
            + constitutionAdjustment
            + maximumHitPointAbility);
        maximumHitPoints = Math.Max(1, maximumHitPoints);

        var magic = characterClass.Magic;
        var maximumMana = magic switch
        {
            null => 0,
            { Type: 0 } => 0,
            { Type: 5 } => Math.Max(0, level - 1),
            { Type: >= 1 and <= 4 } => Math.Max(0, checked(maximumManaAbility + checked(magic.Level * level * 2) + equipmentManaCache + 6)),
            _ => throw new InvalidDataException($"Class '{characterClass.Id}' has unsupported magic type '{magic.Type}'.")
        };

        return new RuntimeCharacterDerivedStatistics(
            maximumHitPoints,
            maximumMana,
            constitutionAdjustment,
            accumulatedHitPointRolls,
            "proven",
            FormulaId);
    }

    public static RuntimeCharacterPromotionGrowth CalculatePromotionGrowth(
        RuntimeCharacterDefinition race,
        RuntimeCharacterDefinition characterClass,
        int currentLevel,
        IReadOnlyDictionary<string, int> baseStats,
        int hitPointRoll)
    {
        if (currentLevel < 1) throw new ArgumentOutOfRangeException(nameof(currentLevel));
        if (!baseStats.TryGetValue("hea", out var health))
            throw new InvalidDataException("Character statistics require canonical 'hea'.");
        var hp = characterClass.HitPoints
            ?? throw new InvalidDataException($"Class '{characterClass.Id}' has no hit-point definition.");
        if (hitPointRoll < 0 || hitPointRoll > hp.MaximumBonusPerLevel)
            throw new ArgumentOutOfRangeException(nameof(hitPointRoll), $"HP roll must be between 0 and {hp.MaximumBonusPerLevel}.");

        static int ConstitutionAdjustment(int healthValue, int level)
        {
            var product = checked((healthValue - 50) * level);
            if (product < 0) product = checked(product + 15);
            return product >> 4;
        }

        var nextLevel = checked(currentLevel + 1);
        var constitutionDelta = checked(ConstitutionAdjustment(health, nextLevel) - ConstitutionAdjustment(health, currentLevel));
        var hpDelta = checked(hp.MinimumPerLevel + race.HitPointsPerLevelAdjustment + hitPointRoll + checked(constitutionDelta * 2));
        var manaDelta = characterClass.Magic switch
        {
            null or { Type: 0 } => 0,
            { Type: 5 } => 1,
            { Type: >= 1 and <= 4 } magic => checked(magic.Level * 2),
            _ => throw new InvalidDataException($"Class '{characterClass.Id}' has unsupported magic type '{characterClass.Magic!.Type}'.")
        };
        return new RuntimeCharacterPromotionGrowth(hitPointRoll, hpDelta, manaDelta, "proven", FormulaId);
    }

    private static int ReadAbility(IReadOnlyDictionary<string, int> values, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (values.TryGetValue(key, out var value)) return value;
        }
        return 0;
    }
}

public sealed record RuntimeCharacterCombatStatisticProjection(
    IReadOnlyDictionary<string, int> DerivedStats,
    IReadOnlyDictionary<string, int> Resistances,
    IReadOnlyDictionary<string, int> SavingThrows,
    int HitPointRegenerationPercent,
    int ManaRegenerationPercent,
    IReadOnlyList<string> EquipmentSourceInstanceIds,
    string EvidenceClassification,
    string FormulaId);

public static class RuntimeCharacterCombatStatisticProjector
{
    public const string FormulaId = "character.statistics.explicit_modifier_projection_0155";
    public const string EvidenceStronglyCorrelated = "strongly_correlated";
    public const string ProjectionMarker = "statistics.last_projection";

    // Ability IDs are authoritative MMExplorer/Librarium identifiers.
    private const int ResistColdAbilityId = 3;
    private const int ResistFireAbilityId = 5;
    private const int ResistStoneAbilityId = 65;
    private const int ResistLightningAbilityId = 66;
    private const int SpeedAbilityId = 87;
    private const int HitPointRegenerationAbilityId = 123;
    private const int ManaRegenerationAbilityId = 145;
    private const int ResistWaterAbilityId = 147;

    private static readonly string[] CanonicalDerivedKeys =
    {
        "armor_class_bonus",
        "damage_resistance_bonus",
        "accuracy_bonus",
        "speed_bonus"
    };

    public static RuntimeCharacterRecord Recalculate(
        RuntimeCharacterRecord character,
        RuntimeEquipmentModifierTotals equipment,
        IReadOnlyDictionary<int, int>? abilityValues = null,
        DateTimeOffset? projectedAtUtc = null)
    {
        ArgumentNullException.ThrowIfNull(equipment);
        var semantic = character.SemanticState ?? RuntimeCharacterSemanticState.Empty;
        var derived = new Dictionary<string, int>(semantic.DerivedStats, StringComparer.OrdinalIgnoreCase);

        // Only direct numeric fields exposed by the authoritative item projection are executable here.
        // Base AC/AV/DV/initiative formulas and cross-category ordering remain native-only unknowns.
        derived[CanonicalDerivedKeys[0]] = equipment.ArmorClassBonus;
        derived[CanonicalDerivedKeys[1]] = equipment.DamageResistanceBonus;
        derived[CanonicalDerivedKeys[2]] = equipment.AccuracyBonus;
        var abilities = new Dictionary<int, int>(abilityValues ?? RuntimeCharacterAbilityValues.Restore(character.Conditions));
        foreach (var modifier in equipment.AbilityModifiers)
            abilities[modifier.Key] = checked(abilities.GetValueOrDefault(modifier.Key) + modifier.Value);
        derived[CanonicalDerivedKeys[3]] = checked(equipment.SpeedBonus + RuntimeCharacterAbilityValues.Read(abilities, SpeedAbilityId));
        derived["hit_point_regeneration_percent"] = RuntimeCharacterAbilityValues.Read(abilities, HitPointRegenerationAbilityId);
        derived["mana_regeneration_percent"] = RuntimeCharacterAbilityValues.Read(abilities, ManaRegenerationAbilityId);
        var previousMaximumHitPointBonus = semantic.DerivedStats.GetValueOrDefault("maximum_hit_points_bonus");
        var previousMaximumManaBonus = semantic.DerivedStats.GetValueOrDefault("maximum_mana_bonus");
        var maximumHitPointBonus = equipment.AbilityModifiers.GetValueOrDefault(88);
        var maximumManaBonus = equipment.AbilityModifiers.GetValueOrDefault(69);
        derived["maximum_hit_points_bonus"] = maximumHitPointBonus;
        derived["maximum_mana_bonus"] = maximumManaBonus;
        derived["encumbrance_modifier_percent"] = equipment.AbilityModifiers.GetValueOrDefault(96);
        derived["stealth_bonus"] = RuntimeCharacterAbilityValues.Read(abilities, 27);
        derived["maximum_damage_bonus"] = RuntimeCharacterAbilityValues.Read(abilities, 4);
        derived["dodge_bonus"] = RuntimeCharacterAbilityValues.Read(abilities, 34);
        derived["critical_chance_bonus"] = RuntimeCharacterAbilityValues.Read(abilities, 58);
        derived["magic_resistance_bonus"] = RuntimeCharacterAbilityValues.Read(abilities, 36);
        derived["anti_magic_rank"] = equipment.AbilityModifiers.ContainsKey(51) ? Math.Max(1, RuntimeCharacterAbilityValues.Read(abilities, 51)) : 0;
        derived["poison_immunity_rank"] = equipment.AbilityModifiers.ContainsKey(21) ? Math.Max(1, RuntimeCharacterAbilityValues.Read(abilities, 21)) : 0;
        derived["protection_from_evil_bonus"] = RuntimeCharacterAbilityValues.Read(abilities, 24);
        derived["protection_from_good_bonus"] = RuntimeCharacterAbilityValues.Read(abilities, 25);
        derived["damage_shield_bonus"] = RuntimeCharacterAbilityValues.Read(abilities, 72);
        derived["punch_accuracy_bonus"] = RuntimeCharacterAbilityValues.Read(abilities, 89);
        derived["kick_accuracy_bonus"] = RuntimeCharacterAbilityValues.Read(abilities, 90);
        derived["jumpkick_accuracy_bonus"] = RuntimeCharacterAbilityValues.Read(abilities, 91);
        derived["punch_damage_bonus"] = RuntimeCharacterAbilityValues.Read(abilities, 92);
        derived["kick_damage_bonus"] = RuntimeCharacterAbilityValues.Read(abilities, 93);
        derived["jumpkick_damage_bonus"] = RuntimeCharacterAbilityValues.Read(abilities, 94);
        derived["perception_bonus"] = RuntimeCharacterAbilityValues.Read(abilities, 77);
        derived["backstab_accuracy_bonus"] = RuntimeCharacterAbilityValues.Read(abilities, 116);
        derived["backstab_minimum_damage_bonus"] = RuntimeCharacterAbilityValues.Read(abilities, 117);
        derived["backstab_maximum_damage_bonus"] = RuntimeCharacterAbilityValues.Read(abilities, 118);
        derived["find_traps_ability_bonus"] = RuntimeCharacterAbilityValues.Read(abilities, 40);
        derived["disarm_traps_bonus"] = RuntimeCharacterAbilityValues.Read(abilities, 41);
        derived["find_traps_bonus"] = RuntimeCharacterAbilityValues.Read(abilities, 179);
        // Native WCCMMUD Picklocks composes ability 37 itself plus paired PickLocksValue ability 180.
        // Keep both projections distinct so the grant (class ability.37 value 0) is not mistaken for a bonus.
        derived["picklocks_ability_bonus"] = RuntimeCharacterAbilityValues.Read(abilities, 37);
        derived["picklocks_bonus"] = RuntimeCharacterAbilityValues.Read(abilities, 180);
        derived["shadowform_textblock"] = equipment.AbilityModifiers.GetValueOrDefault(178);

        var resistances = new Dictionary<string, int>(NormalizeCanonicalMap(semantic.Resistances, "resistance"), StringComparer.OrdinalIgnoreCase)
        {
            ["cold"] = RuntimeCharacterAbilityValues.Read(abilities, ResistColdAbilityId),
            ["fire"] = RuntimeCharacterAbilityValues.Read(abilities, ResistFireAbilityId),
            ["stone"] = RuntimeCharacterAbilityValues.Read(abilities, ResistStoneAbilityId),
            ["lightning"] = RuntimeCharacterAbilityValues.Read(abilities, ResistLightningAbilityId),
            ["water"] = RuntimeCharacterAbilityValues.Read(abilities, ResistWaterAbilityId)
        };
        var saves = NormalizeCanonicalMap(semantic.SavingThrows, "saving throw");
        var orderedSources = equipment.SourceInstanceIds
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var markers = new Dictionary<string, string>(semantic.RuntimeMarkers, StringComparer.OrdinalIgnoreCase)
        {
            [ProjectionMarker] = string.Join("|",
                (projectedAtUtc ?? DateTimeOffset.UtcNow).ToString("O", System.Globalization.CultureInfo.InvariantCulture),
                FormulaId,
                string.Join(",", orderedSources))
        };

        var baseMaximumHitPoints = checked(character.MaximumHitPoints - previousMaximumHitPointBonus);
        var baseMaximumMana = checked(character.MaximumMana - previousMaximumManaBonus);
        var projectedMaximumHitPoints = Math.Max(1, checked(baseMaximumHitPoints + maximumHitPointBonus));
        var projectedMaximumMana = Math.Max(0, checked(baseMaximumMana + maximumManaBonus));

        return character with
        {
            SaveVersion = RuntimeCharacterRecordValidator.CurrentSaveVersion,
            HitPoints = Math.Min(character.HitPoints, projectedMaximumHitPoints),
            MaximumHitPoints = projectedMaximumHitPoints,
            Mana = Math.Min(character.Mana, projectedMaximumMana),
            MaximumMana = projectedMaximumMana,
            SemanticState = semantic with
            {
                DerivedStats = derived.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
                Resistances = resistances.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
                SavingThrows = saves,
                RuntimeMarkers = markers
            },
            UpdatedAtUtc = projectedAtUtc ?? DateTimeOffset.UtcNow
        };
    }

    public static RuntimeCharacterCombatStatisticProjection Snapshot(RuntimeCharacterRecord character)
    {
        var semantic = character.SemanticState ?? RuntimeCharacterSemanticState.Empty;
        var sources = semantic.RuntimeMarkers.TryGetValue(ProjectionMarker, out var marker)
            ? marker.Split('|').LastOrDefault()?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? Array.Empty<string>()
            : Array.Empty<string>();
        return new RuntimeCharacterCombatStatisticProjection(
            new Dictionary<string, int>(semantic.DerivedStats, StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, int>(semantic.Resistances, StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, int>(semantic.SavingThrows, StringComparer.OrdinalIgnoreCase),
            semantic.DerivedStats.TryGetValue("hit_point_regeneration_percent", out var hpRegen) ? hpRegen : 0,
            semantic.DerivedStats.TryGetValue("mana_regeneration_percent", out var manaRegen) ? manaRegen : 0,
            sources,
            EvidenceStronglyCorrelated,
            FormulaId);
    }

    public static void Validate(RuntimeCharacterSemanticState semantic, int saveVersion)
    {
        ValidateMap(semantic.DerivedStats, "derived statistic");
        ValidateMap(semantic.Resistances, "resistance");
        ValidateMap(semantic.SavingThrows, "saving throw");
        if (!semantic.RuntimeMarkers.ContainsKey(ProjectionMarker)) return;
        foreach (var key in CanonicalDerivedKeys.Concat(new[] { "hit_point_regeneration_percent", "mana_regeneration_percent" }))
        {
            if (!semantic.DerivedStats.ContainsKey(key))
                throw new InvalidDataException($"Character statistic projection is missing canonical key '{key}'.");
        }
        foreach (var key in new[] { "cold", "fire", "stone", "lightning", "water" })
        {
            if (!semantic.Resistances.ContainsKey(key))
                throw new InvalidDataException($"Character statistic projection is missing canonical resistance '{key}'.");
        }
        if (!semantic.RuntimeMarkers.TryGetValue(ProjectionMarker, out var marker) || string.IsNullOrWhiteSpace(marker))
            throw new InvalidDataException("Character statistic projection marker is missing.");
        if (!marker.Contains(FormulaId, StringComparison.Ordinal))
            throw new InvalidDataException("Character statistic projection marker has an invalid formula id.");
    }

    private static IReadOnlyDictionary<string, int> NormalizeCanonicalMap(IReadOnlyDictionary<string, int> source, string label)
    {
        ValidateMap(source, label);
        return source
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(pair => pair.Key.Trim().ToLowerInvariant(), pair => pair.Value, StringComparer.OrdinalIgnoreCase);
    }

    private static void ValidateMap(IReadOnlyDictionary<string, int> source, string label)
    {
        foreach (var pair in source)
        {
            if (string.IsNullOrWhiteSpace(pair.Key)) throw new InvalidDataException($"Character {label} contains an empty canonical id.");
        }
    }
}


/// <summary>
/// Resolves the decoded spellcasting type table from character_foundation.json.
/// This is the single character-side family identity used by spell eligibility and
/// by live casting consumers; it does not invent class-name tables.
/// </summary>
public static class RuntimeCharacterSpellcastingTypeResolver
{
    public static string Resolve(RuntimeCharacterDefinition characterClass, RuntimeCharacterDerivedFormulaContract contract)
    {
        var type = characterClass.Magic?.Type ?? 0;
        if (type == 0) return string.Empty;
        if (!contract.SpellcastingTypes.TryGetValue(type, out var family) || string.IsNullOrWhiteSpace(family))
            throw new InvalidDataException($"Decoded spellcasting type {type} for class '{characterClass.Id}' has no family mapping.");
        return family;
    }
}
