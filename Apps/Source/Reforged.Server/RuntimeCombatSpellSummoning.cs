namespace Reforged.Server;

public enum RuntimeCombatSummonOwnerKind { Character, Npc, Party, World }
public enum RuntimeCombatSummonEventKind { ValidateDefinition, ValidatePopulationLimit, ValidateDuplicateLimit, CreateSpawnRequest, AssignOwnership, RegisterDuration, RegisterFollow, InsertIntoCombat, InheritThreat, PersistSummonState, RegisterCleanup, EmitSummonedEvent }
public enum RuntimeCombatSummonCleanupReason { DurationExpired, Death, Dismissed, Dispelled, OwnerLogout, OwnerDisconnect, WorldReset, Removed }

public sealed record RuntimeCombatSummonPolicy(
    string PolicyId, RuntimeCombatSummonOwnerKind OwnerKind, bool AllowCombatInsertion,
    bool AllowThreatInheritance, bool PersistAcrossLogout, bool PersistAcrossRestart,
    int MaximumOwned, int MaximumPerRoom, int MaximumPerWorld, int MaximumDuplicateDefinition,
    long DurationTicks, IReadOnlyList<RuntimeCombatSummonEventKind> OrderedEvents,
    IReadOnlyList<RuntimeCombatSummonCleanupReason> CleanupReasons,
    IReadOnlyList<string> EvidenceReferences, RuntimeResolutionConfidence Confidence, string SafeFallback);

public sealed record RuntimeCombatSummonRequest(
    string SummonId, string SourceSpellId, string SummonDefinitionId, string OwnerId,
    string SpawnRoomId, string? CombatId, long WorldTick, int ExistingOwned,
    int ExistingInRoom, int ExistingInWorld, int ExistingDuplicateDefinitions,
    bool OwnerIsActive, bool DefinitionIsAvailable, IReadOnlyList<string> EvidenceReferences);

public sealed record RuntimeCombatSummonEvent(
    string EventId, string SummonId, RuntimeCombatSummonEventKind Kind, string SourceSpellId,
    string SummonDefinitionId, string OwnerId, string SpawnRoomId, string? CombatId,
    long WorldTick, long? ExpiresAtTick, int Sequence, bool InsertIntoCombat,
    bool InheritThreat, bool PersistAcrossLogout, bool PersistAcrossRestart,
    IReadOnlyList<RuntimeCombatSummonCleanupReason> CleanupReasons, IReadOnlyList<string> EvidenceReferences);

public sealed record RuntimeCombatSummonResult(RuntimeCombatSummonRequest Request,
    IReadOnlyList<RuntimeCombatSummonEvent> Events, long? ExpiresAtTick,
    bool InsertedIntoCombat, bool ThreatInherited, bool Committed, IReadOnlyList<string> Trace);

public interface IRuntimeCombatSummonEventSink
{
    string SinkId { get; }
    void Prepare(RuntimeCombatSummonEvent summonEvent);
    void Commit(RuntimeCombatSummonEvent summonEvent);
    void Rollback(RuntimeCombatSummonEvent summonEvent);
}

public sealed class RuntimeCombatSpellSummoningCoordinator
{
    private readonly RuntimeCombatSummonPolicy _policy;
    private readonly IReadOnlyDictionary<RuntimeCombatSummonEventKind, IRuntimeCombatSummonEventSink> _sinks;
    private readonly HashSet<string> _completed = new(StringComparer.OrdinalIgnoreCase);

    public RuntimeCombatSpellSummoningCoordinator(RuntimeCombatSummonPolicy policy,
        IEnumerable<(RuntimeCombatSummonEventKind Kind, IRuntimeCombatSummonEventSink Sink)> sinks)
    {
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        ValidatePolicy(policy);
        var materialized = (sinks ?? throw new ArgumentNullException(nameof(sinks))).ToArray();
        if (materialized.Any(x => x.Sink is null)) throw new InvalidOperationException("COMBAT_SUMMON_SINK_REQUIRED");
        if (materialized.Select(x => x.Kind).Distinct().Count() != materialized.Length) throw new InvalidOperationException("COMBAT_SUMMON_DUPLICATE_SINK_KIND");
        if (materialized.Select(x => x.Sink.SinkId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != materialized.Length) throw new InvalidOperationException("COMBAT_SUMMON_DUPLICATE_SINK_ID");
        _sinks = materialized.ToDictionary(x => x.Kind, x => x.Sink);
    }

    public RuntimeCombatSummonResult Execute(RuntimeCombatSummonRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request, _policy);
        if (!_completed.Add(request.SummonId)) throw new InvalidOperationException("COMBAT_SUMMON_ALREADY_COMPLETED");
        var expires = _policy.DurationTicks > 0 ? checked(request.WorldTick + _policy.DurationTicks) : (long?)null;
        var events = BuildEvents(request, expires);
        var prepared = new List<(RuntimeCombatSummonEvent Event, IRuntimeCombatSummonEventSink Sink)>();
        var committed = new List<(RuntimeCombatSummonEvent Event, IRuntimeCombatSummonEventSink Sink)>();
        var trace = new List<string> { "summon_validated", $"owner:{_policy.OwnerKind}", $"duration:{_policy.DurationTicks}" };
        try
        {
            foreach (var summonEvent in events)
            {
                if (!_sinks.TryGetValue(summonEvent.Kind, out var sink)) throw new InvalidOperationException($"COMBAT_SUMMON_SINK_UNAVAILABLE:{summonEvent.Kind}");
                sink.Prepare(summonEvent); prepared.Add((summonEvent, sink));
                trace.Add($"prepared:{summonEvent.Sequence}:{summonEvent.Kind}:{sink.SinkId}");
            }
            foreach (var item in prepared)
            {
                item.Sink.Commit(item.Event); committed.Add(item);
                trace.Add($"committed:{item.Event.Sequence}:{item.Event.Kind}:{item.Sink.SinkId}");
            }
            trace.Add("summon_committed");
            return new RuntimeCombatSummonResult(request, events, expires, _policy.AllowCombatInsertion && request.CombatId is not null,
                _policy.AllowThreatInheritance && request.CombatId is not null, true, trace);
        }
        catch
        {
            foreach (var item in committed.AsEnumerable().Reverse()) item.Sink.Rollback(item.Event);
            _completed.Remove(request.SummonId);
            throw;
        }
    }

    private IReadOnlyList<RuntimeCombatSummonEvent> BuildEvents(RuntimeCombatSummonRequest request, long? expires)
    {
        var evidence = _policy.EvidenceReferences.Concat(request.EvidenceReferences).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
        return _policy.OrderedEvents.Select((kind, sequence) => new RuntimeCombatSummonEvent(
            $"summon-event.{request.SummonId[7..]}.{sequence:D2}", request.SummonId, kind,
            request.SourceSpellId, request.SummonDefinitionId, request.OwnerId, request.SpawnRoomId,
            request.CombatId, request.WorldTick, expires, sequence,
            _policy.AllowCombatInsertion && request.CombatId is not null,
            _policy.AllowThreatInheritance && request.CombatId is not null,
            _policy.PersistAcrossLogout, _policy.PersistAcrossRestart,
            _policy.CleanupReasons, evidence)).ToArray();
    }

    private static void ValidatePolicy(RuntimeCombatSummonPolicy policy)
    {
        RequireCanonical(policy.PolicyId, "summon-policy.", "COMBAT_SUMMON_POLICY_ID_REQUIRED");
        if (policy.MaximumOwned < 1 || policy.MaximumPerRoom < 1 || policy.MaximumPerWorld < 1 || policy.MaximumDuplicateDefinition < 1) throw new InvalidOperationException("COMBAT_SUMMON_LIMIT_POLICY_INVALID");
        if (policy.DurationTicks < 0) throw new InvalidOperationException("COMBAT_SUMMON_DURATION_INVALID");
        if (policy.PersistAcrossRestart) throw new InvalidOperationException("COMBAT_SUMMON_RESTART_PERSISTENCE_UNPROVEN");
        if (policy.OrderedEvents is null || policy.OrderedEvents.Count == 0 || policy.OrderedEvents.Distinct().Count() != policy.OrderedEvents.Count) throw new InvalidOperationException("COMBAT_SUMMON_EVENT_ORDER_INVALID");
        var required = new[] { RuntimeCombatSummonEventKind.ValidateDefinition, RuntimeCombatSummonEventKind.ValidatePopulationLimit,
            RuntimeCombatSummonEventKind.ValidateDuplicateLimit, RuntimeCombatSummonEventKind.CreateSpawnRequest,
            RuntimeCombatSummonEventKind.AssignOwnership, RuntimeCombatSummonEventKind.RegisterCleanup, RuntimeCombatSummonEventKind.EmitSummonedEvent };
        if (required.Any(x => !policy.OrderedEvents.Contains(x))) throw new InvalidOperationException("COMBAT_SUMMON_REQUIRED_EVENT_MISSING");
        if (policy.CleanupReasons is null || policy.CleanupReasons.Count == 0 || policy.CleanupReasons.Distinct().Count() != policy.CleanupReasons.Count) throw new InvalidOperationException("COMBAT_SUMMON_CLEANUP_POLICY_INVALID");
        if (policy.EvidenceReferences.Count == 0) throw new InvalidOperationException("COMBAT_SUMMON_EVIDENCE_REQUIRED");
        if (policy.Confidence == RuntimeResolutionConfidence.WeakHypothesis) throw new InvalidOperationException("COMBAT_SUMMON_POLICY_NOT_PROMOTED");
        if (policy.Confidence == RuntimeResolutionConfidence.StronglyCorrelated && string.IsNullOrWhiteSpace(policy.SafeFallback)) throw new InvalidOperationException("COMBAT_SUMMON_FALLBACK_REQUIRED");
    }

    private static void ValidateRequest(RuntimeCombatSummonRequest request, RuntimeCombatSummonPolicy policy)
    {
        RequireCanonical(request.SummonId, "summon.", "COMBAT_SUMMON_ID_REQUIRED");
        RequireCanonical(request.SourceSpellId, "spell.", "COMBAT_SUMMON_SOURCE_SPELL_REQUIRED");
        RequireCanonical(request.SummonDefinitionId, "npc.", "COMBAT_SUMMON_DEFINITION_REQUIRED");
        RequireEntity(request.OwnerId);
        RequireCanonical(request.SpawnRoomId, "room.", "COMBAT_SUMMON_ROOM_REQUIRED");
        if (request.CombatId is not null) RequireCanonical(request.CombatId, "combat.", "COMBAT_SUMMON_COMBAT_ID_INVALID");
        if (request.WorldTick < 0) throw new InvalidOperationException("COMBAT_SUMMON_TICK_INVALID");
        if (!request.OwnerIsActive) throw new InvalidOperationException("COMBAT_SUMMON_OWNER_INACTIVE");
        if (!request.DefinitionIsAvailable) throw new InvalidOperationException("COMBAT_SUMMON_DEFINITION_UNAVAILABLE");
        if (request.ExistingOwned >= policy.MaximumOwned) throw new InvalidOperationException("COMBAT_SUMMON_OWNER_LIMIT_REACHED");
        if (request.ExistingInRoom >= policy.MaximumPerRoom) throw new InvalidOperationException("COMBAT_SUMMON_ROOM_LIMIT_REACHED");
        if (request.ExistingInWorld >= policy.MaximumPerWorld) throw new InvalidOperationException("COMBAT_SUMMON_WORLD_LIMIT_REACHED");
        if (request.ExistingDuplicateDefinitions >= policy.MaximumDuplicateDefinition) throw new InvalidOperationException("COMBAT_SUMMON_DUPLICATE_LIMIT_REACHED");
        if (request.ExistingOwned < 0 || request.ExistingInRoom < 0 || request.ExistingInWorld < 0 || request.ExistingDuplicateDefinitions < 0) throw new InvalidOperationException("COMBAT_SUMMON_COUNT_INVALID");
        if (request.CombatId is not null && !policy.AllowCombatInsertion) throw new InvalidOperationException("COMBAT_SUMMON_COMBAT_INSERTION_UNPROVEN");
        if (request.EvidenceReferences.Count == 0) throw new InvalidOperationException("COMBAT_SUMMON_REQUEST_EVIDENCE_REQUIRED");
    }

    private static void RequireEntity(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !(value.StartsWith("character.", StringComparison.OrdinalIgnoreCase) || value.StartsWith("npc.", StringComparison.OrdinalIgnoreCase) || value.StartsWith("party.", StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("COMBAT_SUMMON_OWNER_ID_REQUIRED");
    }
    private static void RequireCanonical(string value, string prefix, string error)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException(error);
    }
}
