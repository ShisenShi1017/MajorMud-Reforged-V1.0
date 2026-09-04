using System.Text.Json;

namespace RealmGate.Server;

public enum RuntimeWorldEventStatus { Inactive, Scheduled, Active, Completed, Cancelled, Failed }
public enum RuntimeWorldEventActionKind { Trigger, Script, Spawn, Despawn, Reward, WorldState, Announcement, Cleanup }
public enum RuntimeWorldEventPersistence { Ephemeral, WorldState }

public sealed record RuntimeWorldEventAction(string ActionId, RuntimeWorldEventActionKind Kind, string OwnerSubsystem, int Order, bool Required);
public sealed record RuntimeWorldEventDefinition(
    string EventId,
    IReadOnlyList<RuntimeWorldEventAction> StartActions,
    IReadOnlyList<RuntimeWorldEventAction> EndActions,
    IReadOnlyList<RuntimeWorldEventAction> CleanupActions,
    RuntimeWorldEventPersistence Persistence,
    bool Repeatable,
    int? MaximumParticipants = null);
public sealed record RuntimeWorldEventState(
    string EventId,
    RuntimeWorldEventStatus Status,
    DateTimeOffset? ScheduledStartUtc,
    DateTimeOffset? ScheduledEndUtc,
    DateTimeOffset? ActivatedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    IReadOnlySet<string> ParticipantIds,
    long Revision,
    string? FailureReason = null);
public sealed record RuntimeWorldEventDispatch(
    string EventId,
    string ActionId,
    RuntimeWorldEventActionKind Kind,
    string OwnerSubsystem,
    int Order,
    string Phase,
    long EventRevision,
    DateTimeOffset DispatchedAtUtc);
public sealed record RuntimeWorldEventActionResult(string ActionId, bool Succeeded, string? Error = null);
public sealed record RuntimeWorldEventSnapshot(int SchemaVersion, IReadOnlyList<RuntimeWorldEventState> Events, DateTimeOffset SavedAtUtc);

public sealed class RuntimeWorldEventService
{
    private readonly object _gate = new();
    private readonly Dictionary<string, RuntimeWorldEventDefinition> _definitions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RuntimeWorldEventState> _states = new(StringComparer.OrdinalIgnoreCase);
    private readonly string? _persistencePath;

    public RuntimeWorldEventService(IEnumerable<RuntimeWorldEventDefinition> definitions, string? persistencePath = null)
    {
        _persistencePath = persistencePath;
        foreach (var definition in definitions)
        {
            ValidateDefinition(definition);
            if (!_definitions.TryAdd(definition.EventId, definition)) throw new InvalidOperationException("duplicate-world-event:" + definition.EventId);
            _states[definition.EventId] = NewState(definition.EventId);
        }
        Restore();
    }

    public RuntimeWorldEventState GetState(string eventId)
    {
        lock (_gate) return _states.TryGetValue(eventId, out var state) ? state : throw new InvalidOperationException("unknown-world-event:" + eventId);
    }

    public RuntimeWorldEventState Schedule(string eventId, DateTimeOffset startUtc, DateTimeOffset? endUtc = null)
    {
        lock (_gate)
        {
            var current = RequireState(eventId);
            var definition = _definitions[eventId];
            if (current.Status == RuntimeWorldEventStatus.Active) throw new InvalidOperationException("world-event-active:" + eventId);
            if (!definition.Repeatable && current.Status == RuntimeWorldEventStatus.Completed) throw new InvalidOperationException("world-event-not-repeatable:" + eventId);
            if (endUtc is not null && endUtc <= startUtc) throw new ArgumentException("world-event-end-must-follow-start:" + eventId);
            var updated = current with { Status = RuntimeWorldEventStatus.Scheduled, ScheduledStartUtc = startUtc, ScheduledEndUtc = endUtc, ActivatedAtUtc = null, CompletedAtUtc = null, ParticipantIds = EmptyIds(), FailureReason = null, Revision = current.Revision + 1 };
            _states[eventId] = updated; SaveLocked(); return updated;
        }
    }

    public IReadOnlyList<RuntimeWorldEventDispatch> Activate(string eventId, DateTimeOffset nowUtc, bool administrativeOverride = false)
    {
        lock (_gate)
        {
            var current = RequireState(eventId);
            var definition = _definitions[eventId];
            if (current.Status == RuntimeWorldEventStatus.Active) throw new InvalidOperationException("world-event-already-active:" + eventId);
            if (!administrativeOverride)
            {
                if (current.Status != RuntimeWorldEventStatus.Scheduled) throw new InvalidOperationException("world-event-not-scheduled:" + eventId);
                if (current.ScheduledStartUtc is null || nowUtc < current.ScheduledStartUtc) throw new InvalidOperationException("world-event-not-due:" + eventId);
            }
            if (!definition.Repeatable && current.Status == RuntimeWorldEventStatus.Completed) throw new InvalidOperationException("world-event-not-repeatable:" + eventId);
            var updated = current with { Status = RuntimeWorldEventStatus.Active, ActivatedAtUtc = nowUtc, CompletedAtUtc = null, FailureReason = null, Revision = current.Revision + 1 };
            _states[eventId] = updated; SaveLocked();
            return Dispatch(definition.StartActions, updated, "start", nowUtc);
        }
    }

    public RuntimeWorldEventState Join(string eventId, string participantId)
    {
        lock (_gate)
        {
            if (string.IsNullOrWhiteSpace(participantId)) throw new ArgumentException("world-event-participant-required");
            var current = RequireState(eventId); var definition = _definitions[eventId];
            if (current.Status != RuntimeWorldEventStatus.Active) throw new InvalidOperationException("world-event-not-active:" + eventId);
            if (current.ParticipantIds.Contains(participantId)) return current;
            if (definition.MaximumParticipants is int max && current.ParticipantIds.Count >= max) throw new InvalidOperationException("world-event-capacity-reached:" + eventId);
            var participants = new HashSet<string>(current.ParticipantIds, StringComparer.OrdinalIgnoreCase) { participantId };
            var updated = current with { ParticipantIds = participants, Revision = current.Revision + 1 };
            _states[eventId] = updated; SaveLocked(); return updated;
        }
    }

    public RuntimeWorldEventState Leave(string eventId, string participantId)
    {
        lock (_gate)
        {
            var current = RequireState(eventId);
            var participants = new HashSet<string>(current.ParticipantIds, StringComparer.OrdinalIgnoreCase);
            if (!participants.Remove(participantId)) return current;
            var updated = current with { ParticipantIds = participants, Revision = current.Revision + 1 };
            _states[eventId] = updated; SaveLocked(); return updated;
        }
    }

    public IReadOnlyList<RuntimeWorldEventDispatch> End(string eventId, DateTimeOffset nowUtc, bool cancelled = false)
    {
        lock (_gate)
        {
            var current = RequireState(eventId); var definition = _definitions[eventId];
            if (current.Status != RuntimeWorldEventStatus.Active && !cancelled) throw new InvalidOperationException("world-event-not-active:" + eventId);
            if (current.Status is RuntimeWorldEventStatus.Completed or RuntimeWorldEventStatus.Cancelled) throw new InvalidOperationException("world-event-already-ended:" + eventId);
            var status = cancelled ? RuntimeWorldEventStatus.Cancelled : RuntimeWorldEventStatus.Completed;
            var updated = current with { Status = status, CompletedAtUtc = nowUtc, Revision = current.Revision + 1 };
            _states[eventId] = updated; SaveLocked();
            var actions = cancelled ? definition.CleanupActions : definition.EndActions.Concat(definition.CleanupActions).ToArray();
            return Dispatch(actions, updated, cancelled ? "cancel" : "end", nowUtc);
        }
    }

    public RuntimeWorldEventState ApplyResults(string eventId, IEnumerable<RuntimeWorldEventActionResult> results)
    {
        lock (_gate)
        {
            var current = RequireState(eventId); var definition = _definitions[eventId];
            var map = results.GroupBy(x => x.ActionId, StringComparer.OrdinalIgnoreCase).ToDictionary(x => x.Key, x => x.Last(), StringComparer.OrdinalIgnoreCase);
            var allActions = definition.StartActions.Concat(definition.EndActions).Concat(definition.CleanupActions);
            var requiredFailure = allActions.Any(action => action.Required && map.TryGetValue(action.ActionId, out var result) && !result.Succeeded);
            if (!requiredFailure) return current;
            var reason = string.Join(";", map.Values.Where(x => !x.Succeeded).Select(x => x.Error ?? x.ActionId));
            var updated = current with { Status = RuntimeWorldEventStatus.Failed, FailureReason = reason, Revision = current.Revision + 1 };
            _states[eventId] = updated; SaveLocked(); return updated;
        }
    }

    public IReadOnlyList<RuntimeWorldEventDispatch> Advance(DateTimeOffset nowUtc)
    {
        lock (_gate)
        {
            var output = new List<RuntimeWorldEventDispatch>();
            foreach (var state in _states.Values.OrderBy(x => x.ScheduledStartUtc).ThenBy(x => x.EventId, StringComparer.OrdinalIgnoreCase).ToArray())
            {
                if (state.Status == RuntimeWorldEventStatus.Scheduled && state.ScheduledStartUtc <= nowUtc) output.AddRange(Activate(state.EventId, nowUtc));
                var current = _states[state.EventId];
                if (current.Status == RuntimeWorldEventStatus.Active && current.ScheduledEndUtc is not null && current.ScheduledEndUtc <= nowUtc) output.AddRange(End(current.EventId, nowUtc));
            }
            return output;
        }
    }

    public IReadOnlyList<string> ValidateState()
    {
        lock (_gate)
        {
            var failures = new List<string>();
            foreach (var state in _states.Values)
            {
                if (!_definitions.ContainsKey(state.EventId)) failures.Add("missing-definition:" + state.EventId);
                if (state.Revision < 0) failures.Add("negative-revision:" + state.EventId);
                if (state.Status == RuntimeWorldEventStatus.Active && state.ActivatedAtUtc is null) failures.Add("active-without-activation:" + state.EventId);
                if (state.Status is RuntimeWorldEventStatus.Completed or RuntimeWorldEventStatus.Cancelled && state.CompletedAtUtc is null) failures.Add("ended-without-completion:" + state.EventId);
                var max = _definitions[state.EventId].MaximumParticipants;
                if (max is not null && state.ParticipantIds.Count > max) failures.Add("participant-capacity-exceeded:" + state.EventId);
            }
            return failures;
        }
    }

    private RuntimeWorldEventState RequireState(string eventId) => _states.TryGetValue(eventId, out var state) ? state : throw new InvalidOperationException("unknown-world-event:" + eventId);
    private static RuntimeWorldEventState NewState(string eventId) => new(eventId, RuntimeWorldEventStatus.Inactive, null, null, null, null, EmptyIds(), 0);
    private static IReadOnlySet<string> EmptyIds() => new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static IReadOnlyList<RuntimeWorldEventDispatch> Dispatch(IEnumerable<RuntimeWorldEventAction> actions, RuntimeWorldEventState state, string phase, DateTimeOffset nowUtc) => actions.OrderBy(x => x.Order).ThenBy(x => x.ActionId, StringComparer.OrdinalIgnoreCase).Select(x => new RuntimeWorldEventDispatch(state.EventId, x.ActionId, x.Kind, x.OwnerSubsystem, x.Order, phase, state.Revision, nowUtc)).ToArray();

    private static void ValidateDefinition(RuntimeWorldEventDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.EventId)) throw new ArgumentException("world-event-id-required");
        if (definition.MaximumParticipants is <= 0) throw new ArgumentException("world-event-capacity-invalid:" + definition.EventId);
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var action in definition.StartActions.Concat(definition.EndActions).Concat(definition.CleanupActions))
        {
            if (string.IsNullOrWhiteSpace(action.ActionId) || string.IsNullOrWhiteSpace(action.OwnerSubsystem)) throw new ArgumentException("world-event-action-required-field:" + definition.EventId);
            if (!ids.Add(action.ActionId)) throw new ArgumentException("duplicate-world-event-action:" + action.ActionId);
            if (action.Order < 0) throw new ArgumentException("world-event-action-order-invalid:" + action.ActionId);
        }
    }

    private void SaveLocked()
    {
        if (string.IsNullOrWhiteSpace(_persistencePath)) return;
        var states = _states.Values.Where(x => _definitions[x.EventId].Persistence == RuntimeWorldEventPersistence.WorldState).ToArray();
        var snapshot = new RuntimeWorldEventSnapshot(1, states, DateTimeOffset.UtcNow);
        Directory.CreateDirectory(Path.GetDirectoryName(_persistencePath!)!);
        var temporary = _persistencePath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, _persistencePath!, true);
    }

    private void Restore()
    {
        if (string.IsNullOrWhiteSpace(_persistencePath) || !File.Exists(_persistencePath)) return;
        try
        {
            var snapshot = JsonSerializer.Deserialize<RuntimeWorldEventSnapshot>(File.ReadAllText(_persistencePath!));
            if (snapshot?.SchemaVersion != 1) return;
            foreach (var state in snapshot.Events)
                if (_definitions.TryGetValue(state.EventId, out var definition) && definition.Persistence == RuntimeWorldEventPersistence.WorldState)
                    _states[state.EventId] = state with { ParticipantIds = new HashSet<string>(state.ParticipantIds, StringComparer.OrdinalIgnoreCase) };
        }
        catch (JsonException) { }
        catch (IOException) { }
    }
}
