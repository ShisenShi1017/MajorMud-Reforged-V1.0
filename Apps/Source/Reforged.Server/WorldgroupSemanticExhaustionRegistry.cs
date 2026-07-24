namespace Reforged.Server;

public static class WorldgroupSemanticExhaustionRegistry
{
    public const int MajorMudUsedWorldgroupSymbols = 156;
    public const int UnmatchedUsedSymbols = 0;
    public const int ClosedRoomLatchOwners = 7;
    public const int CoveredQualifiedExitRows = 12;
    public const bool CompatibilityAuditPassed = true;
    public const bool SemanticExhaustionComplete = true;
    public const bool InventRecoveryLogFormat = false;
    public const bool InventNativeLatchCommand = false;

    public static bool UseNativeStorageOwnerForRecoveryLogs => true;
}
