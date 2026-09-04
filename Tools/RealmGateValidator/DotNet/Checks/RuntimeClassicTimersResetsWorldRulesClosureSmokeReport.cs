using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeClassicTimersResetsWorldRulesClosureSmokeReport(bool Passed, IReadOnlyList<string> Checks, IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

    public static RuntimeClassicTimersResetsWorldRulesClosureSmokeReport Run(string projectRoot)
    {
        var checks = new List<string>();
        var failures = new List<string>();
        void Check(string name, bool value)
        {
            checks.Add(name + ":" + value);
            if (!value) failures.Add(name);
        }

        var schedulerAudit = RuntimeClassicNativeSchedulerClockAudit.Run();
        Check("native-scheduler-audit-six", schedulerAudit.Count == 6);
        Check("scheduler-evidence-zero-failures", RuntimeClassicSchedulerEvidenceRegistry.Validate().Count == 0);
        Check("scheduler-restart-fresh-arm", RuntimeClassicSchedulerEvidenceRegistry.RestartFreshArmResolved);
        Check("scheduler-spawn-admission-resolved", RuntimeClassicSchedulerEvidenceRegistry.SpawnAdmissionResolved);
        Check("scheduler-no-universal-ten-second-phase", !RuntimeClassicSchedulerEvidenceRegistry.UniversalTenSecondGameplayPhasePromoted);
        Check("scheduler-two-host-owned-boundaries", RuntimeClassicSchedulerEvidenceRegistry.Rows.Count(row => row.State == RuntimeClassicSchedulerEvidenceState.HostOwnedBoundary) == 2);

        var spawnTiming = RuntimeMajorMudSpawnTimingSmokeReport.Run(projectRoot);
        Check("ordinary-lair-delay-smoke", spawnTiming.Passed);

        Check("configuration-authority-zero-failures", RuntimeClassicConfigurationAuthority.ValidateEvidenceCatalog().Count == 0);
        Check("configuration-visible-prompts-44", RuntimeClassicConfigurationAuthority.VisibleOptionAuthorities.Count == 44);
        Check("configuration-reloadable-29", RuntimeClassicConfigurationAuthority.VisibleOptionAuthorities.Count(row => row.LoadOwner == RuntimeClassicConfigurationLoadOwner.Reloadable) == 29);
        Check("configuration-startup-only-13", RuntimeClassicConfigurationAuthority.VisibleOptionAuthorities.Count(row => row.LoadOwner == RuntimeClassicConfigurationLoadOwner.StartupOnly) == 13);
        Check("configuration-catalog-only-2", RuntimeClassicConfigurationAuthority.VisibleOptionAuthorities.Count(row => row.LoadOwner == RuntimeClassicConfigurationLoadOwner.CatalogOnlyNoNativeLoadCall) == 2);
        Check("world-rules-evidence-zero-failures", RuntimeClassicWorldRulesConfigurationEvidenceRegistry.Validate().Count == 0);

        Check("plus-event-evidence-zero-failures", RuntimeClassicMajorMudPlusEventEvidenceRegistry.Validate().Count == 0);
        Check("plus-rng-order-resolved", RuntimeClassicMajorMudPlusEventEvidenceRegistry.ExactNativeRngCallsiteThreadDrawOrderingResolved);
        Check("plus-no-generic-event-scheduler", !RuntimeClassicMajorMudPlusEventEvidenceRegistry.GenericNativeEventSchedulerPromoted);

        var reset = RuntimeWorldResetCleanupSmokeReport.Run();
        Check("world-reset-cleanup-owner-smoke", reset.Passed);
        Check("world-reset-no-universal-transaction", !RuntimeClassicWorldResetCleanupAuthority.UniversalResetTransactionExists);
        Check("world-reset-room-count-offset-606", RuntimeClassicWorldResetCleanupAuthority.NativeRoomGeneratedMonsterCountOffset == 0x606);
        Check("world-reset-area-count-offset-5c0", RuntimeClassicWorldResetCleanupAuthority.NativeAreaPopulationCountOffset == 0x5C0);
        Check("world-reset-area-link-offset-5c4", RuntimeClassicWorldResetCleanupAuthority.NativeLinkedAreaRoomOffset == 0x5C4);
        Check("world-reset-dirty-offset-5f8", RuntimeClassicWorldResetCleanupAuthority.NativeRoomDirtyMarkerOffset == 0x5F8);

        return new(failures.Count == 0, checks, failures);
    }
}
