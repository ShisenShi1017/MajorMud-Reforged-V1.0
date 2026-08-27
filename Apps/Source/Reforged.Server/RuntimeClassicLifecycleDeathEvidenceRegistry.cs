namespace Reforged.Server;

public enum RuntimeClassicLifecycleDeathEvidenceState
{
    Proven,
    Strong,
    UnsupportedHypothesis,
    Unknown,
    ExcludedWorldgroup
}

public sealed record RuntimeClassicLifecycleDeathEvidenceRow(
    string Id,
    RuntimeClassicLifecycleDeathEvidenceState State,
    string Claim,
    string EvidenceBoundary);

/// <summary>
/// Pass 39 verifies user-supplied lifecycle/death/combat-order hypotheses against retained
/// Classic evidence. Only MajorMUD-owned gameplay consequences are represented here; raw
/// Worldgroup socket/account takeover remains outside Classic parity scope.
/// </summary>
public static class RuntimeClassicLifecycleDeathEvidenceRegistry
{
    public static IReadOnlyList<RuntimeClassicLifecycleDeathEvidenceRow> Rows { get; } =
    [
        new("exit.delay-save", RuntimeClassicLifecycleDeathEvidenceState.Proven,
            "EXIT/QUIT waits five seconds and automatically saves the character when leaving MajorMUD.",
            "Exact retained WCCMMHLP/official MajorMUD help; live host already uses the five-second authority and session save finalizer."),
        new("party.follower-explicit-exit", RuntimeClassicLifecycleDeathEvidenceState.Proven,
            "A follower who explicitly exits the game stops following.",
            "Retained native FOLLOW help message 147; live Classic explicit-exit hook removes non-leader followers."),
        new("death.suicide-normal-death", RuntimeClassicLifecycleDeathEvidenceState.Proven,
            "SUICIDE has the same normal-death consequences for the life counter and costs one life.",
            "Official MajorMUD command help plus existing PASS character.life-loss-on-death authority."),
        new("death.final-life-reroll", RuntimeClassicLifecycleDeathEvidenceState.Proven,
            "SUICIDE on the last life is equivalent to REROLL; SUICIDE NOW also rerolls.",
            "Official MajorMUD command help. Exact final-death retained-experience arithmetic remains separately gated."),
        new("combat.player-priority-queue", RuntimeClassicLifecycleDeathEvidenceState.UnsupportedHypothesis,
            "Physical actions are globally sorted by ActionDelay = WeaponSpeed - Agility/20 - Haste/10 with agility/level/random tie-breakers.",
            "Contradicted by the already-PASS stock attack-energy equation and 1000-energy carry-forward/five-swing scheduler."),
        new("combat.precompute-all-attacks", RuntimeClassicLifecycleDeathEvidenceState.Unknown,
            "Every physical attack result in a round is precomputed from immutable state before any HP changes are applied.",
            "No retained native trace proves a two-pass PendingAttack model or same-round lethal purge ordering."),
        new("linkdead.fixed-three-minutes", RuntimeClassicLifecycleDeathEvidenceState.UnsupportedHypothesis,
            "Unexpected socket loss creates a MajorMUD-owned fixed three-minute LINKDEATH timer.",
            "User document is not native evidence; retained MajorMUD evidence does not recover this timer."),
        new("linkdead.same-user-ip-reattach", RuntimeClassicLifecycleDeathEvidenceState.ExcludedWorldgroup,
            "Reconnect requires the same username/IP and reattaches the socket to the in-memory player object.",
            "Raw reconnect/socket takeover/account ownership is Worldgroup host plumbing and excluded from MajorMUD gameplay parity."),
        new("linkdead.combat-retention", RuntimeClassicLifecycleDeathEvidenceState.Unknown,
            "A disconnected character necessarily remains in room combat, auto-attacks, and can die until combat ends.",
            "Exact MajorMUD gameplay consequences of unexpected disconnect remain unresolved."),
        new("linkdead.timer-pause-in-combat", RuntimeClassicLifecycleDeathEvidenceState.Unknown,
            "The disconnect cleanup timer pauses whenever combat resumes.",
            "No retained native call trace or help text proves this interaction."),
        new("party.leader-exit-successor", RuntimeClassicLifecycleDeathEvidenceState.Unknown,
            "When a party leader exits, the next highest-level member is automatically promoted.",
            "Native leader-leave succession and tie-breaking remain unresolved; live Classic fails closed rather than guessing."),
        new("followers.mercenary-tavern-return", RuntimeClassicLifecycleDeathEvidenceState.Unknown,
            "Hired followers are serialized, unspawned, and returned for hire at their original tavern on player exit.",
            "No retained exact native follower ownership/cleanup evidence proves this sequence."),
        new("followers.charm-hostile-on-exit", RuntimeClassicLifecycleDeathEvidenceState.Unknown,
            "Charmed/summoned monsters immediately become hostile to remaining room occupants when the controller exits.",
            "Retained control-lifecycle work gates exact logout/disconnect consequences."),
        new("persistence.player-json-save-destroy", RuntimeClassicLifecycleDeathEvidenceState.UnsupportedHypothesis,
            "Native MajorMUD serializes the character to player.json and destroys the in-memory object after a verified file write.",
            "Modern Reforged-style architecture; not a native MajorMUD storage contract."),
        new("death.xp-level-under-ten-immunity", RuntimeClassicLifecycleDeathEvidenceState.Unknown,
            "Characters below level 10 lose no experience on normal death.",
            "User document hypothesis only; not recovered from retained native help, configuration, or formula evidence."),
        new("death.xp-ten-percent", RuntimeClassicLifecycleDeathEvidenceState.Unknown,
            "Normal death removes floor(total experience / 10).",
            "No retained exact native formula/callsite proves percentage or rounding."),
        new("death.xp-two-million-cap", RuntimeClassicLifecycleDeathEvidenceState.Unknown,
            "Normal death experience loss is capped at 2,000,000 experience.",
            "No retained exact native cap evidence."),
        new("death.xp-delevel-one", RuntimeClassicLifecycleDeathEvidenceState.Unknown,
            "Normal death can regress exactly one trained level when experience falls below the current threshold.",
            "Exact level-regression policy and post-death stat/pool recalculation remain unresolved."),
        new("death.xp-race-class-modifier-exclusion", RuntimeClassicLifecycleDeathEvidenceState.Strong,
            "Race/class EXP modifiers belong to the experience chart rather than monster-earned XP; therefore they must not be assumed to multiply a death penalty without separate native proof.",
            "Existing experience-chart parity evidence; exact death-loss arithmetic is still UNKNOWN."),
        new("ordering.explicit-exit-save-vs-cleanup", RuntimeClassicLifecycleDeathEvidenceState.Unknown,
            "Exact native ordering among graceful save, combat cleanup, party/follower cleanup, corpse/world-state cleanup, and final departure.",
            "Component behaviors are partly proven, but the cross-owner native order is not."),
        new("ordering.unexpected-disconnect-cleanup", RuntimeClassicLifecycleDeathEvidenceState.Unknown,
            "Exact MajorMUD-owned cleanup ordering after unexpected disconnect.",
            "Linkdead combat retention, party leadership, follower state, corpse reconciliation, and persistence ordering remain open.")
    ];

    public const bool PriorityQueueFormulaPromoted = false;
    public const bool FixedThreeMinuteLinkdeadTimerPromoted = false;
    public const bool SameUserIpReconnectRulePromoted = false;
    public const bool PlayerJsonNativeSavePromoted = false;
    public const bool DeathXpUnderLevelTenImmunityPromoted = false;
    public const bool DeathXpTenPercentPromoted = false;
    public const bool DeathXpTwoMillionCapPromoted = false;
    public const bool ExactLeaderExitSuccessionResolved = false;
    public const bool ExactLinkdeadGameplayResolved = false;
    public const bool ExactCrossOwnerExitOrderingResolved = false;

    public static IReadOnlyList<string> Validate()
    {
        var failures = new List<string>();
        if (Rows.Count != 21) failures.Add("expected-twenty-one-lifecycle-death-evidence-rows");
        if (Rows.Count(row => row.State == RuntimeClassicLifecycleDeathEvidenceState.Proven) != 4) failures.Add("expected-four-proven-lifecycle-rows");
        if (Rows.Count(row => row.State == RuntimeClassicLifecycleDeathEvidenceState.Strong) != 1) failures.Add("expected-one-strong-lifecycle-row");
        if (Rows.Count(row => row.State == RuntimeClassicLifecycleDeathEvidenceState.UnsupportedHypothesis) != 3) failures.Add("expected-three-unsupported-hypotheses");
        if (Rows.Count(row => row.State == RuntimeClassicLifecycleDeathEvidenceState.ExcludedWorldgroup) != 1) failures.Add("expected-one-worldgroup-exclusion");
        if (Rows.Count(row => row.State == RuntimeClassicLifecycleDeathEvidenceState.Unknown) != 12) failures.Add("expected-twelve-open-lifecycle-death-rows");
        if (PriorityQueueFormulaPromoted) failures.Add("priority-queue-formula-must-not-be-promoted");
        if (FixedThreeMinuteLinkdeadTimerPromoted) failures.Add("three-minute-linkdead-timer-must-not-be-promoted");
        if (SameUserIpReconnectRulePromoted) failures.Add("same-user-ip-reconnect-must-not-be-promoted");
        if (PlayerJsonNativeSavePromoted) failures.Add("player-json-native-save-must-not-be-promoted");
        if (DeathXpUnderLevelTenImmunityPromoted) failures.Add("death-xp-level-gate-must-not-be-promoted");
        if (DeathXpTenPercentPromoted) failures.Add("death-xp-ten-percent-must-not-be-promoted");
        if (DeathXpTwoMillionCapPromoted) failures.Add("death-xp-cap-must-not-be-promoted");
        if (ExactLeaderExitSuccessionResolved) failures.Add("leader-exit-succession-must-remain-open");
        if (ExactLinkdeadGameplayResolved) failures.Add("linkdead-gameplay-must-remain-open");
        if (ExactCrossOwnerExitOrderingResolved) failures.Add("cross-owner-exit-order-must-remain-open");
        return failures;
    }
}
