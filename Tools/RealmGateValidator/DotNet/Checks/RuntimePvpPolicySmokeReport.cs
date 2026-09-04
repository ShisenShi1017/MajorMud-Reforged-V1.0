using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimePvpPolicySmokeReport(bool Passed, IReadOnlyList<string> Checks, IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    public static RuntimePvpPolicySmokeReport Run()
    {
        var now = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
        var service = new RuntimePvpPolicyService(new[]
        {
            new RuntimePvpZoneRule("safe", RuntimePvpZonePolicy.Forbidden),
            new RuntimePvpZoneRule("arena", RuntimePvpZonePolicy.ConsentOnly, 5, true, true, false, TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(2)),
            new RuntimePvpZoneRule("lawless", RuntimePvpZonePolicy.Open, 0, true, false, true)
        });
        var checks = new List<string>(); var failures = new List<string>();
        service.UpsertActor(new("a", 10, "safe", PvpEnabled: true));
        service.UpsertActor(new("b", 12, "safe", PvpEnabled: true));
        Check(!service.EvaluateAttack("a", "b", now).Allowed, "safe-zone-denial");
        service.UpsertActor(new("a", 10, "arena", PvpEnabled: true));
        service.UpsertActor(new("b", 12, "arena", PvpEnabled: false));
        Check(service.EvaluateAttack("a", "b", now).Code == RuntimePvpDecisionCode.ConsentRequired, "mutual-consent");
        service.UpsertActor(new("b", 12, "arena", PvpEnabled: true));
        Check(service.RegisterAttack("a", "b", now).Allowed, "consented-attack");
        Check(service.EvaluateAttack("a", "b", now.AddSeconds(5)).Code == RuntimePvpDecisionCode.CooldownActive, "attack-cooldown");
        Check(service.EvaluateLogout("a", now.AddSeconds(5)).Code == RuntimePvpDecisionCode.LogoutRestricted, "logout-restriction");
        Check(service.EvaluateLogout("a", now.AddMinutes(3)).Allowed, "logout-release");
        service.UpsertActor(new("c", 30, "arena", PvpEnabled: true));
        Check(service.EvaluateAttack("a", "c", now.AddMinutes(3)).Code == RuntimePvpDecisionCode.LevelRangeDenied, "level-range");
        service.UpsertActor(new("a", 10, "lawless", PartyId: "p", PvpEnabled: false));
        service.UpsertActor(new("b", 12, "lawless", PartyId: "p", PvpEnabled: false, IsCriminal: true));
        Check(service.EvaluateAttack("a", "b", now.AddMinutes(4)).Code == RuntimePvpDecisionCode.PartyDenied, "same-party-denial");
        var evidenceCatalog = new RuntimePvpZoneCatalog(1, [],
            [new RuntimePvpSafeRoomEvidence("proven-safe", "proven", ["decoded.room.attribute:42"]),
             new RuntimePvpSafeRoomEvidence("unproven-safe", "candidate", ["room-name-match"])]);
        Check(evidenceCatalog.IsProvenSafe("proven-safe"), "proven-safe-room-admitted");
        Check(!evidenceCatalog.IsProvenSafe("unproven-safe") && !evidenceCatalog.IsProvenSafe("ordinary-room"),
            "unproven-and-ordinary-rooms-remain-open");
        Check(service.ValidateState().Count == 0, "state-invariants");
        return new(failures.Count == 0, checks, failures);
        void Check(bool condition, string name) { checks.Add(name); if (!condition) failures.Add(name); }
    }
}
