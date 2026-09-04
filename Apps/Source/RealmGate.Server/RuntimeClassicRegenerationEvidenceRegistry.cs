namespace RealmGate.Server;

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
/// CodeUpdate0336 direct WCCMMUD.DLL regeneration evidence. Historical HELP is kept
/// separately where its MEDITATE timing guidance conflicts with executable behavior.
/// </summary>
public static class RuntimeClassicRegenerationEvidenceRegistry
{
    public const int StockHitPointDivisor = 750;
    public const int RestingMultiplier = 3;
    public const int ManaDivisor = 1650;
    public const int PassiveCadenceSeconds = 30;
    public const int RestCadenceFastTicks = 21;
    public const int MeditateCadenceFastTicks = 15;
    public const int FocusHelpSeconds = 20;

    public static IReadOnlyList<RuntimeClassicRegenerationEvidenceRow> Rows { get; } =
    [
        new("native.help.focus-guidance", RuntimeClassicRegenerationEvidenceState.Proven,
            "Historical REST/MEDITATE HELP says increased regeneration requires at least 20 uninterrupted seconds.",
            "Exact WCCMMHLP message; MEDITATE executable timing conflicts and is governed by the DLL."),
        new("native.help.interruptions", RuntimeClassicRegenerationEvidenceState.Proven,
            "Movement, attacking, or being attacked terminates REST/MEDITATE and yields no pending bonus.",
            "Exact WCCMMHLP message plus native command/state branches."),
        new("hp.stock-base", RuntimeClassicRegenerationEvidenceState.Proven,
            "Stock HP regeneration uses max(1, trunc(((Level + 20) * Health) / 750)).",
            "Direct WCCMMUD.DLL _slow_update_character arithmetic."),
        new("hp.greatermud-divisor", RuntimeClassicRegenerationEvidenceState.RejectedForClassic,
            "Use divisor 500 for Classic MajorMUD HP regeneration.",
            "Direct DLL uses /750; retained /500 branch belongs to GreaterMUD reference code."),
        new("hp.rest-multiplier", RuntimeClassicRegenerationEvidenceState.Proven,
            "REST multiplies the stock HP base by three before HPRegen adjustment.",
            "Direct WCCMMUD.DLL _fast_update_character REST branch."),
        new("timer.passive-hp", RuntimeClassicRegenerationEvidenceState.Proven,
            "Passive player regeneration runs in the 30-fast-tick native slow bucket.",
            "Direct _background_slow -> slow-character call path and scheduler constant 30."),
        new("timer.rest-hp", RuntimeClassicRegenerationEvidenceState.Proven,
            "REST fires when its fast counter exceeds 20, so each bonus occurs on the 21st serviced fast callback after reset.",
            "Direct _fast_update_character counter increment/compare/reset branch."),
        new("timer.meditate-mana", RuntimeClassicRegenerationEvidenceState.Proven,
            "MEDITATE fires when its fast counter exceeds 14, so each bonus occurs on the 15th serviced fast callback after reset.",
            "Direct _fast_update_character counter branch; executable behavior supersedes conflicting 20-second HELP guidance."),
        new("mana.types1-4-base", RuntimeClassicRegenerationEvidenceState.Proven,
            "Magic types 1-4 use INT, WIL, trunc((INT+WIL)/2), or CHA and trunc(((Level+20)*seed*(MagicLevel+2))/1650).",
            "Direct WCCMMUD.DLL slow/fast character mana branches."),
        new("mana.meditation-mpregen-order", RuntimeClassicRegenerationEvidenceState.Proven,
            "Passive mana applies ((100+MPRegen)*base)/100; MEDITATE bypasses MPRegen.",
            "Direct WCCMMUD.DLL passive and meditation branches."),
        new("mana.kai-special", RuntimeClassicRegenerationEvidenceState.Proven,
            "Kai/type 5 uses base 1; passive mana applies MPRegen, while MEDITATE returns the unmodified one-point base.",
            "Direct WCCMMUD.DLL type-5 branches."),
        new("timer.character-host-phase", RuntimeClassicRegenerationEvidenceState.Proven,
            "The one-second background-fast callback processes due internal callbacks before fast-character counters; delayed callbacks do not synthesize elapsed catch-up ticks.",
            "Direct _background_fast, internal due queue, _background_slow and _fast_update_character call order."),
        new("response.meditate", RuntimeClassicRegenerationEvidenceState.Proven,
            "Successful MEDITATE response is exactly 'You are now meditating.'",
            "Direct WCCMMUD.DLL native string and command branch."),
        new("cleanup.status-order", RuntimeClassicRegenerationEvidenceState.Unknown,
            "Exact cleanup ordering for every non-movement status/death/disconnect transition while REST/MEDITATE is active.",
            "The focused regen owner is exact; broader lifecycle cleanup ordering is not fully traced.")
    ];

    public static readonly bool GameplayFormulaCorrected = true;
    public static readonly bool StockMajorMudBranchSelected = true;
    public static readonly bool ExactHostPhaseResolved = true;
    public static readonly bool ExactMeditateResponseResolved = true;
    public static readonly bool ExactStatusCleanupOrderingResolved = false;

    public static IReadOnlyList<string> Validate()
    {
        var failures = new List<string>();
        if (Rows.Count != 14) failures.Add("expected-fourteen-regeneration-evidence-rows");
        if (Rows.Count(row => row.State == RuntimeClassicRegenerationEvidenceState.Proven) != 12) failures.Add("expected-twelve-proven-regeneration-rows");
        if (Rows.Count(row => row.State == RuntimeClassicRegenerationEvidenceState.RejectedForClassic) != 1) failures.Add("expected-one-rejected-classic-row");
        if (Rows.Count(row => row.State == RuntimeClassicRegenerationEvidenceState.Unknown) != 1) failures.Add("expected-one-unknown-regeneration-row");
        if (!GameplayFormulaCorrected) failures.Add("gameplay-formula-must-be-corrected");
        if (!StockMajorMudBranchSelected) failures.Add("stock-major-mud-branch-must-be-selected");
        if (!ExactHostPhaseResolved) failures.Add("character-host-phase-must-be-resolved");
        if (!ExactMeditateResponseResolved) failures.Add("meditate-response-must-be-resolved");
        if (ExactStatusCleanupOrderingResolved) failures.Add("status-cleanup-order-must-remain-open");
        return failures;
    }
}
