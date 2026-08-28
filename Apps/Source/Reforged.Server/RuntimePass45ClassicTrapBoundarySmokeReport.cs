using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimePass45ClassicTrapBoundarySmokeReport(
    bool Passed,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

    public static RuntimePass45ClassicTrapBoundarySmokeReport Run(string projectRoot)
    {
        var checks = new List<string>();
        var failures = new List<string>();
        void Check(bool value, string name) { checks.Add(name); if (!value) failures.Add(name); }

        Check(RuntimeClassicTrapEvidenceRegistry.Validate().Count == 0, "classic-trap-evidence-registry-valid");
        Check(RuntimeClassicTrapEvidenceRegistry.TrapSkillHelpMessageNumber == 15, "native-trap-skill-help-15");
        Check(RuntimeClassicTrapEvidenceRegistry.TrapDisarmHelpMessageNumber == 245, "native-trap-disarm-help-245");
        Check(RuntimeClassicTrapEvidenceRegistry.DecodedExitTrapCount == 295, "decoded-exit-trap-count-295");
        Check(RuntimeClassicTrapEvidenceRegistry.DecodedDamageTrapCount == 273, "decoded-damage-trap-count-273");
        Check(RuntimeClassicTrapEvidenceRegistry.DecodedSpellTrapCount == 22, "decoded-spell-trap-count-22");
        Check(!RuntimeClassicTrapEvidenceRegistry.ExactPerExitPara2Resolved, "per-exit-para2-remains-open");
        Check(!RuntimeClassicTrapEvidenceRegistry.ExactSearchProbabilityResolved, "search-roll-remains-open");
        Check(!RuntimeClassicTrapEvidenceRegistry.ExactDisarmProbabilityResolved, "disarm-roll-remains-open");
        Check(!RuntimeClassicTrapEvidenceRegistry.ExactFailedDisarmTriggerResolved, "failed-disarm-trigger-remains-open");
        Check(!RuntimeClassicTrapEvidenceRegistry.ExactTrapMessageOwnershipResolved, "trap-message-ownership-remains-open");
        Check(!RuntimeClassicTrapEvidenceRegistry.ExactTrapStateTransitionsResolved, "trap-transitions-remain-open");
        Check(!RuntimeClassicTrapEvidenceRegistry.ExactTrapResetRearmResolved, "trap-reset-rearm-remains-open");

        var catalog = RuntimeClassicExitTrapCatalog.Load(projectRoot);
        Check(catalog.Count == 295, "runtime-classic-trap-catalog-295");
        Check(catalog.Definitions.Count(x => x.TrapKind.Equals("damage_trap", StringComparison.OrdinalIgnoreCase)) == 273, "runtime-damage-traps-273");
        Check(catalog.Definitions.Count(x => x.TrapKind.Equals("spell_trap", StringComparison.OrdinalIgnoreCase)) == 22, "runtime-spell-traps-22");
        Check(catalog.Definitions.All(x => !x.NativeActivationStateProjected && x.NativeActivationState is null), "mme-projection-does-not-invent-per-exit-para2");

        return new RuntimePass45ClassicTrapBoundarySmokeReport(failures.Count == 0, checks, failures);
    }
}
