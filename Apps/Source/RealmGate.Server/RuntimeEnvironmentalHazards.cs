using System.Text.Json;

namespace RealmGate.Server;

public enum RuntimeHazardKind { Darkness, Light, Fire, Water, Drowning, Falling, Lava, Poison, Disease, Cold, Heat, AirRestriction, Terrain, Slippery, ForcedMovement, RegenerationPenalty, SpellRestriction }
public enum RuntimeHazardPhase { Enter, Exit, Periodic }
public enum RuntimeHazardPersistence { Ephemeral, WorldState }
public enum RuntimeHazardRequestKind { Damage, Status, Movement, RegenerationModifier, SpellRestriction, VisibilityModifier }

public sealed record RuntimeHazardDefinition(
    string HazardId,
    string RoomId,
    RuntimeHazardKind Kind,
    IReadOnlySet<RuntimeHazardPhase> Phases,
    TimeSpan TickInterval,
    string? ResistanceId,
    string? RequiredProtectionId,
    string? RequestId,
    int Severity,
    bool AffectsCharacters,
    bool AffectsNpcs,
    RuntimeHazardPersistence Persistence,
    bool EnabledByDefault = true);

public sealed record RuntimeHazardActorContext(
    string ActorId,
    bool IsNpc,
    IReadOnlySet<string> Resistances,
    IReadOnlySet<string> EquipmentProtections,
    IReadOnlySet<string> SpellProtections,
    IReadOnlySet<string> AbilityProtections,
    DateTimeOffset NowUtc);

public sealed record RuntimeHazardState(
    string HazardId,
    bool Enabled,
    DateTimeOffset? NextTickUtc,
    long Revision);

public sealed record RuntimeHazardRequest(
    RuntimeHazardRequestKind Kind,
    string RequestId,
    string HazardId,
    string RoomId,
    string ActorId,
    int Severity);

public sealed record RuntimeHazardDecision(
    bool Allowed,
    string Code,
    RuntimeHazardState State,
    IReadOnlyList<RuntimeHazardRequest> Requests,
    bool Protected,
    bool EvidenceOnly);

public sealed record RuntimeHazardSnapshot(int SchemaVersion, IReadOnlyList<RuntimeHazardState> Hazards, DateTimeOffset SavedUtc);

public sealed class RuntimeEnvironmentalHazardService
{
    private readonly object _gate = new();
    private readonly Dictionary<string, RuntimeHazardDefinition> _definitions;
    private readonly Dictionary<string, RuntimeHazardState> _states;
    private readonly Dictionary<string, HashSet<string>> _roomOccupants = new(StringComparer.OrdinalIgnoreCase);
    private readonly string? _persistencePath;

    public RuntimeEnvironmentalHazardService(IEnumerable<RuntimeHazardDefinition> definitions, string? persistencePath = null)
    {
        _definitions = definitions.ToDictionary(x => x.HazardId, StringComparer.OrdinalIgnoreCase);
        if (_definitions.Values.Any(x => string.IsNullOrWhiteSpace(x.HazardId) || string.IsNullOrWhiteSpace(x.RoomId)))
            throw new ArgumentException("Hazard IDs and room IDs are required.", nameof(definitions));
        if (_definitions.Values.Any(x => x.TickInterval < TimeSpan.Zero || x.Severity < 0))
            throw new ArgumentException("Hazard timing and severity cannot be negative.", nameof(definitions));
        _states = _definitions.Values.ToDictionary(x => x.HazardId, x => new RuntimeHazardState(x.HazardId, x.EnabledByDefault, null, 0), StringComparer.OrdinalIgnoreCase);
        _persistencePath = persistencePath;
        Restore();
    }

    public RuntimeHazardDecision Enter(string hazardId, RuntimeHazardActorContext actor)
    {
        lock (_gate)
        {
            if (!TryResolve(hazardId, actor, out var definition, out var state, out var rejection)) return rejection!;
            if (!definition!.Phases.Contains(RuntimeHazardPhase.Enter)) return Allow("hazard-phase-not-applicable", state!);
            TrackOccupant(definition.RoomId, actor.ActorId, true);
            var decision = Evaluate(definition, state!, actor, RuntimeHazardPhase.Enter);
            ArmTick(definition, actor.NowUtc);
            SaveLocked();
            return decision;
        }
    }

    public RuntimeHazardDecision Exit(string hazardId, RuntimeHazardActorContext actor)
    {
        lock (_gate)
        {
            if (!TryResolve(hazardId, actor, out var definition, out var state, out var rejection)) return rejection!;
            if (!definition!.Phases.Contains(RuntimeHazardPhase.Exit)) { TrackOccupant(definition.RoomId, actor.ActorId, false); return Allow("hazard-phase-not-applicable", state!); }
            var decision = Evaluate(definition, state!, actor, RuntimeHazardPhase.Exit);
            TrackOccupant(definition.RoomId, actor.ActorId, false);
            return decision;
        }
    }

    public IReadOnlyList<RuntimeHazardRequest> Tick(DateTimeOffset nowUtc, Func<string, RuntimeHazardActorContext?> actorResolver)
    {
        lock (_gate)
        {
            var requests = new List<RuntimeHazardRequest>();
            foreach (var definition in _definitions.Values.Where(x => x.Phases.Contains(RuntimeHazardPhase.Periodic)))
            {
                var state = _states[definition.HazardId];
                if (!state.Enabled || state.NextTickUtc is null || state.NextTickUtc > nowUtc) continue;
                if (_roomOccupants.TryGetValue(definition.RoomId, out var occupants))
                {
                    foreach (var actorId in occupants.ToArray())
                    {
                        var actor = actorResolver(actorId);
                        if (actor is null || !Eligible(definition, actor)) continue;
                        requests.AddRange(Evaluate(definition, state, actor with { NowUtc = nowUtc }, RuntimeHazardPhase.Periodic).Requests);
                    }
                }
                var next = definition.TickInterval > TimeSpan.Zero ? nowUtc + definition.TickInterval : (DateTimeOffset?)null;
                _states[definition.HazardId] = state with { NextTickUtc = next, Revision = state.Revision + 1 };
            }
            SaveLocked();
            return requests;
        }
    }

    public RuntimeHazardDecision SetEnabled(string hazardId, bool enabled, DateTimeOffset nowUtc)
    {
        lock (_gate)
        {
            if (!_definitions.TryGetValue(hazardId, out var definition)) return Missing(hazardId);
            var state = _states[hazardId];
            var next = enabled && definition.Phases.Contains(RuntimeHazardPhase.Periodic) && definition.TickInterval > TimeSpan.Zero ? (DateTimeOffset?)(nowUtc + definition.TickInterval) : null;
            state = state with { Enabled = enabled, NextTickUtc = next, Revision = state.Revision + 1 };
            _states[hazardId] = state; SaveLocked(); return Allow(enabled ? "hazard-enabled" : "hazard-disabled", state);
        }
    }

    public IReadOnlyList<string> ValidateState()
    {
        lock (_gate)
        {
            var errors = new List<string>();
            foreach (var (id, state) in _states)
            {
                if (!_definitions.ContainsKey(id)) errors.Add("orphan-state:" + id);
                if (state.Revision < 0) errors.Add("negative-revision:" + id);
            }
            foreach (var room in _roomOccupants.Where(x => string.IsNullOrWhiteSpace(x.Key))) errors.Add("invalid-room-occupancy:" + room.Key);
            return errors;
        }
    }

    private RuntimeHazardDecision Evaluate(RuntimeHazardDefinition definition, RuntimeHazardState state, RuntimeHazardActorContext actor, RuntimeHazardPhase phase)
    {
        if (!state.Enabled) return Allow("hazard-disabled", state);
        if (!Eligible(definition, actor)) return Allow("hazard-actor-ineligible", state);
        if (IsProtected(definition, actor)) return new(true, "hazard-protected", state, Array.Empty<RuntimeHazardRequest>(), true, false);
        if (definition.ResistanceId is not null && actor.Resistances.Contains(definition.ResistanceId)) return new(true, "hazard-resisted", state, Array.Empty<RuntimeHazardRequest>(), true, true);
        if (string.IsNullOrWhiteSpace(definition.RequestId)) return Allow("hazard-presentation-only", state, true);
        return new(true, "hazard-requested", state, new[] { new RuntimeHazardRequest(MapRequestKind(definition.Kind), definition.RequestId!, definition.HazardId, definition.RoomId, actor.ActorId, definition.Severity) }, false, true);
    }

    private bool TryResolve(string hazardId, RuntimeHazardActorContext actor, out RuntimeHazardDefinition? definition, out RuntimeHazardState? state, out RuntimeHazardDecision? rejection)
    {
        if (!_definitions.TryGetValue(hazardId, out definition)) { state = null; rejection = Missing(hazardId); return false; }
        state = _states[hazardId]; rejection = null; return true;
    }

    private static bool Eligible(RuntimeHazardDefinition definition, RuntimeHazardActorContext actor) => actor.IsNpc ? definition.AffectsNpcs : definition.AffectsCharacters;
    private static bool IsProtected(RuntimeHazardDefinition definition, RuntimeHazardActorContext actor) => definition.RequiredProtectionId is not null && (actor.EquipmentProtections.Contains(definition.RequiredProtectionId) || actor.SpellProtections.Contains(definition.RequiredProtectionId) || actor.AbilityProtections.Contains(definition.RequiredProtectionId));
    private static RuntimeHazardRequestKind MapRequestKind(RuntimeHazardKind kind) => kind switch
    {
        RuntimeHazardKind.Darkness or RuntimeHazardKind.Light => RuntimeHazardRequestKind.VisibilityModifier,
        RuntimeHazardKind.Falling or RuntimeHazardKind.Slippery or RuntimeHazardKind.ForcedMovement => RuntimeHazardRequestKind.Movement,
        RuntimeHazardKind.RegenerationPenalty => RuntimeHazardRequestKind.RegenerationModifier,
        RuntimeHazardKind.SpellRestriction => RuntimeHazardRequestKind.SpellRestriction,
        RuntimeHazardKind.Poison or RuntimeHazardKind.Disease => RuntimeHazardRequestKind.Status,
        _ => RuntimeHazardRequestKind.Damage
    };
    private void TrackOccupant(string roomId, string actorId, bool add) { if (!_roomOccupants.TryGetValue(roomId, out var set)) _roomOccupants[roomId] = set = new(StringComparer.OrdinalIgnoreCase); if (add) set.Add(actorId); else set.Remove(actorId); }
    private void ArmTick(RuntimeHazardDefinition definition, DateTimeOffset nowUtc) { if (!definition.Phases.Contains(RuntimeHazardPhase.Periodic) || definition.TickInterval <= TimeSpan.Zero) return; var state = _states[definition.HazardId]; if (state.NextTickUtc is null) _states[definition.HazardId] = state with { NextTickUtc = nowUtc + definition.TickInterval, Revision = state.Revision + 1 }; }
    private RuntimeHazardDecision Missing(string id) => new(false, "hazard-not-found", new RuntimeHazardState(id, false, null, 0), Array.Empty<RuntimeHazardRequest>(), false, false);
    private static RuntimeHazardDecision Allow(string code, RuntimeHazardState state, bool evidenceOnly = false) => new(true, code, state, Array.Empty<RuntimeHazardRequest>(), false, evidenceOnly);

    private void Restore()
    {
        if (string.IsNullOrWhiteSpace(_persistencePath) || !File.Exists(_persistencePath)) return;
        try
        {
            var snapshot = JsonSerializer.Deserialize<RuntimeHazardSnapshot>(File.ReadAllText(_persistencePath));
            if (snapshot?.SchemaVersion != 1) return;
            foreach (var state in snapshot.Hazards)
                if (_definitions.TryGetValue(state.HazardId, out var definition) && definition.Persistence == RuntimeHazardPersistence.WorldState)
                    _states[state.HazardId] = state;
        }
        catch { }
    }
    private void SaveLocked()
    {
        if (string.IsNullOrWhiteSpace(_persistencePath)) return;
        var states = _states.Values.Where(x => _definitions[x.HazardId].Persistence == RuntimeHazardPersistence.WorldState).ToArray();
        var snapshot = new RuntimeHazardSnapshot(1, states, DateTimeOffset.UtcNow);
        Directory.CreateDirectory(Path.GetDirectoryName(_persistencePath) ?? ".");
        var temp = _persistencePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, _persistencePath, true);
    }
}
