using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeTrapSmokeReport(bool Passed, IReadOnlyList<string> Checks, IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

    public static RuntimeTrapSmokeReport Run()
    {
        var now = DateTimeOffset.UtcNow;
        var definition = new RuntimeTrapDefinition(
            "trap-1", RuntimeTrapLocation.Door, "door-1", new HashSet<RuntimeTrapTrigger> { RuntimeTrapTrigger.Open },
            5, 6, 1, TimeSpan.FromMinutes(1), TimeSpan.FromHours(1), RuntimeTrapPersistence.Ephemeral, true,
            "damage:spikes", "status:poison", null, null, "alarm:guards", null, null);
        var service = new RuntimeTrapService(new[] { definition });
        var actor = new RuntimeTrapActorContext("char-1", 5, 6, new HashSet<string> { "detect-traps", "disarm-traps" }, new HashSet<string>(), now);
        var checks = new List<string>(); var failures = new List<string>();
        Check(service.Search("trap-1", actor).Code == "trap-detected", "detect");
        Check(service.Disarm("trap-1", actor).Code == "trap-disarmed", "disarm");
        Check(service.Trigger("trap-1", RuntimeTrapTrigger.Open, actor).Code == "trap-inactive", "disarmed-blocks-trigger");
        service.Tick(now.AddMinutes(2));
        var triggered = service.Trigger("trap-1", RuntimeTrapTrigger.Open, actor with { NowUtc = now.AddMinutes(2) });
        Check(triggered.Triggered && triggered.EffectRequests.Count == 3, "trigger-routes-effects");
        Check(triggered.State.ChargesRemaining == 0 && !triggered.State.Armed, "charge-consumed");
        Check(service.ValidateState().Count == 0, "state-invariants");
        return new(failures.Count == 0, checks, failures);
        void Check(bool condition, string name) { checks.Add(name); if (!condition) failures.Add(name); }
    }
}
