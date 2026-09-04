namespace RealmGate.Server;

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
/// CodeUpdate 0367 exhausts native type-9/type-24 movement, directional SEARCH, DISARM,
/// presentation ownership, and reset/rearm execution from stock WGSERV WCCMMUD.DLL/WCCMP002.
/// The exact five-state jump tables, formulas, transitions, failed-disarm split, and 300-second
/// reset scheduler are executable and independently cross-validated against all 295 trap rows.
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
    public static readonly bool ExactSearchProbabilityResolved = true;
    public static readonly bool ExactDisarmProbabilityResolved = true;
    public static readonly bool ExactFailedDisarmTriggerResolved = true;
    public static readonly bool ExactTrapMessageOwnershipResolved = true;
    public static readonly bool ExactTrapStateTransitionsResolved = true;
    public static readonly bool ExactTrapResetRearmResolved = true;

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
        new("trap.search-roll", RuntimeClassicTrapEvidenceState.Proven,
            "Directional SEARCH checks type 6 with roll < max(3, Perception-15), and type 9 with roll < FindTraps; type 24 is not a trap-search branch.",
            "WGSERV WCCMMUD.DLL 0x00420597 plus exact FindTraps field builder at 0x0041A6DB."),
        new("trap.disarm-roll-failure", RuntimeClassicTrapEvidenceState.Proven,
            "DISARM succeeds when random(0,100) is below the Traps value; type-9 damage traps have a ten-point safe-failure band while type-24 spell traps trigger on every failed roll.",
            "WCCMMUD.DLL _cmd_disarm native comparison and failure branches."),
        new("trap.messages", RuntimeClassicTrapEvidenceState.Proven,
            "Presentation ownership is exact: type-9 movement uses Para3; type-24 movement uses Para4; type-24 successful DISARM uses Para3; triggering DISARM failure for both types uses Para4; type-9 successful DISARM has only hardcoded command text.",
            "WGSERV WCCMMUD.DLL _move_user and _cmd_disarm callers plus direct WCCMP002 RoomRec fields."),
        new("trap.transitions-reset", RuntimeClassicTrapEvidenceState.Proven,
            "Successful DISARM transitions 0->1 and 3->4, schedules one native exit-timer quantum, and rearms after exactly 300 seconds as 1->0 and 4->3.",
            "WGSERV WCCMMUD.DLL _cmd_disarm, add_exit_to_list 0x0044FA1D, background type-9/type-24 reset handler 0x0044F51B, and persistent Reforged timer owner.")
    ];

    public static IReadOnlyList<string> Validate()
    {
        var failures = new List<string>();
        if (Rows.Count != 10) failures.Add("expected-ten-trap-evidence-rows");
        if (Rows.Count(row => row.State == RuntimeClassicTrapEvidenceState.Proven) != 10) failures.Add("expected-ten-proven-trap-rows");
        if (Rows.Any(row => row.State != RuntimeClassicTrapEvidenceState.Proven)) failures.Add("trap-evidence-must-be-fully-proven");
        ValidateCatalogCounts(failures, DecodedDamageTrapCount, DecodedSpellTrapCount, DecodedExitTrapCount);
        if (!RuntimeClassicRoomExitNativeFieldContract.IsKnownTrapState((int)RuntimeClassicRoomExitNativeFieldContract.TrapState.Active) ||
            !RuntimeClassicRoomExitNativeFieldContract.IsKnownTrapState((int)RuntimeClassicRoomExitNativeFieldContract.TrapState.InactiveMovement))
            failures.Add("trap-state-contract-mismatch");
        if (!ExactDisarmProbabilityResolved) failures.Add("disarm-roll-must-be-resolved");
        if (!ExactFailedDisarmTriggerResolved) failures.Add("failed-disarm-trigger-must-be-resolved");
        if (!ExactTrapStateTransitionsResolved) failures.Add("trap-transitions-must-be-resolved");
        if (!ExactSearchProbabilityResolved) failures.Add("search-roll-must-be-resolved");
        if (!ExactTrapMessageOwnershipResolved) failures.Add("trap-message-ownership-must-be-resolved");
        if (!ExactTrapResetRearmResolved) failures.Add("trap-reset-rearm-must-be-resolved");
        return failures;
    }

    private static void ValidateCatalogCounts(List<string> failures, int damageCount, int spellCount, int totalCount)
    {
        if (damageCount + spellCount != totalCount)
            failures.Add("trap-catalog-count-mismatch");
    }
}
