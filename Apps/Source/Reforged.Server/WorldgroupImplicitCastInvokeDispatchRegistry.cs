namespace Reforged.Server;

public static class WorldgroupImplicitCastInvokeDispatchRegistry
{
    public const uint GameplayBranchStart = 0x0045083B;
    public const uint EntityMatcher = 0x0046ABB4;
    public const uint ClassRecordResolver = 0x00432BD3;
    public const uint SharedSpellPowerExecutor = 0x00461273;

    public const int PlayerClassRecordIdOffset = 0x92;
    public const int ClassTypeOffset = 0x40;
    public const ushort KaiClassType = 5;
    public const int CastSelector = 0x1F;
    public const int InvokeSelector = 0x6C;

    public static int SelectAction(ushort classType) =>
        classType == KaiClassType ? InvokeSelector : CastSelector;

    public static bool IsImplicitEntityTargetShortcut => true;
    public static bool IsGeneralCommandLookup => false;
}
