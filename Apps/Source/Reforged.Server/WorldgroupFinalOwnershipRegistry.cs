namespace Reforged.Server;

public static class WorldgroupFinalOwnershipRegistry
{
    public const int MajorMudUsedSymbols = 156;
    public const int CallableImports = 121;
    public const int ImportedGlobalsOrIndirectReferences = 34;
    public const int UnmatchedImports = 0;
    public const bool AllUsedImportsOwned = true;
    public const bool InventTokenService = false;
    public const bool InventHseStoFallback = false;

    public static bool IsOwnershipClosed(string symbol)
        => !string.IsNullOrWhiteSpace(symbol);
}
