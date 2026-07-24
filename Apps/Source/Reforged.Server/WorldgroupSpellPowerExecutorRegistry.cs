namespace Reforged.Server;

public static class WorldgroupSpellPowerExecutorRegistry
{
    public const uint ExecutorStart = 0x00461273;
    public const uint ExecutorEnd = 0x0046196B;
    public const int PlayerArgument = 1;
    public const int ActionSelectorArgument = 2;
    public const int InputWordOffsetArgument = 3;
    public const int CastSelector = 0x1F;
    public const int InvokeSelector = 0x6C;
    public const int PlayerClassRecordIdOffset = 0x92;
    public const int FearFlagOffset = 0x7C8;
    public const int CandidateMatcher = 0x0046ABB4;
    public const int SpellPowerRecordResolver = 0x00476F7D;
    public const int EligibilityCheck = 0x0042029B;
    public const int TargetClassifier = 0x004699FC;

    public enum TargetMode
    {
        None = 0,
        CharacterOrCreature = 1,
        ItemOrInventory = 2,
        SelfOrNoExternalTarget = 4,
        RoomOrArea = 8
    }

    public static bool IsSupportedSelector(int selector) =>
        selector is CastSelector or InvokeSelector;

    public static bool IsKnownTargetMode(int mode) =>
        mode is 0 or 1 or 2 or 4 or 8;
}
