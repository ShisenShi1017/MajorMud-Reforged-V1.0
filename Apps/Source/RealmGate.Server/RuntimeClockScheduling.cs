using System.Text.Json;

namespace RealmGate.Server;

public enum RuntimeScheduleKind { OneShot, FixedInterval, DailyUtc }
public enum RuntimeTimerPersistence { Ephemeral, WorldState }
public enum RuntimeMissedTimerPolicy { FireOnce, SkipToNext, FireAllBounded }

public sealed record RuntimeScheduleDefinition(
    string ScheduleId,
    string OwnerSubsystem,
    string ActionId,
    RuntimeScheduleKind Kind,
    DateTimeOffset FirstDueUtc,
    TimeSpan? Interval,
    TimeSpan? DailyUtcTime,
    RuntimeTimerPersistence Persistence,
    RuntimeMissedTimerPolicy MissedPolicy,
    int MaximumCatchUpExecutions = 32,
    bool Enabled = true);

public sealed record RuntimeScheduledState(string ScheduleId, DateTimeOffset NextDueUtc, bool Enabled, long Revision, long FireCount);
public sealed record RuntimeScheduledDispatch(string ScheduleId, string OwnerSubsystem, string ActionId, DateTimeOffset DueUtc, DateTimeOffset DispatchedAtUtc, long Sequence);
public sealed record RuntimeSchedulingSnapshot(int SchemaVersion, IReadOnlyList<RuntimeScheduledState> Schedules, DateTimeOffset SavedAtUtc);

public sealed class RuntimeClockSchedulingService
{
    private readonly object _gate = new();
    private readonly Dictionary<string, RuntimeScheduleDefinition> _definitions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RuntimeScheduledState> _states = new(StringComparer.OrdinalIgnoreCase);
    private readonly string? _persistencePath;
    private DateTimeOffset? _lastObservedUtc;
    private long _dispatchSequence;

    public RuntimeClockSchedulingService(IEnumerable<RuntimeScheduleDefinition> definitions, string? persistencePath = null)
    {
        _persistencePath = persistencePath;
        foreach (var definition in definitions)
        {
            ValidateDefinition(definition);
            if (!_definitions.TryAdd(definition.ScheduleId, definition)) throw new InvalidOperationException("duplicate-schedule-id:" + definition.ScheduleId);
            _states[definition.ScheduleId] = new(definition.ScheduleId, definition.FirstDueUtc, definition.Enabled, 0, 0);
        }
        Restore();
    }

    public IReadOnlyList<RuntimeScheduledDispatch> Advance(DateTimeOffset nowUtc)
    {
        lock (_gate)
        {
            if (_lastObservedUtc is { } last && nowUtc < last) throw new InvalidOperationException("clock-rollback-detected");
            _lastObservedUtc = nowUtc;
            var due = new List<RuntimeScheduledDispatch>();
            foreach (var pair in _states.Values.Where(s => s.Enabled && s.NextDueUtc <= nowUtc).OrderBy(s => s.NextDueUtc).ThenBy(s => s.ScheduleId, StringComparer.OrdinalIgnoreCase).ToArray())
                DispatchLocked(pair, nowUtc, due);
            if (due.Count > 0) SaveLocked();
            return due;
        }
    }

    public bool SetEnabled(string scheduleId, bool enabled, DateTimeOffset nowUtc)
    {
        lock (_gate)
        {
            if (!_states.TryGetValue(scheduleId, out var state)) return false;
            var nextDue = state.NextDueUtc;
            if (enabled && nextDue < nowUtc) nextDue = ComputeNext(_definitions[scheduleId], nowUtc);
            _states[scheduleId] = state with { Enabled = enabled, NextDueUtc = nextDue, Revision = state.Revision + 1 };
            SaveLocked(); return true;
        }
    }

    public bool Reschedule(string scheduleId, DateTimeOffset nextDueUtc)
    {
        lock (_gate)
        {
            if (!_states.TryGetValue(scheduleId, out var state)) return false;
            _states[scheduleId] = state with { NextDueUtc = nextDueUtc, Revision = state.Revision + 1 };
            SaveLocked(); return true;
        }
    }

    public IReadOnlyList<RuntimeScheduledState> SnapshotStates() { lock (_gate) return _states.Values.OrderBy(x => x.ScheduleId, StringComparer.OrdinalIgnoreCase).ToArray(); }

    public IReadOnlyList<string> ValidateState()
    {
        lock (_gate)
        {
            var failures = new List<string>();
            foreach (var d in _definitions.Values)
            {
                if (!_states.TryGetValue(d.ScheduleId, out var s)) { failures.Add("missing-state:" + d.ScheduleId); continue; }
                if (s.FireCount < 0 || s.Revision < 0) failures.Add("negative-counter:" + d.ScheduleId);
            }
            return failures;
        }
    }

    private void DispatchLocked(RuntimeScheduledState state, DateTimeOffset nowUtc, List<RuntimeScheduledDispatch> output)
    {
        var definition = _definitions[state.ScheduleId];
        var due = state.NextDueUtc;
        var count = 0;
        if (definition.MissedPolicy == RuntimeMissedTimerPolicy.SkipToNext)
        {
            _states[state.ScheduleId] = state with { NextDueUtc = ComputeNext(definition, nowUtc), Revision = state.Revision + 1 };
            return;
        }
        do
        {
            output.Add(new(definition.ScheduleId, definition.OwnerSubsystem, definition.ActionId, due, nowUtc, ++_dispatchSequence));
            count++;
            if (definition.Kind == RuntimeScheduleKind.OneShot) { _states[state.ScheduleId] = state with { Enabled = false, FireCount = state.FireCount + count, Revision = state.Revision + 1 }; return; }
            due = ComputeNext(definition, due);
        }
        while (definition.MissedPolicy == RuntimeMissedTimerPolicy.FireAllBounded && due <= nowUtc && count < definition.MaximumCatchUpExecutions);
        if (definition.MissedPolicy == RuntimeMissedTimerPolicy.FireOnce && due <= nowUtc) due = ComputeNext(definition, nowUtc);
        _states[state.ScheduleId] = state with { NextDueUtc = due, FireCount = state.FireCount + count, Revision = state.Revision + 1 };
    }

    private static DateTimeOffset ComputeNext(RuntimeScheduleDefinition d, DateTimeOffset fromUtc)
    {
        if (d.Kind == RuntimeScheduleKind.FixedInterval) return fromUtc + d.Interval!.Value;
        if (d.Kind == RuntimeScheduleKind.DailyUtc)
        {
            var t = d.DailyUtcTime!.Value;
            var candidate = new DateTimeOffset(fromUtc.Year, fromUtc.Month, fromUtc.Day, t.Hours, t.Minutes, t.Seconds, TimeSpan.Zero);
            return candidate > fromUtc ? candidate : candidate.AddDays(1);
        }
        return DateTimeOffset.MaxValue;
    }

    private void SaveLocked()
    {
        if (string.IsNullOrWhiteSpace(_persistencePath)) return;
        var states = _states.Values.Where(s => _definitions[s.ScheduleId].Persistence == RuntimeTimerPersistence.WorldState).ToArray();
        var snapshot = new RuntimeSchedulingSnapshot(1, states, DateTimeOffset.UtcNow);
        Directory.CreateDirectory(Path.GetDirectoryName(_persistencePath!)!);
        var tmp = _persistencePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(tmp, _persistencePath!, true);
    }

    private void Restore()
    {
        if (string.IsNullOrWhiteSpace(_persistencePath) || !File.Exists(_persistencePath)) return;
        try
        {
            var snapshot = JsonSerializer.Deserialize<RuntimeSchedulingSnapshot>(File.ReadAllText(_persistencePath!));
            if (snapshot?.SchemaVersion != 1) return;
            foreach (var state in snapshot.Schedules)
                if (_definitions.TryGetValue(state.ScheduleId, out var d) && d.Persistence == RuntimeTimerPersistence.WorldState) _states[state.ScheduleId] = state;
        }
        catch (JsonException) { }
        catch (IOException) { }
    }

    private static void ValidateDefinition(RuntimeScheduleDefinition d)
    {
        if (string.IsNullOrWhiteSpace(d.ScheduleId) || string.IsNullOrWhiteSpace(d.OwnerSubsystem) || string.IsNullOrWhiteSpace(d.ActionId)) throw new ArgumentException("schedule-required-field");
        if (d.Kind == RuntimeScheduleKind.FixedInterval && (d.Interval is null || d.Interval <= TimeSpan.Zero)) throw new ArgumentException("schedule-interval-invalid:" + d.ScheduleId);
        if (d.Kind == RuntimeScheduleKind.DailyUtc && (d.DailyUtcTime is null || d.DailyUtcTime < TimeSpan.Zero || d.DailyUtcTime >= TimeSpan.FromDays(1))) throw new ArgumentException("schedule-daily-time-invalid:" + d.ScheduleId);
        if (d.MaximumCatchUpExecutions < 1 || d.MaximumCatchUpExecutions > 10000) throw new ArgumentException("schedule-catchup-limit-invalid:" + d.ScheduleId);
    }
}
