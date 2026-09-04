namespace RealmGate.App.Brains;

public sealed record BorinProgressionPlan(
    string Phase,
    IReadOnlyList<string> Priorities,
    IReadOnlyList<string> Evidence,
    string Detail)
{
    public string PromptText
    {
        get
        {
            var priorities = Priorities.Count == 0 ? "- No progression priority is currently available." : string.Join("\n", Priorities.Select((value, index) => $"{index + 1}. {value}"));
            var evidence = Evidence.Count == 0 ? "- No progression-specific evidence is currently available." : string.Join("\n", Evidence.Select(value => $"- {value}"));
            return $"Progression phase: {Phase}\n{Detail}\nPriorities:\n{priorities}\nSupporting evidence:\n{evidence}";
        }
    }

    public static BorinProgressionPlan Disabled() => new(
        "disabled",
        Array.Empty<string>(),
        Array.Empty<string>(),
        "Autonomous progression planning is disabled.");
}

/// <summary>
/// Deterministic strategic layer for Borin's autonomous player loop. It does not send
/// commands or invent routes. It ranks what Borin should accomplish next from live state,
/// verified personal experience, structured Librarium guidance, and revisable reflections.
/// </summary>
public static class BorinProgressionPlanner
{
    public static BorinProgressionPlan Build(
        BorinGameStateSnapshot state,
        BorinGameplayLearningSnapshot personalExperience,
        BorinLibrariumPlayerGuidance librarium,
        string reflectionGuidance)
    {
        var priorities = new List<string>();
        var evidence = new List<string>();
        var phase = ResolvePhase(state);

        if (!state.HasVerifiedPrompt || !state.HasVerifiedRoom)
        {
            priorities.Add("Re-establish verified live perception before making a progression move. Use ordinary information commands rather than guessing.");
        }

        if (state.CharacterLevel <= 0 || state.ExperienceCurrent < 0)
        {
            priorities.Add("Establish current character progression state with normal player status/experience commands before selecting a leveling target.");
        }

        if (state.InCombat)
        {
            priorities.Add("Resolve the current combat first. Preserve survival; continue, heal, use resources, or flee based on the live combat state.");
        }
        else if (state.HealthPercent > 0 && state.HealthPercent <= 30)
        {
            priorities.Add("Recover health before committing to another progression fight unless remaining in place is more dangerous than moving.");
        }

        var personalLeveling = personalExperience.RelevantLessons
            .Where(value => value.Contains("leveling", StringComparison.OrdinalIgnoreCase) ||
                            value.Contains("experience", StringComparison.OrdinalIgnoreCase) ||
                            value.Contains("danger", StringComparison.OrdinalIgnoreCase) ||
                            value.Contains("recovery", StringComparison.OrdinalIgnoreCase))
            .Take(4)
            .ToArray();
        foreach (var lesson in personalLeveling)
            evidence.Add($"PERSONAL • {lesson}");

        var candidates = librarium.Evidence
            .Where(value => value.Contains("LEVELING/SCOUTING CANDIDATE", StringComparison.OrdinalIgnoreCase))
            .Take(4)
            .ToArray();
        foreach (var candidate in candidates)
            evidence.Add($"LIBRARIUM • {candidate}");

        var questOrUpgrade = librarium.Evidence
            .Where(value => value.Contains("quest", StringComparison.OrdinalIgnoreCase) ||
                            value.Contains("items", StringComparison.OrdinalIgnoreCase) ||
                            value.Contains("shops", StringComparison.OrdinalIgnoreCase) ||
                            value.Contains("spells", StringComparison.OrdinalIgnoreCase) ||
                            value.Contains("classes", StringComparison.OrdinalIgnoreCase))
            .Take(3)
            .ToArray();
        foreach (var item in questOrUpgrade)
            evidence.Add($"REFERENCE • {item}");

        if (!state.InCombat && state.HealthPercent > 30)
        {
            if (personalLeveling.Length > 0)
                priorities.Add("Prefer a previously verified productive and survivable leveling pattern for this character when it still fits the current level and live conditions.");
            else if (candidates.Length > 0)
                priorities.Add("Scout the strongest conservative Librarium leveling candidate and verify its route, spawn, combat difficulty, and XP through normal play before committing to a grind.");
            else
                priorities.Add("Gather enough live and Librarium evidence to identify a conservative leveling area instead of wandering or grinding an unverified target indefinitely.");
        }

        if (state.EquippedItems.Count == 0)
            priorities.Add("Verify equipment state and correct obvious missing gear before judging combat efficiency.");
        else if (questOrUpgrade.Length > 0)
            priorities.Add("Compare plausible quest, shop, spell, or equipment opportunities against the expected benefit of continued leveling; pursue an upgrade when it materially improves progression or survivability.");

        priorities.Add("Re-evaluate the plan after meaningful XP gain, a level-up, a death/near-death, a major equipment change, reaching a target area, or evidence that the current target is inefficient.");

        var reflectionLine = reflectionGuidance
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(value => value.StartsWith("- ", StringComparison.Ordinal));
        if (!string.IsNullOrWhiteSpace(reflectionLine))
            evidence.Add($"REFLECTION • {reflectionLine[2..]}");

        return new BorinProgressionPlan(
            phase,
            priorities.Distinct(StringComparer.OrdinalIgnoreCase).Take(7).ToArray(),
            evidence.Distinct(StringComparer.OrdinalIgnoreCase).Take(10).ToArray(),
            "This is a strategic priority stack, not permission to invent routes or game facts. Live server evidence controls the immediate action; verified personal experience may outweigh a theoretically attractive Librarium target for this character.");
    }

    private static string ResolvePhase(BorinGameStateSnapshot state)
    {
        if (!state.HasVerifiedPrompt || !state.HasVerifiedRoom) return "re-establish perception";
        if (state.InCombat) return "survive and resolve combat";
        if (state.HealthPercent > 0 && state.HealthPercent <= 30) return "recover and reassess";
        if (state.CharacterLevel <= 0) return "establish progression baseline";
        if (state.CharacterLevel <= 5) return "early leveling and basic equipment";
        if (state.CharacterLevel <= 15) return "leveling efficiency and core upgrades";
        if (state.CharacterLevel <= 30) return "midgame progression, quests, and equipment";
        return "advanced progression and optimization";
    }
}
