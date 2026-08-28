namespace Reforged.Server;

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
/// Pass 44 closes the broad Classic party-coordination assertion only to STRONG.
/// Exact WCCMMHLP payloads prove the command/follow/rank/share shell. Retained runtime
/// observations support the six-character cap and reward boundary. User gameplay reports
/// are retained as hypotheses, not promoted as exact native proof. Leader-leave ordering,
/// unexpected disconnect cleanup, and special cross-subsystem edges remain fail-closed.
/// </summary>
public static class RuntimeClassicPartyCoordinationEvidenceRegistry
{
    public const int PartyHelpMessageNumber = 27;
    public const int FollowHelpMessageNumber = 147;
    public const int FollowModeHelpMessageNumber = 153;
    public const int LeaveHelpMessageNumber = 217;
    public const int ShareHelpMessageNumber = 227;
    public const int StrongMaximumPartyMembers = 6;

    public static readonly bool ExactLeaderLeaveDisbandOrderingResolved = false;
    public static readonly bool ExactUnexpectedDisconnectMembershipResolved = false;
    public static readonly bool ExactDeathMembershipCleanupResolved = false;
    public static readonly bool ExactSpecialTeleportPartyPropagationResolved = false;
    public static readonly bool ExactPvpRoomSpellPartyExclusionResolved = false;
    public static readonly bool MegaMudWaitTimerPromotedAsNativeMajorMud = false;

    public static IReadOnlyList<RuntimeClassicPartyCoordinationEvidenceRow> Rows { get; } =
    [
        new("party.native-help-command-shell", RuntimeClassicPartyCoordinationEvidenceState.Proven,
            "Native help defines INVITE, UNINVITE, JOIN, LEAVE, PARTY, FRONTRANK, and BACKRANK and requires an invitation before joining.",
            "WCCMMHLP message 27 plus JOIN/LEAVE help messages 157/217."),
        new("party.native-follow-movement", RuntimeClassicPartyCoordinationEvidenceState.Proven,
            "After an invitation, FOLLOW starts following; while following the character moves where the leader moves; LEAVE, UNINVITE, or explicit game exit stops the follower.",
            "Exact WCCMMHLP message 147 payload."),
        new("party.native-rank-follow-mode", RuntimeClassicPartyCoordinationEvidenceState.Proven,
            "Backrank may not outnumber Frontrank, and SET FOLLOW BLIND/NORMAL controls room-description output while following.",
            "Exact WCCMMHLP messages 27 and 153."),
        new("party.native-share", RuntimeClassicPartyCoordinationEvidenceState.Proven,
            "SHARE defaults to PARTY, divides whole currency units among eligible recipients, excludes the sender, and leaves the remainder with the sender.",
            "Exact WCCMMHLP message 227."),
        new("party.capacity", RuntimeClassicPartyCoordinationEvidenceState.Strong,
            "Classic party capacity is six total characters including the leader.",
            "Retained native-era/operator correlation already activated by Pass 24; exact party-full prose remains unresolved."),
        new("party.reward-boundary", RuntimeClassicPartyCoordinationEvidenceState.Strong,
            "NPC XP eligibility is active-combat based rather than party-membership based, native-era captures drop monster currency to the room, and SHARE is the explicit party currency distribution path.",
            "Pass 23/24 retained combat-XP and party-reward evidence."),
        new("party.keyed-door-composition", RuntimeClassicPartyCoordinationEvidenceState.Strong,
            "A follower movement is re-evaluated after the leader moves; a door already opened/unlocked by the leader is traversed by resulting door state rather than by inventing a second party key requirement.",
            "Composition of exact FOLLOW movement help with the evidence-backed door open/locked traversal boundary; no party-specific key-sharing rule is invented."),
        new("party.leader-leave", RuntimeClassicPartyCoordinationEvidenceState.Unknown,
            "Exact multi-member leader LEAVE/disband state transition, cleanup ordering, and native rendering.",
            "User report says the party disbands, but no retained native executable/message proof closes the exact edge yet."),
        new("party.disconnect-death-teleport-pvp", RuntimeClassicPartyCoordinationEvidenceState.Unknown,
            "Unexpected disconnect membership cleanup, death membership cleanup, special teleport propagation, resurrection edges, and PvP room-spell party exclusion require exact native ownership/evidence.",
            "No MegaMUD client wait behavior is imported as a MajorMUD server rule; special scripted/targeted effects must be proven individually."),
        new("party.megamud-wait", RuntimeClassicPartyCoordinationEvidenceState.Excluded,
            "MegaMUD client-side wait timers are not MajorMUD party-server evidence and are not promoted into Classic runtime behavior.",
            "Client automation behavior is outside the native MajorMUD server parity boundary.")
    ];

    public static IReadOnlyList<string> Validate()
    {
        var failures = new List<string>();
        if (Rows.Count != 10) failures.Add("expected-ten-party-evidence-rows");
        if (Rows.Count(row => row.State == RuntimeClassicPartyCoordinationEvidenceState.Proven) != 4) failures.Add("expected-four-proven-party-rows");
        if (Rows.Count(row => row.State == RuntimeClassicPartyCoordinationEvidenceState.Strong) != 3) failures.Add("expected-three-strong-party-rows");
        if (Rows.Count(row => row.State == RuntimeClassicPartyCoordinationEvidenceState.Unknown) != 2) failures.Add("expected-two-open-party-rows");
        if (Rows.Count(row => row.State == RuntimeClassicPartyCoordinationEvidenceState.Excluded) != 1) failures.Add("expected-one-excluded-party-row");
        if (ExactLeaderLeaveDisbandOrderingResolved) failures.Add("leader-leave-must-remain-open");
        if (ExactUnexpectedDisconnectMembershipResolved) failures.Add("unexpected-disconnect-must-remain-open");
        if (ExactDeathMembershipCleanupResolved) failures.Add("death-membership-cleanup-must-remain-open");
        if (ExactSpecialTeleportPartyPropagationResolved) failures.Add("special-teleport-party-propagation-must-remain-open");
        if (ExactPvpRoomSpellPartyExclusionResolved) failures.Add("pvp-room-spell-party-exclusion-must-remain-open");
        if (MegaMudWaitTimerPromotedAsNativeMajorMud) failures.Add("megamud-wait-timer-must-not-be-native");
        if (RuntimePartyPolicy.ClassicStrongEvidence.MaximumMembers != StrongMaximumPartyMembers) failures.Add("classic-party-cap-policy-mismatch");
        if (RuntimePartyPolicy.ClassicStrongEvidence.InviteLifetime is not null) failures.Add("classic-invite-timeout-must-remain-unproven");
        return failures;
    }
}
