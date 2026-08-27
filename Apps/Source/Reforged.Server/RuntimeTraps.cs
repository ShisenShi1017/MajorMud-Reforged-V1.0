using System.Text.Json;

namespace Reforged.Server;

public enum RuntimeTrapLocation { Room, Exit, Door, Container, Item, Quest, Script, Spell }
public enum RuntimeTrapTrigger { Enter, Exit, Open, Close, Unlock, Take, Use, Search, Script }
public enum RuntimeTrapPersistence { Ephemeral, WorldState }

public sealed record RuntimeTrapDefinition(
    string TrapId,
    RuntimeTrapLocation Location,
    string OwnerId,
    IReadOnlySet<RuntimeTrapTrigger> Triggers,
    int DetectionDifficulty,
    int DisarmDifficulty,
    int InitialCharges,
    TimeSpan? RearmAfter,
    TimeSpan? ResetAfter,
    RuntimeTrapPersistence Persistence,
    bool Hidden,
    string? DamageRequestId,
    string? StatusRequestId,
    string? TeleportRequestId,
    string? SummonRequestId,
    string? AlarmRequestId,
    string? DoorStateRequest,
    string? QuestStateRequest);

public sealed record RuntimeTrapActorContext(
    string ActorId,
    int DetectionScore,
    int DisarmScore,
    IReadOnlySet<string> AbilityIds,
    IReadOnlySet<string> ProtectionIds,
    DateTimeOffset NowUtc);

public sealed record RuntimeTrapState(
    string TrapId,
    bool Armed,
    bool Revealed,
    int ChargesRemaining,
    DateTimeOffset? RearmAtUtc,
    DateTimeOffset? ResetAtUtc,
    long Revision);

public sealed record RuntimeTrapEffectRequest(string Kind, string RequestId, string TrapId, string ActorId);

public sealed record RuntimeTrapDecision(
    bool Allowed,
    string Code,
    RuntimeTrapState State,
    IReadOnlyList<RuntimeTrapEffectRequest> EffectRequests,
    bool Triggered,
    bool EvidenceOnly);

public sealed record RuntimeTrapSnapshot(int SchemaVersion, IReadOnlyList<RuntimeTrapState> Traps, DateTimeOffset SavedAtUtc);

/// <summary>
/// Generic Reforged trap service for Custom/AI/extensible content. This is not the Classic
/// MajorMUD exit-trap parity authority. Classic type-09/type-24 exits are owned by
/// RuntimeClassicExitTrapCatalog and fail closed where native WCCMP002 state/roll evidence is absent.
/// </summary>
public sealed class RuntimeTrapService
{
    private readonly object _gate = new();
    private readonly Dictionary<string, RuntimeTrapDefinition> _definitions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RuntimeTrapState> _states = new(StringComparer.OrdinalIgnoreCase);
    private readonly string? _persistencePath;

    public RuntimeTrapService(IEnumerable<RuntimeTrapDefinition> definitions, string? persistencePath = null)
    {
        _persistencePath = persistencePath;
        foreach (var definition in definitions)
        {
            ValidateDefinition(definition);
            if (!_definitions.TryAdd(definition.TrapId, definition)) throw new InvalidOperationException($"duplicate-trap-id:{definition.TrapId}");
            _states[definition.TrapId] = InitialState(definition, DateTimeOffset.UtcNow);
        }
        Restore();
    }

    public RuntimeTrapDecision Search(string trapId, RuntimeTrapActorContext actor)
    {
        lock (_gate)
        {
            TickLocked(actor.NowUtc);
            if (!TryResolve(trapId, out var definition, out var state)) return Missing();
            if (state.Revealed) return Allow("trap-already-revealed", state);
            if (!state.Armed) return Deny("trap-inactive", state);
            if (!actor.AbilityIds.Contains("detect-traps")) return Deny("detect-traps-ability-required", state);
            if (actor.DetectionScore < definition.DetectionDifficulty) return Deny("trap-not-detected", state, evidenceOnly: true);
            var next = state with { Revealed = true, Revision = state.Revision + 1 };
            _states[trapId] = next;
            SaveLocked();
            return Allow("trap-detected", next, evidenceOnly: true);
        }
    }

    public RuntimeTrapDecision Disarm(string trapId, RuntimeTrapActorContext actor)
    {
        lock (_gate)
        {
            TickLocked(actor.NowUtc);
            if (!TryResolve(trapId, out var definition, out var state)) return Missing();
            if (!state.Armed) return Deny("trap-already-disarmed", state);
            if (!state.Revealed) return Deny("trap-not-visible", state);
            if (!actor.AbilityIds.Contains("disarm-traps")) return Deny("disarm-traps-ability-required", state);
            if (actor.DisarmScore < definition.DisarmDifficulty) return Deny("trap-disarm-failed", state, evidenceOnly: true);
            var next = state with
            {
                Armed = false,
                RearmAtUtc = definition.RearmAfter is null ? null : actor.NowUtc + definition.RearmAfter,
                Revision = state.Revision + 1
            };
            _states[trapId] = next;
            SaveLocked();
            return Allow("trap-disarmed", next, evidenceOnly: true);
        }
    }

    public RuntimeTrapDecision Trigger(string trapId, RuntimeTrapTrigger trigger, RuntimeTrapActorContext actor)
    {
        lock (_gate)
        {
            TickLocked(actor.NowUtc);
            if (!TryResolve(trapId, out var definition, out var state)) return Missing();
            if (!definition.Triggers.Contains(trigger)) return Deny("trap-trigger-not-applicable", state);
            if (!state.Armed || state.ChargesRemaining <= 0) return Deny("trap-inactive", state);
            if (actor.ProtectionIds.Contains("trap-immunity")) return Deny("trap-protected", state);

            var requests = BuildRequests(definition, actor.ActorId);
            var remaining = Math.Max(0, state.ChargesRemaining - 1);
            var armed = remaining > 0;
            var next = state with
            {
                Armed = armed,
                Revealed = true,
                ChargesRemaining = remaining,
                RearmAtUtc = !armed && definition.RearmAfter is not null ? actor.NowUtc + definition.RearmAfter : null,
                Revision = state.Revision + 1
            };
            _states[trapId] = next;
            SaveLocked();
            return new(true, "trap-triggered", next, requests, true, false);
        }
    }

    public RuntimeTrapDecision Reset(string trapId, DateTimeOffset nowUtc)
    {
        lock (_gate)
        {
            if (!TryResolve(trapId, out var definition, out var state)) return Missing();
            var next = InitialState(definition, nowUtc) with { Revision = state.Revision + 1 };
            _states[trapId] = next;
            SaveLocked();
            return Allow("trap-reset", next);
        }
    }

    public IReadOnlyList<string> Tick(DateTimeOffset nowUtc)
    {
        lock (_gate) return TickLocked(nowUtc);
    }

    public IReadOnlyList<string> ValidateState()
    {
        lock (_gate)
        {
            var failures = new List<string>();
            foreach (var pair in _definitions)
            {
                if (!_states.TryGetValue(pair.Key, out var state)) { failures.Add("missing-state:" + pair.Key); continue; }
                if (state.ChargesRemaining < 0 || state.ChargesRemaining > pair.Value.InitialCharges) failures.Add("invalid-charges:" + pair.Key);
                if (state.Armed && state.ChargesRemaining == 0) failures.Add("armed-without-charges:" + pair.Key);
            }
            return failures;
        }
    }

    private IReadOnlyList<string> TickLocked(DateTimeOffset nowUtc)
    {
        var changed = new List<string>();
        foreach (var definition in _definitions.Values)
        {
            var state = _states[definition.TrapId];
            if (state.ResetAtUtc is not null && state.ResetAtUtc <= nowUtc)
            {
                _states[definition.TrapId] = InitialState(definition, nowUtc) with { Revision = state.Revision + 1 };
                changed.Add(definition.TrapId + ":reset");
            }
            else if (state.RearmAtUtc is not null && state.RearmAtUtc <= nowUtc)
            {
                _states[definition.TrapId] = state with { Armed = true, ChargesRemaining = definition.InitialCharges, RearmAtUtc = null, Revision = state.Revision + 1 };
                changed.Add(definition.TrapId + ":rearmed");
            }
        }
        if (changed.Count > 0) SaveLocked();
        return changed;
    }

    private static IReadOnlyList<RuntimeTrapEffectRequest> BuildRequests(RuntimeTrapDefinition d, string actorId)
    {
        var requests = new List<RuntimeTrapEffectRequest>();
        Add("damage", d.DamageRequestId); Add("status", d.StatusRequestId); Add("teleport", d.TeleportRequestId);
        Add("summon", d.SummonRequestId); Add("alarm", d.AlarmRequestId); Add("door-state", d.DoorStateRequest); Add("quest-state", d.QuestStateRequest);
        return requests;
        void Add(string kind, string? id) { if (!string.IsNullOrWhiteSpace(id)) requests.Add(new(kind, id, d.TrapId, actorId)); }
    }

    private static RuntimeTrapState InitialState(RuntimeTrapDefinition d, DateTimeOffset nowUtc) =>
        new(d.TrapId, d.InitialCharges > 0, !d.Hidden, d.InitialCharges, null, d.ResetAfter is null ? null : nowUtc + d.ResetAfter, 0);

    private bool TryResolve(string trapId, out RuntimeTrapDefinition definition, out RuntimeTrapState state)
    {
        var definitionFound = _definitions.TryGetValue(trapId, out definition!);
        var stateFound = _states.TryGetValue(trapId, out state!);
        return definitionFound && stateFound;
    }

    private static void ValidateDefinition(RuntimeTrapDefinition d)
    {
        if (string.IsNullOrWhiteSpace(d.TrapId)) throw new InvalidOperationException("trap-id-required");
        if (string.IsNullOrWhiteSpace(d.OwnerId)) throw new InvalidOperationException("trap-owner-required:" + d.TrapId);
        if (d.Triggers.Count == 0) throw new InvalidOperationException("trap-trigger-required:" + d.TrapId);
        if (d.DetectionDifficulty < 0 || d.DisarmDifficulty < 0) throw new InvalidOperationException("trap-difficulty-invalid:" + d.TrapId);
        if (d.InitialCharges < 0) throw new InvalidOperationException("trap-charges-invalid:" + d.TrapId);
    }

    private RuntimeTrapDecision Missing() => new(false, "trap-not-found", new("missing", false, false, 0, null, null, 0), Array.Empty<RuntimeTrapEffectRequest>(), false, false);
    private static RuntimeTrapDecision Allow(string code, RuntimeTrapState state, bool evidenceOnly = false) => new(true, code, state, Array.Empty<RuntimeTrapEffectRequest>(), false, evidenceOnly);
    private static RuntimeTrapDecision Deny(string code, RuntimeTrapState state, bool evidenceOnly = false) => new(false, code, state, Array.Empty<RuntimeTrapEffectRequest>(), false, evidenceOnly);

    private void Restore()
    {
        if (string.IsNullOrWhiteSpace(_persistencePath) || !File.Exists(_persistencePath)) return;
        try
        {
            var snapshot = JsonSerializer.Deserialize<RuntimeTrapSnapshot>(File.ReadAllText(_persistencePath));
            if (snapshot?.SchemaVersion != 1) return;
            foreach (var state in snapshot.Traps)
                if (_definitions.TryGetValue(state.TrapId, out var definition) && definition.Persistence == RuntimeTrapPersistence.WorldState)
                    _states[state.TrapId] = state;
        }
        catch { /* fail safely to catalog defaults */ }
    }

    private void SaveLocked()
    {
        if (string.IsNullOrWhiteSpace(_persistencePath)) return;
        var states = _states.Values.Where(s => _definitions[s.TrapId].Persistence == RuntimeTrapPersistence.WorldState).ToArray();
        var snapshot = new RuntimeTrapSnapshot(1, states, DateTimeOffset.UtcNow);
        Directory.CreateDirectory(Path.GetDirectoryName(_persistencePath) ?? ".");
        var temp = _persistencePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, _persistencePath, true);
    }
}
