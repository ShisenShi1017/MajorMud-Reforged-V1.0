using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeClassicImportExportLimitsRecoverySmokeReport(bool Passed, IReadOnlyList<string> Checks, IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

    public static RuntimeClassicImportExportLimitsRecoverySmokeReport Run()
    {
        var checks = new List<string>();
        var failures = new List<string>();
        void Check(string id, bool passed) { checks.Add(id); if (!passed) failures.Add(id); }

        var importExport = RuntimeImportExportCompatibilitySmokeReport.Run();
        Check("import-export-exact-boundary", importExport.Passed);

        var capacityFailures = RuntimeClassicCapacityResourceLimitsAuthority.Validate();
        Check("capacity-authority-valid", capacityFailures.Count == 0);
        Check("inventory-root-100", RuntimeClassicCapacityResourceLimitsAuthority.InventoryRootSlots == 100);
        Check("secondary-item-slots-50", RuntimeClassicCapacityResourceLimitsAuthority.SecondaryItemSlots == 50);
        Check("room-visible-17", RuntimeClassicCapacityResourceLimitsAuthority.VisibleRoomItemSlots == 17);
        Check("room-hidden-15", RuntimeClassicCapacityResourceLimitsAuthority.HiddenRoomItemSlots == 15);
        Check("party-six-exact", RuntimeClassicCapacityResourceLimitsAuthority.MaximumPartyMembers == 6);
        Check("gang-no-invented-member-cap", !RuntimeClassicCapacityResourceLimitsAuthority.GangJoinHasFixedMemberAdmissionCap);
        Check("reforged-character-slots-excluded", !RuntimeClassicCapacityResourceLimitsAuthority.MultiCharacterAccountSlotCapIsClassicMajorMudOwned);
        Check("buffer-defaults-not-world-maxima", !RuntimeClassicCapacityResourceLimitsAuthority.BufferDefaultsAreHardWorldMaxima);
        Check("generic-capacity-extension-excluded", !RuntimeClassicCapacityResourceLimitsAuthority.GenericReservationCoordinatorIsNativeClassic);
        Check("capacity-failure-text", RuntimeClassicCapacityResourceLimitsAuthority.RecipientCarryCapacityFailure == "You cannot carry that much!");

        var failureRows = RuntimeClassicGameplayFailureEvidenceRegistry.Validate();
        Check("failure-evidence-valid", failureRows.Count == 0);
        Check("failure-evidence-zero-unknown", !RuntimeClassicGameplayFailureEvidenceRegistry.Rows.Any(row => row.State == RuntimeClassicGameplayFailureEvidenceState.Unknown));
        Check("failure-evidence-zero-strong", !RuntimeClassicGameplayFailureEvidenceRegistry.Rows.Any(row => row.State == RuntimeClassicGameplayFailureEvidenceState.Strong));

        var genericRecovery = new RuntimeErrorRecoveryService();
        var noOp = genericRecovery.Apply(new(RuntimeFailureKind.GameplayActionRejected, "ClassicGameplay", "command", CharacterScoped: true, RollbackAvailable: true));
        Check("ordinary-rejection-no-op", noOp.Action == RuntimeRecoveryAction.NoOp && !noOp.RequiresRollback && !noOp.DisableSubsystem && !noOp.StopWorldLoad);
        Check("ordinary-rejection-no-disabled-state", !genericRecovery.IsSubsystemDisabled("ClassicGameplay"));

        Check("save-no-wcc-transaction-imports", RuntimeClassicSaveCoordinationAuthority.MajorMudDfaTransactionImports.Count == 0);
        Check("save-partial-semantics-exact", RuntimeClassicSaveCoordinationAuthority.ExactPartialSaveSemanticsResolved);
        Check("save-shutdown-order-exact", RuntimeClassicSaveCoordinationAuthority.ShutdownSaveOrderingResolved);

        var closureFailures = RuntimeClassicImportExportLimitsRecoveryAuthority.Validate();
        Check("combined-authority-valid", closureFailures.Count == 0);
        Check("classification-rejection", RuntimeClassicImportExportLimitsRecoveryAuthority.Classify(new(RuntimeFailureKind.GameplayActionRejected, "ClassicGameplay", "command")) == RuntimeClassicGlobalFailureDisposition.StatePreservingNoOp);
        Check("classification-load", RuntimeClassicImportExportLimitsRecoveryAuthority.Classify(new(RuntimeFailureKind.InvalidRecord, "ClassicData", "startup", WorldLoadCritical: true)) == RuntimeClassicGlobalFailureDisposition.HardStartupLoadFailure);
        Check("classification-save", RuntimeClassicImportExportLimitsRecoveryAuthority.Classify(new(RuntimeFailureKind.PersistenceFailure, "ClassicSave", "update")) == RuntimeClassicGlobalFailureDisposition.RecordLocalPersistenceFailure);
        Check("classification-reference", RuntimeClassicImportExportLimitsRecoveryAuthority.Classify(new(RuntimeFailureKind.MissingReference, "Rooms", "resolve")) == RuntimeClassicGlobalFailureDisposition.OwnerLocalFailure);

        failures.AddRange(capacityFailures.Select(x => "capacity:" + x));
        failures.AddRange(failureRows.Select(x => "failure:" + x));
        failures.AddRange(closureFailures.Select(x => "closure:" + x));
        failures = failures.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return new(failures.Count == 0, checks, failures);
    }
}
