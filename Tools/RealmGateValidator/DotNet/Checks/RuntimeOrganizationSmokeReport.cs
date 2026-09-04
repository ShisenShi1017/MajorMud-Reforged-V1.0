using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeOrganizationSmokeReport(bool Passed, IReadOnlyList<string> Checks, IReadOnlyList<string> Failures)
{
    public static RuntimeOrganizationSmokeReport Run()
    {
        var checks = new List<string>();
        var failures = new List<string>();
        var service = new RuntimeOrganizationService();
        var now = new DateTimeOffset(2026, 7, 15, 18, 0, 0, TimeSpan.Zero);
        var created = service.CreateDefinedOrganization("guild:wardens", "Wardens", RuntimeOrganizationKind.Guild, "leader", now);
        Check(created.Succeeded, "defined-organization-created", checks, failures);
        var rank = new RuntimeOrganizationRank("officer", "Officer", 100, RuntimeOrganizationPermission.Invite | RuntimeOrganizationPermission.RemoveMember | RuntimeOrganizationPermission.Speak);
        Check(service.DefineRank("leader", "guild:wardens", rank).Succeeded, "rank-defined", checks, failures);
        var invited = service.Invite("leader", "guild:wardens", "member", now);
        var inviteId = invited.Snapshot?.Invitations.Single().InvitationId ?? string.Empty;
        Check(invited.Succeeded && inviteId.Length > 0, "invitation-created", checks, failures);
        Check(service.Accept("member", inviteId, now.AddSeconds(1)).Succeeded, "invitation-accepted", checks, failures);
        Check(service.SetRank("leader", "guild:wardens", "member", "officer").Succeeded, "member-promoted", checks, failures);
        var route = service.RouteGuildMessage("member");
        Check(route.Succeeded && route.RecipientActorIds.SequenceEqual(new[] { "leader", "member" }), "guild-chat-routed", checks, failures);
        Check(!service.Leave("leader", "guild:wardens").Succeeded, "leader-leave-blocked", checks, failures);
        Check(service.TransferLeadership("leader", "guild:wardens", "member").Succeeded, "leadership-transferred", checks, failures);
        Check(service.Leave("leader", "guild:wardens").Succeeded, "former-leader-left", checks, failures);
        Check(service.Validate().Count == 0, "organization-state-valid", checks, failures);
        Check(service.Disband("member", "guild:wardens").Succeeded, "organization-disbanded", checks, failures);
        var disabled = service.CreatePlayerOrganization("guild:players", "Players", RuntimeOrganizationKind.Guild, "p1", now, RuntimeOrganizationPolicy.ClassicSafeDefault);
        Check(!disabled.Succeeded && disabled.FailureCode == "organization-player-creation-disabled", "player-creation-fails-closed", checks, failures);
        return new(failures.Count == 0, checks, failures);
    }

    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    private static void Check(bool condition, string name, List<string> checks, List<string> failures) { checks.Add(name); if (!condition) failures.Add(name); }
}
