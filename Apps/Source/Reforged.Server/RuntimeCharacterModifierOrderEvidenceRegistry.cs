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
/// Evidence-only Pass 36 boundary. It records what the retained native evidence can and
/// cannot prove about character modifier ordering. It is intentionally not a gameplay
/// calculation service and must not be used as a substitute for unrecovered native order.
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
            "Exact MaxHP/MaxMana operand order is recovered; this does not imply one universal modifier pipeline."),
        new("ability.primary-stat-signed-delta", RuntimeCharacterModifierOrderEvidenceState.Strong,
            "Retained primary-stat abilities 45-49 are signed parameter-value deltas.",
            "No generic percentage stage is proven by the retained ability descriptions."),
        new("equipment.direct-combat-additive", RuntimeCharacterModifierOrderEvidenceState.Strong,
            "Decoded direct AC/DR/accuracy/speed equipment fields aggregate additively.",
            "No universal flat-then-percent equipment stat stage is proven."),
        new("combat.raw-dodge-encumbrance-placement", RuntimeCharacterModifierOrderEvidenceState.Strong,
            "Recovered raw Dodge has an explicit encumbrance placement before Dodge-vs-Accuracy resolution.",
            "This is formula-specific and not a universal all-stat encumbrance stage."),
        new("combat.defense-resolution-order", RuntimeCharacterModifierOrderEvidenceState.Proven,
            "Native calculate_attack resolves AC-bearing hit chance before the later independent Dodge gate.",
            "The retained blind/debuff path is not a universal final -50% Accuracy transform."),
        new("status.periodic-clock", RuntimeCharacterModifierOrderEvidenceState.Proven,
            "Periodic spell/status processing uses the recovered three-second spell/system round.",
            "Rejects a universal ten-second poison tick."),
        new("screenshot.global-linear-pipeline", RuntimeCharacterModifierOrderEvidenceState.Unknown,
            "One linear Race/Class -> Ability -> Equipment -> Aura/Status -> Hard Override pipeline for every statistic.",
            "No exact native routine proving that universal order is retained."),
        new("screenshot.blindness-minus50-accuracy", RuntimeCharacterModifierOrderEvidenceState.Rejected,
            "Blindness is a universal final -50% Accuracy override.",
            "No retained native proof supports this rule; recovered combat debuff handling follows different fields."),
        new("screenshot.poison-ten-second-tick", RuntimeCharacterModifierOrderEvidenceState.Rejected,
            "Poison universally ticks every ten seconds.",
            "Conflicts with the promoted three-second spell/status round authority.")
    ];

    public static readonly bool GameplayFormulaChanged = false;
    public static readonly bool UniversalCrossCategoryOrderResolved = false;
    public static readonly bool GenericEquipmentPercentageStageResolved = false;
    public static readonly bool DiseaseCurseInjuryOrderingResolved = false;
    public static readonly bool HardStateStructMutationResolved = false;

    public static IReadOnlyList<string> Validate()
    {
        var failures = new List<string>();
        if (Rows.Count != 10) failures.Add("expected-ten-evidence-rows");
        if (Rows.Count(row => row.State == RuntimeCharacterModifierOrderEvidenceState.Proven) != 4) failures.Add("expected-four-proven-rows");
        if (Rows.Count(row => row.State == RuntimeCharacterModifierOrderEvidenceState.Strong) != 3) failures.Add("expected-three-strong-rows");
        if (Rows.Count(row => row.State == RuntimeCharacterModifierOrderEvidenceState.Rejected) != 2) failures.Add("expected-two-rejected-rows");
        if (Rows.Count(row => row.State == RuntimeCharacterModifierOrderEvidenceState.Unknown) != 1) failures.Add("expected-one-unknown-row");
        if (GameplayFormulaChanged) failures.Add("pass36-must-not-change-gameplay-formula");
        if (UniversalCrossCategoryOrderResolved) failures.Add("universal-order-must-remain-open");
        if (GenericEquipmentPercentageStageResolved) failures.Add("equipment-percent-stage-must-remain-open");
        if (DiseaseCurseInjuryOrderingResolved) failures.Add("disease-curse-order-must-remain-open");
        if (HardStateStructMutationResolved) failures.Add("hard-state-struct-order-must-remain-open");
        return failures;
    }
}
