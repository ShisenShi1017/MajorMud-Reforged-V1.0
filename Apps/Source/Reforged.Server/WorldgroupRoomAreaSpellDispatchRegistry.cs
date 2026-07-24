namespace Reforged.Server;

public static class WorldgroupRoomAreaSpellDispatchRegistry
{
    public const uint Entry = 0x00449232;
    public const uint End = 0x00449BF0;
    public const int SizeBytes = 2494;
    public const int ArgumentCount = 4;
    public const int DefinitionArgument = 1;
    public const int CasterSessionArgument = 2;
    public const int RoomHandleArgument = 3;
    public const int InvocationModeArgument = 4;
    public const int CasterResolver = 0x004320D9;
    public const int RoomResolver = 0x00432295;
    public const int ClassResolver = 0x00432BD3;
    public const int EligibilityGate = 0x00420CB2;
    public const int EffectPrerequisiteGate = 0x0043FDCC;
    public const int GenericEffectHelper = 0x0043FC13;
    public const int RoomMutationHelper = 0x0046C241;
    public const int RoomBroadcast = 0x0046B010;
    public const int OutputFlush = 0x0046B170;
    public const int MaximumEffectSlots = 10;
    public const int DefinitionTypeOffset = 0xCC;
    public const int DefinitionEffectIdOffset = 0xD8;
    public const int DefinitionEffectValueOffset = 0xA8;
    public const int DefinitionResourceCostOffset = 0xBC;
    public const int DefinitionSecondaryRequirementOffset = 0xF0;
    public const int DefinitionLevelRequirementOffset = 0xBE;
    public const int CasterResourceOffset = 0xBA;
    public const int CasterSecondaryResourceOffset = 0x602;
    public const int CasterLevelOffset = 0x94;
    public const int CasterSkillOffset = 0x604;

    public static bool IsAllowedDefinitionType(int type) => type is 6 or 7;

    public static bool IsDirectSpecialEffectId(int id) =>
        id is 105 or 106 or 122 or 143 or 150 or 153 or 162 or 178;
}
