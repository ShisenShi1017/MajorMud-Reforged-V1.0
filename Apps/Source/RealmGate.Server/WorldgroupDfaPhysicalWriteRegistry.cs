namespace RealmGate.Server;

public enum WorldgroupDfaPhysicalOperation
{
    BeginTransaction,
    EndTransaction,
    AbortTransaction,
    Insert,
    Update,
    Delete,
    Unlock
}

public sealed record WorldgroupDfaPhysicalWriteBinding(
    string Export,
    uint Rva,
    ushort BtrieveOperation,
    WorldgroupDfaPhysicalOperation Operation,
    string ProvenBehavior);

public static class WorldgroupDfaPhysicalWriteRegistry
{
    public const string StorageModule = "WBTRV32.DLL";
    public const string StorageEntryPoint = "BTRCALL";
    public const uint SharedContextPointer = 0x00467C74;
    public const uint SharedRecordBuffer = 0x00467CA4;
    public const uint LastStatus = 0x00467CA0;
    public const uint LastAuxiliaryStatus = 0x00467CA2;

    public static readonly IReadOnlyList<WorldgroupDfaPhysicalWriteBinding> Operations =
    [
        new("_dfaBegTrans", 0x00041F0A, 0x13, WorldgroupDfaPhysicalOperation.BeginTransaction,
            "Adds the caller-provided transaction subtype to 0x13, then synchronously calls WBTRV32!BTRCALL."),
        new("_dfaEndTrans", 0x00041F5D, 0x14, WorldgroupDfaPhysicalOperation.EndTransaction,
            "Calls WBTRV32!BTRCALL with operation 0x14 and zeroed data arguments."),
        new("_dfaAbtTrans", 0x00041F39, 0x15, WorldgroupDfaPhysicalOperation.AbortTransaction,
            "Calls WBTRV32!BTRCALL with operation 0x15 and zeroed data arguments."),
        new("_dfaInsertV", 0x000428CA, 0x02, WorldgroupDfaPhysicalOperation.Insert,
            "Clamps length to 0xFFF0, copies the caller record into the shared record buffer, then calls BTRCALL operation 2."),
        new("_dfaInsertDup", 0x0004293E, 0x02, WorldgroupDfaPhysicalOperation.Insert,
            "Uses retained record length, stages the record in the shared buffer, and calls BTRCALL operation 2; status 1 is normalized as duplicate-success/handled."),
        new("_dfaUpdateV", 0x00042783, 0x03, WorldgroupDfaPhysicalOperation.Update,
            "Clamps length to 0xFFF0, stages the record, supplies the retained key number, and calls BTRCALL operation 3."),
        new("_dfaUpdateDup", 0x00042803, 0x03, WorldgroupDfaPhysicalOperation.Update,
            "Uses retained record length and key number, stages the record, and calls BTRCALL operation 3; status 1 is normalized as handled."),
        new("_dfaDelete", 0x000429CB, 0x04, WorldgroupDfaPhysicalOperation.Delete,
            "Calls BTRCALL operation 4 with the retained key number and record length from the active DFA context."),
        new("_dfaUnlock", 0x00042EA0, 0x1B, WorldgroupDfaPhysicalOperation.Unlock,
            "Calls BTRCALL operation 0x1B; selector -1 uses a four-byte key buffer, otherwise the selector is passed directly.")
    ];

    public static bool IsPhysicalMutation(ushort operation) => operation is 0x02 or 0x03 or 0x04;
}
