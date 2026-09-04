namespace RealmGate.Server;

public enum RuntimeCombatMovementKind { VoluntaryFlee, ForcedFlee, FearFlee }
public enum RuntimeCombatMovementBlocker { Rooted, Stunned, Sleeping, Paralyzed, Encumbered, ExitLocked, ExitRestricted }

public sealed record RuntimeCombatMovementPolicy(
    string PolicyId,
    RuntimeCombatMovementKind Kind,
    bool RequiresPromotedEscapeOutcome,
    bool InterruptPendingActions,
    bool TransitionParticipantToFled,
    bool CleanupThreat,
    IReadOnlySet<RuntimeCombatMovementBlocker> BlockingConditions,
    IReadOnlyList<string> EvidenceReferences,
    RuntimeResolutionConfidence Confidence,
    string SafeFallback);

public sealed record RuntimeCombatMovementRequest(
    string MovementId,
    string CombatId,
    string ActorId,
    string FromRoomId,
    string DestinationRoomId,
    string ExitId,
    RuntimeCombatMovementKind Kind,
    IReadOnlySet<RuntimeCombatMovementBlocker> ActiveConditions,
    bool? PromotedEscapeSucceeded,
    long WorldTick);

public sealed record RuntimeCombatRoomExitHandoff(
    string MovementId,
    string CombatId,
    string ActorId,
    string FromRoomId,
    string DestinationRoomId,
    string ExitId,
    RuntimeCombatMovementKind Kind,
    long WorldTick,
    IReadOnlyList<string> CancelledActionIds,
    int RemovedThreatEntryCount);

public sealed record RuntimeCombatMovementResult(
    bool Moved,
    string Outcome,
    RuntimeCombatRoomExitHandoff? Handoff,
    IReadOnlyList<RuntimeCombatMovementBlocker> BlockingConditions);

public sealed class RuntimeCombatMovementCoordinator
{
    private readonly RuntimeCombatInstanceLifecycle _combat;
    private readonly RuntimeCombatActionQueue _queue;
    private readonly RuntimeCombatActionInvalidationCoordinator _invalidation;
    private readonly RuntimeCombatThreatLedger _threat;
    private readonly IReadOnlyDictionary<RuntimeCombatMovementKind, RuntimeCombatMovementPolicy> _policies;
    private readonly HashSet<string> _processed = new(StringComparer.OrdinalIgnoreCase);

    public RuntimeCombatMovementCoordinator(
        RuntimeCombatInstanceLifecycle combat,
        RuntimeCombatActionQueue queue,
        RuntimeCombatActionInvalidationCoordinator invalidation,
        RuntimeCombatThreatLedger threat,
        IEnumerable<RuntimeCombatMovementPolicy> policies)
    {
        _combat = combat ?? throw new ArgumentNullException(nameof(combat));
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
        _invalidation = invalidation ?? throw new ArgumentNullException(nameof(invalidation));
        _threat = threat ?? throw new ArgumentNullException(nameof(threat));
        var materialized = (policies ?? throw new ArgumentNullException(nameof(policies))).ToArray();
        foreach (var policy in materialized) ValidatePolicy(policy);
        _policies = materialized.ToDictionary(x => x.Kind);
        if (_policies.Count != materialized.Length) throw new InvalidOperationException("COMBAT_MOVEMENT_DUPLICATE_POLICY");
    }

    public RuntimeCombatMovementResult Execute(RuntimeCombatMovementRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);
        if (!_processed.Add(request.MovementId)) throw new InvalidOperationException("COMBAT_MOVEMENT_ALREADY_PROCESSED");
        try
        {
            if (!_policies.TryGetValue(request.Kind, out var policy))
                throw new InvalidOperationException("COMBAT_MOVEMENT_POLICY_UNRESOLVED");
            if (!_combat.CombatId.Equals(request.CombatId, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("COMBAT_MOVEMENT_COMBAT_MISMATCH");
            if (!_combat.RoomId.Equals(request.FromRoomId, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("COMBAT_MOVEMENT_SOURCE_ROOM_MISMATCH");
            if (_queue.WorldTick != request.WorldTick)
                throw new InvalidOperationException("COMBAT_MOVEMENT_TICK_MISMATCH");
            var participant = _combat.Participants.SingleOrDefault(x => x.ActorId.Equals(request.ActorId, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException("COMBAT_MOVEMENT_PARTICIPANT_NOT_FOUND");
            if (participant.State != RuntimeCombatParticipantState.Active)
                throw new InvalidOperationException("COMBAT_MOVEMENT_PARTICIPANT_NOT_ACTIVE");

            var blockers = request.ActiveConditions.Intersect(policy.BlockingConditions).OrderBy(x => x).ToArray();
            if (blockers.Length != 0)
                return new RuntimeCombatMovementResult(false, "blocked", null, blockers);
            if (policy.RequiresPromotedEscapeOutcome && request.PromotedEscapeSucceeded is null)
                throw new InvalidOperationException("COMBAT_MOVEMENT_ESCAPE_OUTCOME_UNRESOLVED");
            if (request.PromotedEscapeSucceeded == false)
                return new RuntimeCombatMovementResult(false, "escape_failed", null, Array.Empty<RuntimeCombatMovementBlocker>());
            if (!policy.InterruptPendingActions || !policy.TransitionParticipantToFled || !policy.CleanupThreat)
                throw new InvalidOperationException("COMBAT_MOVEMENT_EXECUTION_POLICY_INCOMPLETE");

            var invalidation = _invalidation.Invalidate(new RuntimeCombatEntityInvalidation(
                request.CombatId, request.ActorId, RuntimeCombatEntityInvalidationReason.Fled, request.WorldTick));
            var cleanup = _threat.Cleanup($"threatcleanup.{request.MovementId["movement.".Length..]}", request.CombatId,
                request.ActorId, RuntimeCombatThreatCleanupReason.Fled);
            _combat.SetParticipantState(request.ActorId, RuntimeCombatParticipantState.Fled, request.WorldTick);

            var handoff = new RuntimeCombatRoomExitHandoff(
                request.MovementId, request.CombatId, request.ActorId, request.FromRoomId,
                request.DestinationRoomId, request.ExitId, request.Kind, request.WorldTick,
                invalidation.Cancellations.Select(x => x.ActionId).ToArray(), cleanup.RemovedEntries.Count);
            return new RuntimeCombatMovementResult(true, "room_exit_handoff_ready", handoff,
                Array.Empty<RuntimeCombatMovementBlocker>());
        }
        catch
        {
            _processed.Remove(request.MovementId);
            throw;
        }
    }

    private static void ValidatePolicy(RuntimeCombatMovementPolicy policy)
    {
        RequireCanonical(policy.PolicyId, "movementpolicy.", "COMBAT_MOVEMENT_POLICY_ID_REQUIRED");
        if (policy.BlockingConditions is null) throw new InvalidOperationException("COMBAT_MOVEMENT_BLOCKERS_REQUIRED");
        if (policy.EvidenceReferences is null || policy.EvidenceReferences.Count == 0)
            throw new InvalidOperationException("COMBAT_MOVEMENT_EVIDENCE_REQUIRED");
        if (policy.Confidence == RuntimeResolutionConfidence.WeakHypothesis)
            throw new InvalidOperationException("COMBAT_MOVEMENT_POLICY_NOT_PROMOTED");
        if (policy.Confidence == RuntimeResolutionConfidence.StronglyCorrelated && string.IsNullOrWhiteSpace(policy.SafeFallback))
            throw new InvalidOperationException("COMBAT_MOVEMENT_FALLBACK_REQUIRED");
    }

    private static void ValidateRequest(RuntimeCombatMovementRequest request)
    {
        RequireCanonical(request.MovementId, "movement.", "COMBAT_MOVEMENT_ID_REQUIRED");
        RequireCanonical(request.CombatId, "combat.", "COMBAT_MOVEMENT_COMBAT_ID_REQUIRED");
        RequireActor(request.ActorId);
        RequireCanonical(request.FromRoomId, "room.", "COMBAT_MOVEMENT_SOURCE_ROOM_ID_REQUIRED");
        RequireCanonical(request.DestinationRoomId, "room.", "COMBAT_MOVEMENT_DESTINATION_ROOM_ID_REQUIRED");
        RequireCanonical(request.ExitId, "exit.", "COMBAT_MOVEMENT_EXIT_ID_REQUIRED");
        if (request.FromRoomId.Equals(request.DestinationRoomId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("COMBAT_MOVEMENT_DESTINATION_EQUALS_SOURCE");
        if (request.ActiveConditions is null) throw new InvalidOperationException("COMBAT_MOVEMENT_ACTIVE_CONDITIONS_REQUIRED");
        if (request.WorldTick < 0) throw new InvalidOperationException("COMBAT_MOVEMENT_TICK_INVALID");
    }

    private static void RequireActor(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !(
            value.StartsWith("character.", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("npc.", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("COMBAT_MOVEMENT_ACTOR_ID_REQUIRED");
    }

    private static void RequireCanonical(string value, string prefix, string error)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(error);
    }
}
