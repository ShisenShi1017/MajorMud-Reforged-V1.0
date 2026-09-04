namespace RealmGate.Server;

public enum RuntimeCombatRevivalMode
{
    Resurrection,
    Revival
}

public enum RuntimeCombatRevivalEffectDisposition
{
    Preserve,
    RemoveHarmful,
    RemoveAll
}

public enum RuntimeCombatRevivalEventKind
{
    ValidateEligibility,
    ConsumeResource,
    ClearDeathState,
    RestoreHitPoints,
    RestoreMana,
    RestoreEnergy,
    ApplyEffectDisposition,
    MoveToDestination,
    ApplyResurrectionSickness,
    PersistCharacterState,
    EmitRevivalEvent
}

public sealed record RuntimeCombatRevivalPolicy(
    string PolicyId,
    RuntimeCombatRevivalMode Mode,
    bool RequiresCorpse,
    bool RequiresSameRoom,
    bool AllowsCombatReentry,
    int HitPointRestoreBasisPoints,
    int ManaRestoreBasisPoints,
    int EnergyRestoreBasisPoints,
    RuntimeCombatRevivalEffectDisposition EffectDisposition,
    IReadOnlyList<RuntimeCombatRevivalEventKind> OrderedEvents,
    IReadOnlyList<string> EvidenceReferences,
    RuntimeResolutionConfidence Confidence,
    string SafeFallback);

public sealed record RuntimeCombatRevivalRequest(
    string RevivalId,
    string TargetId,
    string? SourceActorId,
    string SourceDefinitionId,
    string CurrentRoomId,
    string DestinationRoomId,
    string? CorpseId,
    long WorldTick,
    bool IsDead,
    bool IsEligible,
    long? EligibilityExpiresAtTick,
    int CurrentHitPoints,
    int MaximumHitPoints,
    int CurrentMana,
    int MaximumMana,
    int CurrentEnergy,
    int MaximumEnergy,
    IReadOnlyList<string> EvidenceReferences);

public sealed record RuntimeCombatRevivalEvent(
    string EventId,
    string RevivalId,
    RuntimeCombatRevivalEventKind Kind,
    string TargetId,
    string? SourceActorId,
    string SourceDefinitionId,
    string CurrentRoomId,
    string DestinationRoomId,
    string? CorpseId,
    long WorldTick,
    int Sequence,
    int RestoredHitPoints,
    int RestoredMana,
    int RestoredEnergy,
    RuntimeCombatRevivalEffectDisposition EffectDisposition,
    bool RestoreCombatState,
    IReadOnlyList<string> EvidenceReferences);

public sealed record RuntimeCombatRevivalResult(
    RuntimeCombatRevivalRequest Request,
    IReadOnlyList<RuntimeCombatRevivalEvent> Events,
    int FinalHitPoints,
    int FinalMana,
    int FinalEnergy,
    bool CombatStateRestored,
    bool Committed,
    IReadOnlyList<string> Trace);

public interface IRuntimeCombatRevivalEventSink
{
    string SinkId { get; }
    void Prepare(RuntimeCombatRevivalEvent revivalEvent);
    void Commit(RuntimeCombatRevivalEvent revivalEvent);
    void Rollback(RuntimeCombatRevivalEvent revivalEvent);
}

public sealed class RuntimeCombatResurrectionRevivalCoordinator
{
    private readonly RuntimeCombatRevivalPolicy _policy;
    private readonly IReadOnlyDictionary<RuntimeCombatRevivalEventKind, IRuntimeCombatRevivalEventSink> _sinks;
    private readonly HashSet<string> _completed = new(StringComparer.OrdinalIgnoreCase);

    public RuntimeCombatResurrectionRevivalCoordinator(
        RuntimeCombatRevivalPolicy policy,
        IEnumerable<(RuntimeCombatRevivalEventKind Kind, IRuntimeCombatRevivalEventSink Sink)> sinks)
    {
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        ValidatePolicy(_policy);
        var materialized = (sinks ?? throw new ArgumentNullException(nameof(sinks))).ToArray();
        if (materialized.Any(x => x.Sink is null)) throw new InvalidOperationException("COMBAT_REVIVAL_SINK_REQUIRED");
        if (materialized.Select(x => x.Kind).Distinct().Count() != materialized.Length)
            throw new InvalidOperationException("COMBAT_REVIVAL_DUPLICATE_SINK_KIND");
        if (materialized.Select(x => x.Sink.SinkId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != materialized.Length)
            throw new InvalidOperationException("COMBAT_REVIVAL_DUPLICATE_SINK_ID");
        _sinks = materialized.ToDictionary(x => x.Kind, x => x.Sink);
    }

    public RuntimeCombatRevivalResult Execute(RuntimeCombatRevivalRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request, _policy);
        if (!_completed.Add(request.RevivalId))
            throw new InvalidOperationException("COMBAT_REVIVAL_ALREADY_COMPLETED");

        var hp = CalculateRestore(request.CurrentHitPoints, request.MaximumHitPoints, _policy.HitPointRestoreBasisPoints, true);
        var mana = CalculateRestore(request.CurrentMana, request.MaximumMana, _policy.ManaRestoreBasisPoints, false);
        var energy = CalculateRestore(request.CurrentEnergy, request.MaximumEnergy, _policy.EnergyRestoreBasisPoints, false);
        var events = BuildEvents(request, hp, mana, energy);
        var prepared = new List<(RuntimeCombatRevivalEvent Event, IRuntimeCombatRevivalEventSink Sink)>();
        var committed = new List<(RuntimeCombatRevivalEvent Event, IRuntimeCombatRevivalEventSink Sink)>();
        var trace = new List<string> { "revival_validated", "death_state_required", "combat_state_not_restored" };
        try
        {
            foreach (var revivalEvent in events)
            {
                if (!_sinks.TryGetValue(revivalEvent.Kind, out var sink))
                    throw new InvalidOperationException($"COMBAT_REVIVAL_SINK_UNAVAILABLE:{revivalEvent.Kind}");
                sink.Prepare(revivalEvent);
                prepared.Add((revivalEvent, sink));
                trace.Add($"prepared:{revivalEvent.Sequence}:{revivalEvent.Kind}:{sink.SinkId}");
            }
            foreach (var item in prepared)
            {
                item.Sink.Commit(item.Event);
                committed.Add(item);
                trace.Add($"committed:{item.Event.Sequence}:{item.Event.Kind}:{item.Sink.SinkId}");
            }
            trace.Add("revival_committed");
            return new RuntimeCombatRevivalResult(request, events, hp, mana, energy, false, true, trace);
        }
        catch
        {
            foreach (var item in committed.AsEnumerable().Reverse())
            {
                item.Sink.Rollback(item.Event);
                trace.Add($"rolled_back:{item.Event.Sequence}:{item.Event.Kind}:{item.Sink.SinkId}");
            }
            _completed.Remove(request.RevivalId);
            throw;
        }
    }

    private IReadOnlyList<RuntimeCombatRevivalEvent> BuildEvents(RuntimeCombatRevivalRequest request, int hp, int mana, int energy)
    {
        var evidence = _policy.EvidenceReferences.Concat(request.EvidenceReferences)
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
        return _policy.OrderedEvents.Select((kind, sequence) => new RuntimeCombatRevivalEvent(
            $"revival-event.{request.RevivalId[8..]}.{sequence:D2}", request.RevivalId, kind,
            request.TargetId, request.SourceActorId, request.SourceDefinitionId,
            request.CurrentRoomId, request.DestinationRoomId, request.CorpseId,
            request.WorldTick, sequence, hp, mana, energy, _policy.EffectDisposition,
            false, evidence)).ToArray();
    }

    private static int CalculateRestore(int current, int maximum, int basisPoints, bool requirePositive)
    {
        var restored = checked((int)((long)maximum * basisPoints / 10_000L));
        if (requirePositive && maximum > 0 && restored < 1) restored = 1;
        return Math.Clamp(Math.Max(current, restored), requirePositive && maximum > 0 ? 1 : 0, maximum);
    }

    private static void ValidatePolicy(RuntimeCombatRevivalPolicy policy)
    {
        RequireCanonical(policy.PolicyId, "revival-policy.", "COMBAT_REVIVAL_POLICY_ID_REQUIRED");
        ValidateBasisPoints(policy.HitPointRestoreBasisPoints, "COMBAT_REVIVAL_HP_POLICY_INVALID");
        ValidateBasisPoints(policy.ManaRestoreBasisPoints, "COMBAT_REVIVAL_MANA_POLICY_INVALID");
        ValidateBasisPoints(policy.EnergyRestoreBasisPoints, "COMBAT_REVIVAL_ENERGY_POLICY_INVALID");
        if (policy.HitPointRestoreBasisPoints == 0) throw new InvalidOperationException("COMBAT_REVIVAL_HP_RESTORE_REQUIRED");
        if (policy.AllowsCombatReentry) throw new InvalidOperationException("COMBAT_REVIVAL_COMBAT_REENTRY_UNPROVEN");
        if (policy.OrderedEvents is null || policy.OrderedEvents.Count == 0)
            throw new InvalidOperationException("COMBAT_REVIVAL_ORDER_REQUIRED");
        if (policy.OrderedEvents.Distinct().Count() != policy.OrderedEvents.Count)
            throw new InvalidOperationException("COMBAT_REVIVAL_DUPLICATE_EVENT_KIND");
        var required = new[] { RuntimeCombatRevivalEventKind.ValidateEligibility, RuntimeCombatRevivalEventKind.ClearDeathState,
            RuntimeCombatRevivalEventKind.RestoreHitPoints, RuntimeCombatRevivalEventKind.ApplyEffectDisposition,
            RuntimeCombatRevivalEventKind.PersistCharacterState, RuntimeCombatRevivalEventKind.EmitRevivalEvent };
        if (required.Any(x => !policy.OrderedEvents.Contains(x)))
            throw new InvalidOperationException("COMBAT_REVIVAL_REQUIRED_EVENT_MISSING");
        var order = policy.OrderedEvents.ToArray();
        if (Array.IndexOf(order, RuntimeCombatRevivalEventKind.ClearDeathState) < Array.IndexOf(order, RuntimeCombatRevivalEventKind.ValidateEligibility) ||
            Array.IndexOf(order, RuntimeCombatRevivalEventKind.PersistCharacterState) < Array.IndexOf(order, RuntimeCombatRevivalEventKind.RestoreHitPoints))
            throw new InvalidOperationException("COMBAT_REVIVAL_ORDER_INVALID");
        if (policy.EvidenceReferences.Count == 0) throw new InvalidOperationException("COMBAT_REVIVAL_EVIDENCE_REQUIRED");
        if (policy.Confidence == RuntimeResolutionConfidence.WeakHypothesis)
            throw new InvalidOperationException("COMBAT_REVIVAL_POLICY_NOT_PROMOTED");
        if (policy.Confidence == RuntimeResolutionConfidence.StronglyCorrelated && string.IsNullOrWhiteSpace(policy.SafeFallback))
            throw new InvalidOperationException("COMBAT_REVIVAL_FALLBACK_REQUIRED");
    }

    private static void ValidateRequest(RuntimeCombatRevivalRequest request, RuntimeCombatRevivalPolicy policy)
    {
        RequireCanonical(request.RevivalId, "revival.", "COMBAT_REVIVAL_ID_REQUIRED");
        RequireEntity(request.TargetId);
        if (request.SourceActorId is not null) RequireEntity(request.SourceActorId);
        if (string.IsNullOrWhiteSpace(request.SourceDefinitionId) ||
            !(request.SourceDefinitionId.StartsWith("spell.", StringComparison.OrdinalIgnoreCase) ||
              request.SourceDefinitionId.StartsWith("ability.", StringComparison.OrdinalIgnoreCase) ||
              request.SourceDefinitionId.StartsWith("item.", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("COMBAT_REVIVAL_SOURCE_DEFINITION_REQUIRED");
        RequireCanonical(request.CurrentRoomId, "room.", "COMBAT_REVIVAL_CURRENT_ROOM_REQUIRED");
        RequireCanonical(request.DestinationRoomId, "room.", "COMBAT_REVIVAL_DESTINATION_ROOM_REQUIRED");
        if (!request.IsDead) throw new InvalidOperationException("COMBAT_REVIVAL_DEAD_STATE_REQUIRED");
        if (!request.IsEligible) throw new InvalidOperationException("COMBAT_REVIVAL_NOT_ELIGIBLE");
        if (request.WorldTick < 0) throw new InvalidOperationException("COMBAT_REVIVAL_TICK_INVALID");
        if (request.EligibilityExpiresAtTick.HasValue && request.WorldTick > request.EligibilityExpiresAtTick.Value)
            throw new InvalidOperationException("COMBAT_REVIVAL_ELIGIBILITY_EXPIRED");
        if (policy.RequiresCorpse && (string.IsNullOrWhiteSpace(request.CorpseId) || !request.CorpseId.StartsWith("corpse.", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("COMBAT_REVIVAL_CORPSE_REQUIRED");
        if (policy.RequiresSameRoom && !request.CurrentRoomId.Equals(request.DestinationRoomId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("COMBAT_REVIVAL_SAME_ROOM_REQUIRED");
        ValidateResource(request.CurrentHitPoints, request.MaximumHitPoints, "COMBAT_REVIVAL_HP_INVALID");
        ValidateResource(request.CurrentMana, request.MaximumMana, "COMBAT_REVIVAL_MANA_INVALID");
        ValidateResource(request.CurrentEnergy, request.MaximumEnergy, "COMBAT_REVIVAL_ENERGY_INVALID");
        if (request.CurrentHitPoints != 0) throw new InvalidOperationException("COMBAT_REVIVAL_ZERO_HP_REQUIRED");
        if (request.EvidenceReferences.Count == 0) throw new InvalidOperationException("COMBAT_REVIVAL_REQUEST_EVIDENCE_REQUIRED");
    }

    private static void ValidateResource(int current, int maximum, string error)
    {
        if (maximum < 0 || current < 0 || current > maximum) throw new InvalidOperationException(error);
    }

    private static void ValidateBasisPoints(int value, string error)
    {
        if (value < 0 || value > 10_000) throw new InvalidOperationException(error);
    }

    private static void RequireEntity(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !(value.StartsWith("character.", StringComparison.OrdinalIgnoreCase) || value.StartsWith("npc.", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("COMBAT_REVIVAL_ENTITY_ID_REQUIRED");
    }

    private static void RequireCanonical(string value, string prefix, string error)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(error);
    }
}
