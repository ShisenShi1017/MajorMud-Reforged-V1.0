namespace Reforged.Server;

public enum RuntimeCharacterModifierOrderEvidenceState
{
    Proven,
    Strong,
    Rejected,
    Unknown
}

public sealed record RuntimeCharacterModifierOrderEvidenceRow(
    string Id,
    RuntimeCharacterModifierOrderEvidenceState State,
    string Claim,
    string EvidenceBoundary);

/// <summary>
/// Character modifier-order closure. Direct WCCMMUD.DLL disassembly proves that Classic does
/// not own one universal race/class/equipment/status pipeline. Each formula owner has its own
/// traversal/order. _update_dynamic_stats is one concrete owner: character dynamic abilities,
/// equipped-object abilities, class abilities, race abilities, additional object-reference
/// groups, then the deferred ability-0x7C pass. Formula-specific combat/secondary-stat owners
/// retain their separately recovered ordering.
/// </summary>
public static class RuntimeCharacterModifierOrderEvidenceRegistry
{
    public static IReadOnlyList<RuntimeCharacterModifierOrderEvidenceRow> Rows { get; } =
    [
        new("creation.starting-stat-source", RuntimeCharacterModifierOrderEvidenceState.Proven,
            "Level-1 starting primary statistics are the selected race minimum statistics.",
            "Rejects a generic class-base plus per-level stat-growth creation equation."),
        new("secondary-stat.native-recalculation", RuntimeCharacterModifierOrderEvidenceState.Proven,
            "Native calculate_secondary_stats recomputes formula-specific secondary statistics after stat/equipment changes.",
            "Exact MaxHP/MaxMana operand order is owner-local and does not imply one universal modifier pipeline."),
        new("ability.primary-stat-signed-delta", RuntimeCharacterModifierOrderEvidenceState.Strong,
            "Retained primary-stat abilities 45-49 are signed parameter-value deltas.",
            "No generic percentage stage is promoted beyond the owner that consumes each ability."),
        new("equipment.direct-combat-additive", RuntimeCharacterModifierOrderEvidenceState.Strong,
            "Decoded direct AC/DR/accuracy/speed equipment fields aggregate additively in their recovered owners.",
            "No universal flat-then-percent equipment stat stage exists as a Classic-wide rule."),
        new("combat.raw-dodge-encumbrance-placement", RuntimeCharacterModifierOrderEvidenceState.Strong,
            "Recovered raw Dodge has an explicit encumbrance placement before Dodge-vs-Accuracy resolution.",
            "This remains formula-specific rather than a universal all-stat encumbrance stage."),
        new("combat.defense-resolution-order", RuntimeCharacterModifierOrderEvidenceState.Proven,
            "Native calculate_attack resolves AC-bearing hit chance before the later independent Dodge gate.",
            "The retained blind/debuff path is not a universal final -50% Accuracy transform."),
        new("status.periodic-clock", RuntimeCharacterModifierOrderEvidenceState.Proven,
            "Periodic spell/status processing uses the recovered three-second spell/system round.",
            "Rejects a universal ten-second poison tick."),
        new("native.no-global-linear-pipeline", RuntimeCharacterModifierOrderEvidenceState.Proven,
            "Classic has no single Race/Class -> Ability -> Equipment -> Aura/Status -> Hard Override pipeline shared by every statistic.",
            "Direct _update_dynamic_stats disassembly exposes an owner-specific traversal and deferred ability-0x7C pass, while other formulas execute in separate native owners."),
        new("screenshot.blindness-minus50-accuracy", RuntimeCharacterModifierOrderEvidenceState.Rejected,
            "Blindness is a universal final -50% Accuracy override.",
            "No retained native proof supports this rule; recovered combat debuff handling follows different fields."),
        new("screenshot.poison-ten-second-tick", RuntimeCharacterModifierOrderEvidenceState.Rejected,
            "Poison universally ticks every ten seconds.",
            "Conflicts with the promoted three-second spell/status round authority.")
    ];

    public static readonly bool GameplayFormulaChanged = false;
    public static readonly bool UniversalCrossCategoryOrderResolved = true;
    public static readonly bool UniversalCrossCategoryOrderExists = false;
    public static readonly bool OwnerLocalProjectionOrderIsAuthoritative = true;
    public static readonly bool GenericEquipmentPercentageStageIsUniversalClassicRule = false;
    public static readonly bool DiseaseCurseInjuryUniversalOrderingIsClassicRule = false;
    public static readonly bool HardStateUniversalOverrideStageIsClassicRule = false;

    public static IReadOnlyList<string> Validate()
    {
        var failures = new List<string>();
        if (Rows.Count != 10) failures.Add("expected-ten-evidence-rows");
        if (Rows.Count(row => row.State == RuntimeCharacterModifierOrderEvidenceState.Proven) != 5) failures.Add("expected-five-proven-rows");
        if (Rows.Count(row => row.State == RuntimeCharacterModifierOrderEvidenceState.Strong) != 3) failures.Add("expected-three-strong-rows");
        if (Rows.Count(row => row.State == RuntimeCharacterModifierOrderEvidenceState.Rejected) != 2) failures.Add("expected-two-rejected-rows");
        if (Rows.Any(row => row.State == RuntimeCharacterModifierOrderEvidenceState.Unknown)) failures.Add("modifier-order-unknowns-must-be-zero");
        if (GameplayFormulaChanged) failures.Add("closure-must-not-invent-global-gameplay-formula");
        if (!UniversalCrossCategoryOrderResolved || UniversalCrossCategoryOrderExists) failures.Add("universal-order-negative-closure-mismatch");
        if (!OwnerLocalProjectionOrderIsAuthoritative) failures.Add("owner-local-projection-order-must-be-authoritative");
        if (GenericEquipmentPercentageStageIsUniversalClassicRule) failures.Add("generic-equipment-percent-stage-must-remain-rejected");
        if (DiseaseCurseInjuryUniversalOrderingIsClassicRule) failures.Add("universal-disease-curse-order-must-remain-rejected");
        if (HardStateUniversalOverrideStageIsClassicRule) failures.Add("universal-hard-state-stage-must-remain-rejected");
        return failures;
    }
}
