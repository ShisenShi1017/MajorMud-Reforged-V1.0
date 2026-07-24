namespace Reforged.Server;

public enum RuntimeCombatInstanceState { Created, Active, Terminating, Terminated }
public enum RuntimeCombatParticipantState { Active, Defeated, Fled, Disconnected, Removed }

public sealed record RuntimeCombatSchedulingPolicy(
    string PolicyId,
    string ActorKind,
    int? BaseInitiative,
    int? ActionDelayTicks,
    IReadOnlyList<string> EvidenceReferences,
    RuntimeResolutionConfidence Confidence,
    string SafeFallback);

public sealed record RuntimeCombatInstanceParticipant(
    string ActorId,
    string ActorKind,
    RuntimeCombatParticipantState State,
    long JoinedTick,
    long NextActionTick,
    int Initiative,
    long Sequence);

public sealed record RuntimeCombatScheduledTurn(
    string CombatId,
    string ActorId,
    long WorldTick,
    int RoundNumber,
    int Initiative,
    long Sequence);

public sealed record RuntimeCombatTransition(
    string CombatId,
    RuntimeCombatInstanceState From,
    RuntimeCombatInstanceState To,
    long WorldTick,
    string Reason);

public sealed class RuntimeCombatInstanceLifecycle
{
    private readonly IReadOnlyDictionary<string, RuntimeCombatSchedulingPolicy> _policies;
    private readonly Dictionary<string, RuntimeCombatInstanceParticipant> _participants = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<RuntimeCombatTransition> _transitions = new();
    private long _lastAdvancedTick = -1;
    private long _nextSequence;

    public RuntimeCombatInstanceLifecycle(string combatId, string roomId, long createdTick, IEnumerable<RuntimeCombatSchedulingPolicy> policies)
    {
        RequireCanonical(combatId, "combat.", "COMBAT_ID_REQUIRED");
        RequireCanonical(roomId, "room.", "COMBAT_ROOM_ID_REQUIRED");
        if (createdTick < 0) throw new InvalidOperationException("COMBAT_CREATED_TICK_INVALID");
        CombatId = combatId;
        RoomId = roomId;
        CreatedTick = createdTick;
        State = RuntimeCombatInstanceState.Created;
        RoundNumber = 0;
        var materialized = (policies ?? throw new ArgumentNullException(nameof(policies))).ToArray();
        foreach (var policy in materialized) ValidatePolicy(policy);
        _policies = materialized.ToDictionary(x => x.ActorKind, StringComparer.OrdinalIgnoreCase);
        if (_policies.Count != materialized.Length) throw new InvalidOperationException("COMBAT_DUPLICATE_SCHEDULING_POLICY");
    }

    public string CombatId { get; }
    public string RoomId { get; }
    public long CreatedTick { get; }
    public RuntimeCombatInstanceState State { get; private set; }
    public int RoundNumber { get; private set; }
    public IReadOnlyCollection<RuntimeCombatInstanceParticipant> Participants => _participants.Values;
    public IReadOnlyList<RuntimeCombatTransition> Transitions => _transitions;

    public RuntimeCombatInstanceParticipant AddParticipant(string actorId, string actorKind, long worldTick)
    {
        RequireActiveOrCreated();
        RequireActor(actorId);
        if (worldTick < CreatedTick) throw new InvalidOperationException("COMBAT_PARTICIPANT_TICK_BEFORE_CREATION");
        if (_participants.ContainsKey(actorId)) throw new InvalidOperationException("COMBAT_PARTICIPANT_ALREADY_PRESENT");
        if (!_policies.TryGetValue(actorKind, out var policy)) throw new InvalidOperationException("COMBAT_SCHEDULING_POLICY_UNAVAILABLE");
        if (policy.BaseInitiative is null || policy.ActionDelayTicks is null) throw new InvalidOperationException("COMBAT_SCHEDULING_POLICY_UNRESOLVED");
        var participant = new RuntimeCombatInstanceParticipant(actorId, actorKind, RuntimeCombatParticipantState.Active, worldTick,
            checked(worldTick + policy.ActionDelayTicks.Value), policy.BaseInitiative.Value, _nextSequence++);
        _participants.Add(actorId, participant);
        if (State == RuntimeCombatInstanceState.Created) Transition(RuntimeCombatInstanceState.Active, worldTick, "first_participant_inserted");
        return participant;
    }

    public IReadOnlyList<RuntimeCombatScheduledTurn> Advance(long worldTick)
    {
        if (State != RuntimeCombatInstanceState.Active) throw new InvalidOperationException("COMBAT_NOT_ACTIVE");
        if (worldTick < _lastAdvancedTick) throw new InvalidOperationException("COMBAT_TICK_REGRESSION");
        _lastAdvancedTick = worldTick;
        var due = _participants.Values.Where(x => x.State == RuntimeCombatParticipantState.Active && x.NextActionTick <= worldTick)
            .OrderBy(x => x.NextActionTick)
            .ThenByDescending(x => x.Initiative)
            .ThenBy(x => x.Sequence)
            .ThenBy(x => x.ActorId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (due.Length == 0) return Array.Empty<RuntimeCombatScheduledTurn>();
        RoundNumber = checked(RoundNumber + 1);
        var turns = new List<RuntimeCombatScheduledTurn>(due.Length);
        foreach (var participant in due)
        {
            var policy = _policies[participant.ActorKind];
            if (policy.ActionDelayTicks is null) throw new InvalidOperationException("COMBAT_ACTION_DELAY_UNRESOLVED");
            turns.Add(new RuntimeCombatScheduledTurn(CombatId, participant.ActorId, worldTick, RoundNumber, participant.Initiative, participant.Sequence));
            _participants[participant.ActorId] = participant with { NextActionTick = checked(worldTick + policy.ActionDelayTicks.Value) };
        }
        return turns;
    }

    public RuntimeCombatInstanceParticipant SetParticipantState(string actorId, RuntimeCombatParticipantState state, long worldTick)
    {
        if (!_participants.TryGetValue(actorId, out var participant)) throw new InvalidOperationException("COMBAT_PARTICIPANT_NOT_FOUND");
        if (participant.State != RuntimeCombatParticipantState.Active) throw new InvalidOperationException("COMBAT_PARTICIPANT_ALREADY_TERMINAL");
        if (state == RuntimeCombatParticipantState.Active) throw new InvalidOperationException("COMBAT_PARTICIPANT_TERMINAL_STATE_REQUIRED");
        var updated = participant with { State = state };
        _participants[actorId] = updated;
        TryTerminate(worldTick, $"participant_{state.ToString().ToLowerInvariant()}");
        return updated;
    }

    public void Stop(long worldTick, string reason)
    {
        if (State is RuntimeCombatInstanceState.Terminating or RuntimeCombatInstanceState.Terminated)
            throw new InvalidOperationException("COMBAT_ALREADY_TERMINAL");
        if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("COMBAT_STOP_REASON_REQUIRED");
        Transition(RuntimeCombatInstanceState.Terminating, worldTick, reason);
        foreach (var participant in _participants.Values.Where(x => x.State == RuntimeCombatParticipantState.Active).ToArray())
            _participants[participant.ActorId] = participant with { State = RuntimeCombatParticipantState.Removed };
        Transition(RuntimeCombatInstanceState.Terminated, worldTick, "terminal_cleanup_complete");
    }

    private void TryTerminate(long worldTick, string reason)
    {
        if (State != RuntimeCombatInstanceState.Active) return;
        var activeKinds = _participants.Values.Where(x => x.State == RuntimeCombatParticipantState.Active)
            .Select(x => x.ActorKind).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        if (activeKinds >= 2) return;
        Transition(RuntimeCombatInstanceState.Terminating, worldTick, reason);
        foreach (var participant in _participants.Values.Where(x => x.State == RuntimeCombatParticipantState.Active).ToArray())
            _participants[participant.ActorId] = participant with { State = RuntimeCombatParticipantState.Removed };
        Transition(RuntimeCombatInstanceState.Terminated, worldTick, "insufficient_opposing_participants");
    }

    private void Transition(RuntimeCombatInstanceState to, long tick, string reason)
    {
        var from = State;
        State = to;
        _transitions.Add(new RuntimeCombatTransition(CombatId, from, to, tick, reason));
    }

    private void RequireActiveOrCreated()
    {
        if (State is not (RuntimeCombatInstanceState.Created or RuntimeCombatInstanceState.Active))
            throw new InvalidOperationException("COMBAT_NOT_ACCEPTING_PARTICIPANTS");
    }

    private static void ValidatePolicy(RuntimeCombatSchedulingPolicy policy)
    {
        if (string.IsNullOrWhiteSpace(policy.PolicyId)) throw new InvalidOperationException("COMBAT_POLICY_ID_REQUIRED");
        if (policy.ActorKind is not ("character" or "npc")) throw new InvalidOperationException("COMBAT_ACTOR_KIND_INVALID");
        if (policy.BaseInitiative < 0 || policy.ActionDelayTicks <= 0) throw new InvalidOperationException("COMBAT_SCHEDULING_VALUE_INVALID");
        if (policy.EvidenceReferences.Count == 0) throw new InvalidOperationException("COMBAT_POLICY_EVIDENCE_REQUIRED");
        if (policy.Confidence == RuntimeResolutionConfidence.StronglyCorrelated && string.IsNullOrWhiteSpace(policy.SafeFallback))
            throw new InvalidOperationException("COMBAT_POLICY_FALLBACK_REQUIRED");
    }

    private static void RequireActor(string actorId)
    {
        if (string.IsNullOrWhiteSpace(actorId) || !(actorId.StartsWith("character.", StringComparison.OrdinalIgnoreCase) || actorId.StartsWith("npc.", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("COMBAT_CANONICAL_ACTOR_ID_REQUIRED");
    }

    private static void RequireCanonical(string value, string prefix, string error)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException(error);
    }
}
