namespace RealmGate.Server;

public enum RuntimeSpellResourceTiming
{
    Unresolved,
    CommitAtStart,
    ReserveAtStartCommitAtCompletion,
    CommitAtCompletion
}

public enum RuntimeSpellCastTerminalState
{
    Completed,
    Interrupted,
    FailedValidation,
    FailedExecution
}

public sealed record RuntimeSpellResourcePolicy(
    string PolicyId,
    string SpellId,
    string ResourceType,
    int Cost,
    RuntimeSpellResourceTiming Timing,
    bool RefundOnInterrupt,
    bool RefundOnExecutionFailure,
    IReadOnlyList<string> EvidenceReferences,
    RuntimeResolutionConfidence Confidence,
    string SafeFallback);

public sealed record RuntimeSpellResourceReservation(
    string ReservationId,
    string CastId,
    string ActorId,
    string SpellId,
    string ResourceType,
    int Cost,
    RuntimeSpellResourceTiming Timing);

public interface IRuntimeSpellResourceSink
{
    int GetAvailable(string actorId, string resourceType);
    object Capture(string actorId, string resourceType);
    void Reserve(RuntimeSpellResourceReservation reservation);
    void Commit(RuntimeSpellResourceReservation reservation);
    void Refund(RuntimeSpellResourceReservation reservation);
    void Restore(string actorId, string resourceType, object snapshot);
}

public sealed record RuntimeSpellResourceStartResult(
    RuntimeSpellResourceReservation Reservation,
    bool Reserved,
    bool Committed);

public sealed record RuntimeSpellResourceFinishResult(
    RuntimeSpellResourceReservation Reservation,
    RuntimeSpellCastTerminalState TerminalState,
    bool Committed,
    bool Refunded);

public sealed class RuntimeSpellResourceLifecycle
{
    private readonly IReadOnlyDictionary<string, RuntimeSpellResourcePolicy> _policies;
    private readonly IRuntimeSpellResourceSink _sink;
    private readonly HashSet<string> _started = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _finished = new(StringComparer.OrdinalIgnoreCase);

    public RuntimeSpellResourceLifecycle(IEnumerable<RuntimeSpellResourcePolicy> policies, IRuntimeSpellResourceSink sink)
    {
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        var materialized = (policies ?? throw new ArgumentNullException(nameof(policies))).ToArray();
        foreach (var policy in materialized) ValidatePolicy(policy);
        _policies = materialized.ToDictionary(x => x.SpellId, StringComparer.OrdinalIgnoreCase);
        if (_policies.Count != materialized.Length) throw new InvalidOperationException("SPELL_RESOURCE_DUPLICATE_POLICY");
    }

    public RuntimeSpellResourceStartResult Start(string castId, string actorId, string spellId)
    {
        RequireId(castId, "SPELL_RESOURCE_CAST_ID_REQUIRED");
        RequireId(actorId, "SPELL_RESOURCE_ACTOR_ID_REQUIRED");
        if (!spellId.StartsWith("spell.", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("SPELL_RESOURCE_CANONICAL_SPELL_ID_REQUIRED");
        if (!_policies.TryGetValue(spellId, out var policy)) throw new InvalidOperationException("SPELL_RESOURCE_POLICY_UNAVAILABLE");
        if (policy.Timing == RuntimeSpellResourceTiming.Unresolved) throw new InvalidOperationException("SPELL_RESOURCE_TIMING_UNRESOLVED");
        if (!_started.Add(castId)) throw new InvalidOperationException("SPELL_RESOURCE_CAST_ALREADY_STARTED");
        if (_sink.GetAvailable(actorId, policy.ResourceType) < policy.Cost)
        {
            _started.Remove(castId);
            throw new InvalidOperationException("SPELL_RESOURCE_INSUFFICIENT");
        }

        var reservation = CreateReservation(castId, actorId, policy);
        var snapshot = _sink.Capture(actorId, policy.ResourceType);
        try
        {
            var reserved = false;
            var committed = false;
            if (policy.Timing == RuntimeSpellResourceTiming.ReserveAtStartCommitAtCompletion)
            {
                _sink.Reserve(reservation);
                reserved = true;
            }
            else if (policy.Timing == RuntimeSpellResourceTiming.CommitAtStart)
            {
                _sink.Commit(reservation);
                committed = true;
            }
            return new RuntimeSpellResourceStartResult(reservation, reserved, committed);
        }
        catch
        {
            _sink.Restore(actorId, policy.ResourceType, snapshot);
            _started.Remove(castId);
            throw;
        }
    }

    public RuntimeSpellResourceFinishResult Finish(RuntimeSpellResourceReservation reservation, RuntimeSpellCastTerminalState terminalState)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        if (!_started.Contains(reservation.CastId)) throw new InvalidOperationException("SPELL_RESOURCE_CAST_NOT_STARTED");
        if (!_finished.Add(reservation.CastId)) throw new InvalidOperationException("SPELL_RESOURCE_CAST_ALREADY_FINISHED");
        var policy = _policies[reservation.SpellId];
        var snapshot = _sink.Capture(reservation.ActorId, reservation.ResourceType);
        try
        {
            var committed = false;
            var refunded = false;
            if (terminalState == RuntimeSpellCastTerminalState.Completed)
            {
                if (policy.Timing is RuntimeSpellResourceTiming.ReserveAtStartCommitAtCompletion or RuntimeSpellResourceTiming.CommitAtCompletion)
                {
                    _sink.Commit(reservation);
                    committed = true;
                }
            }
            else
            {
                var shouldRefund = terminalState == RuntimeSpellCastTerminalState.Interrupted
                    ? policy.RefundOnInterrupt
                    : policy.RefundOnExecutionFailure;
                if (policy.Timing == RuntimeSpellResourceTiming.ReserveAtStartCommitAtCompletion && shouldRefund)
                {
                    _sink.Refund(reservation);
                    refunded = true;
                }
            }
            return new RuntimeSpellResourceFinishResult(reservation, terminalState, committed, refunded);
        }
        catch
        {
            _sink.Restore(reservation.ActorId, reservation.ResourceType, snapshot);
            _finished.Remove(reservation.CastId);
            throw;
        }
    }

    private static RuntimeSpellResourceReservation CreateReservation(string castId, string actorId, RuntimeSpellResourcePolicy policy) =>
        new($"resource:{castId}", castId, actorId, policy.SpellId, policy.ResourceType, policy.Cost, policy.Timing);

    private static void ValidatePolicy(RuntimeSpellResourcePolicy policy)
    {
        if (!policy.SpellId.StartsWith("spell.", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("SPELL_RESOURCE_CANONICAL_SPELL_ID_REQUIRED");
        if (string.IsNullOrWhiteSpace(policy.PolicyId) || string.IsNullOrWhiteSpace(policy.ResourceType)) throw new InvalidOperationException("SPELL_RESOURCE_POLICY_ID_REQUIRED");
        if (policy.Cost < 0) throw new InvalidOperationException("SPELL_RESOURCE_COST_INVALID");
        if (policy.EvidenceReferences.Count == 0) throw new InvalidOperationException("SPELL_RESOURCE_EVIDENCE_REQUIRED");
        if (policy.Confidence == RuntimeResolutionConfidence.StronglyCorrelated && string.IsNullOrWhiteSpace(policy.SafeFallback))
            throw new InvalidOperationException("SPELL_RESOURCE_FALLBACK_REQUIRED");
    }

    private static void RequireId(string value, string error)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException(error);
    }
}
