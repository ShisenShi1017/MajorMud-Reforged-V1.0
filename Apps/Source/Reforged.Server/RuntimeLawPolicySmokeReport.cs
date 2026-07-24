using System.Text.Json;
namespace Reforged.Server;
public sealed record RuntimeLawPolicySmokeReport(bool Passed, IReadOnlyList<string> Checks, IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    public static RuntimeLawPolicySmokeReport Run()
    {
        var now = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
        var service = new RuntimeLawPolicyService(new[] { new RuntimeLawZoneRule("town", true, "town-guard", 1, TimeSpan.FromHours(1), true), new RuntimeLawZoneRule("wild", false) });
        var checks = new List<string>(); var failures = new List<string>();
        Check(!service.RecordCrime("c0", "offender", RuntimeCrimeKind.Theft, "wild", now, 5).Accepted, "unenforced-zone-denial");
        var crime = service.RecordCrime("c1", "offender", RuntimeCrimeKind.Theft, "town", now, 5, "victim", new[] { "w2", "w1", "w1" });
        Check(crime.Accepted, "crime-recording");
        Check(crime.Requests.Any(x => x.Kind == RuntimeLawActionKind.RequestGuardAlert), "guard-routing");
        Check(service.InspectBounty("offender") is { Wanted: true, Amount: 5 }, "wanted-bounty-state");
        Check(service.ApplyFine("offender", 3, "theft").Accepted, "fine-routing");
        Check(service.RequestJail("offender", "jail", "wanted").Accepted, "jail-routing");
        Check(service.ProcessDecay(now.AddHours(2)).Any(x => x.Kind == RuntimeLawActionKind.ClearWanted), "crime-decay");
        service.RecordCrime("c2", "offender", RuntimeCrimeKind.Assault, "town", now.AddHours(3), 2);
        Check(service.Pardon("offender", "sysop").Accepted, "pardon");
        Check(service.ValidateState().Count == 0, "state-invariants");
        return new(failures.Count == 0, checks, failures);
        void Check(bool condition, string name) { checks.Add(name); if (!condition) failures.Add(name); }
    }
}
