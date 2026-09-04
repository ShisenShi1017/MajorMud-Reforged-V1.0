namespace RealmGate.Server;

public enum RuntimeSpellCastState
{
    Created,
    Casting,
    ReadyToComplete,
    Completed,
    Interrupted,
    Failed
}

public enum RuntimeSpellInterruptionReason
{
    ExplicitCancel,
    ActorMoved,
    ActorDamaged,
    ActorDisabled,
    TargetInvalidated,
    OwnerDisconnected,
    NativeReasonUnresolved
}

public sealed record RuntimeSpellCastTimingPolicy(
    string PolicyId,
    string SpellId,
    int? CastDurationTicks,
    IReadOnlySet<RuntimeSpellInterruptionReason> PromotedInterruptions,
    IReadOnlyList<string> EvidenceReferences,
    RuntimeResolutionConfidence Confidence,
    string SafeFallback);

public sealed record RuntimeSpellCastInstance(
    string CastId,
    string ActorId,
    string SpellId,
    long StartTick,
    long? CompletionTick,
    RuntimeSpellCastState State,
    RuntimeSpellResourceReservation? ResourceReservation,
    RuntimeSpellInterruptionReason? InterruptionReason);

public sealed record RuntimeSpellCastTransition(
    string CastId,
    RuntimeSpellCastState From,
    RuntimeSpellCastState To,
    long WorldTick,
    string Reason);

public sealed class RuntimeSpellCastLifecycle
{
    private readonly IReadOnlyDictionary<string, RuntimeSpellCastTimingPolicy> _policies;
    private readonly RuntimeSpellResourceLifecycle? _resources;
    private readonly Dictionary<string, RuntimeSpellCastInstance> _casts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _activeByActor = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<RuntimeSpellCastTransition> _transitions = new();
    private long _lastAdvancedTick = -1;

    public RuntimeSpellCastLifecycle(IEnumerable<RuntimeSpellCastTimingPolicy> policies, RuntimeSpellResourceLifecycle? resources = null)
    {
        var materialized = (policies ?? throw new ArgumentNullException(nameof(policies))).ToArray();
        foreach (var policy in materialized) ValidatePolicy(policy);
        _policies = materialized.ToDictionary(x => x.SpellId, StringComparer.OrdinalIgnoreCase);
        if (_policies.Count != materialized.Length) throw new InvalidOperationException("SPELL_CAST_DUPLICATE_POLICY");
        _resources = resources;
    }

    public IReadOnlyList<RuntimeSpellCastTransition> Transitions => _transitions;

    public RuntimeSpellCastInstance Start(string castId, string actorId, string spellId, long worldTick)
    {
        RequireId(castId, "SPELL_CAST_ID_REQUIRED");
        RequireId(actorId, "SPELL_CAST_ACTOR_ID_REQUIRED");
        RequireCanonicalSpell(spellId);
        if (worldTick < 0) throw new InvalidOperationException("SPELL_CAST_TICK_INVALID");
        if (_casts.ContainsKey(castId)) throw new InvalidOperationException("SPELL_CAST_ID_ALREADY_EXISTS");
        if (_activeByActor.ContainsKey(actorId)) throw new InvalidOperationException("SPELL_CAST_ACTOR_ALREADY_CASTING");
        if (!_policies.TryGetValue(spellId, out var policy)) throw new InvalidOperationException("SPELL_CAST_POLICY_UNAVAILABLE");
        if (policy.CastDurationTicks is null) throw new InvalidOperationException("SPELL_CAST_DURATION_UNRESOLVED");

        RuntimeSpellResourceReservation? reservation = null;
        if (_resources is not null) reservation = _resources.Start(castId, actorId, spellId).Reservation;

        var completionTick = checked(worldTick + policy.CastDurationTicks.Value);
        var created = new RuntimeSpellCastInstance(castId, actorId, spellId, worldTick, completionTick,
            RuntimeSpellCastState.Created, reservation, null);
        _casts.Add(castId, created);
        _activeByActor.Add(actorId, castId);
        return Transition(created, RuntimeSpellCastState.Casting, worldTick, "cast_started");
    }

    public IReadOnlyList<RuntimeSpellCastInstance> Advance(long worldTick)
    {
        if (worldTick < _lastAdvancedTick) throw new InvalidOperationException("SPELL_CAST_TICK_REGRESSION");
        _lastAdvancedTick = worldTick;
        var ready = new List<RuntimeSpellCastInstance>();
        foreach (var cast in _casts.Values.Where(x => x.State == RuntimeSpellCastState.Casting)
                     .OrderBy(x => x.CompletionTick).ThenBy(x => x.CastId, StringComparer.OrdinalIgnoreCase).ToArray())
        {
            if (cast.CompletionTick <= worldTick)
                ready.Add(Transition(cast, RuntimeSpellCastState.ReadyToComplete, worldTick, "cast_timer_elapsed"));
        }
        return ready;
    }

    public RuntimeSpellCastInstance Complete(string castId, long worldTick)
    {
        var cast = RequireCast(castId);
        if (cast.State != RuntimeSpellCastState.ReadyToComplete) throw new InvalidOperationException("SPELL_CAST_NOT_READY");
        if (cast.CompletionTick > worldTick) throw new InvalidOperationException("SPELL_CAST_COMPLETION_EARLY");
        if (_resources is not null && cast.ResourceReservation is not null)
            _resources.Finish(cast.ResourceReservation, RuntimeSpellCastTerminalState.Completed);
        var completed = Transition(cast, RuntimeSpellCastState.Completed, worldTick, "cast_completed");
        _activeByActor.Remove(cast.ActorId);
        return completed;
    }

    public RuntimeSpellCastInstance Fail(string castId, long worldTick, string reason)
    {
        var cast = RequireCast(castId);
        if (cast.State is not (RuntimeSpellCastState.Casting or RuntimeSpellCastState.ReadyToComplete))
            throw new InvalidOperationException("SPELL_CAST_NOT_FAILABLE");
        RequireId(reason, "SPELL_CAST_FAILURE_REASON_REQUIRED");
        if (_resources is not null && cast.ResourceReservation is not null)
            _resources.Finish(cast.ResourceReservation, reason.Equals("target_validation_failed", StringComparison.OrdinalIgnoreCase)
                ? RuntimeSpellCastTerminalState.FailedValidation
                : RuntimeSpellCastTerminalState.FailedExecution);
        var failed = Transition(cast, RuntimeSpellCastState.Failed, worldTick, $"failed:{reason}");
        _activeByActor.Remove(cast.ActorId);
        return failed;
    }

    public RuntimeSpellCastInstance Interrupt(string castId, RuntimeSpellInterruptionReason reason, long worldTick)
    {
        var cast = RequireCast(castId);
        if (cast.State is not (RuntimeSpellCastState.Casting or RuntimeSpellCastState.ReadyToComplete))
            throw new InvalidOperationException("SPELL_CAST_NOT_INTERRUPTIBLE");
        var policy = _policies[cast.SpellId];
        if (!policy.PromotedInterruptions.Contains(reason)) throw new InvalidOperationException("SPELL_CAST_INTERRUPTION_UNRESOLVED");
        if (_resources is not null && cast.ResourceReservation is not null)
            _resources.Finish(cast.ResourceReservation, RuntimeSpellCastTerminalState.Interrupted);
        var interrupted = cast with { InterruptionReason = reason };
        interrupted = Transition(interrupted, RuntimeSpellCastState.Interrupted, worldTick, $"interrupted:{reason}");
        _activeByActor.Remove(cast.ActorId);
        return interrupted;
    }

    public RuntimeSpellCastInstance Get(string castId) => RequireCast(castId);

    private RuntimeSpellCastInstance Transition(RuntimeSpellCastInstance cast, RuntimeSpellCastState to, long tick, string reason)
    {
        var updated = cast with { State = to };
        _casts[cast.CastId] = updated;
        _transitions.Add(new RuntimeSpellCastTransition(cast.CastId, cast.State, to, tick, reason));
        return updated;
    }

    private RuntimeSpellCastInstance RequireCast(string castId)
    {
        RequireId(castId, "SPELL_CAST_ID_REQUIRED");
        return _casts.TryGetValue(castId, out var cast) ? cast : throw new InvalidOperationException("SPELL_CAST_NOT_FOUND");
    }

    private static void ValidatePolicy(RuntimeSpellCastTimingPolicy policy)
    {
        RequireCanonicalSpell(policy.SpellId);
        if (string.IsNullOrWhiteSpace(policy.PolicyId)) throw new InvalidOperationException("SPELL_CAST_POLICY_ID_REQUIRED");
        if (policy.CastDurationTicks < 0) throw new InvalidOperationException("SPELL_CAST_DURATION_INVALID");
        if (policy.EvidenceReferences.Count == 0) throw new InvalidOperationException("SPELL_CAST_EVIDENCE_REQUIRED");
        if (policy.Confidence == RuntimeResolutionConfidence.StronglyCorrelated && string.IsNullOrWhiteSpace(policy.SafeFallback))
            throw new InvalidOperationException("SPELL_CAST_FALLBACK_REQUIRED");
    }

    private static void RequireCanonicalSpell(string spellId)
    {
        if (string.IsNullOrWhiteSpace(spellId) || !spellId.StartsWith("spell.", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("SPELL_CAST_CANONICAL_SPELL_ID_REQUIRED");
    }

    private static void RequireId(string value, string error)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException(error);
    }
}
