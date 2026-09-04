namespace RealmGate.Server;

public static class WorldgroupTextblockSpecialCommandRegistry
{
    public const uint EntryPoint = 0x00471D55;
    public const uint EndAddress = 0x00471E99;
    public const int FunctionSize = 0x144;
    public const int SessionArgumentIndex = 0;
    public const int TextblockIdArgumentIndex = 1;
    public const int ReusableRecordBufferSize = 0x7E8;
    public const int RecordTextblockIdOffset = 0x10;
    public const int RecordPayloadOffset = 0x18;
    public const int MaximumRecordFetches = 50;
    public const byte StopAndHandledResult = 1;
    public const uint AcquireRecordBuffer = 0x00433724;
    public const uint ReleaseRecordBuffer = 0x00433752;
    public const uint FetchTextblockRecord = 0x0043379C;
    public const uint ExecuteSpecialCommandLine = 0x00470209;
}
