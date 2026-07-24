namespace Reforged.Server;

public static class WorldgroupCharacterTargetSpellDispatchRegistry
{
    public const uint DispatcherStart = 0x00444712;
    public const uint DispatcherEnd = 0x00447110;
    public const int ArgumentCount = 5;
    public const int SpellDefinitionArgument = 1;
    public const int CasterSessionArgument = 2;
    public const int TargetSessionArgument = 3;
    public const int AuxiliaryTargetArgument = 4;
    public const int InvocationModeArgument = 5;
    public const int SessionResolver = 0x004320D9;
    public const int ClassRecordResolver = 0x00432BD3;
    public const int ResourceOrStateGate = 0x0043D570;
    public const int RoomRecordResolver = 0x0043202C;
    public const int OutputFlush = 0x0046B170;
    public const int MaximumBaseChance = 100;

    public static bool IsKnownTargetKind(int kind) => kind is 0 or 1 or 2 or 3;
}
