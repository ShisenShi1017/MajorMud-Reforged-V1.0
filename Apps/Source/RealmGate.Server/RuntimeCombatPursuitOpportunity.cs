namespace RealmGate.Server;

public enum RuntimeCombatEscapeDisposition { Escaped, Failed }
public enum RuntimeCombatPursuitDisposition { None, Pursue }

public sealed record RuntimeCombatPursuitPolicy(
    string PolicyId,
    RuntimeCombatMovementKind MovementKind,
    bool? FailedEscapeConsumesRound,
    int? FailedEscapeResourceCost,
    bool? OpportunityAttackBeforeExit,
    bool? PursuitAllowed,
    int? MaximumPursuers,
    IReadOnlyList<string> EvidenceReferences,
    RuntimeResolutionConfidence Confidence,
    string SafeFallback);

public sealed record RuntimeCombatPursuitRequest(
    string ResolutionId,
    RuntimeCombatMovementRequest Movement,
    RuntimeCombatPursuitPolicy Policy,
    RuntimeCombatEscapeDisposition EscapeDisposition,
    IReadOnlyList<string> OpportunityAttackerIds,
    IReadOnlyList<string> PursuerIds,
    long ScheduledTurnSequence);

public sealed record RuntimeCombatOpportunityAttackRequest(
    string AttackId,
    string CombatId,
    string RoomId,
    string AttackerId,
    string DefenderId,
    long WorldTick,
    long Sequence);

public sealed record RuntimeCombatOpportunityAttackResult(string AttackId, bool Hit, int Damage, bool DefenderDefeated);

public interface IRuntimeCombatOpportunityAttackExecutor
{
    RuntimeCombatOpportunityAttackResult Execute(RuntimeCombatOpportunityAttackRequest request);
}

public interface IRuntimeCombatEscapeResourceSink
{
    object Capture(string actorId);
    void Consume(string actorId, int amount, string resolutionId);
    void ConsumeRound(string actorId, string combatId, long worldTick, long sequence, string resolutionId);
    void Restore(string actorId, object snapshot);
}

public sealed record RuntimeCombatPursuitHandoff(
    string ResolutionId,
    RuntimeCombatRoomExitHandoff Exit,
    IReadOnlyList<string> PursuerIds,
    IReadOnlyList<RuntimeCombatOpportunityAttackResult> OpportunityAttacks);

public sealed record RuntimeCombatPursuitResult(
    bool Escaped,
    bool RoundConsumed,
    int ResourceConsumed,
    bool DefeatedBeforeExit,
    RuntimeCombatPursuitHandoff? Handoff,
    IReadOnlyList<RuntimeCombatOpportunityAttackResult> OpportunityAttacks,
    IReadOnlyList<string> EvidenceReferences);

public sealed class RuntimeCombatPursuitOpportunityCoordinator
{
    private readonly RuntimeCombatInstanceLifecycle _combat;
    private readonly RuntimeCombatMovementCoordinator _movement;
    private readonly IRuntimeCombatOpportunityAttackExecutor _attacks;
    private readonly IRuntimeCombatEscapeResourceSink _resources;
    private readonly HashSet<string> _processed = new(StringComparer.OrdinalIgnoreCase);

    public RuntimeCombatPursuitOpportunityCoordinator(
        RuntimeCombatInstanceLifecycle combat,
        RuntimeCombatMovementCoordinator movement,
        IRuntimeCombatOpportunityAttackExecutor attacks,
        IRuntimeCombatEscapeResourceSink resources)
    {
        _combat = combat ?? throw new ArgumentNullException(nameof(combat));
        _movement = movement ?? throw new ArgumentNullException(nameof(movement));
        _attacks = attacks ?? throw new ArgumentNullException(nameof(attacks));
        _resources = resources ?? throw new ArgumentNullException(nameof(resources));
    }

    public RuntimeCombatPursuitResult Execute(RuntimeCombatPursuitRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        Validate(request);
        if (!_processed.Add(request.ResolutionId)) throw new InvalidOperationException("COMBAT_PURSUIT_RESOLUTION_ALREADY_PROCESSED");
        try
        {
            if (request.EscapeDisposition == RuntimeCombatEscapeDisposition.Failed)
                return ResolveFailedEscape(request);
            return ResolveSuccessfulEscape(request);
        }
        catch
        {
            _processed.Remove(request.ResolutionId);
            throw;
        }
    }

    private RuntimeCombatPursuitResult ResolveFailedEscape(RuntimeCombatPursuitRequest request)
    {
        var policy = request.Policy;
        if (policy.FailedEscapeConsumesRound is null || policy.FailedEscapeResourceCost is null)
            throw new InvalidOperationException("COMBAT_PURSUIT_FAILED_ESCAPE_POLICY_UNRESOLVED");
        object? snapshot = null;
        try
        {
            if (policy.FailedEscapeResourceCost.Value > 0)
            {
                snapshot = _resources.Capture(request.Movement.ActorId);
                _resources.Consume(request.Movement.ActorId, policy.FailedEscapeResourceCost.Value, request.ResolutionId);
            }
            if (policy.FailedEscapeConsumesRound.Value)
                _resources.ConsumeRound(request.Movement.ActorId, request.Movement.CombatId, request.Movement.WorldTick, request.ScheduledTurnSequence, request.ResolutionId);
            return new RuntimeCombatPursuitResult(false, policy.FailedEscapeConsumesRound.Value,
                policy.FailedEscapeResourceCost.Value, false, null,
                Array.Empty<RuntimeCombatOpportunityAttackResult>(), policy.EvidenceReferences);
        }
        catch
        {
            if (snapshot is not null) _resources.Restore(request.Movement.ActorId, snapshot);
            throw;
        }
    }

    private RuntimeCombatPursuitResult ResolveSuccessfulEscape(RuntimeCombatPursuitRequest request)
    {
        var policy = request.Policy;
        if (policy.OpportunityAttackBeforeExit is null || policy.PursuitAllowed is null || policy.MaximumPursuers is null)
            throw new InvalidOperationException("COMBAT_PURSUIT_SUCCESS_POLICY_UNRESOLVED");
        var opportunity = new List<RuntimeCombatOpportunityAttackResult>();
        if (policy.OpportunityAttackBeforeExit.Value)
        {
            foreach (var attackerId in request.OpportunityAttackerIds.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                var result = _attacks.Execute(new RuntimeCombatOpportunityAttackRequest(
                    $"attack.opportunity.{request.ResolutionId["pursuit.".Length..]}.{opportunity.Count + 1}",
                    request.Movement.CombatId, request.Movement.FromRoomId, attackerId,
                    request.Movement.ActorId, request.Movement.WorldTick,
                    checked(request.ScheduledTurnSequence * 100L + opportunity.Count + 1L)));
                opportunity.Add(result);
                if (result.DefenderDefeated)
                    return new RuntimeCombatPursuitResult(false, false, 0, true, null, opportunity, policy.EvidenceReferences);
            }
        }
        var movement = _movement.Execute(request.Movement with { PromotedEscapeSucceeded = true });
        if (!movement.Moved || movement.Handoff is null) throw new InvalidOperationException("COMBAT_PURSUIT_EXIT_HANDOFF_REQUIRED");
        var pursuers = policy.PursuitAllowed.Value
            ? request.PursuerIds.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).Take(policy.MaximumPursuers.Value).ToArray()
            : Array.Empty<string>();
        return new RuntimeCombatPursuitResult(true, false, 0, false,
            new RuntimeCombatPursuitHandoff(request.ResolutionId, movement.Handoff, pursuers, opportunity),
            opportunity, policy.EvidenceReferences);
    }

    private static void Validate(RuntimeCombatPursuitRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ResolutionId) || !request.ResolutionId.StartsWith("pursuit.", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("COMBAT_PURSUIT_RESOLUTION_ID_REQUIRED");
        if (request.Policy.MovementKind != request.Movement.Kind) throw new InvalidOperationException("COMBAT_PURSUIT_MOVEMENT_KIND_MISMATCH");
        if (request.Policy.EvidenceReferences.Count == 0) throw new InvalidOperationException("COMBAT_PURSUIT_EVIDENCE_REQUIRED");
        if (request.Policy.Confidence == RuntimeResolutionConfidence.WeakHypothesis) throw new InvalidOperationException("COMBAT_PURSUIT_POLICY_NOT_PROMOTED");
        if (request.Policy.Confidence == RuntimeResolutionConfidence.StronglyCorrelated && string.IsNullOrWhiteSpace(request.Policy.SafeFallback))
            throw new InvalidOperationException("COMBAT_PURSUIT_FALLBACK_REQUIRED");
        if (request.Policy.FailedEscapeResourceCost < 0 || request.Policy.MaximumPursuers < 0)
            throw new InvalidOperationException("COMBAT_PURSUIT_POLICY_RANGE_INVALID");
        if (request.OpportunityAttackerIds.Distinct(StringComparer.OrdinalIgnoreCase).Count() != request.OpportunityAttackerIds.Count)
            throw new InvalidOperationException("COMBAT_PURSUIT_DUPLICATE_OPPORTUNITY_ATTACKER");
        if (request.PursuerIds.Distinct(StringComparer.OrdinalIgnoreCase).Count() != request.PursuerIds.Count)
            throw new InvalidOperationException("COMBAT_PURSUIT_DUPLICATE_PURSUER");
        foreach (var actor in request.OpportunityAttackerIds.Concat(request.PursuerIds))
            if (!(actor.StartsWith("character.", StringComparison.OrdinalIgnoreCase) || actor.StartsWith("npc.", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("COMBAT_PURSUIT_CANONICAL_ACTOR_REQUIRED");
    }
}
