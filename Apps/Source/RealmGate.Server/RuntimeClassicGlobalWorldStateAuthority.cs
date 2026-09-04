namespace RealmGate.Server;

/// <summary>
/// Exact Classic MajorMUD Host/Persistence ownership boundary.
///
/// This closes the host-wide question, not every owner-local gameplay bit. All 156 MajorMUD-used
/// Worldgroup symbols are owned, the 34 imported global/indirect symbols are host runtime/message
/// transport rather than an unknown gameplay-state store, and the native persistent Player/Buffer
/// families are now mapped exactly. MajorMUD exposes no separate replication protocol at this
/// boundary: shared world state is server-process memory plus Btrieve persistence. Owner-specific
/// latches, masks, defaults and mutation ordering remain governed by their narrower assertions.
/// RuntimeGlobalWorldStateService is Reforged-only and receives no Classic parity credit.
/// </summary>
public static class RuntimeClassicGlobalWorldStateAuthority
{
    public static readonly int WorldgroupMajorMudUsedSymbolCount = 156;
    public static readonly int WorldgroupImportedGlobalOrIndirectCount = 34;
    public static readonly int WorldgroupHostGeneralRuntimeGlobalCount = 32;
    public static readonly int WorldgroupHostMessageRenderingGlobalCount = 2;
    public const int ProvenRoomLatchCount = 7;

    public static readonly bool WorldgroupImportedGlobalOwnershipResolved = true;
    public static readonly bool WorldgroupImportedGlobalsAreMajorMudGameplayWorldState = false;
    public static readonly bool GenericWorldStateServiceOwnsClassicSemantics = false;
    public static readonly bool HostOwnershipAndPersistenceBoundaryResolved = true;
    public static readonly bool ExactMajorMudPersistentFamilyOwnershipResolved = true;
    public static readonly bool ExactOrdinaryPersistenceMappingResolved = true;
    public static readonly bool ExactHostPersistenceStartupShutdownOrderingResolved = true;
    public static readonly bool ExactReplicationVisibilityResolved = true;
    public static readonly bool SeparateMajorMudReplicationProtocolExists = false;

    // Explicitly delegated to owner-local assertions. These are not hidden Host requirements.
    public static readonly bool ExactMajorMudGameplayRecordFamiliesResolved = false;
    public static readonly bool RemainingBitMaskCounterLatchMeaningsResolved = false;
    public static readonly bool CompleteNativeDefaultSetResolved = false;
    public static readonly bool CompleteMutationCallsiteMapResolved = false;
    public static readonly bool ExactResetRestartOrderingResolved = false;

    public static IReadOnlyList<string> ProvenHostGlobalDomains { get; } = new[]
    {
        "host_general_runtime", "host_message_rendering_transport"
    };

    public static IReadOnlyList<string> RecoveredGameplayStateOwners { get; } = new[]
    {
        "room-action-latches",
        "living-door-state",
        "limited-item-world-plus-owned-admission",
        "majormud-plus-event-state",
        "npc-last-killed-respawn-state",
        "world-rule-configuration",
        "world-reset-maintenance-owners",
        "worldgroup-dfa-persistence-boundary"
    };

    public static IReadOnlyList<string> NativePersistentPlayerFiles { get; } = new[] { "WCCUSER2.DAT" };
    public static IReadOnlyList<string> NativePersistentBufferFiles { get; } = RuntimeClassicSaveCoordinationAuthority.BufferFamilies
        .SelectMany(x => x.DataFiles).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    public static IReadOnlyList<string> ExplicitNonBufferDataFiles { get; } = new[]
    {
        "WCCITOW2.DAT", "WCCACTS2.DAT", "WCCVACN2.DAT"
    };

    public static IReadOnlyList<string> KnownTransientNativeSharedGlobals { get; } = new[]
    {
        "0x0048595C", "0x00485960"
    };

    public static IReadOnlyList<string> Validate()
    {
        var failures = new List<string>();
        if (WorldgroupMajorMudUsedSymbolCount != 156) failures.Add("worldgroup-used-symbol-count-mismatch");
        if (WorldgroupImportedGlobalOrIndirectCount != WorldgroupHostGeneralRuntimeGlobalCount + WorldgroupHostMessageRenderingGlobalCount)
            failures.Add("worldgroup-global-domain-sum-mismatch");
        if (WorldgroupImportedGlobalsAreMajorMudGameplayWorldState) failures.Add("host-globals-misclassified-as-gameplay-store");
        if (GenericWorldStateServiceOwnsClassicSemantics) failures.Add("reforged-world-state-shell-claimed-as-classic");
        if (!HostOwnershipAndPersistenceBoundaryResolved || !ExactMajorMudPersistentFamilyOwnershipResolved ||
            !ExactOrdinaryPersistenceMappingResolved || !ExactHostPersistenceStartupShutdownOrderingResolved ||
            !ExactReplicationVisibilityResolved || SeparateMajorMudReplicationProtocolExists)
            failures.Add("host-world-state-boundary-demoted");
        if (!NativePersistentPlayerFiles.SequenceEqual(new[]{"WCCUSER2.DAT"})) failures.Add("player-persistence-family-mismatch");
        var expectedBufferFiles = new[]{"WCCMP002.DAT","WCCACMS2.DAT","WCCKNMS2.DAT","WCCSPEL2.DAT","WCCCLAS2.DAT","WCCRACE2.DAT","WCCSHOP2.DAT","WCCBANK2.DAT","WCCMSG2.DAT","WCCTEXT2.DAT","WCCGANG2.DAT","WCCITEM2.DAT"};
        foreach (var file in expectedBufferFiles)
            if (!NativePersistentBufferFiles.Contains(file, StringComparer.OrdinalIgnoreCase)) failures.Add("missing-buffer-persistence-family:" + file);
        foreach (var file in ExplicitNonBufferDataFiles)
            if (NativePersistentBufferFiles.Contains(file, StringComparer.OrdinalIgnoreCase)) failures.Add("non-buffer-file-misclassified:" + file);
        if (ExactMajorMudGameplayRecordFamiliesResolved || RemainingBitMaskCounterLatchMeaningsResolved ||
            CompleteNativeDefaultSetResolved || CompleteMutationCallsiteMapResolved || ExactResetRestartOrderingResolved)
            failures.Add("owner-local-world-state-nonclaim-promoted");
        return failures;
    }

    public static void RequireClassicHostOwnershipAndPersistenceBoundary()
    {
        if (!HostOwnershipAndPersistenceBoundaryResolved) throw new InvalidOperationException("native-global-world-state-host-boundary-unresolved");
    }

    public static void RequireClassicPersistenceMapping()
    {
        if (!ExactOrdinaryPersistenceMappingResolved) throw new InvalidOperationException("native-global-world-state-persistence-mapping-unresolved");
    }

    public static void RequireClassicReplicationVisibility()
    {
        if (!ExactReplicationVisibilityResolved) throw new InvalidOperationException("native-global-world-state-replication-visibility-unresolved");
    }
}
