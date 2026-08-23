namespace Reforged.Server;

public static class WorldgroupRemoteActionDispatchRegistry
{
    public const uint Entry = 0x0046C573;
    public const uint End = 0x0046CC0B;
    public const int ArgumentCount = 4;
    public const int ActionTypeOffset = 0x360;
    public const int ActionStateOffset = 0x374;
    public const int ActionParameterOffset = 0x39C;
    public const int ActionRepeatOffset = 0x3B0;
    public const int LinkedRecordIdOffset = 0x338;
    public const int RoomObjectListOffset = 0x400;
    public const int RoomObjectSlotCount = 15;
    public const int DirtyFlagOffset = 0x5F8;
    public const int MinimumDirectType = 6;
    public const int MaximumDirectType = 24;
    public const int MinimumActionSlot = 0;
    public const int MaximumActionSlot = 10;

    public static readonly ushort[] PairedToggleTypes = { 7, 11 };
    public static readonly ushort[] RoomWideDispatchTypes = { 9, 24 };
    public static readonly int[] ModeClearedMasks =
    {
        0x3FF0, 0x0010, 0x0020, 0x0040, 0x0080, 0x0100,
        0x0200, 0x0400, 0x0800, 0x1000, 0x2000,
    };
}
