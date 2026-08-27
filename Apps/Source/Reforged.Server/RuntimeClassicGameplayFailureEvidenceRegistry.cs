namespace Reforged.Server;

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
/// Pass 41 separates ordinary Classic gameplay rejection from startup/data-file failure and from
/// Reforged-owned transactional recovery.  An ordinary rejected MajorMUD gameplay action is a
/// state-preserving no-op.  Exact command-specific wording versus silence is still evidence-gated.
/// </summary>
public static class RuntimeClassicGameplayFailureEvidenceRegistry
{
    public const string RejectedGameplayDecisionCode = "classic-gameplay-action-no-op";

    public static IReadOnlyList<RuntimeClassicGameplayFailureEvidenceRow> Rows { get; } =
    [
        new("gameplay.rejection-state-preserving", RuntimeClassicGameplayFailureEvidenceState.Proven,
            "An ordinary rejected Classic gameplay action leaves gameplay state unchanged.",
            "Operator-confirmed Classic boundary promoted as the narrow PASS in Pass 33; no synthetic recovery transaction is part of the rejected action."),
        new("gameplay.rejection-no-recovery-side-effects", RuntimeClassicGameplayFailureEvidenceState.Proven,
            "Ordinary rejection does not request rollback, disable a subsystem, stop world load, or disconnect the character.",
            "RuntimeErrorRecoveryService maps GameplayActionRejected to NoOp and the Pass 41 smoke applies that decision without changing recovery-service state."),
        new("native-file.incompatible-hard-load-boundary", RuntimeClassicGameplayFailureEvidenceState.Proven,
            "Wrong-version, wrong-identity, or corrupt native MajorMUD data belongs to the catastrophic startup/load boundary rather than ordinary gameplay rejection.",
            "Pass 33 exact-file/exact-version evidence; no migration or graceful gameplay recovery is invented."),
        new("gameplay.command-failure-wording", RuntimeClassicGameplayFailureEvidenceState.Unknown,
            "Exact command/action-specific failure text versus silent no-op.",
            "Must be recovered per native command/action; a generic synthetic error string is not authoritative."),
        new("gameplay.malformed-reference-behavior", RuntimeClassicGameplayFailureEvidenceState.Unknown,
            "Exact runtime behavior for malformed or missing MajorMUD data references reached after startup.",
            "Do not generalize the catastrophic incompatible-file boundary to every missing record/reference."),
        new("persistence.catastrophic-save-rollback-order", RuntimeClassicGameplayFailureEvidenceState.Unknown,
            "Exact native catastrophic save rollback/participant ordering.",
            "Ordinary player/world save coordination remains a separate native persistence gap."),
        new("reforged.transactional-rollback", RuntimeClassicGameplayFailureEvidenceState.Excluded,
            "Reforged transactional rollback helpers are implementation safety mechanisms, not proof of a native ordinary-gameplay rollback subsystem.",
            "May remain for Custom/AI or atomic internal mutations but cannot define Classic rejection semantics."),
        new("worldgroup.crash-recovery", RuntimeClassicGameplayFailureEvidenceState.Excluded,
            "Worldgroup process/session crash recovery is not a MajorMUD gameplay error-handling contract.",
            "Host crash/restart coordination remains outside this Classic gameplay assertion.")
    ];

    public static IReadOnlyList<string> Validate()
    {
        var failures = new List<string>();
        if (Rows.Count != 8) failures.Add("expected-eight-gameplay-failure-evidence-rows");
        if (Rows.Count(row => row.State == RuntimeClassicGameplayFailureEvidenceState.Proven) != 3) failures.Add("expected-three-proven-failure-rows");
        if (Rows.Count(row => row.State == RuntimeClassicGameplayFailureEvidenceState.Unknown) != 3) failures.Add("expected-three-open-failure-rows");
        if (Rows.Count(row => row.State == RuntimeClassicGameplayFailureEvidenceState.Excluded) != 2) failures.Add("expected-two-excluded-failure-rows");
        return failures;
    }
}
