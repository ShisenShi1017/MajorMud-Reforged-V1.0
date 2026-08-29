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
/// CodeUpdate0336 scheduler boundary. The root native background cadence and character
/// regeneration phase are directly recovered, while restart persistence and full
/// cross-owner ordering remain intentionally open.
/// </summary>
public static class RuntimeClassicSchedulerEvidenceRegistry
{
    public static IReadOnlyList<RuntimeClassicSchedulerEvidenceRow> Rows { get; } =
    [
        new("combat.round", RuntimeClassicSchedulerEvidenceState.Proven, "Physical combat/energy uses the five-fast-tick owner.", "Direct native energy bucket plus existing combat PASS."),
        new("spell.system-round", RuntimeClassicSchedulerEvidenceState.Proven, "Spell/status system cadence is three fast ticks.", "Direct native medium bucket plus existing spell PASS."),
        new("save.player", RuntimeClassicSchedulerEvidenceState.Proven, "Configured Player data save interval is 60 seconds.", "Exact WCCMMUD.mcv setup prompt."),
        new("save.buffer", RuntimeClassicSchedulerEvidenceState.Proven, "Configured Buffer data setup interval is 300 seconds.", "Exact WCCMMUD.mcv prompt; exact native callback ownership remains unresolved."),
        new("exit.grace", RuntimeClassicSchedulerEvidenceState.Proven, "EXIT/QUIT completion delay is five seconds.", "Exact WCCMMHLP native help."),
        new("focus.help-guidance", RuntimeClassicSchedulerEvidenceState.Proven, "Historical HELP says REST/MEDITATE require at least 20 uninterrupted seconds.", "Exact WCCMMHLP; executable MEDITATE timing conflicts."),
        new("background.fast", RuntimeClassicSchedulerEvidenceState.Proven, "_background_fast is one-shot RTKICK(1), increments one logical tick, services due work, then fast character/monster work and re-arms from execution time.", "Direct WCCMMUD.DLL call sequence."),
        new("background.medium", RuntimeClassicSchedulerEvidenceState.Proven, "Native medium background owner is due every 3 logical fast ticks.", "Direct scheduler constant/callback."),
        new("background.energy", RuntimeClassicSchedulerEvidenceState.Proven, "Native energy background owner is due every 5 logical fast ticks.", "Direct scheduler constant/callback."),
        new("background.monster-create", RuntimeClassicSchedulerEvidenceState.Proven, "Native monster-create owner is due every 5 logical fast ticks.", "Direct scheduler constant/callback."),
        new("background.slow", RuntimeClassicSchedulerEvidenceState.Proven, "Native slow owner is due every 30 logical fast ticks.", "Direct scheduler constant/callback."),
        new("regen.passive", RuntimeClassicSchedulerEvidenceState.Proven, "Passive character regeneration executes inside the 30-tick slow character update.", "Direct _background_slow -> _slow_update_character path."),
        new("regen.rest", RuntimeClassicSchedulerEvidenceState.Proven, "REST bonus fires on fast counter >20 (21st callback).", "Direct _fast_update_character branch."),
        new("regen.meditate", RuntimeClassicSchedulerEvidenceState.Proven, "MEDITATE bonus fires on fast counter >14 (15th callback).", "Direct _fast_update_character branch; DLL supersedes HELP conflict."),
        new("host.delayed-work", RuntimeClassicSchedulerEvidenceState.Proven, "A delayed fast callback advances one logical tick only; no elapsed wall-clock catch-up loop is performed while running.", "Direct one-shot RTKICK re-arm and logical tick increment sequence."),
        new("order.due-before-fast", RuntimeClassicSchedulerEvidenceState.Proven, "Internal due callbacks execute before fast character and fast monster updates in the same background-fast callback.", "Direct _background_fast call order."),
        new("npc.energy-player-order", RuntimeClassicSchedulerEvidenceState.Proven, "On energy cycles one [0,100) roll selects player-autocombat first 60% of the time and NPC scheduling first 40%.", "CodeUpdate0335 direct DLL combat/RNG trace."),
        new("spawn.room-entry", RuntimeClassicSchedulerEvidenceState.Strong, "Due lair/NPC regeneration is admitted on home-room player entry after its minute delay rather than materializing solely from a global heartbeat.", "Native Last Killed/Delay strings plus room-entry behavior correlation."),
        new("hypothesis.universal-ten-second-phase", RuntimeClassicSchedulerEvidenceState.UnsupportedHypothesis, "All Classic gameplay executes in one universal ten-second phase.", "Rejected by directly recovered 1/3/5/30-tick owners and separate save/exit boundaries."),
        new("order.same-host-cycle", RuntimeClassicSchedulerEvidenceState.Unknown, "Complete ordering among every combat/status/save/reset/spawn/other owner sharing a host cycle.", "Character due-before-fast order is recovered; the entire owner graph is not."),
        new("restart.missed-timers", RuntimeClassicSchedulerEvidenceState.Unknown, "Restart persistence/reconciliation of logical deadlines and missed callbacks.", "Within-run no-catch-up is proven; persisted scheduler queue state is not."),
        new("spawn.next-tick-action", RuntimeClassicSchedulerEvidenceState.Unknown, "A newly regenerated NPC is always barred from acting until the next scheduler tick.", "No complete native cross-owner admission trace yet."),
        new("npc.slow-generation-stagger", RuntimeClassicSchedulerEvidenceState.Strong, "Each active NPC receives at most one slow update per 30-tick generation, staggered across host cycles after the generation boundary.", "Direct generation-marker and _majormud_syscyc service path; exact per-record micro-order remains to be mirrored."),
        new("background.other-save-ownership", RuntimeClassicSchedulerEvidenceState.Unknown, "Exact ownership/meaning of every background_other/save callback and configuration interaction.", "Cadence roots are recovered, but not every callback's persistence contract.")
    ];

    public static readonly bool UniversalTenSecondGameplayPhasePromoted = false;
    public static readonly bool ExactSameCycleOrderingResolved = false;
    public static readonly bool ExactHostPhaseResolved = true;
    public static readonly bool ExactBackgroundCadenceResolved = true;
    public static readonly bool ExactNpcPlayerFirstOrderingResolved = true;
    public static readonly bool ExactSpawnNextTickActionResolved = false;

    public static IReadOnlyList<string> Validate()
    {
        var failures = new List<string>();
        if (Rows.Count != 24) failures.Add("expected-twenty-four-scheduler-evidence-rows");
        if (Rows.Count(row => row.State == RuntimeClassicSchedulerEvidenceState.Proven) != 17) failures.Add("expected-seventeen-proven-scheduler-rows");
        if (Rows.Count(row => row.State == RuntimeClassicSchedulerEvidenceState.Strong) != 2) failures.Add("expected-two-strong-scheduler-rows");
        if (Rows.Count(row => row.State == RuntimeClassicSchedulerEvidenceState.UnsupportedHypothesis) != 1) failures.Add("expected-one-unsupported-scheduler-hypothesis");
        if (Rows.Count(row => row.State == RuntimeClassicSchedulerEvidenceState.Unknown) != 4) failures.Add("expected-four-unknown-scheduler-rows");
        if (UniversalTenSecondGameplayPhasePromoted) failures.Add("universal-ten-second-phase-must-not-be-promoted");
        if (ExactSameCycleOrderingResolved) failures.Add("complete-same-cycle-order-must-remain-open");
        if (!ExactHostPhaseResolved) failures.Add("root-host-phase-must-be-resolved");
        if (!ExactBackgroundCadenceResolved) failures.Add("background-cadence-must-be-resolved");
        if (!ExactNpcPlayerFirstOrderingResolved) failures.Add("npc-player-combat-order-must-be-resolved");
        if (ExactSpawnNextTickActionResolved) failures.Add("spawn-next-tick-action-must-remain-open");
        return failures;
    }
}
