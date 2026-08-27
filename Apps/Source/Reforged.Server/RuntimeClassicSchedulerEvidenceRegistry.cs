namespace Reforged.Server;

public enum RuntimeClassicSchedulerEvidenceState
{
    Proven,
    Strong,
    Corroborated,
    UnsupportedHypothesis,
    Unknown
}

public sealed record RuntimeClassicSchedulerEvidenceRow(
    string Id,
    RuntimeClassicSchedulerEvidenceState State,
    string Claim,
    string EvidenceBoundary);

/// <summary>
/// Pass 38 Classic scheduler boundary. MajorMUD does not get modeled as a single
/// synthetic global tick. Each promoted cadence is tied to its actual owner, while
/// host phase and same-cycle ordering remain unresolved.
/// </summary>
public static class RuntimeClassicSchedulerEvidenceRegistry
{
    public static IReadOnlyList<RuntimeClassicSchedulerEvidenceRow> Rows { get; } =
    [
        new("combat.round", RuntimeClassicSchedulerEvidenceState.Proven,
            "Physical combat advances on a five-second Classic combat round.",
            "Existing PASS combat.round-five-seconds runtime authority."),
        new("spell.system-round", RuntimeClassicSchedulerEvidenceState.Proven,
            "Spell/status durations and periodic status processing use a three-second spell/system round.",
            "Existing PASS spells.spell-round-three-seconds runtime authority."),
        new("save.player", RuntimeClassicSchedulerEvidenceState.Proven,
            "Player data save interval is 60 seconds.",
            "Exact WCCMMUD.mcv setup prompt."),
        new("save.buffer", RuntimeClassicSchedulerEvidenceState.Proven,
            "Native Buffer data setup interval is 300 seconds.",
            "Exact WCCMMUD.mcv setup prompt; exact buffer participants/order remain unknown."),
        new("exit.grace", RuntimeClassicSchedulerEvidenceState.Proven,
            "EXIT/QUIT completion delay is five seconds.",
            "Exact WCCMMHLP native help."),
        new("focus.activation", RuntimeClassicSchedulerEvidenceState.Proven,
            "REST/MEDITATE require 20 uninterrupted seconds before focused regeneration becomes active.",
            "Exact WCCMMHLP message 205."),
        new("regen.passive", RuntimeClassicSchedulerEvidenceState.Strong,
            "Passive player regeneration uses a 30-second cadence.",
            "Recovered MMUD Explorer timing/formula evidence; exact host phase unresolved."),
        new("regen.rest", RuntimeClassicSchedulerEvidenceState.Strong,
            "Focused REST HP regeneration uses a 20-second cadence after activation.",
            "Recovered MMUD Explorer timing/formula evidence; exact host phase unresolved."),
        new("regen.meditate", RuntimeClassicSchedulerEvidenceState.Strong,
            "Focused MEDITATE mana regeneration uses a 10-second cadence after activation.",
            "Recovered MMUD Explorer timing/formula evidence; exact host phase unresolved."),
        new("spawn.room-entry", RuntimeClassicSchedulerEvidenceState.Strong,
            "Due lair/NPC regeneration is admitted when a player enters the home room after the minute delay has elapsed, rather than materializing from the global heartbeat.",
            "Native Last Killed/Delay binary strings plus Nightmare Redux room-entry behavior correlation."),
        new("host.delayed-work", RuntimeClassicSchedulerEvidenceState.Corroborated,
            "MajorMUD participates in host-driven delayed/background scheduling through Worldgroup-style callbacks.",
            "WCC/WBB _rtkick/_my_rtkick/_syscyc/_majormud_syscyc/_background_* symbols corroborated with MBBSEmu host APIs."),
        new("hypothesis.universal-ten-second-phase", RuntimeClassicSchedulerEvidenceState.UnsupportedHypothesis,
            "All Classic gameplay/status/regen/spawn work executes in one universal ten-second phase.",
            "Not supported: independently recovered 3s, 5s, 10s, 20s, 30s, 60s, 300s and room-entry/minute-gated owners must not be collapsed into one clock."),
        new("order.same-host-cycle", RuntimeClassicSchedulerEvidenceState.Unknown,
            "Exact ordering when combat, status, regeneration, save, NPC, reset, or other work becomes due on the same host cycle.",
            "No retained exact native call trace proves the cross-owner ordering."),
        new("cadence.background-buckets", RuntimeClassicSchedulerEvidenceState.Unknown,
            "Exact cadence and priority of _background_fast/_medium/_slow/_energy/_inactivity and related native buckets.",
            "Symbols are proven present, cadence constants/callers are not recovered."),
        new("restart.missed-timers", RuntimeClassicSchedulerEvidenceState.Unknown,
            "Exact restart/missed-timer reconciliation, overflow/drift, and host phase semantics.",
            "No retained exact native persistence/dispatch trace."),
        new("npc.player-first-phase", RuntimeClassicSchedulerEvidenceState.Unknown,
            "Players always receive a one-tick reaction advantage before NPC decisions.",
            "User-provided phase document is a hypothesis only; exact native NPC decision cadence/order remains unresolved."),
        new("spawn.next-tick-action", RuntimeClassicSchedulerEvidenceState.Unknown,
            "A newly regenerated NPC is always barred from acting until the next scheduler tick.",
            "No retained native trace proves this cross-owner admission rule.")
    ];

    public const bool UniversalTenSecondGameplayPhasePromoted = false;
    public const bool ExactSameCycleOrderingResolved = false;
    public const bool ExactHostPhaseResolved = false;
    public const bool ExactBackgroundCadenceResolved = false;
    public const bool ExactNpcPlayerFirstOrderingResolved = false;
    public const bool ExactSpawnNextTickActionResolved = false;

    public static IReadOnlyList<string> Validate()
    {
        var failures = new List<string>();
        if (Rows.Count != 17) failures.Add("expected-seventeen-scheduler-evidence-rows");
        if (Rows.Count(row => row.State == RuntimeClassicSchedulerEvidenceState.Proven) != 6) failures.Add("expected-six-proven-scheduler-rows");
        if (Rows.Count(row => row.State == RuntimeClassicSchedulerEvidenceState.Strong) != 4) failures.Add("expected-four-strong-scheduler-rows");
        if (Rows.Count(row => row.State == RuntimeClassicSchedulerEvidenceState.Corroborated) != 1) failures.Add("expected-one-corroborated-scheduler-row");
        if (Rows.Count(row => row.State == RuntimeClassicSchedulerEvidenceState.UnsupportedHypothesis) != 1) failures.Add("expected-one-unsupported-scheduler-hypothesis");
        if (Rows.Count(row => row.State == RuntimeClassicSchedulerEvidenceState.Unknown) != 5) failures.Add("expected-five-unknown-scheduler-rows");
        if (UniversalTenSecondGameplayPhasePromoted) failures.Add("universal-ten-second-phase-must-not-be-promoted");
        if (ExactSameCycleOrderingResolved) failures.Add("same-cycle-order-must-remain-open");
        if (ExactHostPhaseResolved) failures.Add("host-phase-must-remain-open");
        if (ExactBackgroundCadenceResolved) failures.Add("background-cadence-must-remain-open");
        if (ExactNpcPlayerFirstOrderingResolved) failures.Add("npc-player-first-order-must-remain-open");
        if (ExactSpawnNextTickActionResolved) failures.Add("spawn-next-tick-action-must-remain-open");
        return failures;
    }
}
