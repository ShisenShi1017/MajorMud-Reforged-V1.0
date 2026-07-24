namespace Reforged.Server;

public enum RuntimeQueuedCombatActionKind
{
    PhysicalAttack,
    SpellCompletion,
    PeriodicEffect
}

public enum RuntimeCombatQueuedActionState
{
    Queued,
    Claimed,
    Completed,
    Failed,
    Cancelled
}

public sealed record RuntimeCombatActionOrderingPolicy(
    string PolicyId,
    RuntimeQueuedCombatActionKind Kind,
    int? SameTickPriority,
    IReadOnlyList<string> EvidenceReferences,
    RuntimeResolutionConfidence Confidence,
    string SafeFallback);

public sealed record RuntimeCombatActionRequest(
    string ActionId,
    string CombatId,
    string ActorId,
    RuntimeQueuedCombatActionKind Kind,
    long DueTick,
    string RuntimeReferenceId,
    long SourceSequence);

public sealed record RuntimeCombatQueuedAction(
    string ActionId,
    string CombatId,
    string ActorId,
    RuntimeQueuedCombatActionKind Kind,
    long DueTick,
    string RuntimeReferenceId,
    long SourceSequence,
    int SameTickPriority,
    RuntimeCombatQueuedActionState State,
    long? ClaimedTick,
    string? TerminalReason);

public sealed record RuntimeCombatActionBatch(
    long WorldTick,
    IReadOnlyList<RuntimeCombatQueuedAction> Actions);

public sealed class RuntimeCombatActionQueue
{
    private readonly IReadOnlyDictionary<RuntimeQueuedCombatActionKind, RuntimeCombatActionOrderingPolicy> _policies;
    private readonly Dictionary<string, RuntimeCombatQueuedAction> _actions = new(StringComparer.OrdinalIgnoreCase);
    private long _worldTick;

    public RuntimeCombatActionQueue(IEnumerable<RuntimeCombatActionOrderingPolicy> policies, long initialWorldTick = 0)
    {
        if (initialWorldTick < 0) throw new InvalidOperationException("COMBAT_ACTION_QUEUE_TICK_INVALID");
        _worldTick = initialWorldTick;
        var materialized = (policies ?? throw new ArgumentNullException(nameof(policies))).ToArray();
        foreach (var policy in materialized) ValidatePolicy(policy);
        _policies = materialized.ToDictionary(x => x.Kind);
        if (_policies.Count != materialized.Length)
            throw new InvalidOperationException("COMBAT_ACTION_QUEUE_DUPLICATE_POLICY");
    }

    public long WorldTick => _worldTick;
    public IReadOnlyCollection<RuntimeCombatQueuedAction> Actions => _actions.Values
        .OrderBy(x => x.DueTick).ThenBy(x => x.SameTickPriority)
        .ThenBy(x => x.SourceSequence).ThenBy(x => x.ActionId, StringComparer.OrdinalIgnoreCase).ToArray();

    public RuntimeCombatQueuedAction Enqueue(RuntimeCombatActionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);
        if (request.DueTick < _worldTick) throw new InvalidOperationException("COMBAT_ACTION_QUEUE_PAST_DUE_TICK");
        if (_actions.ContainsKey(request.ActionId)) throw new InvalidOperationException("COMBAT_ACTION_QUEUE_DUPLICATE_ACTION");
        if (!_policies.TryGetValue(request.Kind, out var policy))
            throw new InvalidOperationException("COMBAT_ACTION_QUEUE_POLICY_REQUIRED");
        if (policy.Confidence == RuntimeResolutionConfidence.WeakHypothesis)
            throw new InvalidOperationException("COMBAT_ACTION_QUEUE_POLICY_NOT_PROMOTED");
        if (policy.SameTickPriority is null)
            throw new InvalidOperationException("COMBAT_ACTION_QUEUE_ORDER_UNRESOLVED");

        var action = new RuntimeCombatQueuedAction(
            request.ActionId, request.CombatId, request.ActorId, request.Kind,
            request.DueTick, request.RuntimeReferenceId, request.SourceSequence,
            policy.SameTickPriority.Value, RuntimeCombatQueuedActionState.Queued, null, null);
        _actions.Add(action.ActionId, action);
        return action;
    }

    public RuntimeCombatActionBatch ClaimDue(long worldTick)
    {
        if (worldTick < _worldTick) throw new InvalidOperationException("COMBAT_ACTION_QUEUE_TICK_REGRESSION");
        _worldTick = worldTick;
        var due = _actions.Values
            .Where(x => x.State == RuntimeCombatQueuedActionState.Queued && x.DueTick <= worldTick)
            .OrderBy(x => x.DueTick)
            .ThenBy(x => x.SameTickPriority)
            .ThenBy(x => x.SourceSequence)
            .ThenBy(x => x.ActionId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var action in due)
            _actions[action.ActionId] = action with {
                State = RuntimeCombatQueuedActionState.Claimed,
                ClaimedTick = worldTick
            };

        return new RuntimeCombatActionBatch(worldTick, due.Select(x => _actions[x.ActionId]).ToArray());
    }

    public RuntimeCombatQueuedAction Complete(string actionId, long worldTick)
        => Transition(actionId, worldTick, RuntimeCombatQueuedActionState.Completed, "execution_completed");

    public RuntimeCombatQueuedAction Fail(string actionId, long worldTick, string reason)
        => Transition(actionId, worldTick, RuntimeCombatQueuedActionState.Failed, RequireReason(reason));

    public RuntimeCombatQueuedAction Cancel(string actionId, long worldTick, string reason)
    {
        var current = Get(actionId);
        if (current.State is not (RuntimeCombatQueuedActionState.Queued or RuntimeCombatQueuedActionState.Claimed))
            throw new InvalidOperationException("COMBAT_ACTION_QUEUE_ALREADY_TERMINAL");
        if (worldTick != _worldTick) throw new InvalidOperationException("COMBAT_ACTION_QUEUE_TRANSITION_TICK_MISMATCH");
        var terminal = current with { State = RuntimeCombatQueuedActionState.Cancelled, TerminalReason = RequireReason(reason) };
        _actions[actionId] = terminal;
        return terminal;
    }

    public RuntimeCombatQueuedAction Get(string actionId) =>
        _actions.TryGetValue(actionId, out var action) ? action :
        throw new KeyNotFoundException("COMBAT_ACTION_QUEUE_ACTION_NOT_FOUND");

    private RuntimeCombatQueuedAction Transition(string actionId, long worldTick, RuntimeCombatQueuedActionState state, string reason)
    {
        var current = Get(actionId);
        if (worldTick != _worldTick) throw new InvalidOperationException("COMBAT_ACTION_QUEUE_TRANSITION_TICK_MISMATCH");
        if (current.State != RuntimeCombatQueuedActionState.Claimed)
            throw new InvalidOperationException("COMBAT_ACTION_QUEUE_ACTION_NOT_CLAIMED");
        var terminal = current with { State = state, TerminalReason = reason };
        _actions[actionId] = terminal;
        return terminal;
    }

    private static void ValidatePolicy(RuntimeCombatActionOrderingPolicy policy)
    {
        if (string.IsNullOrWhiteSpace(policy.PolicyId)) throw new InvalidOperationException("COMBAT_ACTION_QUEUE_POLICY_ID_REQUIRED");
        if (policy.SameTickPriority < 0) throw new InvalidOperationException("COMBAT_ACTION_QUEUE_PRIORITY_INVALID");
        if (policy.EvidenceReferences.Count == 0) throw new InvalidOperationException("COMBAT_ACTION_QUEUE_POLICY_EVIDENCE_REQUIRED");
        if (policy.Confidence == RuntimeResolutionConfidence.StronglyCorrelated && string.IsNullOrWhiteSpace(policy.SafeFallback))
            throw new InvalidOperationException("COMBAT_ACTION_QUEUE_POLICY_FALLBACK_REQUIRED");
    }

    private static void ValidateRequest(RuntimeCombatActionRequest request)
    {
        RequireCanonical(request.ActionId, "action.", "COMBAT_ACTION_QUEUE_ACTION_ID_REQUIRED");
        RequireCanonical(request.CombatId, "combat.", "COMBAT_ACTION_QUEUE_COMBAT_ID_REQUIRED");
        if (!(request.ActorId.StartsWith("character.", StringComparison.OrdinalIgnoreCase) ||
              request.ActorId.StartsWith("npc.", StringComparison.OrdinalIgnoreCase) ||
              request.ActorId.StartsWith("world.", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("COMBAT_ACTION_QUEUE_ACTOR_ID_REQUIRED");
        if (request.DueTick < 0 || request.SourceSequence < 0)
            throw new InvalidOperationException("COMBAT_ACTION_QUEUE_SEQUENCE_INVALID");
        var prefix = request.Kind switch {
            RuntimeQueuedCombatActionKind.PhysicalAttack => "attack.",
            RuntimeQueuedCombatActionKind.SpellCompletion => "cast.",
            RuntimeQueuedCombatActionKind.PeriodicEffect => "effect.",
            _ => throw new InvalidOperationException("COMBAT_ACTION_QUEUE_KIND_INVALID")
        };
        RequireCanonical(request.RuntimeReferenceId, prefix, "COMBAT_ACTION_QUEUE_RUNTIME_REFERENCE_INVALID");
    }

    private static string RequireReason(string reason) =>
        string.IsNullOrWhiteSpace(reason) ? throw new InvalidOperationException("COMBAT_ACTION_QUEUE_REASON_REQUIRED") : reason;

    private static void RequireCanonical(string value, string prefix, string error)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(error);
    }
}
