namespace Reforged.Server;

/// <summary>
/// Evidence gate for the recovered Classic MajorMUD world-reset / maintenance-cleanup boundary.
///
/// This authority intentionally does not orchestrate a universal reset. The retained native and
/// database evidence proves several owner-specific maintenance/reset effects, but it does not yet
/// prove one global category order, hard-reset exception encoding, active-player/combat handling,
/// corpse survival, Remove@Maint's distinct lifecycle meaning, or transactional rollback behavior.
/// </summary>
public static class RuntimeClassicWorldResetCleanupAuthority
{
    public const string NativeArtifactSha256 = "5a99e7c34cf9cf58dc6e97fc3ef28f1bd83f9bcec15ca59ad08826d2f21d2077";

    public static bool GenericCoordinatorOwnsClassicOrdering => false;
    public const bool ExactUniversalResetOrderResolved = false;
    public const bool HardResetExceptionEncodingResolved = false;
    public const bool ActivePlayerCombatResetResolved = false;
    public const bool CorpseResetSurvivalResolved = false;
    public static bool RemoveAtMaintenanceDistinctMeaningResolved => false;
    public const bool FailureRollbackBehaviorResolved = false;
    public const bool ResetCleanupSameBoundaryOrderingResolved = false;
    public const bool UniversalResetClockResolved = false;

    public static IReadOnlyList<string> NativeOwnershipExports { get; } = new[]
    {
        "_cleanup_currency",
        "_begin_refreshing_limited_items",
        "_check_initiate_restocking",
        "_restock_items"
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
        "room-ground-item-cleanup",
        "room-ground-currency-cleanup",
        "item-recharge-at-cleanup",
        "evil-point-forgiveness-at-cleanup",
        "static-room-item-restoration-with-limited-admission",
        "generated-invasion-npc-maintenance-removal",
        "generated-npc-room-entry-respawn-admission"
    };

    public static void RequireUniversalClassicResetOrder()
    {
        if (!ExactUniversalResetOrderResolved)
            throw new InvalidOperationException("native-world-reset-order-unresolved");
    }

    public static void RequireClassicHardResetEncoding()
    {
        if (!HardResetExceptionEncodingResolved)
            throw new InvalidOperationException("native-hard-reset-exception-encoding-unresolved");
    }

    public static void RequireClassicActivePlayerCombatHandling()
    {
        if (!ActivePlayerCombatResetResolved)
            throw new InvalidOperationException("native-reset-active-player-combat-unresolved");
    }

    public static void RequireClassicCorpseSurvivalRule()
    {
        if (!CorpseResetSurvivalResolved)
            throw new InvalidOperationException("native-reset-corpse-survival-unresolved");
    }

    public static void RequireClassicFailureRollbackBehavior()
    {
        if (!FailureRollbackBehaviorResolved)
            throw new InvalidOperationException("native-reset-failure-rollback-unresolved");
    }
}
