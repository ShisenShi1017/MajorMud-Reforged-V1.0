namespace RealmGate.Server;

public enum RuntimeClassicGameplayFailureEvidenceState
{
    Proven,
    Strong,
    Unknown,
    Excluded
}

public sealed record RuntimeClassicGameplayFailureEvidenceRow(
    string Id,
    RuntimeClassicGameplayFailureEvidenceState State,
    string Claim,
    string EvidenceBoundary);

/// <summary>
/// Exact Classic MajorMUD failure/recovery ownership boundary.
///
/// The key closure is that Classic has no universal gameplay "recovery manager" and no universal
/// failure-text renderer. Ordinary rejected actions are state-preserving; incompatible native files
/// are hard startup/load failures; post-startup lookup/presentation remains with the command/data
/// owner; and native persistence is record-level best effort with no WCC-selected transaction
/// rollback. Reforged's generic rollback/disable helpers remain implementation extensions.
/// </summary>
public static class RuntimeClassicGameplayFailureEvidenceRegistry
{
    public const string RejectedGameplayDecisionCode = "classic-gameplay-action-no-op";

    public static IReadOnlyList<RuntimeClassicGameplayFailureEvidenceRow> Rows { get; } =
    [
        new("gameplay.rejection-state-preserving", RuntimeClassicGameplayFailureEvidenceState.Proven,
            "An ordinary rejected Classic gameplay action leaves gameplay state unchanged.",
            "Operator-confirmed Classic boundary retained from Pass 33; no synthetic recovery transaction is part of the rejected action."),
        new("gameplay.rejection-no-recovery-side-effects", RuntimeClassicGameplayFailureEvidenceState.Proven,
            "Ordinary rejection does not request rollback, disable a subsystem, stop world load, or disconnect the character merely because the action was rejected.",
            "RuntimeErrorRecoveryService maps GameplayActionRejected to NoOp and the Classic closure smoke verifies no disabled-subsystem state is created."),
        new("native-file.incompatible-hard-load-boundary", RuntimeClassicGameplayFailureEvidenceState.Proven,
            "Wrong-version, wrong-identity, or corrupt native MajorMUD data belongs to the hard startup/load boundary rather than ordinary gameplay rejection.",
            "Pass 33 exact-file/exact-version evidence; Classic has no native migration path."),
        new("gameplay.command-failure-presentation-owner-local", RuntimeClassicGameplayFailureEvidenceState.Proven,
            "Classic failure text versus silence is command/action-owner specific; there is no universal MajorMUD recovery error string to emulate.",
            "Exact strings already recovered by individual command/item/shop/combat owners stay there. The broad recovery layer must not synthesize a generic Classic failure line."),
        new("gameplay.post-startup-reference-owner-local", RuntimeClassicGameplayFailureEvidenceState.Excluded,
            "A malformed or missing post-startup record/reference is handled by the native owner/caller path and is not a single global MajorMUD recovery contract.",
            "The broad recovery assertion therefore fails closed on unbound references without claiming that every owner crashes or every owner silently continues."),
        new("persistence.record-local-partial-save", RuntimeClassicGameplayFailureEvidenceState.Proven,
            "Catastrophic/failed MajorMUD persistence does not roll back earlier successful records through a WCC-selected transaction; saves are synchronous record-level DFA mutations and full buffer saves are best effort by record/family.",
            "Native persistence analysis proves zero _dfaBegTrans/_dfaEndTrans/_dfaAbtTrans/_dfaUnlock imports, exact player/buffer save ownership, exact partial-save semantics, and shutdown buffer-flush ordering."),
        new("reforged.transactional-rollback", RuntimeClassicGameplayFailureEvidenceState.Excluded,
            "Reforged transactional rollback helpers are implementation safety mechanisms, not proof of a native ordinary-gameplay rollback subsystem.",
            "May remain for Custom/AI or atomic internal mutations but cannot define Classic rejection or persistence semantics."),
        new("worldgroup.crash-recovery", RuntimeClassicGameplayFailureEvidenceState.Excluded,
            "Worldgroup/MKDE process crash replay and session restart behavior is not a MajorMUD gameplay error-handling contract.",
            "The supplied host stack owns physical database/cache recovery below WCCMMUD; exact BTRVLOG replay bytes remain outside this gameplay assertion.")
    ];

    public static IReadOnlyList<string> Validate()
    {
        var failures = new List<string>();
        if (Rows.Count != 8) failures.Add("expected-eight-gameplay-failure-evidence-rows");
        if (Rows.Count(row => row.State == RuntimeClassicGameplayFailureEvidenceState.Proven) != 5) failures.Add("expected-five-proven-failure-rows");
        if (Rows.Any(row => row.State == RuntimeClassicGameplayFailureEvidenceState.Unknown)) failures.Add("classic-failure-unknown-row-remains");
        if (Rows.Any(row => row.State == RuntimeClassicGameplayFailureEvidenceState.Strong)) failures.Add("classic-failure-strong-row-remains");
        if (Rows.Count(row => row.State == RuntimeClassicGameplayFailureEvidenceState.Excluded) != 3) failures.Add("expected-three-excluded-failure-rows");
        if (!RuntimeClassicSaveCoordinationAuthority.MajorMudTransactionGroupingResolved ||
            !RuntimeClassicSaveCoordinationAuthority.ExactPartialSaveSemanticsResolved ||
            RuntimeClassicSaveCoordinationAuthority.MajorMudDfaTransactionImports.Count != 0)
            failures.Add("native-partial-save-boundary-not-closed");
        return failures;
    }
}
