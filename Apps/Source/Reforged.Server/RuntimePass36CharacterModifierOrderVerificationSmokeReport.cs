using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimePass36CharacterModifierOrderVerificationSmokeReport(
    bool Passed,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

    public static RuntimePass36CharacterModifierOrderVerificationSmokeReport Run()
    {
        var checks = new List<string>();
        var failures = new List<string>();
        void Check(bool value, string name) { checks.Add(name); if (!value) failures.Add(name); }

        Check(RuntimeCharacterModifierOrderEvidenceRegistry.Validate().Count == 0, "modifier-order-evidence-registry-valid");
        var rows = RuntimeCharacterModifierOrderEvidenceRegistry.Rows;
        Check(rows.Single(row => row.Id == "creation.starting-stat-source").State == RuntimeCharacterModifierOrderEvidenceState.Proven, "race-minimum-creation-base-retained");
        Check(rows.Single(row => row.Id == "status.periodic-clock").EvidenceBoundary.Contains("three-second", StringComparison.OrdinalIgnoreCase), "three-second-status-clock-retained");
        Check(rows.Single(row => row.Id == "screenshot.poison-ten-second-tick").State == RuntimeCharacterModifierOrderEvidenceState.Rejected, "ten-second-poison-claim-rejected");
        Check(rows.Single(row => row.Id == "screenshot.blindness-minus50-accuracy").State == RuntimeCharacterModifierOrderEvidenceState.Rejected, "blindness-minus50-claim-not-adopted");
        Check(rows.Single(row => row.Id == "screenshot.global-linear-pipeline").State == RuntimeCharacterModifierOrderEvidenceState.Unknown, "universal-linear-pipeline-stays-unresolved");
        Check(!RuntimeCharacterModifierOrderEvidenceRegistry.GameplayFormulaChanged, "no-gameplay-formula-change");
        Check(!RuntimeCharacterModifierOrderEvidenceRegistry.UniversalCrossCategoryOrderResolved, "universal-order-stays-open");
        Check(!RuntimeCharacterModifierOrderEvidenceRegistry.GenericEquipmentPercentageStageResolved, "generic-equipment-percent-stage-stays-open");
        Check(!RuntimeCharacterModifierOrderEvidenceRegistry.DiseaseCurseInjuryOrderingResolved, "disease-curse-injury-order-stays-open");
        Check(!RuntimeCharacterModifierOrderEvidenceRegistry.HardStateStructMutationResolved, "hard-state-struct-order-stays-open");

        return new(failures.Count == 0, checks, failures);
    }
}
