namespace Reforged.Server;

/// <summary>
/// MajorMUD character-side formula authority recovered from retained MMUD Explorer/native evidence.
/// These formulas are intentionally separate from the mechanics guide and from client-only calculators.
/// </summary>
public static class RuntimeClassicCharacterFormulaCalculator
{
    public const string MagicResistanceFormulaId = "character.magic_resistance.mmud_explorer_0279";
    public const string RawDodgeFormulaId = "character.raw_dodge.mmud_explorer_0279";

    private const int DodgeAbilityId = 34;
    private const int MagicResistanceAbilityId = 36;

    /// <summary>
    /// MMUD Explorer CalcMR: Fix((INT + (WIL * 3)) / 4) + modifiers.
    /// Integer inputs use C# truncating division, matching VB Fix for this non-negative stat term.
    /// </summary>
    public static int CalculateMagicResistance(int intellect, int willpower, int modifiers = 0)
    {
        if (intellect < 0) throw new ArgumentOutOfRangeException(nameof(intellect));
        if (willpower < 0) throw new ArgumentOutOfRangeException(nameof(willpower));
        return checked((intellect + checked(willpower * 3)) / 4 + modifiers);
    }

    /// <summary>
    /// Stock character raw Dodge builder recovered from MMUD Explorer CalcDodge.
    /// The light-encumbrance bonus applies only below 33 percent encumbrance.
    /// Do not clamp the raw value here; the downstream stock avoidance calculation owns its cap/floor behavior.
    /// </summary>
    public static int CalculateRawDodge(
        int level,
        int agility,
        int charm,
        int plusDodge,
        int encumbrancePercent)
    {
        if (level < 0) throw new ArgumentOutOfRangeException(nameof(level));
        if (agility < 0) throw new ArgumentOutOfRangeException(nameof(agility));
        if (charm < 0) throw new ArgumentOutOfRangeException(nameof(charm));
        if (encumbrancePercent < 0) throw new ArgumentOutOfRangeException(nameof(encumbrancePercent));

        var dodge = level / 5;
        dodge = checked(dodge + ((charm - 50) / 5));
        dodge = checked(dodge + ((agility - 50) / 3));
        dodge = checked(dodge + plusDodge);
        if (encumbrancePercent < 33)
            dodge = checked(dodge + 10 - (encumbrancePercent / 10));
        return dodge;
    }

    public static int ResolveMagicResistance(RuntimeCharacterRecord character)
    {
        ArgumentNullException.ThrowIfNull(character);
        var total = CalculateMagicResistance(
            ReadAdjustedStat(character, "intellect", "int", "intelligence"),
            ReadAdjustedStat(character, "wisdom", "willpower", "wil", "will"),
            ResolveMagicResistanceModifier(character));
        return Math.Max(0, total);
    }

    public static int ResolveMagicResistanceModifier(RuntimeCharacterRecord character)
    {
        ArgumentNullException.ThrowIfNull(character);
        var legacyModifier = 0;
        if (character.SemanticState?.Resistances is { } resistances)
            foreach (var key in new[] { "magic", "magicres", "magic_resistance" })
                if (resistances.TryGetValue(key, out var value))
                {
                    legacyModifier = value;
                    break;
                }

        var questValues = RuntimeCharacterAbilityValues.Restore(character.Conditions);
        var questModifier = RuntimeCharacterAbilityValues.Read(questValues, MagicResistanceAbilityId);
        var equipmentModifier = ResolveProjectedEquipmentContribution(character, "magic_resistance_bonus", questModifier);
        var statusModifier = RuntimeClassicStatusProjection.Numeric(character, "magic_resistance");
        return checked(legacyModifier + questModifier + equipmentModifier + statusModifier);
    }

    public static int ResolveRawDodge(RuntimeCharacterRecord character, int encumbrancePercent)
    {
        ArgumentNullException.ThrowIfNull(character);
        var questValues = RuntimeCharacterAbilityValues.Restore(character.Conditions);
        var questModifier = RuntimeCharacterAbilityValues.Read(questValues, DodgeAbilityId);
        var equipmentModifier = ResolveProjectedEquipmentContribution(character, "dodge_bonus", questModifier);
        var statusModifier = RuntimeClassicStatusProjection.Numeric(character, "dodge");
        return CalculateRawDodge(
            character.Level,
            ReadAdjustedStat(character, "agility", "agi", "agl"),
            ReadAdjustedStat(character, "charm", "cha"),
            checked(questModifier + equipmentModifier + statusModifier),
            encumbrancePercent);
    }

    /// <summary>
    /// The 0155 projector stores quest/acquired ability values plus equipment ability values in a single
    /// derived key. When its projection marker is present, subtract the quest portion so consumers do not
    /// add it twice. Older/unmarked semantic snapshots are treated as equipment-only compatibility values.
    /// </summary>
    public static int ResolveProjectedEquipmentContribution(RuntimeCharacterRecord character, string derivedKey, int questContribution)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentException.ThrowIfNullOrWhiteSpace(derivedKey);
        var semantic = character.SemanticState;
        if (semantic is null || !semantic.DerivedStats.TryGetValue(derivedKey, out var projected)) return 0;
        var isAuthoritativeProjection = semantic.RuntimeMarkers.ContainsKey(RuntimeCharacterCombatStatisticProjector.ProjectionMarker);
        return isAuthoritativeProjection ? checked(projected - questContribution) : projected;
    }

    private static int ReadAdjustedStat(RuntimeCharacterRecord character, params string[] keys)
    {
        var value = 0;
        var matched = string.Empty;
        foreach (var key in keys)
            if (character.BaseStats.TryGetValue(key, out value))
            {
                matched = key;
                break;
            }

        var canonical = matched.ToLowerInvariant() switch
        {
            "int" or "intelligence" => "intellect",
            "wil" or "will" or "willpower" => "wisdom",
            "agi" or "agl" => "agility",
            "cha" => "charm",
            var key => key
        };
        return Math.Max(0, checked(value + RuntimeClassicStatusProjection.Numeric(character, "stat_" + canonical)));
    }
}

public sealed record RuntimeClassicCharacterFormulaAuditReport(
    bool Passed,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Failures);

public static class RuntimeClassicCharacterFormulaAudit
{
    public static RuntimeClassicCharacterFormulaAuditReport Run()
    {
        var checks = new List<string>();
        var failures = new List<string>();
        void Check(bool condition, string name)
        {
            checks.Add(name);
            if (!condition) failures.Add(name);
        }

        Check(RuntimeClassicCharacterFormulaCalculator.CalculateMagicResistance(60, 40, 7) == 52,
            "magic-resistance-int-plus-three-will-over-four");
        Check(RuntimeClassicCharacterFormulaCalculator.CalculateMagicResistance(40, 60, -5) == 50,
            "magic-resistance-signed-modifier-order");
        Check(RuntimeClassicCharacterFormulaCalculator.CalculateRawDodge(20, 80, 60, 5, 32) == 28,
            "raw-dodge-light-encumbrance-vector");
        Check(RuntimeClassicCharacterFormulaCalculator.CalculateRawDodge(20, 80, 60, 5, 33) == 21,
            "raw-dodge-33-percent-boundary");
        Check(RuntimeClassicCharacterFormulaCalculator.CalculateRawDodge(1, 50, 50, 0, 0) == 10,
            "raw-dodge-level-one-neutral-vector");

        Check(RuntimeClassicCharacterProgressionFormula.RequiredExperienceForChart(1, 100) == 0,
            "stock-xp-level-one-zero");
        Check(RuntimeClassicCharacterProgressionFormula.RequiredExperienceForChart(2, 100) == 1_000,
            "stock-xp-level-two-chart-times-ten");
        Check(RuntimeClassicCharacterProgressionFormula.RequiredExperienceForChart(3, 100) == 2_000,
            "stock-xp-level-three-vector");
        Check(RuntimeClassicCharacterProgressionFormula.RequiredExperienceForChart(10, 100) == 81_123,
            "stock-xp-level-ten-vector");
        Check(RuntimeClassicCharacterProgressionFormula.RequiredExperienceForChart(50, 290) == 783_554_100,
            "stock-xp-level-fifty-chart-290-vector");
        Check(RuntimeClassicCharacterProgressionFormula.RequiredExperienceForChart(100, 290) == 51_766_413_104L,
            "stock-xp-uint-rollover-vector");

        Check(RuntimeClassicCharacterProgressionFormula.CharacterPointAwardForPromotion(2) == 10,
            "stock-cp-level-two-award");
        Check(RuntimeClassicCharacterProgressionFormula.CharacterPointAwardForPromotion(10) == 10,
            "stock-cp-level-ten-award");
        Check(RuntimeClassicCharacterProgressionFormula.CharacterPointAwardForPromotion(11) == 15,
            "stock-cp-level-eleven-award");
        Check(RuntimeClassicCharacterProgressionFormula.CharacterPointAwardForPromotion(21) == 20,
            "stock-cp-level-twenty-one-award");
        Check(RuntimeClassicCharacterProgressionFormula.TotalCharacterPointsEarnedAtLevel(11) == 105,
            "stock-cp-cumulative-level-eleven");

        Check(RuntimeClassicCharacterProgressionFormula.PicklocksBase(1, 50, 50) == 31,
            "stock-picklocks-level-one-vector");
        Check(RuntimeClassicCharacterProgressionFormula.PicklocksBase(15, 70, 70) == 82,
            "stock-picklocks-level-fifteen-vector");
        Check(RuntimeClassicCharacterProgressionFormula.PicklocksBase(16, 70, 70) == 82,
            "stock-picklocks-post-fifteen-half-level-boundary");
        Check(RuntimeClassicCharacterProgressionFormula.PicklocksBase(17, 70, 70) == 85,
            "stock-picklocks-level-seventeen-vector");
        Check(RuntimeClassicCharacterProgressionFormula.MaximumLives == 9,
            "stock-life-cap-nine");

        return new(failures.Count == 0, checks, failures);
    }
}
