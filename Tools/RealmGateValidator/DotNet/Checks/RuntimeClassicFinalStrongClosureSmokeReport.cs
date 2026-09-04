using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeClassicFinalStrongClosureSmokeReport(bool Passed, IReadOnlyList<string> Checks, IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

    public static RuntimeClassicFinalStrongClosureSmokeReport Run(string projectRoot)
    {
        var checks = new List<string>();
        var failures = new List<string>();
        void Check(string name, bool value)
        {
            checks.Add(name + ":" + value);
            if (!value) failures.Add(name);
        }

        Check("sysop-worldgroup-access", RuntimeClassicSysopWorldgroupAccessAuthority.Validate().Count == 0);
        Check("combat-spell-native-boundary", RuntimeClassicCombatSpellNativeBoundaryAuthority.ValidateBoundaryClosure().Count == 0);
        Check("classic-live-attack-speed-boundary", RuntimeClassicAttackSpeedBoundaryAuthority.Validate().Count == 0);
        Check("native-help-evidence", RuntimeClassicNativeHelpEvidenceRegistry.Validate().Count == 0);
        Check("native-help-runtime", RuntimeClassicNativeHelpSmokeReport.Run(projectRoot).Passed);

        var commandRegistry = RuntimeClassicNativePlayerCommandRegistry.Load(projectRoot);
        Check("native-player-command-registry", commandRegistry.Validate().Count == 0);
        Check("native-player-command-boundary", RuntimeClassicNativePlayerCommandBoundaryAuthority.Validate(commandRegistry).Count == 0);
        Check("native-player-command-runtime", RuntimeClassicNativePlayerCommandSmokeReport.Run(projectRoot).Passed);

        Check("worldgroup-rng-contract", RuntimeClassicGlobalRngContractAuthority.Validate().Count == 0);
        Check("casino-arena-optional-activities", RuntimeClassicCasinoAuthority.Validate().Count == 0);
        Check("rankings-native-boundary", RuntimeClassicRankingsAuthority.Validate().Count == 0);
        Check("reforged-extension-boundary", RuntimeClassicExtensionBoundaryAuthority.Validate().Count == 0);
        Check("spell-count-1350", RuntimeClassicCombatSpellNativeBoundaryAuthority.CanonicalSpellCount == 1350);
        Check("effect-count-111", RuntimeClassicCombatSpellNativeBoundaryAuthority.RuntimeStatusEffectCount == 111);
        Check("effect-runtime-authoritative-111", RuntimeClassicCombatSpellNativeBoundaryAuthority.RuntimeAuthoritativeStatusEffectCount == 111);
        Check("combat-spell-unclassified-groups-zero", RuntimeClassicCombatSpellNativeBoundaryAuthority.UnclassifiedCombatSpellDependencyGroupCount == 0);
        Check("owner-local-combat-formulas-still-separately-gated", !RuntimeClassicCombatSpellNativeBoundaryAuthority.ExactNativeAttackFormulaOrderingResolved);
        Check("casino-terminal-body-mapping-not-fabricated", !RuntimeClassicCasinoAuthority.TerminalOutcomePayloadBodyMappingClaimedByCasinoAuthority);

        return new(failures.Count == 0, checks, failures);
    }
}
