using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimePass44ClassicPartyCoordinationBoundarySmokeReport(
    bool Passed,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

    public static RuntimePass44ClassicPartyCoordinationBoundarySmokeReport Run()
    {
        var checks = new List<string>();
        var failures = new List<string>();
        void Check(bool value, string name) { checks.Add(name); if (!value) failures.Add(name); }

        Check(RuntimeClassicPartyCoordinationEvidenceRegistry.Validate().Count == 0, "classic-party-evidence-registry-valid");
        Check(RuntimeClassicPartyCoordinationEvidenceRegistry.FollowHelpMessageNumber == 147, "native-follow-help-147");
        Check(RuntimeClassicPartyCoordinationEvidenceRegistry.FollowModeHelpMessageNumber == 153, "native-follow-mode-help-153");
        Check(RuntimeClassicPartyCoordinationEvidenceRegistry.LeaveHelpMessageNumber == 217, "native-leave-help-217");
        Check(RuntimeClassicPartyCoordinationEvidenceRegistry.ShareHelpMessageNumber == 227, "native-share-help-227");
        Check(RuntimePartyPolicy.ClassicStrongEvidence.MaximumMembers == 6, "strong-six-character-party-cap");
        Check(RuntimePartyPolicy.ClassicStrongEvidence.InviteLifetime is null, "no-invented-party-invite-timeout");
        Check(!RuntimeClassicPartyCoordinationEvidenceRegistry.ExactLeaderLeaveDisbandOrderingResolved, "leader-leave-remains-exact-unknown");
        Check(!RuntimeClassicPartyCoordinationEvidenceRegistry.MegaMudWaitTimerPromotedAsNativeMajorMud, "no-megamud-wait-timer-in-native-policy");

        var service = new RuntimePartyCoordinationService();
        var now = DateTimeOffset.UtcNow;
        var party = service.Create("party.pass44", "leader", RuntimePartyPolicy.ClassicStrongEvidence);
        var invite = service.Invite(party.PartyId, "leader", "follower", now);
        var accepted = service.Accept(invite.InviteId, "follower", now, followingLeader: true);
        service.SynchronizeConnection("leader", connected: true, "room.1");
        service.SynchronizeConnection("follower", connected: true, "room.1");
        var moves = service.ResolveFollowerMoves("leader", "room.1", "room.2");
        Check(accepted.Succeeded, "invited-follower-accepts");
        Check(moves.Count == 1 && moves[0].ActorId == "follower" && moves[0].DestinationRoomId == "room.2", "follower-move-projection");
        var leaderLeave = service.Leave("leader");
        Check(!leaderLeave.Succeeded && leaderLeave.FailureCode == "party-leader-leave-native-unknown", "leader-leave-fails-closed-pending-proof");

        return new RuntimePass44ClassicPartyCoordinationBoundarySmokeReport(failures.Count == 0, checks, failures);
    }
}
