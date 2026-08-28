namespace Reforged.Server;

public enum RuntimeClassicRegenerationEvidenceState
{
    Proven,
    Strong,
    RejectedForClassic,
    Unknown
}

public sealed record RuntimeClassicRegenerationEvidenceRow(
    string Id,
    RuntimeClassicRegenerationEvidenceState State,
    string Claim,
    string EvidenceBoundary);

/// <summary>
/// Pass 37 evidence boundary for Classic player HP/mana regeneration. Exact native
/// WCCMMHLP text is separated from MMUD Explorer formula/cadence evidence so the
/// runtime can consume strongly-correlated stock behavior without overstating it as
/// direct DLL proof.
/// </summary>
public static class RuntimeClassicRegenerationEvidenceRegistry
{
    public const int StockHitPointDivisor = 750;
    public const int RestingMultiplier = 3;
    public const int ManaDivisor = 1650;
    public const int PassiveCadenceSeconds = 30;
    public const int RestCadenceSeconds = 20;
    public const int MeditateCadenceSeconds = 10;
    public const int FocusActivationSeconds = 20;

    public static IReadOnlyList<RuntimeClassicRegenerationEvidenceRow> Rows { get; } =
    [
        new("native.help.focus-delay", RuntimeClassicRegenerationEvidenceState.Proven,
            "REST and MEDITATE require at least 20 uninterrupted seconds before increased regeneration takes effect.",
            "Exact WCCMMHLP message 205."),
        new("native.help.interruptions", RuntimeClassicRegenerationEvidenceState.Proven,
            "Movement, attacking, or being attacked terminates the restful/meditative state and yields no bonus.",
            "Exact WCCMMHLP message 205."),
        new("hp.stock-base", RuntimeClassicRegenerationEvidenceState.Strong,
            "Stock MajorMUD HP regeneration starts with max(1, Fix(((Level + 20) * Health) / 750)).",
            "MMUD Explorer CalcRestingRate stock branch; exact DLL arithmetic callsite remains untraced."),
        new("hp.greatermud-divisor", RuntimeClassicRegenerationEvidenceState.RejectedForClassic,
            "Use divisor 500 for Classic MajorMUD resting HP regeneration.",
            "MMUD Explorer guards /500 behind bGreaterMUD; Classic must use the non-GreaterMUD branch."),
        new("hp.rest-multiplier", RuntimeClassicRegenerationEvidenceState.Strong,
            "REST multiplies the stock HP base rate by three before HPRegen percentage application.",
            "MMUD Explorer CalcRestingRate implementation and retained Pascal reference comments."),
        new("timer.passive-hp", RuntimeClassicRegenerationEvidenceState.Strong,
            "Passive HP regeneration ticks every 30 seconds.",
            "MMUD Explorer retained cadence evidence; exact host phase remains unresolved."),
        new("timer.rest-hp", RuntimeClassicRegenerationEvidenceState.Strong,
            "Focused REST HP regeneration ticks every 20 seconds after the activation boundary.",
            "MMUD Explorer retained cadence evidence; exact host phase remains unresolved."),
        new("timer.meditate-mana", RuntimeClassicRegenerationEvidenceState.Strong,
            "Focused MEDITATE mana regeneration ticks every 10 seconds after the activation boundary.",
            "MMUD Explorer retained cadence evidence; exact host phase remains unresolved."),
        new("mana.types1-4-base", RuntimeClassicRegenerationEvidenceState.Strong,
            "Magic types 1-4 use INT, WIL, Fix((INT+WIL)/2), or CHA and then Fix((((Level+20)*seed)*(MagicLevel+2))/1650).",
            "MMUD Explorer CalcManaRegen; exact native DLL branch remains untraced."),
        new("mana.meditation-mpregen-order", RuntimeClassicRegenerationEvidenceState.Strong,
            "For magic types 1-4, MEDITATE returns the base mana rate before MPRegen percentage application; passive mana regeneration applies MPRegen afterward.",
            "MMUD Explorer CalcManaRegen bMeditating early-exit branch."),
        new("mana.kai-special", RuntimeClassicRegenerationEvidenceState.Strong,
            "Kai/type 5 uses a one-point regeneration base with MPRegen percentage and bypasses the level/MagicLevel formula.",
            "MMUD Explorer CalcManaRegen type-5 branch; exact native DLL ordering remains below PASS."),
        new("timer.host-phase", RuntimeClassicRegenerationEvidenceState.Unknown,
            "Exact Worldgroup host-clock phase and simultaneous ordering of passive/focused regeneration ticks.",
            "No retained exact native scheduler callsite proves phase/order.")
    ];

    public static readonly bool GameplayFormulaCorrected = true;
    public static readonly bool StockMajorMudBranchSelected = true;
    public static readonly bool ExactHostPhaseResolved = false;
    public static readonly bool ExactMeditateResponseResolved = false;
    public static readonly bool ExactStatusCleanupOrderingResolved = false;

    public static IReadOnlyList<string> Validate()
    {
        var failures = new List<string>();
        if (Rows.Count != 12) failures.Add("expected-twelve-regeneration-evidence-rows");
        if (Rows.Count(row => row.State == RuntimeClassicRegenerationEvidenceState.Proven) != 2) failures.Add("expected-two-proven-regeneration-rows");
        if (Rows.Count(row => row.State == RuntimeClassicRegenerationEvidenceState.Strong) != 8) failures.Add("expected-eight-strong-regeneration-rows");
        if (Rows.Count(row => row.State == RuntimeClassicRegenerationEvidenceState.RejectedForClassic) != 1) failures.Add("expected-one-rejected-classic-row");
        if (Rows.Count(row => row.State == RuntimeClassicRegenerationEvidenceState.Unknown) != 1) failures.Add("expected-one-unknown-regeneration-row");
        if (!GameplayFormulaCorrected) failures.Add("pass37-must-correct-gameplay-formula");
        if (!StockMajorMudBranchSelected) failures.Add("stock-major-mud-branch-must-be-selected");
        if (ExactHostPhaseResolved) failures.Add("host-phase-must-remain-open");
        if (ExactMeditateResponseResolved) failures.Add("meditate-response-must-remain-open");
        if (ExactStatusCleanupOrderingResolved) failures.Add("status-cleanup-order-must-remain-open");
        return failures;
    }
}
