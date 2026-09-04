namespace RealmGate.Server;

public static class WorldgroupItemTargetSpellDispatchRegistry
{
    public const uint Entry = 0x00447110;
    public const uint End = 0x00449232;
    public const int ArgumentCount = 4;
    public const int DefinitionArgument = 1;
    public const int CasterSessionArgument = 2;
    public const int ItemHandleArgument = 3;
    public const int InvocationModeArgument = 4;
    public const int CasterResolver = 0x004320D9;
    public const int ItemResolver = 0x0043248A;
    public const int RoomResolver = 0x0043202C;
    public const int CompatibilityGate = 0x00429D40;
    public const int ChainedDefinitionResolver = 0x00476F7D;
    public const int ChainedDefinitionRelease = 0x00476F22;
    public const int RoomBroadcast = 0x0046B010;
    public const int OutputFlush = 0x0046B170;
    public const int MaximumEffectSlots = 10;
    public const int MaximumOuterAttempts = 20;
    public const int DefinitionTypeOffset = 0xCC;
    public const int DefinitionResourceCostOffset = 0xBC;
    public const int DefinitionSecondaryRequirementOffset = 0xF0;
    public const int CasterResourceOffset = 0xBA;
    public const int CasterSecondaryResourceOffset = 0x602;

    public static bool IsAllowedDefinitionType(int invocationMode, int type) =>
        invocationMode == 0 ? type is 6 or 8 : type is 6 or 7;
}
