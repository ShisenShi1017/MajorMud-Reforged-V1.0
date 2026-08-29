namespace Reforged.Server;

public sealed record RuntimeClassicDeathExperienceResult(
    long ExperienceBefore,
    long ExperienceAfter,
    long ExperienceLost,
    string Classification);

/// <summary>
/// Native WCCMMUD death/reroll experience arithmetic recovered from the exported
/// _check_kill_user and _save_permanent_info routines.
/// </summary>
public static class RuntimeClassicDeathExperienceAuthority
{
    public const string OrdinaryDeathClassification = "native_normal_death_preserves_experience";
    public const string FinalRerollClassification = "native_final_reroll_retained_experience";
    public const bool FinalRerollConsumerConnected = true;
    public const string KeepExperienceConditionKey = "classic.keep_experience_after_final_death";

    public static bool IsPlayerKeepExperienceEnabled(RuntimeCharacterRecord character) =>
        !character.Conditions.TryGetValue(KeepExperienceConditionKey, out var raw) || !raw.Equals("0", StringComparison.OrdinalIgnoreCase);

    public static RuntimeClassicDeathExperienceResult PreserveOrdinaryDeathExperience(
        long currentExperience,
        int livesBeforeDeath)
    {
        if (currentExperience < 0) throw new ArgumentOutOfRangeException(nameof(currentExperience));
        if (livesBeforeDeath <= 1)
            throw new ArgumentOutOfRangeException(nameof(livesBeforeDeath), "Ordinary death requires at least one life after the death transition.");

        return new RuntimeClassicDeathExperienceResult(
            currentExperience,
            currentExperience,
            0,
            OrdinaryDeathClassification);
    }

    public static long CalculateFinalRerollRetainedExperience(
        long currentExperience,
        int currentLevel,
        int experienceChartPercent,
        bool playerKeepExperienceEnabled,
        bool globalKeepExperienceEnabled)
    {
        if (currentExperience < 0) throw new ArgumentOutOfRangeException(nameof(currentExperience));
        if (currentLevel < 1) throw new ArgumentOutOfRangeException(nameof(currentLevel));
        if (experienceChartPercent <= 0) throw new ArgumentOutOfRangeException(nameof(experienceChartPercent));
        if (!playerKeepExperienceEnabled || !globalKeepExperienceEnabled || currentLevel <= 1) return 0;

        var halfLevelFloor = currentLevel / 2;
        var halfLevelThreshold = checked(
            RuntimeClassicCharacterProgressionFormula.RequiredExperienceForChart(halfLevelFloor, experienceChartPercent) + 1);
        var tenPercentFloor = currentExperience / 10;
        return Math.Max(halfLevelThreshold, tenPercentFloor);
    }
}
