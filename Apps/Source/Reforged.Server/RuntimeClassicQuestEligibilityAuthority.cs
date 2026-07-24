using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeClassicQuestEligibilityState(
    int CharacterLevel,
    int CompletedStage,
    string CharacterClass,
    string CharacterRace,
    string CharacterAlignment,
    IReadOnlySet<int> AbilityIds,
    IReadOnlyDictionary<int, int> InventoryCounts);

public sealed record RuntimeClassicQuestEligibilityDecision(
    bool Eligible,
    bool AlreadyCompleted,
    int RequestedStage,
    int? RequiredLevel,
    string? Failure);

/// <summary>
/// Canonical preflight authority for decoded quest roots. It reads Bundle A/C state but does not
/// own or mutate it. Root stage levels are loaded from decoded quest_narrative_roots.json; detailed
/// class, race, alignment, ability, and inventory predicates remain decoded textblock operations
/// and are evaluated by the existing Bundle F condition binding during the same execution plan.
/// </summary>
public sealed class RuntimeClassicQuestEligibilityAuthority
{
    private readonly RuntimeClassicQuestNarrativeAuthority _quests;
    public RuntimeClassicQuestEligibilityAuthority(RuntimeClassicQuestNarrativeAuthority quests) =>
        _quests = quests ?? throw new ArgumentNullException(nameof(quests));

    public RuntimeClassicQuestEligibilityDecision Evaluate(int rootTextblockId, RuntimeClassicQuestEligibilityState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var root = _quests.Require(rootTextblockId);
        if (state.CharacterLevel < 0 || state.CompletedStage < 0)
            return new(false, false, state.CompletedStage + 1, null, "Character level and quest stage must be non-negative authoritative values.");

        // Roots without a decoded sequential level table (for example Jungle Gnome) defer their
        // eligibility entirely to decoded condition operations in the selected textblock branch.
        if (root.StageLevels.Count == 0)
            return new(true, false, state.CompletedStage + 1, null, null);

        if (state.CompletedStage >= root.StageLevels.Count)
            return new(false, true, root.StageLevels.Count, root.StageLevels[^1], "This decoded quest chain is already complete; duplicate completion is not permitted.");

        var requestedStage = state.CompletedStage + 1;
        var requiredLevel = root.StageLevels[state.CompletedStage];
        if (state.CharacterLevel < requiredLevel)
            return new(false, false, requestedStage, requiredLevel, $"Quest stage {requestedStage} requires level {requiredLevel}.");

        return new(true, false, requestedStage, requiredLevel, null);
    }

    public RuntimeClassicNarrativeExecutionResult Execute(
        RuntimeClassicTextblockGraphAuthority graph,
        RuntimeClassicNarrativeExecutionCoordinator coordinator,
        int rootTextblockId,
        string playerInput,
        RuntimeClassicQuestEligibilityState state,
        RuntimeClassicNarrativeExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(coordinator);
        ArgumentNullException.ThrowIfNull(context);
        if (context.RootTextblockId != rootTextblockId)
            throw new InvalidOperationException("Quest execution context root does not match requested decoded quest root.");

        var decision = Evaluate(rootTextblockId, state);
        if (!decision.Eligible)
            return new(false, rootTextblockId, rootTextblockId, false, false, false,
                Array.Empty<RuntimeClassicNarrativeOperationResult>(), Array.Empty<string>(), decision.Failure);

        var plan = _quests.CreatePlan(graph, rootTextblockId, playerInput);
        return coordinator.Execute(plan, context);
    }
}

public sealed record RuntimeBundleFPhaseF5EligibilityAuditReport(
    bool Passed,
    int RootCount,
    int GateCount,
    int DisconnectCount,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

public static class RuntimeBundleFPhaseF5EligibilityAudit
{
    public static RuntimeBundleFPhaseF5EligibilityAuditReport Run(string projectRoot)
    {
        var checks = new List<string>();
        var failures = new List<string>();
        void Check(bool value, string name) { if (value) checks.Add(name); else failures.Add(name); }

        var roots = RuntimeClassicQuestNarrativeAuthority.Load(projectRoot);
        var authority = new RuntimeClassicQuestEligibilityAuthority(roots);
        var emptyAbilities = (IReadOnlySet<int>)new HashSet<int>();
        var emptyInventory = (IReadOnlyDictionary<int, int>)new Dictionary<int, int>();
        RuntimeClassicQuestEligibilityState State(int level, int stage) => new(level, stage, "audit", "audit", "audit", emptyAbilities, emptyInventory);

        foreach (var rootId in new[] { 335, 366, 428 })
        {
            for (var stage = 0; stage < 5; stage++)
            {
                var required = new[] { 10, 20, 30, 40, 50 }[stage];
                var below = authority.Evaluate(rootId, State(required - 1, stage));
                var at = authority.Evaluate(rootId, State(required, stage));
                Check(!below.Eligible && below.RequiredLevel == required, $"root-{rootId}-stage-{stage + 1}-rejects-below-level-{required}");
                Check(at.Eligible && at.RequestedStage == stage + 1, $"root-{rootId}-stage-{stage + 1}-accepts-level-{required}");
            }
            var complete = authority.Evaluate(rootId, State(100, 5));
            Check(!complete.Eligible && complete.AlreadyCompleted, $"root-{rootId}-duplicate-completion-rejected");
        }

        var jungle = authority.Evaluate(2798, State(1, 0));
        Check(jungle.Eligible && jungle.RequiredLevel is null, "jungle-gnome-does-not-invent-receipt-level-gate");
        Check(roots.Require(2798).StageLevels.Count == 0, "jungle-gnome-detailed-class-item-gates-remain-decoded-branch-conditions");

        return new(failures.Count == 0, roots.Count, 15, failures.Count, checks, failures);
    }
}
