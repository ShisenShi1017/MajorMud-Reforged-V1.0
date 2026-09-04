using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimePartyCoordinationSmokeReport(bool Passed, IReadOnlyList<string> Checks, IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

    public static RuntimePartyCoordinationSmokeReport Run()
    {
        var checks = new List<string>();
        var failures = new List<string>();
        try
        {
            var now = DateTimeOffset.Parse("2026-07-15T17:00:00Z");
            var service = new RuntimePartyCoordinationService();
            service.Create("party.alpha", "actor.leader");
            var invite = service.Invite("party.alpha", "actor.leader", "actor.two", now);
            var accepted = service.Accept(invite.InviteId, "actor.two", now.AddSeconds(1));
            if (!accepted.Succeeded || accepted.Snapshot?.Members.Count != 2) throw new InvalidOperationException("party-accept-failed");
            checks.Add("invite-accept");

            var third = service.Invite("party.alpha", "actor.leader", "actor.three", now);
            service.Accept(third.InviteId, "actor.three", now.AddSeconds(1));
            service.SetConnected("actor.two", true, "room.1");
            service.SetConnected("actor.three", true, "room.2");
            service.SetFollowing("actor.two", true);
            service.SetFollowing("actor.three", true);
            var moves = service.ResolveFollowerMoves("actor.leader", "room.1", "room.9");
            if (moves.Count != 1 || moves[0].ActorId != "actor.two") throw new InvalidOperationException("follow-routing-failed");
            checks.Add("shared-movement-routing");

            var route = service.RoutePartyMessage("actor.two");
            if (!route.Succeeded || route.RecipientActorIds.Count != 3) throw new InvalidOperationException("party-chat-routing-failed");
            checks.Add("party-chat-routing");

            var transferBlocked = service.TransferLeadership("actor.leader", "actor.two");
            if (transferBlocked.Succeeded || transferBlocked.FailureCode != "party-leadership-transfer-disabled") throw new InvalidOperationException("unproven-leadership-transfer-not-gated");
            var kicked = service.Kick("actor.leader", "actor.three");
            if (!kicked.Succeeded || kicked.Snapshot?.Members.Count != 2) throw new InvalidOperationException("party-kick-failed");
            checks.Add("native-uninvite-kick-and-leader-transfer-gate");

            var longLived = service.Invite("party.alpha", "actor.leader", "actor.no-expiry", now);
            var acceptedLater = service.Accept(longLived.InviteId, "actor.no-expiry", now.AddDays(30));
            if (!acceptedLater.Succeeded) throw new InvalidOperationException("invented-invite-expiry-still-active");
            checks.Add("no-invented-invite-timeout");

            var leaderLeave = service.Leave("actor.leader");
            if (leaderLeave.Succeeded || leaderLeave.FailureCode != "party-leader-leave-native-unknown") throw new InvalidOperationException("unproven-leader-leave-succession-not-gated");
            checks.Add("leader-leave-succession-gated");

            service.SetConnected("actor.leader", false, "room.1");
            var onlineRoute = service.RoutePartyMessage("actor.two");
            if (onlineRoute.RecipientActorIds.Contains("actor.leader", StringComparer.OrdinalIgnoreCase)) throw new InvalidOperationException("offline-routing-failed");
            checks.Add("compatibility-shell-offline-routing-not-classic-parity");

            if (service.Validate().Count != 0) throw new InvalidOperationException("party-validator-failed");
            checks.Add("party-state-validation");
        }
        catch (Exception ex)
        {
            failures.Add(ex.GetType().Name + ":" + ex.Message);
        }
        return new(failures.Count == 0, checks, failures);
    }
}
