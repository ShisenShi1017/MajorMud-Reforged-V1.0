namespace Reforged.Server;

/// <summary>
/// Evidence-backed storage contract for the stock MajorMUD room-exit record.
/// MajorMUD is a legacy 16-bit target, but the room exit record is mixed-width:
/// RoomType/Para2 are signed 16-bit values while Para1/Para3/Para4 are signed 32-bit values.
/// These constants describe storage/encoding only; they do not invent unresolved command semantics.
/// </summary>
public static class RuntimeClassicRoomExitNativeFieldContract
{
    public const int SignedInt16Min = -32768;
    public const int SignedInt16Max = 32767;

    public enum RoomExitType : short
    {
        Normal = 0,
        Spell = 1,
        Key = 2,
        Item = 3,
        Toll = 4,
        Action = 5,
        Hidden = 6,
        Door = 7,
        ChangeMap = 8,
        Trap = 9,
        Text = 10,
        Gate = 11,
        RemoteAction = 12,
        Class = 13,
        Race = 14,
        Level = 15,
        Timed = 16,
        Ticket = 17,
        UserCount = 18,
        BlockGuard = 19,
        Alignment = 20,
        Delay = 21,
        Cast = 22,
        Ability = 23,
        SpellTrap = 24,
    }

    public enum TrapState : short
    {
        Active = 0,
        Inactive = 1,
        Hidden = 2,
        ActiveMovement = 3,
        InactiveMovement = 4,
    }

    // Stock Hidden-exit Para1 values recovered from the native database editor map.
    public const int HiddenPassable = 1;
    public const int HiddenNeedsSearch = 2;
    public const int VisibleSearched = 4;
    public const int VisibleBlocked = 8;
    public const int BlockedOneAction = 16;
    public const int BlockedTwoActions = 48;
    public const int BlockedThreeActions = 112;
    public const int BlockedFourActions = 240;
    public const int BlockedFiveActions = 496;
    public const int BlockedSixActions = 1008;
    public const int BlockedSevenActions = 2032;
    public const int BlockedEightActions = 4080;
    public const int BlockedNineActions = 8176;
    public const int BlockedTenActions = 16368;
    public const int HiddenOnce = 16384;
    public const int PermanentVisible = 32768;

    public static bool IsKnownTrapState(int value) => value is >= 0 and <= 4;

    public static bool IsKnownHiddenEncoding(int value) => value is
        HiddenPassable or HiddenNeedsSearch or VisibleSearched or VisibleBlocked or
        BlockedOneAction or BlockedTwoActions or BlockedThreeActions or BlockedFourActions or
        BlockedFiveActions or BlockedSixActions or BlockedSevenActions or BlockedEightActions or
        BlockedNineActions or BlockedTenActions or HiddenOnce or PermanentVisible;
}
