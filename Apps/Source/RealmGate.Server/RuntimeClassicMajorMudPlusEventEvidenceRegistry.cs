namespace RealmGate.Server;

public enum RuntimeClassicMajorMudPlusEventEvidenceState
{
    Proven,
    Strong,
    Unknown,
    Excluded
}

public sealed record RuntimeClassicMajorMudPlusEventEvidenceRow(
    string Id,
    RuntimeClassicMajorMudPlusEventEvidenceState State,
    string Claim,
    string EvidenceBoundary);

/// <summary>
/// CodeUpdate0368 closes the Classic MajorMUD Plus contest-event boundary to PASS.
/// WCCMMUTL.EXE seeds its own Borland CRT stream once with srand(time(NULL)), then iterates
/// range-table order -> rune quantity -> up to 50 raw room draws. Each raw draw uses the same
/// recovered accumulated-remainder range helper and is tested for room admission only afterward.
/// </summary>
public static class RuntimeClassicMajorMudPlusEventEvidenceRegistry
{
    public const int LegacyRuneItemNumber = 965;
    public const string CanonicalRuneItemId = "item.965";
    public const int PlacementRangeCount = 17;
    public const int TotalRuneQuantity = 40;
    public const int MaximumAttemptsPerRune = 50;
    public const int ExcludedRoomAttributeMask = 0x10;
    public const int VisibleRoomCapacity = 17;
    public const int HiddenRoomCapacity = 15;
    public static readonly bool ExactNativeRngCallsiteThreadDrawOrderingResolved = true;
    public static readonly bool AutomaticPrizeOrRankingMutationPromoted = false;
    public static readonly bool GenericNativeEventSchedulerPromoted = false;

    public static IReadOnlyList<RuntimeClassicMajorMudPlusEventEvidenceRow> Rows { get; } =
    [
        new("contest.native-routines", RuntimeClassicMajorMudPlusEventEvidenceState.Proven,
            "WCCMMUTL.EXE retains exact contest placement, room-admission/insert, and result-scan routines.",
            "0x00404D80-0x00404FA1; 0x00404CC4-0x00404D7F; 0x00404FA2-0x0040521C."),
        new("contest.rune-identity", RuntimeClassicMajorMudPlusEventEvidenceState.Proven,
            "The contest object is legacy item 965 / canonical item.965.",
            "Retained MajorMUD Plus contract and native executable closure agree on the rune item."),
        new("contest.placement-ranges", RuntimeClassicMajorMudPlusEventEvidenceState.Proven,
            "The native placement table contains exactly 17 map/room ranges totaling 40 runes.",
            "The retained contest_contract preserves each exact range and quantity."),
        new("contest.attempt-and-room-boundary", RuntimeClassicMajorMudPlusEventEvidenceState.Proven,
            "Placement allows at most 50 attempts per rune, rejects room attribute 0x10, and uses capacities 17 visible / 15 hidden.",
            "Recovered room-admission and placement routines."),
        new("contest.visible-hidden-modes", RuntimeClassicMajorMudPlusEventEvidenceState.Proven,
            "The native contest has visible and hidden placement modes; hidden runes use the existing hidden/SEARCH authority.",
            "Retained Plus contract and live RuntimeMajorMudPlus integration."),
        new("contest.status-finish-scan", RuntimeClassicMajorMudPlusEventEvidenceState.Proven,
            "Status/finish enumerate rune ownership locations and finish removes the enumerated rune from world and character state.",
            "Recovered native result-scan routine and retained runtime closure."),
        new("contest.no-auto-prize-ranking", RuntimeClassicMajorMudPlusEventEvidenceState.Proven,
            "The recovered contest routines tabulate locations but do not perform an automatic prize or ranking mutation.",
            "Do not invent prize/ranking effects absent from the exhausted native routines."),
        new("contest.live-runtime-connection", RuntimeClassicMajorMudPlusEventEvidenceState.Proven,
            "RuntimeMajorMudPlus consumes the recovered contest contract and the WCCMMUTL contest-specific CRT stream for start, placement, status, finish, and cleanup.",
            "Raw map-room draws are attempted before admission, exactly matching the native placement routine."),
        new("contest.native-rng-order", RuntimeClassicMajorMudPlusEventEvidenceState.Proven,
            "WCCMMUTL seeds a separate Borland CRT stream once from time(NULL); range-table order, rune quantity, raw room draw, admission test, and retry order are executable contracts.",
            "WCCMMUTL 0x4059D9 srand(time); range helper 0x40307C; placement loop 0x404D80; maximum 50 attempts per rune."),
        new("generic.event-scheduler", RuntimeClassicMajorMudPlusEventEvidenceState.Excluded,
            "Reforged generic scheduled-event machinery is not evidence for native MajorMUD Plus contest semantics.",
            "Pass 43 covers only the recovered Plus contest boundary; it does not assert a universal native event scheduler.")
    ];

    public static IReadOnlyList<string> Validate()
    {
        var failures = new List<string>();
        if (Rows.Count != 10) failures.Add("expected-ten-plus-event-evidence-rows");
        if (Rows.Count(row => row.State == RuntimeClassicMajorMudPlusEventEvidenceState.Proven) != 9) failures.Add("expected-nine-proven-plus-event-rows");
        if (Rows.Any(row => row.State == RuntimeClassicMajorMudPlusEventEvidenceState.Strong)) failures.Add("plus-event-strong-row-remains");
        if (Rows.Any(row => row.State == RuntimeClassicMajorMudPlusEventEvidenceState.Unknown)) failures.Add("plus-event-open-row-remains");
        if (Rows.Count(row => row.State == RuntimeClassicMajorMudPlusEventEvidenceState.Excluded) != 1) failures.Add("expected-one-excluded-plus-event-row");
        if (!ExactNativeRngCallsiteThreadDrawOrderingResolved) failures.Add("native-contest-rng-order-must-be-resolved");
        if (AutomaticPrizeOrRankingMutationPromoted) failures.Add("automatic-prize-ranking-must-not-be-promoted");
        if (GenericNativeEventSchedulerPromoted) failures.Add("generic-native-event-scheduler-must-not-be-promoted");
        return failures;
    }
}
