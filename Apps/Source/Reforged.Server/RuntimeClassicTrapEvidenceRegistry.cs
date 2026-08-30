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
/// CodeUpdate 0346 closes native type-9/type-24 movement and DISARM execution from the
/// original WCCMMUD.DLL. The exact five-state jump tables, success transitions, disarm roll,
/// failed-disarm trigger split, and pre-movement trap ordering are now executable. SEARCH
/// probability, Para3/Para4 message ownership, and reset/rearm scheduling remain open.
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
    public static readonly bool ExactDisarmProbabilityResolved = true;
    public static readonly bool ExactFailedDisarmTriggerResolved = true;
    public static readonly bool ExactTrapMessageOwnershipResolved = false;
    public static readonly bool ExactTrapStateTransitionsResolved = true;
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
        new("trap.roomrec-state-storage", RuntimeClassicTrapEvidenceState.Proven,
            "RoomRec type 09/type 24 trap Para2 states 0 Active, 1 Inactive, 2 Hidden, 3 Active movement, 4 Inactive movement have exact native movement jump-table behavior.",
            "WCCMMUD.DLL _move_user type-9 and type-24 state jump tables plus CodeUpdate 0333 direct WCCMP002 per-exit state projection."),
        new("trap.runtime-admission", RuntimeClassicTrapEvidenceState.Proven,
            "Hidden state 2 is rejected as no exit during normal movement; active states 0/3 trigger before movement and inactive states 1/4 pass without an effect.",
            "WCCMMUD.DLL _move_user type-9/type-24 cases and RuntimeClassicExitTraps live admission/effect consumer."),
        new("trap.per-exit-state", RuntimeClassicTrapEvidenceState.Proven,
            "Exact stock WCCMP002 Para2 value is recovered for each of the 295 canonical traps: 293 are 0 (Active) and 2 are 2 (Hidden).",
            "CodeUpdate 0333 direct native-room-record decode with 295/295 type, operand, destination, and state validation."),
        new("trap.search-roll", RuntimeClassicTrapEvidenceState.Unknown,
            "Exact SEARCH probability and Traps-skill modifier.",
            "Native help proves relative advantage but not arithmetic."),
        new("trap.disarm-roll-failure", RuntimeClassicTrapEvidenceState.Proven,
            "DISARM succeeds when random(0,100) is below the Traps value; type-9 damage traps have a ten-point safe-failure band while type-24 spell traps trigger on every failed roll.",
            "WCCMMUD.DLL _cmd_disarm native comparison and failure branches."),
        new("trap.messages", RuntimeClassicTrapEvidenceState.Unknown,
            "Exact Para3/Para4 passage/fail ownership and rendering.",
            "Retained editor documentation conflicts; do not choose a side without native caller proof."),
        new("trap.transitions-reset", RuntimeClassicTrapEvidenceState.Strong,
            "Successful DISARM transitions 0->1 and 3->4 exactly and the mutated state is persisted; exact native rearm/reset timing remains unresolved.",
            "WCCMMUD.DLL _cmd_disarm proves the transitions; no generic Reforged reset timer is promoted as native evidence.")
    ];

    public static IReadOnlyList<string> Validate()
    {
        var failures = new List<string>();
        if (Rows.Count != 10) failures.Add("expected-ten-trap-evidence-rows");
        if (Rows.Count(row => row.State == RuntimeClassicTrapEvidenceState.Proven) != 7) failures.Add("expected-seven-proven-trap-rows");
        if (Rows.Count(row => row.State == RuntimeClassicTrapEvidenceState.Strong) != 1) failures.Add("expected-one-strong-trap-row");
        if (Rows.Count(row => row.State == RuntimeClassicTrapEvidenceState.Unknown) != 2) failures.Add("expected-two-open-trap-rows");
        ValidateCatalogCounts(failures, DecodedDamageTrapCount, DecodedSpellTrapCount, DecodedExitTrapCount);
        if (!RuntimeClassicRoomExitNativeFieldContract.IsKnownTrapState((int)RuntimeClassicRoomExitNativeFieldContract.TrapState.Active) ||
            !RuntimeClassicRoomExitNativeFieldContract.IsKnownTrapState((int)RuntimeClassicRoomExitNativeFieldContract.TrapState.InactiveMovement))
            failures.Add("trap-state-contract-mismatch");
        if (!ExactDisarmProbabilityResolved) failures.Add("disarm-roll-must-be-resolved");
        if (!ExactFailedDisarmTriggerResolved) failures.Add("failed-disarm-trigger-must-be-resolved");
        if (!ExactTrapStateTransitionsResolved) failures.Add("trap-transitions-must-be-resolved");
        var openBoundaries = new (bool Resolved, string Failure)[]
        {
            (ExactSearchProbabilityResolved, "search-roll-must-remain-open"),
            (ExactTrapMessageOwnershipResolved, "trap-message-ownership-must-remain-open"),
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
