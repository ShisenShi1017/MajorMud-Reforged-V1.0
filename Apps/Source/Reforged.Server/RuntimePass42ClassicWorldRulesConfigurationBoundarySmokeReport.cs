using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimePass42ClassicWorldRulesConfigurationBoundarySmokeReport(
    bool Passed,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

    public static RuntimePass42ClassicWorldRulesConfigurationBoundarySmokeReport Run()
    {
        var checks = new List<string>();
        var failures = new List<string>();
        void Check(bool value, string name) { checks.Add(name); if (!value) failures.Add(name); }

        Check(RuntimeClassicWorldRulesConfigurationEvidenceRegistry.Validate().Count == 0, "classic-world-rules-evidence-registry-valid");
        Check(!RuntimeClassicConfigurationAuthority.ReforgedUsesNativeConfigurationFile, "reforged-does-not-use-wccmmud-ini");
        Check(RuntimeClassicConfigurationAuthority.NativePrompts.Count == 44, "native-44-prompt-catalog-retained");

        Check(RuntimeClassicTimingAuthority.PlayerDataSaveIntervalSeconds == 60, "player-save-default-60-projected");
        Check(RuntimeClassicTimingAuthority.BufferDataSaveIntervalSeconds == 300, "buffer-save-default-300-projected");
        Check(RuntimeClassicConfigurationAuthority.MaximumPvpLevelDifference == 100, "pvp-level-difference-default-100-retained");

        var forgiveness = RuntimeCharacterAlignmentCleanupConfiguration.ClassicDefault;
        Check(forgiveness.MaximumDailyReduction == 18, "evil-forgive-day-default-18-projected");
        Check(forgiveness.CycleMinutes == 60, "evil-cycle-default-60-projected");
        Check(forgiveness.CycleAmount == 1, "evil-per-cycle-default-1-projected");

        Check(RuntimeClassicWorldRulesConfigurationEvidenceRegistry.Rows.Any(row => row.Id == "remaining.range-contracts" && row.State == RuntimeClassicWorldRulesConfigurationEvidenceState.Unknown), "exact-ranges-remain-unknown");
        Check(RuntimeClassicWorldRulesConfigurationEvidenceRegistry.Rows.Any(row => row.Id == "remaining.mutability-restart" && row.State == RuntimeClassicWorldRulesConfigurationEvidenceState.Unknown), "exact-mutability-remains-unknown");
        Check(RuntimeClassicWorldRulesConfigurationEvidenceRegistry.Rows.Any(row => row.Id == "remaining.owner-effect-map" && row.State == RuntimeClassicWorldRulesConfigurationEvidenceState.Unknown), "remaining-owner-effect-map-unknown");
        Check(RuntimeClassicWorldRulesConfigurationEvidenceRegistry.Rows.Any(row => row.Id == "reforged.generic-world-rule-contract" && row.State == RuntimeClassicWorldRulesConfigurationEvidenceState.Excluded), "generic-reforged-rule-contract-not-native-evidence");

        return new RuntimePass42ClassicWorldRulesConfigurationBoundarySmokeReport(failures.Count == 0, checks, failures);
    }
}
