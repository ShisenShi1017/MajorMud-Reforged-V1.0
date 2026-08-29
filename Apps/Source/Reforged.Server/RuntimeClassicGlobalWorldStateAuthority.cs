namespace Reforged.Server;

/// <summary>
/// Evidence gate for the recovered Classic MajorMUD global-world-state boundary.
///
/// The retained Worldgroup audit closes ownership of every MajorMUD-used imported global/indirect
/// symbol: all 34 are host runtime/rendering state rather than an unidentified MajorMUD gameplay
/// state store. Known gameplay-wide state is already owned by narrower decoded/runtime authorities
/// (room latches, living doors, limited-item admission, MajorMUD Plus event state, NPC respawn state,
/// configuration, reset/cleanup, and persistence). The generic RuntimeGlobalWorldStateService is a
/// Reforged compatibility shell and is not evidence for Classic record layout, defaults, reset order,
/// persistence, replication, or visibility semantics.
/// </summary>
public static class RuntimeClassicGlobalWorldStateAuthority
{
    public const int WorldgroupMajorMudUsedSymbolCount = 156;
    public const int WorldgroupImportedGlobalOrIndirectCount = 34;
    public const int WorldgroupHostGeneralRuntimeGlobalCount = 32;
    public const int WorldgroupHostMessageRenderingGlobalCount = 2;
    public const int ProvenRoomLatchCount = 7;

    public const bool WorldgroupImportedGlobalOwnershipResolved = true;
    public const bool WorldgroupImportedGlobalsAreMajorMudGameplayWorldState = false;
    public const bool GenericWorldStateServiceOwnsClassicSemantics = false;

    public const bool ExactMajorMudGameplayRecordFamiliesResolved = false;
    public const bool RemainingBitMaskCounterLatchMeaningsResolved = false;
    public const bool CompleteNativeDefaultSetResolved = false;
    public const bool CompleteMutationCallsiteMapResolved = false;
    public const bool ExactResetRestartOrderingResolved = false;
    public const bool ExactOrdinaryPersistenceMappingResolved = false;
    public const bool ExactReplicationVisibilityResolved = false;

    public static IReadOnlyList<string> ProvenHostGlobalDomains { get; } = new[]
    {
        "host_general_runtime",
        "host_message_rendering_transport"
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

    public static IReadOnlyList<string> KnownTransientNativeSharedGlobals { get; } = new[]
    {
        "0x0048595C",
        "0x00485960"
    };

    public static void RequireClassicGameplayRecordFamilies()
    {
        if (!ExactMajorMudGameplayRecordFamiliesResolved)
            throw new InvalidOperationException("native-global-world-state-record-families-unresolved");
    }

    public static void RequireClassicBitMaskCounterLatchMeanings()
    {
        if (!RemainingBitMaskCounterLatchMeaningsResolved)
            throw new InvalidOperationException("native-global-world-state-bit-counter-latch-meanings-unresolved");
    }

    public static void RequireClassicDefaultSet()
    {
        if (!CompleteNativeDefaultSetResolved)
            throw new InvalidOperationException("native-global-world-state-defaults-unresolved");
    }

    public static void RequireClassicMutationCallsiteMap()
    {
        if (!CompleteMutationCallsiteMapResolved)
            throw new InvalidOperationException("native-global-world-state-mutation-callsites-unresolved");
    }

    public static void RequireClassicResetRestartOrdering()
    {
        if (!ExactResetRestartOrderingResolved)
            throw new InvalidOperationException("native-global-world-state-reset-restart-order-unresolved");
    }

    public static void RequireClassicPersistenceMapping()
    {
        if (!ExactOrdinaryPersistenceMappingResolved)
            throw new InvalidOperationException("native-global-world-state-persistence-mapping-unresolved");
    }

    public static void RequireClassicReplicationVisibility()
    {
        if (!ExactReplicationVisibilityResolved)
            throw new InvalidOperationException("native-global-world-state-replication-visibility-unresolved");
    }
}
