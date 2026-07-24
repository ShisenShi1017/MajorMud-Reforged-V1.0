namespace Reforged.Server;

/// <summary>
/// Evidence-backed description of WCCMMUD room-scoped output dispatch recovered from
/// wccmmud.dll at 0x0046B010. This routine is not a command tokenizer or command table lookup.
/// </summary>
public static class WorldgroupRoomBroadcastDispatchRegistry
{
    public const uint FunctionAddress = 0x0046B010;
    public const uint EndAddress = 0x0046B132;
    public const int ArgumentCount = 8;

    public static readonly string[] ProvenExecutionOrder =
    {
        "iterate active Worldgroup session slots in ascending index order",
        "exclude up to two caller-supplied session indices",
        "resolve the MajorMUD player/session record",
        "require exact room-coordinate equality",
        "apply visibility, range, and state filters",
        "optionally execute the per-recipient preparation callback",
        "select the message template through the recipient class/category table",
        "flush the prepared text to the recipient",
        "restore and clear the shared output buffer",
        "return the count of successfully eligible recipients"
    };

    public static bool IsCommandTokenizer => false;
    public static bool IsCommandLookup => false;
}
