namespace Reforged.Server;

/// <summary>
/// Stock MajorMUD character-progression formulas recovered from the stock-only MMUD Explorer branch.
/// These formulas intentionally exclude GreaterMUD branches.
/// </summary>
public static class RuntimeClassicCharacterProgressionFormula
{
    public const string ExperienceThresholdFormulaId = "character.stock_experience_threshold";
    public const string CharacterPointAwardFormulaId = "character.stock_cp_award";
    public const string PicklocksFormulaId = "character.stock_picklocks";
    public const int PicklocksGrantAbilityId = 37;
    public const int MaximumLives = 9;

    private const long MaxUInt32 = 4_294_967_295L;
    private const long Billion = 1_000_000_000L;

    public static int ExperienceChartPercent(RuntimeCharacterDefinition race, RuntimeCharacterDefinition characterClass)
    {
        ArgumentNullException.ThrowIfNull(race);
        ArgumentNullException.ThrowIfNull(characterClass);
        var classChart = checked(characterClass.ExperienceStored + 100);
        if (characterClass.ExperiencePercent != 0 && characterClass.ExperiencePercent != classChart)
            throw new InvalidDataException($"Class '{characterClass.Id}' experience percent does not match decoded ExpTable+100 semantics.");
        var chart = checked(classChart + race.ExperienceAddPercent);
        if (chart <= 0) throw new InvalidDataException($"Race/class experience chart must be positive; got {chart}.");
        return chart;
    }

    public static long RequiredExperience(int level, RuntimeCharacterDefinition race, RuntimeCharacterDefinition characterClass)
        => RequiredExperienceForChart(level, ExperienceChartPercent(race, characterClass));

    public static long RequiredExperienceForChart(int level, int chart)
    {
        if (level < 1) throw new ArgumentOutOfRangeException(nameof(level));
        if (chart <= 0) throw new ArgumentOutOfRangeException(nameof(chart));

        long running = 0;
        long billions = 0;
        long lastExperience = 0;

        for (var i = 1; i <= level; i++)
        {
            if (i == 1)
            {
                running = 0;
            }
            else if (i == 2)
            {
                running = checked((long)chart * 10L);
            }
            else
            {
                var (multiplier, divisor) = ExperienceModifier(i);
                var potential = checked(running * multiplier);
                long alternate;
                if (potential > MaxUInt32)
                {
                    var divides = 0;
                    while (potential > MaxUInt32)
                    {
                        running /= 100L;
                        potential = checked(running * multiplier);
                        divides++;
                    }

                    alternate = divides > 1
                        ? checked((running * multiplier * 100L) / divisor)
                        : potential / divisor;
                    while (divides-- > 0) alternate = checked(alternate * 100L);
                }
                else
                {
                    alternate = potential / divisor;
                }

                var j = checked(1_000_000L * multiplier * billions);
                while (j > MaxUInt32) j -= MaxUInt32 + 1L;
                while (j >= Billion)
                {
                    j -= Billion;
                    billions++;
                }

                var k = checked(j + alternate);
                while (k >= Billion)
                {
                    k -= Billion;
                    billions++;
                }
                running = k;
            }

            lastExperience = checked(running + checked(billions * Billion));
        }

        return lastExperience;
    }

    public static int CharacterPointAwardForPromotion(int targetLevel)
    {
        if (targetLevel <= 1) return 0;
        var i = targetLevel - 1;
        return checked((i / 10) * 5 + 10);
    }

    public static int TotalCharacterPointsEarnedAtLevel(int level)
    {
        if (level <= 1) return 0;
        var total = 0;
        for (var target = 2; target <= level; target++)
            total = checked(total + CharacterPointAwardForPromotion(target));
        return total;
    }

    public static int PicklocksBase(int level, int agility, int intellect)
    {
        if (level < 0) throw new ArgumentOutOfRangeException(nameof(level));
        var levelComponent = level <= 15
            ? checked(level * 2)
            : checked((((level - 15) / 2) + 15) * 2);
        return checked((((levelComponent * 5) + checked(agility + intellect)) * 2) / 7);
    }

    public static bool HasPicklocksGrant(RuntimeCharacterRecord character)
    {
        ArgumentNullException.ThrowIfNull(character);
        return character.Skills.Keys.Any(key => LegacyAbilityId(key) == PicklocksGrantAbilityId)
            || character.Skills.ContainsKey("picklocks")
            || character.Skills.ContainsKey("picklock");
    }

    public static int PicklocksValue(RuntimeCharacterRecord character)
    {
        ArgumentNullException.ThrowIfNull(character);
        if (!HasPicklocksGrant(character)) return 0;

        // Preserve explicit imported Picklocks values when present; old external character imports may
        // not carry the original ability.37 grant key. Native-created characters use the stock formula.
        if (!character.Skills.Keys.Any(key => LegacyAbilityId(key) == PicklocksGrantAbilityId))
        {
            if (character.Skills.TryGetValue("picklocks", out var imported)) return imported;
            if (character.Skills.TryGetValue("picklock", out imported)) return imported;
        }

        var agility = ReadStat(character.BaseStats, "agility", "agi", "agl");
        var intellect = ReadStat(character.BaseStats, "intellect", "intelligence", "int");
        var value = PicklocksBase(character.Level, agility, intellect);
        var acquiredValues = RuntimeCharacterAbilityValues.Restore(character.Conditions);
        var acquiredBonus = RuntimeCharacterAbilityValues.Read(acquiredValues, 180);
        var equipmentBonus = RuntimeClassicCharacterFormulaCalculator.ResolveProjectedEquipmentContribution(
            character, "picklocks_bonus", acquiredBonus);
        value = checked(value + acquiredBonus + equipmentBonus);
        value = checked(value + RuntimeClassicStatusProjection.Numeric(character, "picklocks"));
        return value;
    }

    public static int ReadLives(RuntimeCharacterRecord character)
    {
        ArgumentNullException.ThrowIfNull(character);
        if (character.Conditions.TryGetValue("lives", out var raw) && int.TryParse(raw, out var lives))
            return Math.Clamp(lives, 0, MaximumLives);
        if (character.Conditions.TryGetValue("lives_remaining", out raw) && int.TryParse(raw, out lives))
            return Math.Clamp(lives, 0, MaximumLives);
        // Earlier Reforged saves predate the live lives field. Classic starts at the proven cap of 9.
        return MaximumLives;
    }

    private static (int Multiplier, int Divisor) ExperienceModifier(int level)
    {
        if (level <= 27)
        {
            return level switch
            {
                3 => (40, 20),
                4 or 5 => (44, 24),
                6 or 7 => (48, 28),
                8 or 9 => (52, 32),
                10 or 11 => (56, 36),
                12 or 13 => (60, 40),
                14 or 15 => (65, 45),
                16 or 17 => (70, 50),
                18 => (75, 55),
                <= 26 => (50, 40),
                _ => (23, 20)
            };
        }
        if (level <= 55) return (115, 100);
        if (level <= 58) return (109, 100);
        return (108, 100);
    }

    private static int ReadStat(IReadOnlyDictionary<string, int> stats, params string[] names)
    {
        foreach (var name in names) if (stats.TryGetValue(name, out var value)) return value;
        return 0;
    }

    private static int LegacyAbilityId(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return 0;
        var normalized = key.Trim();
        var separator = normalized.LastIndexOf('.');
        if (separator >= 0) normalized = normalized[(separator + 1)..];
        return int.TryParse(normalized, out var parsed) ? parsed : 0;
    }
}
