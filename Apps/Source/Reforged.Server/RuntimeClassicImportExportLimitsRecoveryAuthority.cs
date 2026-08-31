namespace Reforged.Server;

public enum RuntimeClassicGlobalFailureDisposition
{
    StatePreservingNoOp,
    HardStartupLoadFailure,
    OwnerLocalFailure,
    RecordLocalPersistenceFailure,
    OutsideMajorMudGameplay
}

/// <summary>
/// Cross-check authority for the complete Import / Export / Limits / Recovery category.
/// This intentionally composes already-recovered native owners instead of introducing a new
/// synthetic Classic manager.  It is safe to call from smoke/audit paths and documents which
/// behavior is global versus owner-local.
/// </summary>
public static class RuntimeClassicImportExportLimitsRecoveryAuthority
{
    public static readonly bool ClassicHasInGameNativeImportExportMigration = false;
    public static readonly bool ClassicUsesExactExternalNativeFileReplacement = true;
    public static readonly bool ClassicHasUniversalFailureTextRenderer = false;
    public static readonly bool ClassicHasUniversalGameplayRollbackManager = false;
    public static readonly bool ClassicHasWccSelectedSaveTransactionRollback = false;
    public static readonly bool WorldgroupCrashReplayIsMajorMudGameplayRecovery = false;

    public static RuntimeClassicGlobalFailureDisposition Classify(RuntimeFailureContext context)
    {
        if (context.Kind == RuntimeFailureKind.GameplayActionRejected)
            return RuntimeClassicGlobalFailureDisposition.StatePreservingNoOp;
        if (context.WorldLoadCritical || context.Kind is RuntimeFailureKind.PartialWorldLoad or RuntimeFailureKind.DuplicateRuntimeId)
            return RuntimeClassicGlobalFailureDisposition.HardStartupLoadFailure;
        if (context.Kind == RuntimeFailureKind.PersistenceFailure)
            return RuntimeClassicGlobalFailureDisposition.RecordLocalPersistenceFailure;
        if (context.Kind is RuntimeFailureKind.MissingReference or RuntimeFailureKind.InvalidRecord or
            RuntimeFailureKind.ScriptFailure or RuntimeFailureKind.EventFailure or RuntimeFailureKind.ResetFailure)
            return RuntimeClassicGlobalFailureDisposition.OwnerLocalFailure;
        return RuntimeClassicGlobalFailureDisposition.OutsideMajorMudGameplay;
    }

    public static IReadOnlyList<string> Validate()
    {
        var failures = new List<string>();
        failures.AddRange(RuntimeClassicCapacityResourceLimitsAuthority.Validate());
        failures.AddRange(RuntimeClassicGameplayFailureEvidenceRegistry.Validate());
        failures.AddRange(RuntimeClassicSaveCoordinationAuthority.Validate());

        if (ClassicHasInGameNativeImportExportMigration) failures.Add("invented-classic-import-export-migration");
        if (!ClassicUsesExactExternalNativeFileReplacement) failures.Add("classic-exact-file-replacement-demoted");
        if (ClassicHasUniversalFailureTextRenderer) failures.Add("invented-classic-universal-failure-text");
        if (ClassicHasUniversalGameplayRollbackManager) failures.Add("invented-classic-gameplay-rollback-manager");
        if (ClassicHasWccSelectedSaveTransactionRollback) failures.Add("invented-classic-save-transaction-rollback");
        if (WorldgroupCrashReplayIsMajorMudGameplayRecovery) failures.Add("host-crash-replay-promoted-to-gameplay");

        var rejected = new RuntimeFailureContext(RuntimeFailureKind.GameplayActionRejected, "ClassicGameplay", "command", CharacterScoped: true, RollbackAvailable: true);
        if (Classify(rejected) != RuntimeClassicGlobalFailureDisposition.StatePreservingNoOp)
            failures.Add("gameplay-rejection-disposition-mismatch");
        var load = new RuntimeFailureContext(RuntimeFailureKind.InvalidRecord, "ClassicData", "startup", WorldLoadCritical: true);
        if (Classify(load) != RuntimeClassicGlobalFailureDisposition.HardStartupLoadFailure)
            failures.Add("startup-hard-failure-disposition-mismatch");
        var save = new RuntimeFailureContext(RuntimeFailureKind.PersistenceFailure, "ClassicSave", "update", StateMutationStarted: true);
        if (Classify(save) != RuntimeClassicGlobalFailureDisposition.RecordLocalPersistenceFailure)
            failures.Add("persistence-disposition-mismatch");
        var missing = new RuntimeFailureContext(RuntimeFailureKind.MissingReference, "Rooms", "resolve", RecordId: "room:missing");
        if (Classify(missing) != RuntimeClassicGlobalFailureDisposition.OwnerLocalFailure)
            failures.Add("owner-local-reference-disposition-mismatch");

        return failures;
    }
}
