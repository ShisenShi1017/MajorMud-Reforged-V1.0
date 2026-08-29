namespace Reforged.Server;

/// <summary>
/// Evidence gate for the recovered Classic MajorMUD save/persistence boundary.
///
/// The retained evidence proves native player/buffer save clocks, Worldgroup DFA mutation and
/// transaction surfaces, the WGSERVER shared record staging buffer, WBTRV32 transport, and MKDE
/// write-back/flush ownership. It does not prove one MajorMUD player/world participant order,
/// the exact record families meant by native "Buffer data", transaction grouping chosen by the
/// MajorMUD module, per-save flush durability, or shutdown-save ordering.
/// </summary>
public static class RuntimeClassicSaveCoordinationAuthority
{
    public const int PlayerDataSaveIntervalSeconds = 60;
    public const int BufferDataSaveIntervalSeconds = 300;

    public const string WgServerSharedMutationBufferAddress = "0x00467CA4";
    public const string WgServerLastStatusAddress = "0x00467CA0";
    public const string WgServerAuxiliaryStatusAddress = "0x00467CA2";
    public const string MkdePendingWriteCounterOffset = "+0x08";
    public const string MkdeFlushRequiredFlag = "+0x0A bit 0x04";

    public static bool GenericCoordinatorOwnsClassicOrdering => false;
    public const bool ExactPlayerWorldParticipantOrderResolved = false;
    public const bool NativeBufferDataParticipantMappingResolved = false;
    public const bool MajorMudTransactionGroupingResolved = false;
    public const bool ExactOrdinarySaveFlushDurabilityResolved = false;
    public const bool ShutdownSaveOrderingResolved = false;
    public const bool CrashReplayRecordFormatResolved = false;

    public static IReadOnlyList<string> WorldgroupTransactionExports { get; } = new[]
    {
        "_dfaBegTrans",
        "_dfaEndTrans",
        "_dfaAbtTrans",
        "_dfaUnlock"
    };

    public static IReadOnlyList<string> MajorMudDfaMutationImports { get; } = new[]
    {
        "_dfaInsertV",
        "_dfaInsertDup",
        "_dfaUpdateV",
        "_dfaUpdateDup",
        "_dfaDelete"
    };

    public static IReadOnlyList<string> ProvenStorageLayers { get; } = new[]
    {
        "MajorMUD WCC/WBB module",
        "WGSERVER DFA retained context",
        "WBTRV32.DLL BTRCALL transport",
        "W32MKDE.EXE cache/write/flush/recovery owner"
    };

    public static void RequireClassicPlayerWorldSaveOrder()
    {
        if (!ExactPlayerWorldParticipantOrderResolved)
            throw new InvalidOperationException("native-player-world-save-order-unresolved");
    }

    public static void RequireClassicBufferDataParticipantMapping()
    {
        if (!NativeBufferDataParticipantMappingResolved)
            throw new InvalidOperationException("native-buffer-data-participants-unresolved");
    }

    public static void RequireClassicMajorMudTransactionGrouping()
    {
        if (!MajorMudTransactionGroupingResolved)
            throw new InvalidOperationException("native-majormud-save-transaction-grouping-unresolved");
    }

    public static void RequireClassicOrdinarySaveFlushDurability()
    {
        if (!ExactOrdinarySaveFlushDurabilityResolved)
            throw new InvalidOperationException("native-ordinary-save-flush-durability-unresolved");
    }

    public static void RequireClassicShutdownSaveOrdering()
    {
        if (!ShutdownSaveOrderingResolved)
            throw new InvalidOperationException("native-shutdown-save-order-unresolved");
    }
}
