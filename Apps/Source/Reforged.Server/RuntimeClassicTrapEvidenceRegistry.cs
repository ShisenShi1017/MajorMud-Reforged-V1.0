namespace Reforged.Server;

public enum RuntimeClassicTrapEvidenceState
{
    Proven,
    Strong,
    Unknown
}

public sealed record RuntimeClassicTrapEvidenceRow(
    string Id,
    RuntimeClassicTrapEvidenceState State,
    string Claim,
    string EvidenceBoundary);

/// <summary>
/// Pass 45 promotes the broad Classic trap boundary only to STRONG. Exact native help
/// closes SEARCH/DISARM admission semantics, while retained RoomRec evidence and the
/// complete 295-exit trap corpus strongly recover ownership/state storage. CodeUpdate 0333
/// directly decodes stock WCCMP002 and projects every canonical trap Para2 value. Exact rolls,
/// failed-disarm triggering, message ownership, transitions, and reset/rearm behavior remain fail-closed.
/// </summary>
public static class RuntimeClassicTrapEvidenceRegistry
{
    public const int TrapSkillHelpMessageNumber = 15;
    public const int TrapDisarmHelpMessageNumber = 245;
    public const int DecodedExitTrapCount = 295;
    public const int DecodedDamageTrapCount = 273;
    public const int DecodedSpellTrapCount = 22;
    public const int FindTrapsAbilityId = 40;
    public const int DisarmTrapsAbilityId = 41;
    public const int FindTrapsValueAbilityId = 179;

    public static readonly bool ExactPerExitPara2Resolved = true;
    public static readonly bool ExactSearchProbabilityResolved = false;
    public static readonly bool ExactDisarmProbabilityResolved = false;
    public static readonly bool ExactFailedDisarmTriggerResolved = false;
    public static readonly bool ExactTrapMessageOwnershipResolved = false;
    public static readonly bool ExactTrapStateTransitionsResolved = false;
    public static readonly bool ExactTrapResetRearmResolved = false;

    public static IReadOnlyList<RuntimeClassicTrapEvidenceRow> Rows { get; } =
    [
        new("trap.native-help-search", RuntimeClassicTrapEvidenceState.Proven,
            "Normally anyone can find a trap by SEARCH; the Traps skill has a much higher chance.",
            "Exact WCCMMHLP 15 / HL1B4B payload."),
        new("trap.native-help-disarm", RuntimeClassicTrapEvidenceState.Proven,
            "A Traps-skilled character may attempt DISARM TRAP <direction> only after the trap is found with SEARCH.",
            "Exact WCCMMHLP 245 / HL1B106 payload."),
        new("trap.catalog-operands", RuntimeClassicTrapEvidenceState.Proven,
            "The Classic semantic exit corpus retains 295 trap owners: 273 fixed-damage and 22 spell traps, with their effect operands.",
            "Retained canonical Classic exit corpus and room schema evidence."),
        new("trap.roomrec-state-storage", RuntimeClassicTrapEvidenceState.Strong,
            "RoomRec type 09/type 24 trap ownership and signed-16-bit Para2 states 0 Active, 1 Inactive, 2 Hidden, 3 Active movement, 4 Inactive movement are strongly corroborated.",
            "Retained room-record field maps/schema; the lossy MME projection omits per-exit Para2, which CodeUpdate 0333 recovers directly from WCCMP002 in the separate proven per-exit row."),
        new("trap.runtime-admission", RuntimeClassicTrapEvidenceState.Strong,
            "Classic DISARM TRAP enforces the Traps/DisarmTraps admission gate and refuses to invent success or state mutation.",
            "RuntimeLivingWorld Classic command boundary plus RuntimeClassicExitTraps fail-closed activation policy."),
        new("trap.per-exit-state", RuntimeClassicTrapEvidenceState.Proven,
            "Exact stock WCCMP002 Para2 value is recovered for each of the 295 canonical traps: 293 are 0 (Active) and 2 are 2 (Hidden).",
            "CodeUpdate 0333 direct native-room-record decode with 295/295 type, operand, destination, and state validation."),
        new("trap.search-roll", RuntimeClassicTrapEvidenceState.Unknown,
            "Exact SEARCH probability and Traps-skill modifier.",
            "Native help proves relative advantage but not arithmetic."),
        new("trap.disarm-roll-failure", RuntimeClassicTrapEvidenceState.Unknown,
            "Exact DISARM probability and failed-disarm trigger behavior.",
            "No exact native arithmetic/caller sequence retained."),
        new("trap.messages", RuntimeClassicTrapEvidenceState.Unknown,
            "Exact Para3/Para4 passage/fail ownership and rendering.",
            "Retained editor documentation conflicts; do not choose a side without native caller proof."),
        new("trap.transitions-reset", RuntimeClassicTrapEvidenceState.Unknown,
            "Exact Para2 state transitions, rearm/reset/persistence, and simultaneous ordering.",
            "No generic Reforged trap timer/reset behavior is promoted as Classic evidence.")
    ];

    public static IReadOnlyList<string> Validate()
    {
        var failures = new List<string>();
        if (Rows.Count != 10) failures.Add("expected-ten-trap-evidence-rows");
        if (Rows.Count(row => row.State == RuntimeClassicTrapEvidenceState.Proven) != 4) failures.Add("expected-four-proven-trap-rows");
        if (Rows.Count(row => row.State == RuntimeClassicTrapEvidenceState.Strong) != 2) failures.Add("expected-two-strong-trap-rows");
        if (Rows.Count(row => row.State == RuntimeClassicTrapEvidenceState.Unknown) != 4) failures.Add("expected-four-open-trap-rows");
        ValidateCatalogCounts(failures, DecodedDamageTrapCount, DecodedSpellTrapCount, DecodedExitTrapCount);
        if (!RuntimeClassicRoomExitNativeFieldContract.IsKnownTrapState((int)RuntimeClassicRoomExitNativeFieldContract.TrapState.Active) ||
            !RuntimeClassicRoomExitNativeFieldContract.IsKnownTrapState((int)RuntimeClassicRoomExitNativeFieldContract.TrapState.InactiveMovement))
            failures.Add("trap-state-contract-mismatch");
        var openBoundaries = new (bool Resolved, string Failure)[]
        {
            (ExactSearchProbabilityResolved, "search-roll-must-remain-open"),
            (ExactDisarmProbabilityResolved, "disarm-roll-must-remain-open"),
            (ExactFailedDisarmTriggerResolved, "failed-disarm-trigger-must-remain-open"),
            (ExactTrapMessageOwnershipResolved, "trap-message-ownership-must-remain-open"),
            (ExactTrapStateTransitionsResolved, "trap-transitions-must-remain-open"),
            (ExactTrapResetRearmResolved, "trap-reset-rearm-must-remain-open")
        };
        failures.AddRange(openBoundaries.Where(boundary => boundary.Resolved).Select(boundary => boundary.Failure));
        return failures;
    }

    private static void ValidateCatalogCounts(List<string> failures, int damageCount, int spellCount, int totalCount)
    {
        if (damageCount + spellCount != totalCount)
            failures.Add("trap-catalog-count-mismatch");
    }
}
