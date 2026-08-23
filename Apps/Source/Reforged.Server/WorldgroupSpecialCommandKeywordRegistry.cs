namespace Reforged.Server;

public static class WorldgroupSpecialCommandKeywordRegistry
{
    public const uint InterpreterEntry = 0x00470209;
    public const int KeywordCount = 41;
    public const uint TextBranch = 0x004710E2;
    public const uint RoomTextBranch = 0x00471130;
    public const uint RemoteActionBranch = 0x00471605;
    public const uint TextExecutor = 0x0043BCCA;
    public const uint RoomTextExecutor = 0x0043BE15;
    public const uint RemoteActionExecutor = 0x0046C573;
    public const int RemoteActionArgumentCount = 4;
    public const int RemoteActionMinimumActionSlot = 0;
    public const int RemoteActionMaximumActionSlot = 10;
    public const int PlayerRoomXOffset = 0xC4;
    public const int PlayerRoomYOffset = 0xC8;
    public const int ActionTypeArrayOffset = 0x360;

    public static readonly string[] OrderedKeywords =
    {
        "price",
        "cast",
        "giveability",
        "addability",
        "teleport",
        "removeability",
        "nomonsters",
        "needmonster",
        "summon",
        "message",
        "takeitem",
        "addexp",
        "giveitem",
        "hideitem",
        "checkability",
        "class",
        "race",
        "goodaligned",
        "evilaligned",
        "minlevel",
        "maxlevel",
        "checkitem",
        "failitem",
        "text",
        "roomtext",
        "roomitem",
        "failroomitem",
        "clearitem",
        "addevil",
        "testability",
        "failability",
        "remoteaction",
        "random",
        "adddelay",
        "testskill",
        "checkskill",
        "givecoins",
        "test_tournament",
        "checkspell",
        "flag",
        "learnspell",
    };
}
