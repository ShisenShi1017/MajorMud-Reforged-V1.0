using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimePass33ClassicFileConfigurationBoundarySmokeReport(
    bool Passed,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

    public static RuntimePass33ClassicFileConfigurationBoundarySmokeReport Run(string projectRoot)
    {
        var checks = new List<string>();
        var failures = new List<string>();
        void Check(bool condition, string id)
        {
            checks.Add(id);
            if (!condition) failures.Add(id);
        }

        Check(!RuntimeClassicConfigurationAuthority.ReforgedUsesNativeConfigurationFile,
            "reforged-has-no-wccmmud-ini");
        Check(RuntimeClassicConfigurationAuthority.NativeConfigurationFileName == "WCCMMUD.INI",
            "native-configuration-source-wccmmud-ini");
        Check(RuntimeClassicConfigurationAuthority.NativePrompts.Count == 44 &&
              RuntimeClassicConfigurationAuthority.ValidateEvidenceCatalog().Count == 0,
            "native-44-configuration-prompts");
        Check(RuntimeClassicConfigurationAuthority.PlayerDataSaveIntervalSeconds == 60 &&
              RuntimeClassicTimingAuthority.PlayerDataSaveIntervalSeconds == 60,
            "native-player-save-default-60");
        Check(RuntimeClassicConfigurationAuthority.BufferDataSaveIntervalSeconds == 300 &&
              RuntimeClassicTimingAuthority.BufferDataSaveIntervalSeconds == 300,
            "native-buffer-save-default-300");
        Check(RuntimeClassicConfigurationAuthority.MaximumPvpLevelDifference == 100,
            "native-pvp-level-difference-default-100");
        Check(RuntimeClassicConfigurationAuthority.MaximumEvilPointsForgivenPerDay == 18 &&
              RuntimeClassicConfigurationAuthority.EvilPointCycleMinutes == 60 &&
              RuntimeClassicConfigurationAuthority.EvilPointsForgivenPerCycle == 1,
            "native-ep-defaults-preserved");
        Check(RuntimeCharacterAlignmentCleanupConfiguration.ClassicDefault.MaximumDailyReduction == 18 &&
              RuntimeCharacterAlignmentCleanupConfiguration.ClassicDefault.CycleMinutes == 60 &&
              RuntimeCharacterAlignmentCleanupConfiguration.ClassicDefault.CycleAmount == 1,
            "alignment-uses-native-config-authority");

        var tempRoot = Path.Combine(Path.GetTempPath(), "reforged-pass33-pvp-" + Guid.NewGuid().ToString("N"));
        try
        {
            var pvp = new RuntimePvpWorldConfigurationStore(tempRoot, "classic").Capture();
            Check(pvp.MaximumLevelDifference == 100, "pvp-store-default-100");
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
        }

        var compatibility = new RuntimeImportExportCompatibilityService();
        var exact = compatibility.Execute(new("pass33-exact", RuntimeCompatibilityOperation.Validate,
            RuntimeCompatibilityFormat.Wcc, RuntimeCompatibilityMode.Classic, 111, 111, "fixture", false, true, true,
            "WCCMP002.DAT", "WCCMP002.DAT"));
        Check(exact.Success, "classic-exact-file-version-validation");
        var wrongFile = compatibility.Execute(new("pass33-wrong-file", RuntimeCompatibilityOperation.Validate,
            RuntimeCompatibilityFormat.Wcc, RuntimeCompatibilityMode.Classic, 111, 111, "fixture", false, true, true,
            "WCCMP003.DAT", "WCCMP002.DAT"));
        Check(!wrongFile.Success && wrongFile.Code == "classic-native-file-mismatch-hard-failure",
            "classic-wrong-file-hard-failure");
        var wrongVersion = compatibility.Execute(new("pass33-wrong-version", RuntimeCompatibilityOperation.Validate,
            RuntimeCompatibilityFormat.WorldData, RuntimeCompatibilityMode.Classic, 110, 111, "fixture", false, true, true));
        Check(!wrongVersion.Success && wrongVersion.Code == "classic-native-version-mismatch-hard-failure",
            "classic-wrong-version-hard-failure");
        var migration = compatibility.Execute(new("pass33-migrate", RuntimeCompatibilityOperation.Migrate,
            RuntimeCompatibilityFormat.WorldData, RuntimeCompatibilityMode.Classic, 110, 111, "fixture", true, true, true));
        Check(!migration.Success && migration.Code == "classic-native-file-replacement-is-external",
            "classic-no-migration-subsystem");

        var recovery = new RuntimeErrorRecoveryService().Decide(new(
            RuntimeFailureKind.GameplayActionRejected, "ClassicGameplay", "rejected-command"));
        Check(recovery.Action == RuntimeRecoveryAction.NoOp && !recovery.RequiresRollback &&
              !recovery.DisableSubsystem && !recovery.StopWorldLoad,
            "classic-rejected-gameplay-action-no-op");

        return new(failures.Count == 0, checks, failures);
    }
}
