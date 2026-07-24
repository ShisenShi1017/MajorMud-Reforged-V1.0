namespace Reforged.Server;

/// <summary>Evidence-backed WCCMMUD session-state dispatch recovered in Worldgroup Update0010.</summary>
public static class WorldgroupSessionStateDispatchRegistry
{
    public const uint IndexTable = 0x00405221;
    public const uint TargetTable = 0x004052AA;
    public const uint SharedFallback = 0x0040537B;
    public const uint ActiveGameplayEntry = 0x004088CC;
    public const uint NextGameplayBoundary = 0x0045003C;
    public const int MaximumTableState = 0x88;

    public static bool IsActiveGameplayState(int state) => state == 0x82;
    public static bool IsModalGameplayState(int state) => state is >= 0x83 and <= 0x88;
    public static bool IsTableState(int state) => (uint)state <= MaximumTableState;
}
