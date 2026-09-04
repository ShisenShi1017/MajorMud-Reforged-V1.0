namespace RealmGate.Server;

public enum RuntimeCombatEntityInvalidationReason
{
    Death,
    Defeat,
    Fled,
    Disconnected,
    Removed,
    TargetNoLongerValid
}

public sealed record RuntimeCombatActionDependency(
    string ActionId,
    string ActorId,
    IReadOnlyList<string> TargetIds,
    bool CancelOnActorInvalidation,
    bool CancelOnTargetInvalidation,
    IReadOnlyList<string> EvidenceReferences,
    RuntimeResolutionConfidence Confidence,
    string SafeFallback);

public sealed record RuntimeCombatEntityInvalidation(
    string CombatId,
    string EntityId,
    RuntimeCombatEntityInvalidationReason Reason,
    long WorldTick);

public sealed record RuntimeCombatActionCancellation(
    string ActionId,
    string InvalidEntityId,
    RuntimeCombatEntityInvalidationReason Reason,
    string CancellationReason,
    RuntimeCombatQueuedActionState PreviousState,
    RuntimeCombatQueuedActionState FinalState);

public sealed record RuntimeCombatActionInvalidationResult(
    RuntimeCombatEntityInvalidation Invalidation,
    IReadOnlyList<RuntimeCombatActionCancellation> Cancellations);

public sealed class RuntimeCombatActionInvalidationCoordinator
{
    private readonly RuntimeCombatActionQueue _queue;
    private readonly Dictionary<string, RuntimeCombatActionDependency> _dependencies = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _invalidEntities = new(StringComparer.OrdinalIgnoreCase);

    public RuntimeCombatActionInvalidationCoordinator(RuntimeCombatActionQueue queue)
    {
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
    }

    public IReadOnlyCollection<string> InvalidEntities => _invalidEntities.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();

    public void Register(RuntimeCombatActionDependency dependency)
    {
        ArgumentNullException.ThrowIfNull(dependency);
        RequireCanonical(dependency.ActionId, "action.", "COMBAT_ACTION_INVALIDATION_ACTION_ID_REQUIRED");
        RequireEntity(dependency.ActorId);
        if (dependency.TargetIds is null) throw new InvalidOperationException("COMBAT_ACTION_INVALIDATION_TARGETS_REQUIRED");
        if (dependency.TargetIds.Distinct(StringComparer.OrdinalIgnoreCase).Count() != dependency.TargetIds.Count)
            throw new InvalidOperationException("COMBAT_ACTION_INVALIDATION_DUPLICATE_TARGET");
        foreach (var targetId in dependency.TargetIds) RequireEntity(targetId);
        if (dependency.EvidenceReferences.Count == 0)
            throw new InvalidOperationException("COMBAT_ACTION_INVALIDATION_EVIDENCE_REQUIRED");
        if (dependency.Confidence == RuntimeResolutionConfidence.WeakHypothesis)
            throw new InvalidOperationException("COMBAT_ACTION_INVALIDATION_POLICY_NOT_PROMOTED");
        if (dependency.Confidence == RuntimeResolutionConfidence.StronglyCorrelated && string.IsNullOrWhiteSpace(dependency.SafeFallback))
            throw new InvalidOperationException("COMBAT_ACTION_INVALIDATION_FALLBACK_REQUIRED");
        _queue.Get(dependency.ActionId);
        if (!_dependencies.TryAdd(dependency.ActionId, dependency))
            throw new InvalidOperationException("COMBAT_ACTION_INVALIDATION_DUPLICATE_DEPENDENCY");
    }

    public RuntimeCombatActionInvalidationResult Invalidate(RuntimeCombatEntityInvalidation invalidation)
    {
        ArgumentNullException.ThrowIfNull(invalidation);
        RequireCanonical(invalidation.CombatId, "combat.", "COMBAT_ACTION_INVALIDATION_COMBAT_ID_REQUIRED");
        RequireEntity(invalidation.EntityId);
        if (invalidation.WorldTick != _queue.WorldTick)
            throw new InvalidOperationException("COMBAT_ACTION_INVALIDATION_TICK_MISMATCH");
        if (!_invalidEntities.Add(invalidation.EntityId))
            throw new InvalidOperationException("COMBAT_ACTION_INVALIDATION_ALREADY_APPLIED");

        var candidates = _dependencies.Values
            .Select(x => (Dependency: x, Action: _queue.Get(x.ActionId)))
            .Where(x => x.Action.CombatId.Equals(invalidation.CombatId, StringComparison.OrdinalIgnoreCase))
            .Where(x => x.Action.State is RuntimeCombatQueuedActionState.Queued or RuntimeCombatQueuedActionState.Claimed)
            .Where(x =>
                (x.Dependency.CancelOnActorInvalidation && x.Dependency.ActorId.Equals(invalidation.EntityId, StringComparison.OrdinalIgnoreCase)) ||
                (x.Dependency.CancelOnTargetInvalidation && x.Dependency.TargetIds.Contains(invalidation.EntityId, StringComparer.OrdinalIgnoreCase)))
            .OrderBy(x => x.Action.DueTick)
            .ThenBy(x => x.Action.SameTickPriority)
            .ThenBy(x => x.Action.SourceSequence)
            .ThenBy(x => x.Action.ActionId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var cancellations = new List<RuntimeCombatActionCancellation>(candidates.Length);
        foreach (var candidate in candidates)
        {
            var reason = BuildReason(invalidation, candidate.Dependency);
            var terminal = _queue.Cancel(candidate.Action.ActionId, invalidation.WorldTick, reason);
            cancellations.Add(new RuntimeCombatActionCancellation(
                terminal.ActionId, invalidation.EntityId, invalidation.Reason, reason,
                candidate.Action.State, terminal.State));
        }

        return new RuntimeCombatActionInvalidationResult(invalidation, cancellations);
    }

    private static string BuildReason(RuntimeCombatEntityInvalidation invalidation, RuntimeCombatActionDependency dependency)
    {
        var role = dependency.ActorId.Equals(invalidation.EntityId, StringComparison.OrdinalIgnoreCase) ? "actor" : "target";
        return $"{role}_{invalidation.Reason.ToString().ToLowerInvariant()}";
    }

    private static void RequireEntity(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !(
            value.StartsWith("character.", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("npc.", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("world.", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("COMBAT_ACTION_INVALIDATION_ENTITY_ID_REQUIRED");
    }

    private static void RequireCanonical(string value, string prefix, string error)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(error);
    }
}
