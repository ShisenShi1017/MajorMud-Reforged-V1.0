namespace Reforged.Server;

public static class WorldgroupSchedulingRuntimeRegistry
{
    public const uint BeginPolling = 0x0041FA39;
    public const uint StopPolling = 0x0041FA73;
    public const int SessionPollingCallbackOffset = 0x48;
    public const byte PollingSentinelByte = 0xC0;
    [Obsolete("Native evidence proves 0xC0 is an injected polling sentinel, not a delay value.")]
    public const int PollingRearmDelayRaw = PollingSentinelByte;
    public const uint PollingInvokeSite = 0x004231EC;
    public const uint PollingRearmSite = 0x00423204;
    public const uint PollingSentinelDispatchSitePrimary = 0x0042121F;
    public const uint PollingSentinelDispatchSiteSecondary = 0x00421DE0;
    public const uint PollingSentinelDispatchSiteModeTable = 0x00421F9E;
    public const uint InjectByteThunk = 0x0044C264;
    public const uint GalgsblInjectByte = 0x0040412E;
    public const uint CurrentPollingSessionGlobal = 0x00451660;

    public const uint InitializeTask = 0x0043E6C8;
    public const uint ModifyTask = 0x0043E733;
    public const uint ProcessTask = 0x0043E773;
    public const uint TaskVectorHandleGlobal = 0x00456C3C;
    public const uint TaskCursorGlobal = 0x00456C40;
    public const int InitialTaskCapacity = 5;
    public const int TaskGrowthIncrement = 5;
    public const int TaskElementSize = 4;

    public static bool PollingIsRegistered(nuint callback) => callback != 0;
    public static bool IsPollingSentinel(byte value) => value == PollingSentinelByte;
    public static int NextTaskIndex(int current, int count) => count <= 0 ? -1 : (current + 1) % count;
}
