namespace RealmGate.Server;

public enum RuntimeSpellMovementKind { SelfTeleport, TargetTeleport, PartyTeleport, Recall, PortalCreate, SummonTarget, ForcedMove, Knockback, Pull, Swap }
public enum RuntimeSpellMovementEventKind { ValidateSource, ValidateTargets, ValidateDestination, ValidateRoomRestrictions, ValidateCombatRestrictions, ValidateConsent, InterruptActions, CleanupThreat, CleanupCombat, RemoveMovementStatuses, CreatePortal, MovePrimary, MoveSecondary, EmitMovementEvent, PersistMovementState }

public sealed record RuntimeSpellMovementPolicy(
    string PolicyId, RuntimeSpellMovementKind Kind, bool AllowInCombat, bool RequireConsent,
    bool RequireSameRoom, bool AllowCrossWorld, bool InterruptPendingActions, bool CleanupThreat,
    bool CleanupCombat, bool RemoveRoot, bool RemoveHidden, bool PersistMovementState,
    IReadOnlyList<RuntimeSpellMovementEventKind> OrderedEvents,
    IReadOnlyList<string> EvidenceReferences, RuntimeResolutionConfidence Confidence, string SafeFallback);

public sealed record RuntimeSpellMovementRequest(
    string MovementId, string SourceSpellId, string ActorId, string PrimaryTargetId,
    string? SecondaryTargetId, string SourceRoomId, string DestinationRoomId, string? ExitId,
    string? CombatId, long WorldTick, bool ActorActive, bool PrimaryTargetActive,
    bool DestinationAvailable, bool DestinationRestricted, bool SourceRestricted,
    bool InCombat, bool ConsentGranted, bool SameRoom, bool SameWorld,
    bool IsRooted, bool IsHidden, IReadOnlyList<string> EvidenceReferences);

public sealed record RuntimeSpellMovementEvent(
    string EventId, string MovementId, RuntimeSpellMovementEventKind Kind,
    RuntimeSpellMovementKind MovementKind, string SourceSpellId, string ActorId,
    string PrimaryTargetId, string? SecondaryTargetId, string SourceRoomId,
    string DestinationRoomId, string? ExitId, string? CombatId, long WorldTick,
    int Sequence, bool InterruptActions, bool CleanupThreat, bool CleanupCombat,
    bool RemoveRoot, bool RemoveHidden, IReadOnlyList<string> EvidenceReferences);

public sealed record RuntimeSpellMovementResult(RuntimeSpellMovementRequest Request,
    IReadOnlyList<RuntimeSpellMovementEvent> Events, bool Committed,
    bool ActionsInterrupted, bool ThreatCleaned, bool CombatCleaned,
    bool RootRemoved, bool HiddenRemoved, IReadOnlyList<string> Trace);

public interface IRuntimeSpellMovementEventSink
{
    string SinkId { get; }
    void Prepare(RuntimeSpellMovementEvent movementEvent);
    void Commit(RuntimeSpellMovementEvent movementEvent);
    void Rollback(RuntimeSpellMovementEvent movementEvent);
}

public sealed class RuntimeCombatSpellMovementCoordinator
{
    private readonly RuntimeSpellMovementPolicy _policy;
    private readonly IReadOnlyDictionary<RuntimeSpellMovementEventKind, IRuntimeSpellMovementEventSink> _sinks;
    private readonly HashSet<string> _completed = new(StringComparer.OrdinalIgnoreCase);

    public RuntimeCombatSpellMovementCoordinator(RuntimeSpellMovementPolicy policy,
        IEnumerable<(RuntimeSpellMovementEventKind Kind, IRuntimeSpellMovementEventSink Sink)> sinks)
    {
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        ValidatePolicy(policy);
        var materialized = (sinks ?? throw new ArgumentNullException(nameof(sinks))).ToArray();
        if (materialized.Any(x => x.Sink is null)) throw new InvalidOperationException("SPELL_MOVEMENT_SINK_REQUIRED");
        if (materialized.Select(x => x.Kind).Distinct().Count() != materialized.Length) throw new InvalidOperationException("SPELL_MOVEMENT_DUPLICATE_SINK_KIND");
        if (materialized.Select(x => x.Sink.SinkId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != materialized.Length) throw new InvalidOperationException("SPELL_MOVEMENT_DUPLICATE_SINK_ID");
        _sinks = materialized.ToDictionary(x => x.Kind, x => x.Sink);
    }

    public RuntimeSpellMovementResult Execute(RuntimeSpellMovementRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request, _policy);
        if (!_completed.Add(request.MovementId)) throw new InvalidOperationException("SPELL_MOVEMENT_ALREADY_COMPLETED");
        var events = BuildEvents(request);
        var prepared = new List<(RuntimeSpellMovementEvent Event, IRuntimeSpellMovementEventSink Sink)>();
        var committed = new List<(RuntimeSpellMovementEvent Event, IRuntimeSpellMovementEventSink Sink)>();
        var trace = new List<string> { "movement_validated", $"kind:{_policy.Kind}", $"destination:{request.DestinationRoomId}" };
        try
        {
            foreach (var movementEvent in events)
            {
                if (!_sinks.TryGetValue(movementEvent.Kind, out var sink)) throw new InvalidOperationException($"SPELL_MOVEMENT_SINK_UNAVAILABLE:{movementEvent.Kind}");
                sink.Prepare(movementEvent); prepared.Add((movementEvent, sink));
                trace.Add($"prepared:{movementEvent.Sequence}:{movementEvent.Kind}:{sink.SinkId}");
            }
            foreach (var item in prepared)
            {
                item.Sink.Commit(item.Event); committed.Add(item);
                trace.Add($"committed:{item.Event.Sequence}:{item.Event.Kind}:{item.Sink.SinkId}");
            }
            trace.Add("movement_committed");
            return new RuntimeSpellMovementResult(request, events, true,
                _policy.InterruptPendingActions, _policy.CleanupThreat && request.InCombat,
                _policy.CleanupCombat && request.InCombat, _policy.RemoveRoot && request.IsRooted,
                _policy.RemoveHidden && request.IsHidden, trace);
        }
        catch
        {
            foreach (var item in committed.AsEnumerable().Reverse()) item.Sink.Rollback(item.Event);
            _completed.Remove(request.MovementId);
            throw;
        }
    }

    private IReadOnlyList<RuntimeSpellMovementEvent> BuildEvents(RuntimeSpellMovementRequest request)
    {
        var evidence = _policy.EvidenceReferences.Concat(request.EvidenceReferences)
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
        return _policy.OrderedEvents.Select((kind, sequence) => new RuntimeSpellMovementEvent(
            $"movement-event.{request.MovementId[9..]}.{sequence:D2}", request.MovementId, kind,
            _policy.Kind, request.SourceSpellId, request.ActorId, request.PrimaryTargetId,
            request.SecondaryTargetId, request.SourceRoomId, request.DestinationRoomId,
            request.ExitId, request.CombatId, request.WorldTick, sequence,
            _policy.InterruptPendingActions, _policy.CleanupThreat && request.InCombat,
            _policy.CleanupCombat && request.InCombat, _policy.RemoveRoot && request.IsRooted,
            _policy.RemoveHidden && request.IsHidden, evidence)).ToArray();
    }

    private static void ValidatePolicy(RuntimeSpellMovementPolicy policy)
    {
        RequireCanonical(policy.PolicyId, "movement-policy.", "SPELL_MOVEMENT_POLICY_ID_REQUIRED");
        if (policy.OrderedEvents is null || policy.OrderedEvents.Count == 0 || policy.OrderedEvents.Distinct().Count() != policy.OrderedEvents.Count) throw new InvalidOperationException("SPELL_MOVEMENT_EVENT_ORDER_INVALID");
        var required = new[] { RuntimeSpellMovementEventKind.ValidateSource, RuntimeSpellMovementEventKind.ValidateTargets,
            RuntimeSpellMovementEventKind.ValidateDestination, RuntimeSpellMovementEventKind.ValidateRoomRestrictions,
            RuntimeSpellMovementEventKind.ValidateCombatRestrictions, RuntimeSpellMovementEventKind.EmitMovementEvent };
        if (required.Any(x => !policy.OrderedEvents.Contains(x))) throw new InvalidOperationException("SPELL_MOVEMENT_REQUIRED_EVENT_MISSING");
        if (policy.Kind == RuntimeSpellMovementKind.PortalCreate && !policy.OrderedEvents.Contains(RuntimeSpellMovementEventKind.CreatePortal)) throw new InvalidOperationException("SPELL_MOVEMENT_PORTAL_EVENT_REQUIRED");
        if (policy.Kind == RuntimeSpellMovementKind.Swap && !policy.OrderedEvents.Contains(RuntimeSpellMovementEventKind.MoveSecondary)) throw new InvalidOperationException("SPELL_MOVEMENT_SWAP_SECONDARY_REQUIRED");
        if (policy.RequireConsent && !policy.OrderedEvents.Contains(RuntimeSpellMovementEventKind.ValidateConsent)) throw new InvalidOperationException("SPELL_MOVEMENT_CONSENT_EVENT_REQUIRED");
        if (policy.EvidenceReferences.Count == 0) throw new InvalidOperationException("SPELL_MOVEMENT_EVIDENCE_REQUIRED");
        if (policy.Confidence == RuntimeResolutionConfidence.WeakHypothesis) throw new InvalidOperationException("SPELL_MOVEMENT_POLICY_NOT_PROMOTED");
        if (policy.Confidence == RuntimeResolutionConfidence.StronglyCorrelated && string.IsNullOrWhiteSpace(policy.SafeFallback)) throw new InvalidOperationException("SPELL_MOVEMENT_FALLBACK_REQUIRED");
    }

    private static void ValidateRequest(RuntimeSpellMovementRequest request, RuntimeSpellMovementPolicy policy)
    {
        RequireCanonical(request.MovementId, "movement.", "SPELL_MOVEMENT_ID_REQUIRED");
        RequireCanonical(request.SourceSpellId, "spell.", "SPELL_MOVEMENT_SOURCE_SPELL_REQUIRED");
        RequireEntity(request.ActorId, "SPELL_MOVEMENT_ACTOR_REQUIRED");
        RequireEntity(request.PrimaryTargetId, "SPELL_MOVEMENT_PRIMARY_TARGET_REQUIRED");
        if (request.SecondaryTargetId is not null) RequireEntity(request.SecondaryTargetId, "SPELL_MOVEMENT_SECONDARY_TARGET_INVALID");
        RequireCanonical(request.SourceRoomId, "room.", "SPELL_MOVEMENT_SOURCE_ROOM_REQUIRED");
        RequireCanonical(request.DestinationRoomId, "room.", "SPELL_MOVEMENT_DESTINATION_ROOM_REQUIRED");
        if (request.ExitId is not null) RequireCanonical(request.ExitId, "exit.", "SPELL_MOVEMENT_EXIT_INVALID");
        if (request.CombatId is not null) RequireCanonical(request.CombatId, "combat.", "SPELL_MOVEMENT_COMBAT_ID_INVALID");
        if (request.WorldTick < 0) throw new InvalidOperationException("SPELL_MOVEMENT_TICK_INVALID");
        if (!request.ActorActive || !request.PrimaryTargetActive) throw new InvalidOperationException("SPELL_MOVEMENT_ENTITY_INACTIVE");
        if (!request.DestinationAvailable) throw new InvalidOperationException("SPELL_MOVEMENT_DESTINATION_UNAVAILABLE");
        if (request.DestinationRestricted || request.SourceRestricted) throw new InvalidOperationException("SPELL_MOVEMENT_ROOM_RESTRICTED");
        if (request.InCombat && !policy.AllowInCombat) throw new InvalidOperationException("SPELL_MOVEMENT_COMBAT_RESTRICTED");
        if (policy.RequireConsent && !request.ConsentGranted) throw new InvalidOperationException("SPELL_MOVEMENT_CONSENT_REQUIRED");
        if (policy.RequireSameRoom && !request.SameRoom) throw new InvalidOperationException("SPELL_MOVEMENT_SAME_ROOM_REQUIRED");
        if (!policy.AllowCrossWorld && !request.SameWorld) throw new InvalidOperationException("SPELL_MOVEMENT_CROSS_WORLD_RESTRICTED");
        if (policy.Kind == RuntimeSpellMovementKind.Swap && request.SecondaryTargetId is null) throw new InvalidOperationException("SPELL_MOVEMENT_SWAP_TARGET_REQUIRED");
        if (request.EvidenceReferences.Count == 0) throw new InvalidOperationException("SPELL_MOVEMENT_REQUEST_EVIDENCE_REQUIRED");
    }

    private static void RequireEntity(string value, string error)
    {
        if (string.IsNullOrWhiteSpace(value) || !(value.StartsWith("character.", StringComparison.OrdinalIgnoreCase) || value.StartsWith("npc.", StringComparison.OrdinalIgnoreCase) || value.StartsWith("party.", StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException(error);
    }
    private static void RequireCanonical(string value, string prefix, string error)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException(error);
    }
}
