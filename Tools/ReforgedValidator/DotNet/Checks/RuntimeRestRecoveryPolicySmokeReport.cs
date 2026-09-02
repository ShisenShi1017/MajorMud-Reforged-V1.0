using System.Text.Json;
namespace Reforged.Server;
public sealed record RuntimeRestRecoveryPolicySmokeReport(bool Passed, IReadOnlyList<string> Checks, IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    public static RuntimeRestRecoveryPolicySmokeReport Run()
    {
        var service = new RuntimeRestRecoveryPolicyService(new[]
        {
            new RuntimeRestRoomPolicy("field", true, false, false, false, 0, null, 1.0),
            new RuntimeRestRoomPolicy("inn", true, true, true, true, 25, "inn", 1.5)
        });
        var checks = new List<string>(); var failures = new List<string>();
        var active = new RuntimeRestActorContext("hero", "field", false, false, true, RuntimeRestStateKind.Active);
        Check(service.Rest(active).Accepted, "rest-allowed");
        Check(!service.Sleep(active).Accepted, "sleep-room-restriction");
        Check(service.Wake(active).Accepted, "wake");
        var inn = active with { RoomId = "inn" };
        var sleep = service.Sleep(inn);
        Check(sleep.Accepted, "inn-sleep");
        Check(sleep.Requests.Any(x => x.Kind == RuntimeRestActionKind.RequestCharge && x.Amount == 25), "inn-charge-routing");
        Check(sleep.Requests.Any(x => x.Kind == RuntimeRestActionKind.RequestSpawnRoomChange), "login-room-routing");
        Check(service.RequestLogout(inn).Accepted, "safe-logout");
        Check(!service.Rest(inn with { InCombat = true }).Accepted, "combat-restriction");
        Check(service.ValidateState().Count == 0, "state-invariants");
        return new(failures.Count == 0, checks, failures);
        void Check(bool condition, string name) { checks.Add(name); if (!condition) failures.Add(name); }
    }
}
