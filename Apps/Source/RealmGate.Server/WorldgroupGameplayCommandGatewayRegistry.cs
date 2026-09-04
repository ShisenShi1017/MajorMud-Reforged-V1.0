namespace RealmGate.Server;

/// <summary>Evidence-backed active-gameplay command gateway recovered in Worldgroup Update0011.</summary>
public static class WorldgroupGameplayCommandGatewayRegistry
{
    public const uint Gateway = 0x0045003C;
    public const uint CurrentInputPointer = 0x0049D580;
    public const uint GeneralCommandExecutor = 0x0046B010;
    public const int MaximumRoomActionSlots = 10;
    public const int RoomActionAliasSize = 82;
    public static readonly int[] RoomActionAliasOffsets = [0x04, 0x56, 0xA8];
    public static bool IsKnownRoomActionType(int type) => type is 0x0A or 0x0C;
}
