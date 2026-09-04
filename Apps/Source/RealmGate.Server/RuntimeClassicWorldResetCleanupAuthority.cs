namespace RealmGate.Server;

/// <summary>
/// Exhausted Classic MajorMUD reset/maintenance ownership boundary.
///
/// WCCMMUD does not implement one universal transactional world reset. SYSOP RESET is a
/// narrow monster-generation counter operation: ROOM clears the selected room's +0x606
/// generated-monster count and marks the room dirty (+0x5F8); AREA follows +0x5C4 to the
/// linked area-control room, clears its +0x5C0 area-population count, and marks it dirty.
/// Normal generation then repopulates when its ordinary admission owners run.
///
/// Cleanup, limited-item refresh/restock, recharge, Evil-Point forgiveness, initialization,
/// active-player state, combat, corpses, and Remove@Maint are separate owners. Their absence
/// from SYSOP RESET is the recovered contract; they are not unresolved reset phases.
/// </summary>
public static class RuntimeClassicWorldResetCleanupAuthority
{
    public const string NativeArtifactSha256 = "5a99e7c34cf9cf58dc6e97fc3ef28f1bd83f9bcec15ca59ad08826d2f21d2077";
    public const int NativeRoomGeneratedMonsterCountOffset = 0x606;
    public const int NativeAreaPopulationCountOffset = 0x5C0;
    public const int NativeLinkedAreaRoomOffset = 0x5C4;
    public const int NativeRoomDirtyMarkerOffset = 0x5F8;

    public static bool GenericCoordinatorOwnsClassicOrdering => false;
    public const bool UniversalResetTransactionExists = false;
    public const bool ExactUniversalResetBoundaryResolved = true;
    public const bool HardResetExceptionEncodingResolved = true; // resolved as nonexistent in SYSOP RESET
    public const bool ActivePlayerCombatResetResolved = true;    // not touched by SYSOP RESET
    public const bool CorpseResetSurvivalResolved = true;        // not touched by SYSOP RESET
    public static bool RemoveAtMaintenanceDistinctMeaningResolved => true;
    public const bool FailureRollbackBehaviorResolved = true;    // direct owner mutation; no transaction/rollback layer
    public const bool ResetCleanupSameBoundaryOrderingResolved = true; // separate owners, no shared reset transaction
    public const bool UniversalResetClockResolved = true;        // no universal reset clock exists

    public static IReadOnlyList<string> NativeOwnershipExports { get; } = new[]
    {
        "_cleanup_currency",
        "_begin_refreshing_limited_items",
        "_check_initiate_restocking",
        "_restock_items",
        "_generate_monster"
    };

    public static IReadOnlyList<string> ExactSysopResetForms { get; } = new[]
    {
        "SYSOP RESET",
        "SYSOP RESET ROOM",
        "SYSOP RESET AREA",
        "SYSOP RESET ROOM <room>",
        "SYSOP RESET ROOM <room> <map>"
    };

    public static IReadOnlyList<string> ExactSysopInitializeForms { get; } = new[]
    {
        "SYSOP INITIALIZE",
        "SYSOP INITIALIZE -1",
        "SYSOP INITIALIZE <rate>",
        "SYSOP INITIALIZE <rate> RECOVER"
    };

    public static IReadOnlyList<string> RecoveredOwnerBoundaries { get; } = new[]
    {
        "sysop-room-reset-generated-monster-count-only",
        "sysop-area-reset-linked-area-population-count-only",
        "room-ground-item-cleanup",
        "room-ground-currency-cleanup",
        "item-recharge-at-cleanup",
        "evil-point-forgiveness-at-cleanup",
        "limited-item-refresh-restock",
        "generated-invasion-npc-maintenance-removal",
        "room-entry-and-background-monster-generation-admission",
        "initialize-buffer-recovery-separate-from-reset"
    };

    public static void RequireExhaustedClassicResetBoundary()
    {
        if (!ExactUniversalResetBoundaryResolved || UniversalResetTransactionExists)
            throw new InvalidOperationException("native-world-reset-boundary-not-exhausted");
    }
}
