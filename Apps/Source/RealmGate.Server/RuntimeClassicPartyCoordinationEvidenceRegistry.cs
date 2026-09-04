namespace RealmGate.Server;

public enum RuntimeClassicPartyCoordinationEvidenceState
{
    Proven,
    Strong,
    Unknown,
    Excluded
}

public sealed record RuntimeClassicPartyCoordinationEvidenceRow(
    string Id,
    RuntimeClassicPartyCoordinationEvidenceState State,
    string Claim,
    string EvidenceBoundary);

/// <summary>
/// CodeUpdate0340 closes the native Classic party-coordination boundary. WCCMMUD.DLL
/// directly owns the five-slot group representation, invite/follow transitions, exact rank
/// changes, bare-LEAVE behavior, mode-3 follower movement, target-type 12/13 party filtering,
/// and (with CodeUpdate0339) death/logoff/hangup cleanup. MegaMUD client wait timers remain
/// explicitly excluded from MajorMUD server parity.
/// </summary>
public static class RuntimeClassicPartyCoordinationEvidenceRegistry
{
    public const int PartyHelpMessageNumber = 27;
    public const int FollowHelpMessageNumber = 147;
    public const int FollowModeHelpMessageNumber = 153;
    public const int LeaveHelpMessageNumber = 217;
    public const int ShareHelpMessageNumber = 227;
    public const int StrongMaximumPartyMembers = 6;

    public static readonly bool ExactLeaderLeaveDisbandOrderingResolved = true;
    public static readonly bool ExactUnexpectedDisconnectMembershipResolved = true;
    public static readonly bool ExactDeathMembershipCleanupResolved = true;
    public static readonly bool ExactSpecialTeleportPartyPropagationResolved = true;
    public static readonly bool ExactPvpRoomSpellPartyExclusionResolved = true;
    public static readonly bool ExactRankTransitionsResolved = true;
    public static readonly bool ExactInviteSlotAndLifetimeResolved = true;
    public static readonly bool ExactDisplayAndShareRelationResolved = true;
    public static readonly bool MegaMudWaitTimerPromotedAsNativeMajorMud = false;

    public static IReadOnlyList<RuntimeClassicPartyCoordinationEvidenceRow> Rows { get; } =
    [
        new("party.native-help-command-shell", RuntimeClassicPartyCoordinationEvidenceState.Proven,
            "INVITE, UNINVITE, FOLLOW, JOIN, LEAVE, PARTY, FRONTRANK, MIDRANK and BACKRANK are backed by native party owners. FOLLOW consumes the invitation/group-slot relationship; JOIN <user> delegates directly to FOLLOW, numeric JOIN selects a broadcast group, and JOIN GANG enters the gang branch.",
            "WCCMMHLP command help plus WCCMMUD.DLL _cmd_invite/_cmd_follow/_cmd_join/_cmd_leave and _change_user_rank callers; _cmd_join user branch calls _cmd_follow at 0x4542ff."),
        new("party.native-follow-movement", RuntimeClassicPartyCoordinationEvidenceState.Proven,
            "Ordinary successful leader movement propagates to live followers through _fast_update_character -> _move_user(mode 3); a failed mode-3 move clears the follower leader pointer.",
            "WCCMMUD.DLL _fast_update_character 0x4230c0..0x423128 and _move_user 0x417692."),
        new("party.native-rank-follow-mode", RuntimeClassicPartyCoordinationEvidenceState.Proven,
            "Leader/default rank and exact FRONTRANK/BACKRANK/MIDRANK constraints are recovered: leader is Front on invite; followers default Mid; leader cannot enter Back or Mid; only-front/only-back and native front-vs-back checks are executable.",
            "WCCMMUD.DLL _cmd_invite 0x4568ac and _change_user_rank 0x41e169..0x41e577; exact native messages at 0x47f140..0x47f388."),
        new("party.native-share", RuntimeClassicPartyCoordinationEvidenceState.Proven,
            "SHARE defaults to PARTY and its native party branch walks the same five raw slots and leader-pointer travel relation used by PARTY display, excluding root-to-root pending invitations; whole currency units are divided among eligible recipients and remainder stays with the sender.",
            "WCCMMUD.DLL party SHARE branch 0x46d3ee..0x46d4b7 plus exact WCCMMHLP message 227."),
        new("party.capacity", RuntimeClassicPartyCoordinationEvidenceState.Proven,
            "Classic capacity is exactly six total users: self/leader plus five WORD group slots; a pending INVITE consumes one of the five slots before FOLLOW/JOIN and has no timeout timestamp. If all five slots are occupied, native INVITE skips target notification but still runs the inviter confirmation branch without creating a sixth slot.",
            "WCCMMUD.DLL user fields +0x6ae[5], _add_to_group 0x41e077, and _cmd_invite 0x4568dc..0x45696a."),
        new("party.reward-boundary", RuntimeClassicPartyCoordinationEvidenceState.Strong,
            "NPC XP eligibility is active-combat based rather than party-membership based, native-era captures drop monster currency to the room, and SHARE is the explicit party currency distribution path.",
            "Pass 23/24 retained combat-XP and party-reward evidence; this ownership is outside the party membership state machine."),
        new("party.keyed-door-composition", RuntimeClassicPartyCoordinationEvidenceState.Proven,
            "Followers re-enter the central movement owner independently with the same exit index after the leader moves, so traversal/admission is re-evaluated per follower instead of inheriting the leader's key/admission result.",
            "WCCMMUD.DLL _fast_update_character calls _move_user(follower, leaderExit, 3) at 0x4230fb..0x42310d."),
        new("party.leader-leave", RuntimeClassicPartyCoordinationEvidenceState.Proven,
            "Bare LEAVE and session departure are distinct: a following member LEAVEs via _stop_following; a leader's bare LEAVE does not dissolve the travel party. QUIT/disconnect/death lifecycle departure still detaches all followers and dissolves the group with no successor.",
            "WCCMMUD.DLL _cmd_leave 0x453e37..0x453e59 plus shared _cleanup_when_user_leaves ownership."),
        new("party.disconnect-death-teleport-pvp", RuntimeClassicPartyCoordinationEvidenceState.Proven,
            "Unexpected disconnect and death converge on native party cleanup; generic special/direct movement does not carry followers because propagation is owned only by the ordinary fast-update mode-3 path; death cleanup precedes respawn/resurrection; spell target type 12 excludes self/party while type 13 admits self/party.",
            "Native lifecycle proof; WCCMMUD.DLL _fast_update_character/_move_user and _is_valid_target target-type branches 0x43f15b/0x43f19c."),
        new("party.megamud-wait", RuntimeClassicPartyCoordinationEvidenceState.Excluded,
            "MegaMUD client-side wait timers are not MajorMUD party-server evidence and are not promoted into Classic runtime behavior.",
            "Client automation behavior is outside the native MajorMUD server parity boundary.")
    ];

    public static IReadOnlyList<string> Validate()
    {
        var failures = new List<string>();
        if (Rows.Count != 10) failures.Add("expected-ten-party-evidence-rows");
        if (Rows.Count(row => row.State == RuntimeClassicPartyCoordinationEvidenceState.Proven) != 8) failures.Add("expected-eight-proven-party-rows");
        if (Rows.Count(row => row.State == RuntimeClassicPartyCoordinationEvidenceState.Strong) != 1) failures.Add("expected-one-strong-party-row");
        if (Rows.Any(row => row.State == RuntimeClassicPartyCoordinationEvidenceState.Unknown)) failures.Add("party-native-edge-must-have-no-unknown-rows");
        if (Rows.Count(row => row.State == RuntimeClassicPartyCoordinationEvidenceState.Excluded) != 1) failures.Add("expected-one-excluded-party-row");
        if (!ExactLeaderLeaveDisbandOrderingResolved) failures.Add("leader-leave-must-be-resolved");
        if (!ExactUnexpectedDisconnectMembershipResolved) failures.Add("unexpected-disconnect-must-be-resolved");
        if (!ExactDeathMembershipCleanupResolved) failures.Add("death-membership-cleanup-must-be-resolved");
        if (!ExactSpecialTeleportPartyPropagationResolved) failures.Add("special-teleport-party-propagation-must-be-resolved");
        if (!ExactPvpRoomSpellPartyExclusionResolved) failures.Add("pvp-room-spell-party-exclusion-must-be-resolved");
        if (!ExactRankTransitionsResolved) failures.Add("party-rank-transitions-must-be-resolved");
        if (!ExactInviteSlotAndLifetimeResolved) failures.Add("party-invite-slot-lifetime-must-be-resolved");
        if (!ExactDisplayAndShareRelationResolved) failures.Add("party-display-share-relation-must-be-resolved");
        if (MegaMudWaitTimerPromotedAsNativeMajorMud) failures.Add("megamud-wait-timer-must-not-be-native");
        if (RuntimePartyPolicy.ClassicStrongEvidence.MaximumMembers != StrongMaximumPartyMembers) failures.Add("classic-party-cap-policy-mismatch");
        if (RuntimePartyPolicy.ClassicStrongEvidence.InviteLifetime is not null) failures.Add("classic-native-invite-timeout-must-be-null");
        return failures;
    }
}
