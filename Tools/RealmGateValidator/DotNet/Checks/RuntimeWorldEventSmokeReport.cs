using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeWorldEventSmokeReport(bool Passed, IReadOnlyList<string> Checks, IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    public static RuntimeWorldEventSmokeReport Run()
    {
        var now = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
        var definition = new RuntimeWorldEventDefinition(
            "boss-event",
            new[] { new RuntimeWorldEventAction("announce-start", RuntimeWorldEventActionKind.Announcement, "communications", 0, false), new RuntimeWorldEventAction("spawn-boss", RuntimeWorldEventActionKind.Spawn, "npc-ai", 10, true) },
            new[] { new RuntimeWorldEventAction("grant-reward", RuntimeWorldEventActionKind.Reward, "economy", 0, true) },
            new[] { new RuntimeWorldEventAction("despawn-boss", RuntimeWorldEventActionKind.Despawn, "npc-ai", 10, false), new RuntimeWorldEventAction("clear-event-flag", RuntimeWorldEventActionKind.WorldState, "global-world-state", 20, true) },
            RuntimeWorldEventPersistence.WorldState, true, 2);
        var service = new RuntimeWorldEventService(new[] { definition });
        var checks = new List<string>(); var failures = new List<string>();
        service.Schedule("boss-event", now.AddMinutes(1), now.AddMinutes(5));
        Check(service.Advance(now).Count == 0, "not-started-early");
        var start = service.Advance(now.AddMinutes(1));
        Check(start.Count == 2 && start[0].ActionId == "announce-start" && start[1].ActionId == "spawn-boss", "ordered-start-dispatch");
        service.Join("boss-event", "character-a"); service.Join("boss-event", "character-b");
        var full = false; try { service.Join("boss-event", "character-c"); } catch (InvalidOperationException) { full = true; }
        Check(full, "participant-capacity");
        var end = service.Advance(now.AddMinutes(5));
        Check(end.Count == 3 && end[0].Phase == "end", "end-and-cleanup-dispatch");
        Check(service.GetState("boss-event").Status == RuntimeWorldEventStatus.Completed, "completed-state");
        service.Schedule("boss-event", now.AddMinutes(10));
        var activated = service.Activate("boss-event", now.AddMinutes(9), administrativeOverride: true);
        Check(activated.Count == 2, "administrative-activation");
        var failed = service.ApplyResults("boss-event", new[] { new RuntimeWorldEventActionResult("spawn-boss", false, "spawn-failed") });
        Check(failed.Status == RuntimeWorldEventStatus.Failed, "required-action-failure");
        Check(service.ValidateState().Count == 0, "state-invariants");
        return new(failures.Count == 0, checks, failures);
        void Check(bool condition, string name) { checks.Add(name); if (!condition) failures.Add(name); }
    }
}
