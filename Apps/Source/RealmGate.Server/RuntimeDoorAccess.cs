using System.Text.Json;

namespace RealmGate.Server;

public enum RuntimeDoorPersistence { Ephemeral, WorldState }
public enum RuntimeDoorAction { Inspect, Open, Close, Lock, Unlock, Pick, Bash, Traverse, Reset }

public sealed record RuntimeDoorDefinition(
    string DoorId,
    string ExitId,
    string FromRoomId,
    string ToRoomId,
    string DisplayName,
    bool InitiallyOpen,
    bool InitiallyLocked,
    bool InitiallyBroken,
    string? RequiredKeyId,
    bool ConsumableKey,
    int? MinimumLevel,
    IReadOnlySet<string> AllowedRaceIds,
    IReadOnlySet<string> AllowedClassIds,
    IReadOnlySet<string> AllowedAlignmentIds,
    IReadOnlySet<string> AllowedFactionIds,
    IReadOnlySet<string> RequiredQuestFlags,
    IReadOnlySet<string> RequiredAbilityIds,
    IReadOnlySet<string> RequiredSpellIds,
    TimeOnly? AvailableFrom,
    TimeOnly? AvailableUntil,
    string? LinkedDoorId,
    TimeSpan? ResetAfter,
    RuntimeDoorPersistence Persistence,
    bool Pickable,
    bool Bashable);

public sealed record RuntimeDoorActorContext(
    string ActorId,
    int Level,
    string? RaceId,
    string? ClassId,
    string? AlignmentId,
    IReadOnlySet<string> FactionIds,
    IReadOnlySet<string> QuestFlags,
    IReadOnlySet<string> AbilityIds,
    IReadOnlySet<string> SpellIds,
    IReadOnlyDictionary<string, int> ItemCounts,
    DateTimeOffset NowUtc);

public sealed record RuntimeDoorState(
    string DoorId,
    bool Open,
    bool Locked,
    bool Broken,
    DateTimeOffset? ResetAtUtc,
    long Revision);

public sealed record RuntimeDoorDecision(
    bool Allowed,
    string Code,
    RuntimeDoorState State,
    string? ConsumedKeyId,
    bool TrapCheckRequired,
    bool MovementMayProceed,
    bool EvidenceOnly);

public sealed record RuntimeDoorSnapshot(int SchemaVersion, IReadOnlyList<RuntimeDoorState> Doors, DateTimeOffset SavedAtUtc);

public sealed class RuntimeDoorAccessService
{
    private readonly object _gate = new();
    private readonly Dictionary<string, RuntimeDoorDefinition> _definitions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RuntimeDoorState> _states = new(StringComparer.OrdinalIgnoreCase);
    private readonly string? _persistencePath;

    public RuntimeDoorAccessService(IEnumerable<RuntimeDoorDefinition> definitions, string? persistencePath = null)
    {
        _persistencePath = persistencePath;
        foreach (var definition in definitions)
        {
            ValidateDefinition(definition);
            if (!_definitions.TryAdd(definition.DoorId, definition)) throw new InvalidOperationException($"duplicate-door-id:{definition.DoorId}");
            _states[definition.DoorId] = InitialState(definition, DateTimeOffset.UtcNow);
        }
        ValidateLinks();
        Restore();
    }

    public IReadOnlyList<RuntimeDoorState> CaptureStates()
    {
        lock (_gate) return _states.Values.OrderBy(x => x.DoorId, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public RuntimeDoorDecision EvaluateTraversal(string doorId, RuntimeDoorActorContext actor)
    {
        lock (_gate)
        {
            TickLocked(actor.NowUtc);
            if (!TryResolve(doorId, out var definition, out var state)) return Missing();
            var access = EvaluateAccess(definition, actor, state);
            if (access is not null) return access;
            if (state.Broken) return Allow("door-broken-passable", state, trap: true, movement: true);
            if (state.Locked) return Deny("door-locked", state);
            if (!state.Open) return Deny("door-closed", state);
            return Allow("door-open", state, trap: true, movement: true);
        }
    }

    public RuntimeDoorDecision Act(string doorId, RuntimeDoorAction action, RuntimeDoorActorContext actor, string? suppliedKeyId = null)
    {
        lock (_gate)
        {
            TickLocked(actor.NowUtc);
            if (!TryResolve(doorId, out var definition, out var state)) return Missing();
            if (action == RuntimeDoorAction.Inspect) return Allow("door-inspected", state);
            if (action == RuntimeDoorAction.Traverse) return EvaluateTraversal(doorId, actor);
            if (action != RuntimeDoorAction.Reset)
            {
                var access = EvaluateAccess(definition, actor, state);
                if (access is not null) return access;
            }

            RuntimeDoorState next;
            string code;
            string? consumed = null;
            bool evidenceOnly = false;
            switch (action)
            {
                case RuntimeDoorAction.Open:
                    if (state.Broken) return Deny("door-broken", state);
                    if (state.Locked) return Deny("door-locked", state);
                    if (state.Open) return Deny("door-already-open", state);
                    next = state with { Open = true, Revision = state.Revision + 1 };
                    code = "door-opened";
                    break;
                case RuntimeDoorAction.Close:
                    if (state.Broken) return Deny("door-broken", state);
                    if (!state.Open) return Deny("door-already-closed", state);
                    next = state with { Open = false, Revision = state.Revision + 1 };
                    code = "door-closed";
                    break;
                case RuntimeDoorAction.Lock:
                    if (state.Broken) return Deny("door-broken", state);
                    if (state.Open) return Deny("door-must-be-closed", state);
                    if (state.Locked) return Deny("door-already-locked", state);
                    var lockKey = ValidateKey(definition, actor, suppliedKeyId);
                    if (lockKey is not null) return lockKey;
                    next = state with { Locked = true, Revision = state.Revision + 1 };
                    code = "door-locked";
                    consumed = definition.ConsumableKey ? definition.RequiredKeyId : null;
                    break;
                case RuntimeDoorAction.Unlock:
                    if (state.Broken) return Deny("door-broken", state);
                    if (!state.Locked) return Deny("door-already-unlocked", state);
                    var unlockKey = ValidateKey(definition, actor, suppliedKeyId);
                    if (unlockKey is not null) return unlockKey;
                    next = state with { Locked = false, Revision = state.Revision + 1 };
                    code = "door-unlocked";
                    consumed = definition.ConsumableKey ? definition.RequiredKeyId : null;
                    break;
                case RuntimeDoorAction.Pick:
                    if (!definition.Pickable) return Deny("door-not-pickable", state, evidenceOnly: true);
                    if (!state.Locked) return Deny("door-already-unlocked", state);
                    if (!actor.AbilityIds.Contains("lockpick")) return Deny("lockpick-ability-required", state);
                    next = state with { Locked = false, Revision = state.Revision + 1 };
                    code = "door-picked-request-approved";
                    evidenceOnly = true;
                    break;
                case RuntimeDoorAction.Bash:
                    if (!definition.Bashable) return Deny("door-not-bashable", state, evidenceOnly: true);
                    if (!actor.AbilityIds.Contains("bash-door")) return Deny("bash-door-ability-required", state);
                    next = state with { Open = true, Locked = false, Broken = true, Revision = state.Revision + 1 };
                    code = "door-bash-request-approved";
                    evidenceOnly = true;
                    break;
                case RuntimeDoorAction.Reset:
                    next = InitialState(definition, actor.NowUtc) with { Revision = state.Revision + 1 };
                    code = "door-reset";
                    break;
                default:
                    return Deny("door-action-unsupported", state, evidenceOnly: true);
            }
            next = ScheduleReset(definition, next, actor.NowUtc);
            ApplyStateLocked(definition, next, actor.NowUtc);
            SaveLocked();
            return new(true, code, _states[doorId], consumed, false, false, evidenceOnly);
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
                if (state.Open && state.Locked) failures.Add("open-and-locked:" + pair.Key);
                if (state.Broken && (state.Locked || !state.Open)) failures.Add("broken-state-invalid:" + pair.Key);
                if (pair.Value.LinkedDoorId is { Length: > 0 } linked && _states.TryGetValue(linked, out var other)
                    && (state.Open != other.Open || state.Locked != other.Locked || state.Broken != other.Broken)) failures.Add("linked-state-diverged:" + pair.Key);
            }
            return failures;
        }
    }

    private RuntimeDoorDecision? EvaluateAccess(RuntimeDoorDefinition d, RuntimeDoorActorContext a, RuntimeDoorState s)
    {
        if (d.MinimumLevel is int minimum && a.Level < minimum) return Deny("access-level-restricted", s);
        if (!MatchesOptional(d.AllowedRaceIds, a.RaceId)) return Deny("access-race-restricted", s);
        if (!MatchesOptional(d.AllowedClassIds, a.ClassId)) return Deny("access-class-restricted", s);
        if (!MatchesOptional(d.AllowedAlignmentIds, a.AlignmentId)) return Deny("access-alignment-restricted", s);
        if (d.AllowedFactionIds.Count > 0 && !d.AllowedFactionIds.Overlaps(a.FactionIds)) return Deny("access-faction-restricted", s);
        if (!d.RequiredQuestFlags.IsSubsetOf(a.QuestFlags)) return Deny("access-quest-state-restricted", s);
        if (!d.RequiredAbilityIds.IsSubsetOf(a.AbilityIds)) return Deny("access-ability-restricted", s);
        if (!d.RequiredSpellIds.IsSubsetOf(a.SpellIds)) return Deny("access-spell-restricted", s);
        if (!WithinWindow(d, TimeOnly.FromDateTime(a.NowUtc.UtcDateTime))) return Deny("access-time-restricted", s);
        return null;
    }

    private RuntimeDoorDecision? ValidateKey(RuntimeDoorDefinition d, RuntimeDoorActorContext a, string? suppliedKeyId)
    {
        if (string.IsNullOrWhiteSpace(d.RequiredKeyId)) return Deny("door-key-definition-missing", _states[d.DoorId], evidenceOnly: true);
        if (!d.RequiredKeyId.Equals(suppliedKeyId, StringComparison.OrdinalIgnoreCase)) return Deny("door-key-mismatch", _states[d.DoorId]);
        return !a.ItemCounts.TryGetValue(d.RequiredKeyId, out var count) || count < 1 ? Deny("door-key-not-owned", _states[d.DoorId]) : null;
    }

    private void ApplyStateLocked(RuntimeDoorDefinition definition, RuntimeDoorState state, DateTimeOffset nowUtc)
    {
        _states[definition.DoorId] = state;
        if (definition.LinkedDoorId is not { Length: > 0 } linked || !_definitions.TryGetValue(linked, out var linkedDefinition)) return;
        var linkedState = state with { DoorId = linked, Revision = _states[linked].Revision + 1 };
        _states[linked] = ScheduleReset(linkedDefinition, linkedState, nowUtc);
    }

    private IReadOnlyList<string> TickLocked(DateTimeOffset nowUtc)
    {
        var reset = new List<string>();
        foreach (var state in _states.Values.Where(x => x.ResetAtUtc is not null && x.ResetAtUtc <= nowUtc).ToArray())
        {
            var definition = _definitions[state.DoorId];
            ApplyStateLocked(definition, InitialState(definition, nowUtc) with { Revision = state.Revision + 1 }, nowUtc);
            reset.Add(state.DoorId);
        }
        if (reset.Count > 0) SaveLocked();
        return reset;
    }

    private RuntimeDoorState ScheduleReset(RuntimeDoorDefinition d, RuntimeDoorState state, DateTimeOffset nowUtc)
        => d.ResetAfter is null ? state with { ResetAtUtc = null } : state with { ResetAtUtc = nowUtc + d.ResetAfter };
    private static RuntimeDoorState InitialState(RuntimeDoorDefinition d, DateTimeOffset nowUtc)
        => new(d.DoorId, d.InitiallyBroken ? true : d.InitiallyOpen, d.InitiallyBroken ? false : d.InitiallyLocked, d.InitiallyBroken, null, 0);
    private bool TryResolve(string id, out RuntimeDoorDefinition d, out RuntimeDoorState s)
    {
        var definitionFound = _definitions.TryGetValue(id, out d!);
        var stateFound = _states.TryGetValue(id, out s!);
        return definitionFound && stateFound;
    }
    private static bool MatchesOptional(IReadOnlySet<string> allowed, string? value) => allowed.Count == 0 || (!string.IsNullOrWhiteSpace(value) && allowed.Contains(value));
    private static bool WithinWindow(RuntimeDoorDefinition d, TimeOnly time) => d.AvailableFrom is null || d.AvailableUntil is null || (d.AvailableFrom <= d.AvailableUntil ? time >= d.AvailableFrom && time <= d.AvailableUntil : time >= d.AvailableFrom || time <= d.AvailableUntil);
    private static RuntimeDoorDecision Missing() => new(false, "door-not-found", new("missing", false, false, false, null, 0), null, false, false, false);
    private static RuntimeDoorDecision Deny(string code, RuntimeDoorState state, bool evidenceOnly = false) => new(false, code, state, null, false, false, evidenceOnly);
    private static RuntimeDoorDecision Allow(string code, RuntimeDoorState state, bool trap = false, bool movement = false) => new(true, code, state, null, trap, movement, false);

    private static void ValidateDefinition(RuntimeDoorDefinition d)
    {
        if (string.IsNullOrWhiteSpace(d.DoorId) || string.IsNullOrWhiteSpace(d.ExitId) || string.IsNullOrWhiteSpace(d.FromRoomId) || string.IsNullOrWhiteSpace(d.ToRoomId)) throw new InvalidOperationException("door-required-field-missing");
        if (d.InitiallyOpen && d.InitiallyLocked) throw new InvalidOperationException("door-initially-open-and-locked:" + d.DoorId);
        if (d.InitiallyBroken && (d.InitiallyLocked || !d.InitiallyOpen)) throw new InvalidOperationException("door-initial-broken-state-invalid:" + d.DoorId);
        if (d.ConsumableKey && string.IsNullOrWhiteSpace(d.RequiredKeyId)) throw new InvalidOperationException("consumable-key-id-missing:" + d.DoorId);
        if ((d.AvailableFrom is null) != (d.AvailableUntil is null)) throw new InvalidOperationException("door-time-window-incomplete:" + d.DoorId);
    }

    private void ValidateLinks()
    {
        foreach (var d in _definitions.Values)
        {
            if (d.LinkedDoorId is not { Length: > 0 } linked) continue;
            if (!_definitions.TryGetValue(linked, out var other)) throw new InvalidOperationException("linked-door-missing:" + d.DoorId);
            if (!string.Equals(other.LinkedDoorId, d.DoorId, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("linked-door-not-reciprocal:" + d.DoorId);
        }
    }

    private void Restore()
    {
        if (string.IsNullOrWhiteSpace(_persistencePath) || !File.Exists(_persistencePath)) return;
        try
        {
            var snapshot = JsonSerializer.Deserialize<RuntimeDoorSnapshot>(File.ReadAllText(_persistencePath));
            if (snapshot?.SchemaVersion != 1) return;
            foreach (var state in snapshot.Doors) if (_definitions.ContainsKey(state.DoorId) && _definitions[state.DoorId].Persistence == RuntimeDoorPersistence.WorldState) _states[state.DoorId] = state;
        }
        catch (JsonException) { }
    }

    private void SaveLocked()
    {
        if (string.IsNullOrWhiteSpace(_persistencePath)) return;
        var persisted = _states.Values.Where(x => _definitions[x.DoorId].Persistence == RuntimeDoorPersistence.WorldState).ToArray();
        Directory.CreateDirectory(Path.GetDirectoryName(_persistencePath)!);
        var temp = _persistencePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(new RuntimeDoorSnapshot(1, persisted, DateTimeOffset.UtcNow), new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, _persistencePath, true);
    }
}
