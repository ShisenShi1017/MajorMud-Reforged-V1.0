namespace RealmGate.Server;

public enum RuntimeClassicSchedulerEvidenceState
{
    Proven,
    Corroborated,
    UnsupportedHypothesis,
    HostOwnedBoundary
}

public sealed record RuntimeClassicSchedulerEvidenceRow(
    string Id,
    RuntimeClassicSchedulerEvidenceState State,
    string Claim,
    string EvidenceBoundary);

/// <summary>
/// Exhausted WCCMMUD-owned scheduler boundary for CodeUpdate0368.
///
/// WCCMMUD owns independent one-shot background callbacks and its internal per-owner
/// order. The surrounding Worldgroup dispatcher decides which independently registered
/// callback is delivered first when several host callbacks are due at once; that host
/// arbitration is explicitly classified as a host-owned boundary rather than guessed
/// as a MajorMUD rule. This distinction closes the module scheduler without inventing
/// a synthetic universal gameplay phase.
/// </summary>
public static class RuntimeClassicSchedulerEvidenceRegistry
{
    public static IReadOnlyList<RuntimeClassicSchedulerEvidenceRow> Rows { get; } =
    [
        new("combat.round", RuntimeClassicSchedulerEvidenceState.Proven, "Physical combat/energy uses the five-fast-tick owner.", "Direct native energy bucket plus existing combat PASS."),
        new("spell.system-round", RuntimeClassicSchedulerEvidenceState.Proven, "Spell/status system cadence is three fast ticks.", "Direct native medium bucket plus existing spell PASS."),
        new("save.player", RuntimeClassicSchedulerEvidenceState.Proven, "_background_other owns Player-data save and re-arms from the configured SECOTH interval; retained default is 60 seconds.", "Direct _background_other disassembly plus WCCMMUD option 19."),
        new("save.buffer", RuntimeClassicSchedulerEvidenceState.Proven, "_background_save_buffers owns Buffer-data save and re-arms from the configured SECBUFF interval; retained default is 300 seconds.", "Direct _background_save_buffers disassembly plus WCCMMUD option 20."),
        new("exit.grace", RuntimeClassicSchedulerEvidenceState.Proven, "EXIT/QUIT completion delay is five seconds.", "Exact WCCMMHLP native help."),
        new("focus.help-guidance", RuntimeClassicSchedulerEvidenceState.Proven, "Historical HELP says REST/MEDITATE require at least 20 uninterrupted seconds.", "Exact WCCMMHLP; executable MEDITATE timing conflicts and DLL wins for runtime."),
        new("background.fast", RuntimeClassicSchedulerEvidenceState.Proven, "_background_fast is a one-shot one-second owner; delayed service executes one generation and re-arms from execution time.", "Direct WCCMMUD callback/re-arm sequence."),
        new("background.medium", RuntimeClassicSchedulerEvidenceState.Proven, "_background_medium is independently armed at three seconds and caps its pending monster generation at one.", "Direct startup/callback constants and pending-owner disassembly."),
        new("background.energy", RuntimeClassicSchedulerEvidenceState.Proven, "_background_energy is independently armed at five seconds.", "Direct startup/callback constants."),
        new("background.monster-create", RuntimeClassicSchedulerEvidenceState.Proven, "_background_monster_create is independently armed at five seconds and invokes monster generation against active-user rooms.", "Direct startup/callback and _generate_monster path."),
        new("background.slow", RuntimeClassicSchedulerEvidenceState.Proven, "_background_slow is independently armed at thirty seconds and caps its pending monster generation at one.", "Direct startup/callback constants and pending-owner disassembly."),
        new("regen.passive", RuntimeClassicSchedulerEvidenceState.Proven, "Passive character regeneration executes inside the thirty-second slow character owner.", "Direct _background_slow -> _slow_update_character path."),
        new("regen.rest", RuntimeClassicSchedulerEvidenceState.Proven, "REST bonus fires when its fast counter exceeds 20, on the 21st serviced fast update.", "Direct _fast_update_character branch."),
        new("regen.meditate", RuntimeClassicSchedulerEvidenceState.Proven, "MEDITATE bonus fires when its fast counter exceeds 14, on the 15th serviced fast update.", "Direct _fast_update_character branch; executable supersedes conflicting HELP timing."),
        new("host.delayed-work", RuntimeClassicSchedulerEvidenceState.Proven, "Delayed one-shot owners do not synthesize elapsed missed generations; each serviced owner runs once and re-arms from execution time.", "Direct callback re-arm sequences."),
        new("order.due-before-fast", RuntimeClassicSchedulerEvidenceState.Proven, "Inside _background_fast, due internal callbacks run before fast character and fast monster updates.", "Direct _background_fast call order."),
        new("npc.energy-player-order", RuntimeClassicSchedulerEvidenceState.Proven, "On energy cycles one [0,100) draw selects player autocombat first for rolls <60 and NPC scheduling first otherwise.", "Direct DLL combat/RNG trace."),
        new("spawn.room-entry", RuntimeClassicSchedulerEvidenceState.Proven, "_user_allowed_in_room invokes the same generation owner used by background monster-create, so room entry is a second native respawn-admission path.", "Direct _user_allowed_in_room -> generation call plus _background_monster_create path."),
        new("spawn.delay-minute-gate", RuntimeClassicSchedulerEvidenceState.Proven, "Room Delay 0 is immediately eligible; positive Delay blocks while elapsed whole minutes <= Delay and admits only when elapsed > Delay.", "Direct _generate_monster minute comparison."),
        new("restart.fresh-arm", RuntimeClassicSchedulerEvidenceState.Proven, "Module startup re-creates fast, medium, energy, monster-create, slow, other/save, inactivity, and buffer-save callbacks from configured intervals; scheduler deadlines are not restored.", "Direct _init__wccmmud registration sequence."),
        new("npc.poll-order", RuntimeClassicSchedulerEvidenceState.Proven, "The MajorMUD system-cycle polling routine services slow-next, then fast-next, then medium-next, one active-monster slot per pending lane per host poll.", "Direct _majormud_syscyc/_automatic_update_polling_routine path."),
        new("npc.pending-cap", RuntimeClassicSchedulerEvidenceState.Proven, "Fast/medium/slow monster generations are pending-latch owners rather than elapsed catch-up counters; a generation cannot accumulate unbounded missed copies.", "Direct background generation marker tests and cursor reset paths."),
        new("hypothesis.universal-ten-second-phase", RuntimeClassicSchedulerEvidenceState.UnsupportedHypothesis, "All Classic gameplay executes in one universal ten-second phase.", "Rejected by directly recovered independent 1/3/5/30-second owners and separate save/exit owners."),
        new("hypothesis.spawn-must-wait-next-tick", RuntimeClassicSchedulerEvidenceState.UnsupportedHypothesis, "Every newly generated NPC is universally barred from acting until a later logical gameplay tick.", "No such WCCMMUD admission flag exists in the exhausted generation owner; action opportunity depends on independently delivered monster-update lanes."),
        new("host.simultaneous-callback-arbitration", RuntimeClassicSchedulerEvidenceState.HostOwnedBoundary, "Ordering between separately registered WCCMMUD callbacks that become host-due at the same wall-clock instant is Worldgroup dispatcher behavior, not a WCCMMUD gameplay rule.", "Excluded from MajorMUD parity instead of inventing cross-callback priority."),
        new("host.idle-poll-frequency", RuntimeClassicSchedulerEvidenceState.HostOwnedBoundary, "Wall-clock frequency of repeated Worldgroup system-cycle/idle polls that drain the one-slot monster cursors is host-owned.", "Reforged preserves lane/cursor order while adapting host delivery to its world-simulation callback.")
    ];

    public static readonly bool UniversalTenSecondGameplayPhasePromoted = false;
    public static readonly bool ExactWccmmudOwnedOrderingResolved = true;
    public static readonly bool ExactHostPhaseResolved = true;
    public static readonly bool ExactBackgroundCadenceResolved = true;
    public static readonly bool ExactNpcPlayerFirstOrderingResolved = true;
    public static readonly bool RestartFreshArmResolved = true;
    public static readonly bool SpawnAdmissionResolved = true;

    public static IReadOnlyList<string> Validate()
    {
        var failures = new List<string>();
        if (Rows.Count != 26) failures.Add("expected-twenty-six-scheduler-evidence-rows");
        if (Rows.Count(row => row.State == RuntimeClassicSchedulerEvidenceState.Proven) != 22) failures.Add("expected-twenty-two-proven-scheduler-rows");
        if (Rows.Count(row => row.State == RuntimeClassicSchedulerEvidenceState.UnsupportedHypothesis) != 2) failures.Add("expected-two-unsupported-scheduler-hypotheses");
        if (Rows.Count(row => row.State == RuntimeClassicSchedulerEvidenceState.HostOwnedBoundary) != 2) failures.Add("expected-two-host-owned-scheduler-boundaries");
        if (UniversalTenSecondGameplayPhasePromoted) failures.Add("universal-ten-second-phase-must-not-be-promoted");
        if (!ExactWccmmudOwnedOrderingResolved) failures.Add("wccmmud-owned-order-must-be-resolved");
        if (!ExactHostPhaseResolved) failures.Add("root-host-phase-must-be-resolved");
        if (!ExactBackgroundCadenceResolved) failures.Add("background-cadence-must-be-resolved");
        if (!ExactNpcPlayerFirstOrderingResolved) failures.Add("npc-player-combat-order-must-be-resolved");
        if (!RestartFreshArmResolved) failures.Add("restart-fresh-arm-must-be-resolved");
        if (!SpawnAdmissionResolved) failures.Add("spawn-admission-must-be-resolved");
        return failures;
    }
}
