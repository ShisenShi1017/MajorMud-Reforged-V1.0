namespace RealmGate.Server;

public static class WorldgroupSpecialCommandInterpreterRegistry
{
    public const uint EntryPoint = 0x00470209;
    public const uint EndAddress = 0x00471A30;
    public const int FunctionSize = 0x1827;
    public const int SessionArgumentIndex = 0;
    public const int MutableLineArgumentIndex = 1;
    public const int ScratchReferenceCapacity = 100;
    public const char SegmentDelimiter = ':';
    public const byte ContinueResult = 0;
    public const byte StopHandledResult = 1;
    public const byte RejectOrDeferResult = 2;
    public const uint ResolvePlayer = 0x004320D9;
    public const uint CompareKeyword = 0x00478B3E; // WGSERVER!_sameto
    public const uint SkipWord = 0x00478BE6;       // WGSERVER!_skpwrd
    public const uint SkipWhitespace = 0x00478B08; // WGSERVER!_skpwht
}

public static class WorldgroupTextblockRecordFetchRegistry
{
    public const uint EntryPoint = 0x0043379C;
    public const uint EndAddress = 0x004337FC;
    public const int FunctionSize = 0x60;
    public const int TextblockIdArgumentIndex = 0;
    public const int FileOrRecordSelectorArgumentIndex = 1;
    public const int RecordBufferArgumentIndex = 2;
    public const int RecordIdOffset = 0x10;
    public const int SelectorOffset = 0x00;
    public const int TransientFlagOffset = 0x7E7;
    public const int DfaLockMode = 9;
    public const uint DfaSetBlock = 0x00478ADE;
    public const uint DfaAcquireLock = 0x00478BE0;
    public const uint DfaResetBlock = 0x00478AF0;
    public const uint NormalizeRecord = 0x0044E376;
}
