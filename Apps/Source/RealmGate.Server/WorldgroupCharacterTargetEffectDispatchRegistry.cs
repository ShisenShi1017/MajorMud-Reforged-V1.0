namespace RealmGate.Server;

public static class WorldgroupCharacterTargetEffectDispatchRegistry
{
    public const uint EffectSelectorRead = 0x004457B1;
    public const uint EffectSelectorMap = 0x004457D5;
    public const int EffectSelectorCount = 0xA5;
    public const uint EffectDispatchTable = 0x0044587A;
    public const int DispatchClassCount = 33;
    public const int MaximumDirectEffectId = 0xA4;
    public const int EffectIdArrayOffset = 0xD8;
    public const int MaximumReportedEffectNameId = 0xBE;

    public static bool IsDirectlyDispatchedEffectId(int effectId) => effectId is >= 0 and <= MaximumDirectEffectId;
    public static bool IsKnownDispatchClass(int dispatchClass) => dispatchClass is >= 0 and < DispatchClassCount;
}
