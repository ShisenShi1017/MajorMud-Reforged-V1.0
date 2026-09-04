namespace RealmGate.Server;

public enum RuntimeCombatParticipantKind { Character, Npc }
public enum RuntimeCombatState { Active, Completed, Cancelled }
public enum RuntimeCombatActionKind { Attack, Spell, Pass, Flee }

public sealed record RuntimeCombatParticipant(
    string ParticipantId,
    RuntimeCombatParticipantKind Kind,
    string RoomId,
    int CurrentHitPoints,
    int MaximumHitPoints,
    bool IsAlive = true,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record RuntimeCombatAction(
    string ActionId,
    string ActorId,
    RuntimeCombatActionKind Kind,
    string? TargetId = null,
    string? DefinitionId = null,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record RuntimeCombatEvent(
    string EventId,
    string SessionId,
    int Round,
    string EventType,
    string ActorId,
    string? TargetId,
    int Magnitude,
    DateTimeOffset OccurredAtUtc,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record RuntimeCombatActionResult(
    bool Succeeded,
    string? FailureCode,
    string? FailureMessage,
    IReadOnlyList<RuntimeCombatEvent> Events);

public sealed record RuntimeCombatSnapshot(
    string SessionId,
    string RoomId,
    RuntimeCombatState State,
    int Round,
    IReadOnlyList<RuntimeCombatParticipant> Participants,
    IReadOnlyList<string> InitiativeOrder,
    IReadOnlyList<RuntimeCombatEvent> EventLog);

public interface IRuntimeCombatInitiativePolicy
{
    IReadOnlyList<string> Order(IReadOnlyCollection<RuntimeCombatParticipant> participants);
}

public interface IRuntimeCombatActionPolicy
{
    RuntimeCombatActionResult Resolve(RuntimeCombatSnapshot snapshot, RuntimeCombatAction action, DateTimeOffset nowUtc);
}

public interface IRuntimeCombatEventSink
{
    void Apply(RuntimeCombatEvent combatEvent);
    void Revert(RuntimeCombatEvent combatEvent);
}

public interface IRuntimeCombatDeathHandler
{
    IReadOnlyList<RuntimeCombatEvent> HandleDeath(RuntimeCombatSnapshot snapshot, RuntimeCombatParticipant participant, DateTimeOffset nowUtc);
}

public sealed class RuntimeCombatSession
{
    private readonly object _gate = new();
    private readonly IRuntimeCombatInitiativePolicy _initiative;
    private readonly IRuntimeCombatActionPolicy _actions;
    private readonly IRuntimeCombatEventSink _sink;
    private readonly IRuntimeCombatDeathHandler _deaths;
    private readonly Dictionary<string, RuntimeCombatParticipant> _participants = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _initiativeOrder = new();
    private readonly List<RuntimeCombatEvent> _events = new();

    public RuntimeCombatSession(string sessionId, string roomId, IRuntimeCombatInitiativePolicy initiative,
        IRuntimeCombatActionPolicy actions, IRuntimeCombatEventSink sink, IRuntimeCombatDeathHandler deaths)
    {
        SessionId = Require(sessionId, nameof(sessionId));
        RoomId = Require(roomId, nameof(roomId));
        _initiative = initiative;
        _actions = actions;
        _sink = sink;
        _deaths = deaths;
    }

    public string SessionId { get; }
    public string RoomId { get; }
    public RuntimeCombatState State { get; private set; } = RuntimeCombatState.Active;
    public int Round { get; private set; }

    public void AddParticipant(RuntimeCombatParticipant participant)
    {
        lock (_gate)
        {
            EnsureActive();
            if (!string.Equals(participant.RoomId, RoomId, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("COMBAT_ROOM_MISMATCH");
            if (!_participants.TryAdd(participant.ParticipantId, participant))
                throw new InvalidOperationException("COMBAT_DUPLICATE_PARTICIPANT");
            RebuildInitiative();
        }
    }

    public RuntimeCombatParticipant RemoveParticipant(string participantId)
    {
        lock (_gate)
        {
            if (!_participants.Remove(participantId, out var participant))
                throw new KeyNotFoundException(participantId);
            RebuildInitiative();
            CompleteIfResolved();
            return participant;
        }
    }

    public void BeginRound()
    {
        lock (_gate)
        {
            EnsureActive();
            Round = checked(Round + 1);
            RebuildInitiative();
        }
    }

    public RuntimeCombatActionResult Execute(RuntimeCombatAction action, DateTimeOffset nowUtc)
    {
        lock (_gate)
        {
            EnsureActive();
            if (Round <= 0) throw new InvalidOperationException("COMBAT_ROUND_NOT_STARTED");
            if (!_participants.TryGetValue(action.ActorId, out var actor) || !actor.IsAlive)
                return Fail("COMBAT_INVALID_ACTOR", "Actor is not an active living participant.");
            if (!string.IsNullOrWhiteSpace(action.TargetId) && !_participants.ContainsKey(action.TargetId))
                return Fail("COMBAT_INVALID_TARGET", "Target is not a participant in this combat session.");

            var before = Capture();
            var result = _actions.Resolve(before, action, nowUtc);
            if (!result.Succeeded) return result;

            var applied = new List<RuntimeCombatEvent>();
            try
            {
                foreach (var combatEvent in result.Events)
                {
                    ApplyState(combatEvent);
                    _sink.Apply(combatEvent);
                    _events.Add(combatEvent);
                    applied.Add(combatEvent);
                }

                foreach (var participant in _participants.Values.Where(x => !x.IsAlive).ToArray())
                {
                    foreach (var deathEvent in _deaths.HandleDeath(Capture(), participant, nowUtc))
                    {
                        _sink.Apply(deathEvent);
                        _events.Add(deathEvent);
                        applied.Add(deathEvent);
                    }
                }
                CompleteIfResolved();
                return new RuntimeCombatActionResult(true, null, null, applied);
            }
            catch
            {
                for (var i = applied.Count - 1; i >= 0; i--) { try { _sink.Revert(applied[i]); } catch { } }
                Restore(before);
                throw;
            }
        }
    }

    public RuntimeCombatSnapshot Capture() => new(SessionId, RoomId, State, Round,
        _participants.Values.OrderBy(x => x.ParticipantId, StringComparer.OrdinalIgnoreCase).ToArray(),
        _initiativeOrder.ToArray(), _events.ToArray());

    public void Restore(RuntimeCombatSnapshot snapshot)
    {
        lock (_gate)
        {
            if (!string.Equals(snapshot.SessionId, SessionId, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("COMBAT_SNAPSHOT_SESSION_MISMATCH");
            _participants.Clear();
            foreach (var participant in snapshot.Participants) _participants.Add(participant.ParticipantId, participant);
            _initiativeOrder.Clear(); _initiativeOrder.AddRange(snapshot.InitiativeOrder);
            _events.Clear(); _events.AddRange(snapshot.EventLog);
            State = snapshot.State; Round = snapshot.Round;
        }
    }

    public IReadOnlyList<string> Validate()
    {
        lock (_gate)
        {
            var errors = new List<string>();
            if (_participants.Count != _participants.Keys.Distinct(StringComparer.OrdinalIgnoreCase).Count()) errors.Add("duplicate-participants");
            if (_participants.Values.Any(x => !string.Equals(x.RoomId, RoomId, StringComparison.OrdinalIgnoreCase))) errors.Add("participant-room-mismatch");
            if (_participants.Values.Any(x => x.CurrentHitPoints < 0 || x.MaximumHitPoints < 0 || x.CurrentHitPoints > x.MaximumHitPoints)) errors.Add("invalid-hit-points");
            if (_initiativeOrder.Any(id => !_participants.ContainsKey(id))) errors.Add("invalid-initiative-reference");
            if (_events.Any(x => !string.Equals(x.SessionId, SessionId, StringComparison.OrdinalIgnoreCase))) errors.Add("invalid-event-session");
            return errors;
        }
    }

    private void ApplyState(RuntimeCombatEvent combatEvent)
    {
        if (combatEvent.EventType.Equals("damage", StringComparison.OrdinalIgnoreCase) && combatEvent.TargetId is not null && _participants.TryGetValue(combatEvent.TargetId, out var target))
        {
            var hp = Math.Max(0, target.CurrentHitPoints - Math.Max(0, combatEvent.Magnitude));
            _participants[target.ParticipantId] = target with { CurrentHitPoints = hp, IsAlive = hp > 0 };
        }
        else if (combatEvent.EventType.Equals("heal", StringComparison.OrdinalIgnoreCase) && combatEvent.TargetId is not null && _participants.TryGetValue(combatEvent.TargetId, out target))
        {
            var hp = Math.Min(target.MaximumHitPoints, checked(target.CurrentHitPoints + Math.Max(0, combatEvent.Magnitude)));
            _participants[target.ParticipantId] = target with { CurrentHitPoints = hp, IsAlive = hp > 0 };
        }
        else if (combatEvent.EventType.Equals("flee", StringComparison.OrdinalIgnoreCase))
        {
            _participants.Remove(combatEvent.ActorId);
            RebuildInitiative();
        }
    }

    private void RebuildInitiative()
    {
        _initiativeOrder.Clear();
        _initiativeOrder.AddRange(_initiative.Order(_participants.Values));
        if (_initiativeOrder.Count != _participants.Count || _initiativeOrder.Distinct(StringComparer.OrdinalIgnoreCase).Count() != _participants.Count)
            throw new InvalidOperationException("COMBAT_INVALID_INITIATIVE_POLICY");
    }

    private void CompleteIfResolved()
    {
        var livingKinds = _participants.Values.Where(x => x.IsAlive).Select(x => x.Kind).Distinct().Count();
        if (_participants.Count == 0 || livingKinds <= 1) State = RuntimeCombatState.Completed;
    }

    private void EnsureActive() { if (State != RuntimeCombatState.Active) throw new InvalidOperationException("COMBAT_NOT_ACTIVE"); }
    private static RuntimeCombatActionResult Fail(string code, string message) => new(false, code, message, Array.Empty<RuntimeCombatEvent>());
    private static string Require(string value, string name) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", name) : value;
}
