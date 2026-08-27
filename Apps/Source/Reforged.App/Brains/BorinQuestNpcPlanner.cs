namespace Reforged.App.Brains;

public sealed record BorinQuestNpcPlan(
    string Phase,
    IReadOnlyList<string> Priorities,
    IReadOnlyList<string> Evidence,
    string Detail)
{
    public string PromptText
    {
        get
        {
            var priorities = Priorities.Count == 0
                ? "- No quest/NPC priority is currently available."
                : string.Join("\n", Priorities.Select((value, index) => $"{index + 1}. {value}"));
            var evidence = Evidence.Count == 0
                ? "- No quest/NPC-specific evidence is currently available."
                : string.Join("\n", Evidence.Select(value => $"- {value}"));
            return $"Quest/NPC phase: {Phase}\n{Detail}\nPriorities:\n{priorities}\nSupporting evidence:\n{evidence}";
        }
    }

    public static BorinQuestNpcPlan Disabled() => new(
        "disabled",
        Array.Empty<string>(),
        Array.Empty<string>(),
        "Autonomous quest and NPC interaction planning is disabled.");
}

/// <summary>
/// Deterministic policy layer for Borin's ordinary-player quest and NPC decisions.
/// The planner never sends commands and never promotes a Librarium quest-flow relation
/// into live quest state. It favors reversible observation/dialogue first and requires
/// stronger evidence before item transfers or other irreversible quest actions.
/// </summary>
public static class BorinQuestNpcPlanner
{
    public static BorinQuestNpcPlan Build(
        BorinGameStateSnapshot state,
        BorinGameplayLearningSnapshot personalExperience,
        BorinLibrariumPlayerGuidance librarium)
    {
        var priorities = new List<string>();
        var evidence = new List<string>();
        var phase = ResolvePhase(state, librarium);

        if (!state.HasVerifiedPrompt || !state.HasVerifiedRoom)
            priorities.Add("Re-establish live room/prompt perception before pursuing an NPC or quest step. Do not use a remembered NPC location as proof that the NPC is present now.");

        if (state.InCombat)
            priorities.Add("Resolve the current combat/survival state before routine NPC dialogue or quest hand-ins.");
        else if (state.HealthPercent > 0 && state.HealthPercent <= 30)
            priorities.Add("Recover or reach safety before committing to a quest interaction that could trigger travel, combat, or item loss.");

        var npcReferences = librarium.Evidence
            .Where(value => value.Contains("NPC SPAWN REFERENCE FOR CURRENT ROOM", StringComparison.OrdinalIgnoreCase))
            .Take(4)
            .ToArray();
        foreach (var item in npcReferences)
            evidence.Add($"LIBRARIUM NPC • {item}");

        var questFlows = librarium.Evidence
            .Where(value => value.Contains("QUEST/NPC FLOW CANDIDATE", StringComparison.OrdinalIgnoreCase))
            .Take(4)
            .ToArray();
        foreach (var item in questFlows)
            evidence.Add($"LIBRARIUM FLOW • {item}");

        var questItems = librarium.Evidence
            .Where(value => value.Contains("QUEST ITEM CLUE", StringComparison.OrdinalIgnoreCase))
            .Take(4)
            .ToArray();
        foreach (var item in questItems)
            evidence.Add($"LIBRARIUM ITEM • {item}");

        var personalQuestLessons = personalExperience.RelevantLessons
            .Where(value => value.Contains("quest", StringComparison.OrdinalIgnoreCase) ||
                            value.Contains("npc", StringComparison.OrdinalIgnoreCase) ||
                            value.Contains("talk", StringComparison.OrdinalIgnoreCase) ||
                            value.Contains("keyword", StringComparison.OrdinalIgnoreCase) ||
                            value.Contains("give", StringComparison.OrdinalIgnoreCase) ||
                            value.Contains("item", StringComparison.OrdinalIgnoreCase))
            .Take(4)
            .ToArray();
        foreach (var lesson in personalQuestLessons)
            evidence.Add($"PERSONAL • {lesson}");

        if (!state.InCombat && state.HasVerifiedPrompt && state.HasVerifiedRoom)
        {
            if (npcReferences.Length > 0)
                priorities.Add("Use live LOOK/EXAMINE/TALK evidence to verify which referenced NPCs are actually present before treating any Librarium NPC/quest relationship as actionable.");

            if (questFlows.Length > 0)
                priorities.Add("When a decoded quest-flow keyword is relevant, first establish the live NPC/dialogue context; then try only one grounded dialogue step at a time and evaluate the server response before continuing.");

            if (questItems.Length > 0)
                priorities.Add("Preserve inventory items that have quest/gate clues. Do not GIVE, DROP, PUT, SELL, or USE one merely because Librarium associates it with a quest candidate.");

            if (npcReferences.Length == 0 && questFlows.Length == 0 && questItems.Length == 0)
                priorities.Add("If progression would benefit from questing, gather reversible evidence with LOOK, READ, EXAMINE, TALK, room information, and Librarium references instead of inventing a quest giver or objective.");
        }

        if (state.InventoryFreshness.Contains("not", StringComparison.OrdinalIgnoreCase) ||
            state.InventoryFreshness.Contains("stale", StringComparison.OrdinalIgnoreCase))
        {
            priorities.Add("Refresh INVENTORY before any proposed quest hand-in or item-use step so ownership is live-verified.");
        }

        priorities.Add("After every dialogue keyword, item transfer, objective completion, or quest-state-looking server response, stop and re-read the live output before selecting the next step; do not chain an unverified walkthrough sequence.");
        priorities.Add("Treat decoded Librarium quest flows as game-world reference evidence, not as proof of Borin's current quest stage. Live server output and verified personal outcomes decide what actually happened.");

        return new BorinQuestNpcPlan(
            phase,
            priorities.Distinct(StringComparer.OrdinalIgnoreCase).Take(8).ToArray(),
            evidence.Distinct(StringComparer.OrdinalIgnoreCase).Take(12).ToArray(),
            "Questing is autonomous but evidence-gated. Borin may discover, converse, investigate, and pursue quests by himself, while irreversible item/state changes require a live-context reason and post-action verification.");
    }

    private static string ResolvePhase(BorinGameStateSnapshot state, BorinLibrariumPlayerGuidance librarium)
    {
        if (!state.HasVerifiedPrompt || !state.HasVerifiedRoom) return "re-establish live quest context";
        if (state.InCombat) return "survival before dialogue";
        if (state.HealthPercent > 0 && state.HealthPercent <= 30) return "recover before quest commitment";
        if (librarium.Evidence.Any(value => value.Contains("QUEST/NPC FLOW CANDIDATE", StringComparison.OrdinalIgnoreCase)))
            return "verify NPC and advance one quest step";
        if (librarium.Evidence.Any(value => value.Contains("QUEST ITEM CLUE", StringComparison.OrdinalIgnoreCase)))
            return "protect and investigate possible quest item";
        if (librarium.Evidence.Any(value => value.Contains("NPC SPAWN REFERENCE FOR CURRENT ROOM", StringComparison.OrdinalIgnoreCase)))
            return "verify and interact with local NPC references";
        return "discover quest opportunities cautiously";
    }
}
