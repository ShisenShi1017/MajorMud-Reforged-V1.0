namespace Reforged.Server;

public enum RuntimeCombatDeathEventKind
{
    CombatActionCancellation,
    StatusCleanup,
    ThreatCleanup,
    TargetCleanup,
    CombatParticipantDefeat,
    QuestDeathEvent,
    ExperienceRequest,
    LootRequest,
    GroundDropRequest,
    GoldHandlingRequest,
    RespawnRequest,
    ResurrectionEligibilityRequest
}

public sealed record RuntimeCombatDeathFanOutPolicy(
    string PolicyId,
    IReadOnlyList<RuntimeCombatDeathEventKind> OrderedEvents,
    IReadOnlyList<string> EvidenceReferences,
    RuntimeResolutionConfidence Confidence,
    string SafeFallback);

public sealed record RuntimeCombatDeathRequest(
    string DeathId,
    string CombatId,
    string VictimId,
    string? KillerId,
    string RoomId,
    long WorldTick,
    int HitPointsBefore,
    int HitPointsAfter,
    string LethalSourceId,
    IReadOnlyList<string> EvidenceReferences);

public sealed record RuntimeCombatDeathEvent(
    string EventId,
    string DeathId,
    RuntimeCombatDeathEventKind Kind,
    string CombatId,
    string VictimId,
    string? KillerId,
    string RoomId,
    long WorldTick,
    int Sequence,
    string LethalSourceId,
    IReadOnlyList<string> EvidenceReferences);

public sealed record RuntimeCombatDeathFanOutResult(
    RuntimeCombatDeathRequest Request,
    IReadOnlyList<RuntimeCombatDeathEvent> Events,
    bool Committed,
    IReadOnlyList<string> Trace);

public interface IRuntimeCombatDeathEventSink
{
    string SinkId { get; }
    void Prepare(RuntimeCombatDeathEvent deathEvent);
    void Commit(RuntimeCombatDeathEvent deathEvent);
    void Rollback(RuntimeCombatDeathEvent deathEvent);
}

public sealed class RuntimeCombatDeathFanOutCoordinator
{
    private readonly RuntimeCombatDeathFanOutPolicy _policy;
    private readonly IReadOnlyDictionary<RuntimeCombatDeathEventKind, IRuntimeCombatDeathEventSink> _sinks;
    private readonly HashSet<string> _completedDeaths = new(StringComparer.OrdinalIgnoreCase);

    public RuntimeCombatDeathFanOutCoordinator(
        RuntimeCombatDeathFanOutPolicy policy,
        IEnumerable<(RuntimeCombatDeathEventKind Kind, IRuntimeCombatDeathEventSink Sink)> sinks)
    {
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        ValidatePolicy(_policy);
        var materialized = (sinks ?? throw new ArgumentNullException(nameof(sinks))).ToArray();
        if (materialized.Any(x => x.Sink is null)) throw new InvalidOperationException("COMBAT_DEATH_SINK_REQUIRED");
        if (materialized.Select(x => x.Kind).Distinct().Count() != materialized.Length)
            throw new InvalidOperationException("COMBAT_DEATH_DUPLICATE_SINK_KIND");
        if (materialized.Select(x => x.Sink.SinkId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != materialized.Length)
            throw new InvalidOperationException("COMBAT_DEATH_DUPLICATE_SINK_ID");
        _sinks = materialized.ToDictionary(x => x.Kind, x => x.Sink);
    }

    public RuntimeCombatDeathFanOutResult Execute(RuntimeCombatDeathRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);
        if (!_completedDeaths.Add(request.DeathId))
            throw new InvalidOperationException("COMBAT_DEATH_ALREADY_FANNED_OUT");

        var events = BuildEvents(request);
        var prepared = new List<(RuntimeCombatDeathEvent Event, IRuntimeCombatDeathEventSink Sink)>();
        var committed = new List<(RuntimeCombatDeathEvent Event, IRuntimeCombatDeathEventSink Sink)>();
        var trace = new List<string> { "death_validated", "hp_clamped_before_fanout" };
        try
        {
            foreach (var deathEvent in events)
            {
                if (!_sinks.TryGetValue(deathEvent.Kind, out var sink))
                    throw new InvalidOperationException($"COMBAT_DEATH_SINK_UNAVAILABLE:{deathEvent.Kind}");
                sink.Prepare(deathEvent);
                prepared.Add((deathEvent, sink));
                trace.Add($"prepared:{deathEvent.Sequence}:{deathEvent.Kind}:{sink.SinkId}");
            }
            foreach (var item in prepared)
            {
                item.Sink.Commit(item.Event);
                committed.Add(item);
                trace.Add($"committed:{item.Event.Sequence}:{item.Event.Kind}:{item.Sink.SinkId}");
            }
            trace.Add("death_fanout_committed");
            return new RuntimeCombatDeathFanOutResult(request, events, true, trace);
        }
        catch
        {
            foreach (var item in committed.AsEnumerable().Reverse())
            {
                item.Sink.Rollback(item.Event);
                trace.Add($"rolled_back:{item.Event.Sequence}:{item.Event.Kind}:{item.Sink.SinkId}");
            }
            _completedDeaths.Remove(request.DeathId);
            throw;
        }
    }

    private IReadOnlyList<RuntimeCombatDeathEvent> BuildEvents(RuntimeCombatDeathRequest request)
    {
        var evidence = _policy.EvidenceReferences.Concat(request.EvidenceReferences)
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
        return _policy.OrderedEvents.Select((kind, sequence) => new RuntimeCombatDeathEvent(
            $"death-event.{request.DeathId[6..]}.{sequence:D2}", request.DeathId, kind,
            request.CombatId, request.VictimId, request.KillerId, request.RoomId,
            request.WorldTick, sequence, request.LethalSourceId, evidence)).ToArray();
    }

    private static void ValidatePolicy(RuntimeCombatDeathFanOutPolicy policy)
    {
        RequireCanonical(policy.PolicyId, "death-policy.", "COMBAT_DEATH_POLICY_ID_REQUIRED");
        if (policy.OrderedEvents is null || policy.OrderedEvents.Count == 0)
            throw new InvalidOperationException("COMBAT_DEATH_ORDER_REQUIRED");
        if (policy.OrderedEvents.Distinct().Count() != policy.OrderedEvents.Count)
            throw new InvalidOperationException("COMBAT_DEATH_DUPLICATE_EVENT_KIND");
        var required = new[] { RuntimeCombatDeathEventKind.CombatActionCancellation, RuntimeCombatDeathEventKind.StatusCleanup,
            RuntimeCombatDeathEventKind.ThreatCleanup, RuntimeCombatDeathEventKind.TargetCleanup,
            RuntimeCombatDeathEventKind.CombatParticipantDefeat };
        if (required.Any(x => !policy.OrderedEvents.Contains(x)))
            throw new InvalidOperationException("COMBAT_DEATH_REQUIRED_EVENT_MISSING");
        if (Array.IndexOf(policy.OrderedEvents.ToArray(), RuntimeCombatDeathEventKind.CombatParticipantDefeat) <
            Array.IndexOf(policy.OrderedEvents.ToArray(), RuntimeCombatDeathEventKind.CombatActionCancellation))
            throw new InvalidOperationException("COMBAT_DEATH_ORDER_INVALID");
        if (policy.EvidenceReferences.Count == 0) throw new InvalidOperationException("COMBAT_DEATH_EVIDENCE_REQUIRED");
        if (policy.Confidence == RuntimeResolutionConfidence.WeakHypothesis)
            throw new InvalidOperationException("COMBAT_DEATH_POLICY_NOT_PROMOTED");
        if (policy.Confidence == RuntimeResolutionConfidence.StronglyCorrelated && string.IsNullOrWhiteSpace(policy.SafeFallback))
            throw new InvalidOperationException("COMBAT_DEATH_FALLBACK_REQUIRED");
    }

    private static void ValidateRequest(RuntimeCombatDeathRequest request)
    {
        RequireCanonical(request.DeathId, "death.", "COMBAT_DEATH_ID_REQUIRED");
        RequireCanonical(request.CombatId, "combat.", "COMBAT_DEATH_COMBAT_ID_REQUIRED");
        RequireEntity(request.VictimId);
        if (request.KillerId is not null) RequireEntity(request.KillerId);
        RequireCanonical(request.RoomId, "room.", "COMBAT_DEATH_ROOM_ID_REQUIRED");
        if (request.WorldTick < 0) throw new InvalidOperationException("COMBAT_DEATH_TICK_INVALID");
        if (request.HitPointsBefore <= 0 || request.HitPointsAfter != 0)
            throw new InvalidOperationException("COMBAT_DEATH_HP_CLAMP_REQUIRED");
        if (string.IsNullOrWhiteSpace(request.LethalSourceId)) throw new InvalidOperationException("COMBAT_DEATH_SOURCE_REQUIRED");
        if (request.EvidenceReferences.Count == 0) throw new InvalidOperationException("COMBAT_DEATH_REQUEST_EVIDENCE_REQUIRED");
    }

    private static void RequireEntity(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !(value.StartsWith("character.", StringComparison.OrdinalIgnoreCase) || value.StartsWith("npc.", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("COMBAT_DEATH_ENTITY_ID_REQUIRED");
    }

    private static void RequireCanonical(string value, string prefix, string error)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(error);
    }
}
