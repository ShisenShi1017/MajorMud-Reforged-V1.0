using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeClockSchedulingSmokeReport(bool Passed, IReadOnlyList<string> Checks, IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    public static RuntimeClockSchedulingSmokeReport Run()
    {
        var start = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
        var definitions = new[]
        {
            new RuntimeScheduleDefinition("one", "events", "event-start", RuntimeScheduleKind.OneShot, start.AddMinutes(1), null, null, RuntimeTimerPersistence.Ephemeral, RuntimeMissedTimerPolicy.FireOnce),
            new RuntimeScheduleDefinition("repeat", "world-reset-cleanup", "room-reset", RuntimeScheduleKind.FixedInterval, start.AddMinutes(1), TimeSpan.FromMinutes(1), null, RuntimeTimerPersistence.WorldState, RuntimeMissedTimerPolicy.FireAllBounded, 3),
            new RuntimeScheduleDefinition("skip", "merchants", "merchant-open", RuntimeScheduleKind.DailyUtc, start.AddMinutes(1), null, TimeSpan.FromHours(8), RuntimeTimerPersistence.Ephemeral, RuntimeMissedTimerPolicy.SkipToNext)
        };
        var service = new RuntimeClockSchedulingService(definitions);
        var checks = new List<string>(); var failures = new List<string>();
        var first = service.Advance(start.AddMinutes(1));
        Check(first.Count == 2 && first[0].ScheduleId == "one" && first[1].ScheduleId == "repeat", "deterministic-order-and-skip");
        Check(service.Advance(start.AddMinutes(4)).Count == 3, "bounded-catch-up");
        Check(service.SnapshotStates().Single(x => x.ScheduleId == "one").Enabled == false, "one-shot-disabled");
        Check(service.SetEnabled("repeat", false, start.AddMinutes(4)), "disable");
        Check(service.Advance(start.AddMinutes(10)).Count == 0, "disabled-suppression");
        Check(service.ValidateState().Count == 0, "state-invariants");
        var rollbackCaught = false; try { service.Advance(start); } catch (InvalidOperationException) { rollbackCaught = true; }
        Check(rollbackCaught, "clock-rollback-fail-closed");
        return new(failures.Count == 0, checks, failures);
        void Check(bool condition, string name) { checks.Add(name); if (!condition) failures.Add(name); }
    }
}
