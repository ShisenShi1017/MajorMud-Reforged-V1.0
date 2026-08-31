namespace Reforged.Server;

public sealed record RuntimeClassicBufferSaveFamily(
    int BackgroundState,
    int FullFlushOrder,
    string Id,
    string[] DataFiles,
    uint SaveRoutineVa,
    uint CountGlobalVa);

/// <summary>
/// Exact Classic MajorMUD save/persistence coordination boundary for the supplied WCCMMUD build.
///
/// Player data and Buffer data are independent native schedulers. Player data is handled by
/// _background_other -> _save_characters using WCCUSER2.DAT. Buffer data is handled by
/// _background_save_buffers and progressed record-by-record by _save_next_buffer during the
/// main user polling routine. Module finalization performs the full 11-family buffer flush before
/// closing Btrieve handles. WCCMMUD imports record mutations but no begin/end/abort transaction
/// functions, so ordinary MajorMUD saves are record-level best effort rather than a module-selected
/// all-or-nothing transaction. Physical disk flush timing remains MKDE-owned; a successful WCC/DFA
/// mutation does not imply an immediate FlushFileBuffers call.
/// </summary>
public static class RuntimeClassicSaveCoordinationAuthority
{
    public const string WccMudSha256 = "5a99e7c34cf9cf58dc6e97fc3ef28f1bd83f9bcec15ca59ad08826d2f21d2077";
    public static readonly int PlayerDataSaveIntervalSeconds = 60;
    public static readonly int BufferDataSaveIntervalSeconds = 300;
    public static readonly int DefaultSuccessfulPlayersPerSweep = 1;
    public static readonly int MaximumPlayerSlotsVisitedPerSweep = 300;

    public const uint BackgroundOtherVa = 0x004218E0;
    public const uint BackgroundSaveBuffersVa = 0x00421909;
    public const uint SaveCharactersVa = 0x00423DFD;
    public const uint SaveNextBufferVa = 0x00431907;
    public const uint SaveBuffersVa = 0x00431B86;
    public const uint SavePlayerVa = 0x0046DF70;
    public const uint LjngameFinrouVa = 0x004027FE;
    public const uint PlayerSweepCursorVa = 0x0047FBAC;
    public const uint PlayerSaveIntervalOptionVa = 0x00482CAC;
    public const uint BufferSaveIntervalOptionVa = 0x00482CB0;
    public const uint PlayerSaveLimitOptionVa = 0x00482C90;
    public const uint PendingBufferCycleCountVa = 0x00482144;
    public const uint WccUser2HandleVa = 0x0047911C;

    public const string WgServerSharedMutationBufferAddress = "0x00467CA4";
    public const string WgServerLastStatusAddress = "0x00467CA0";
    public const string WgServerAuxiliaryStatusAddress = "0x00467CA2";
    public const string MkdePendingWriteCounterOffset = "+0x08";
    public const string MkdeFlushRequiredFlag = "+0x0A bit 0x04";

    public static readonly bool GenericCoordinatorOwnsClassicOrdering = false;
    public static readonly bool ExactPlayerWorldParticipantOrderResolved = true;
    public static readonly bool NativeBufferDataParticipantMappingResolved = true;
    public static readonly bool MajorMudTransactionGroupingResolved = true;
    public static readonly bool ExactOrdinarySaveFlushDurabilityResolved = true;
    public static readonly bool ShutdownSaveOrderingResolved = true;
    public static readonly bool ExactPartialSaveSemanticsResolved = true;

    // Explicitly outside the ordinary-save coordination assertion. MKDE/BTRVLOG recovery bytes
    // are a host-engine crash-recovery implementation detail, not a hidden WCC save participant.
    public static readonly bool CrashReplayRecordFormatResolved = false;

    public static IReadOnlyList<RuntimeClassicBufferSaveFamily> BufferFamilies { get; } = new[]
    {
        new RuntimeClassicBufferSaveFamily(0, 0, "room",          new[]{"WCCMP002.DAT"},              0x00431EE5, 0x00482C58),
        new RuntimeClassicBufferSaveFamily(1, 2, "monster",       new[]{"WCCACMS2.DAT"},              0x0043234E, 0x00482C60),
        new RuntimeClassicBufferSaveFamily(2, 3, "known-monster", new[]{"WCCKNMS2.DAT"},              0x0043273D, 0x00482C64),
        new RuntimeClassicBufferSaveFamily(3, 4, "spell",         new[]{"WCCSPEL2.DAT"},              0x00432906, 0x00482C68),
        new RuntimeClassicBufferSaveFamily(4, 5, "class",         new[]{"WCCCLAS2.DAT"},              0x00432ACE, 0x00482C74),
        new RuntimeClassicBufferSaveFamily(5, 6, "race",          new[]{"WCCRACE2.DAT"},              0x00432C90, 0x00482C6C),
        new RuntimeClassicBufferSaveFamily(6, 7, "shop",          new[]{"WCCSHOP2.DAT"},              0x00432E52, 0x00482C70),
        new RuntimeClassicBufferSaveFamily(7, 8, "bankbook",      new[]{"WCCBANK2.DAT"},              0x00433013, 0x00482C78),
        new RuntimeClassicBufferSaveFamily(8, 9, "message-text",  new[]{"WCCMSG2.DAT", "WCCTEXT2.DAT"}, 0x00433570, 0x00482C7C),
        new RuntimeClassicBufferSaveFamily(9,10, "gang",          new[]{"WCCGANG2.DAT"},              0x00433814, 0x00482C80),
        new RuntimeClassicBufferSaveFamily(10,1,"item",           new[]{"WCCITEM2.DAT"},              0x00432186, 0x00482C5C)
    };

    public static IReadOnlyList<string> BackgroundRotationOrder { get; } = BufferFamilies
        .OrderBy(x => x.BackgroundState).Select(x => x.Id).ToArray();

    public static IReadOnlyList<string> FullFlushOrder { get; } = BufferFamilies
        .OrderBy(x => x.FullFlushOrder).Select(x => x.Id).ToArray();

    public static IReadOnlyList<string> WorldgroupTransactionExports { get; } = new[]
    {
        "_dfaBegTrans", "_dfaEndTrans", "_dfaAbtTrans", "_dfaUnlock"
    };

    public static IReadOnlyList<string> MajorMudDfaMutationImports { get; } = new[]
    {
        "_dfaInsertV", "_dfaInsertDup", "_dfaUpdateDup", "_dfaDelete"
    };

    public static IReadOnlyList<string> MajorMudDfaTransactionImports { get; } = Array.Empty<string>();

    public static IReadOnlyList<string> ProvenStorageLayers { get; } = new[]
    {
        "MajorMUD WCC/WBB module",
        "WGSERVER DFA retained context",
        "WBTRV32.DLL BTRCALL transport",
        "W32MKDE.EXE cache/write/flush/recovery owner"
    };

    public static IReadOnlyList<string> Validate()
    {
        var failures = new List<string>();
        if (PlayerDataSaveIntervalSeconds != 60) failures.Add("player-save-clock-mismatch");
        if (BufferDataSaveIntervalSeconds != 300) failures.Add("buffer-save-clock-mismatch");
        if (DefaultSuccessfulPlayersPerSweep != 1) failures.Add("player-save-limit-default-mismatch");
        if (MaximumPlayerSlotsVisitedPerSweep != 300) failures.Add("player-sweep-visit-cap-mismatch");
        if (BufferFamilies.Count != 11) failures.Add("buffer-family-count-mismatch");
        if (!BackgroundRotationOrder.SequenceEqual(new[]{"room","monster","known-monster","spell","class","race","shop","bankbook","message-text","gang","item"}))
            failures.Add("background-buffer-order-mismatch");
        if (!FullFlushOrder.SequenceEqual(new[]{"room","item","monster","known-monster","spell","class","race","shop","bankbook","message-text","gang"}))
            failures.Add("full-buffer-flush-order-mismatch");
        var messageText = BufferFamilies.Single(x => x.Id == "message-text");
        if (!messageText.DataFiles.SequenceEqual(new[]{"WCCMSG2.DAT","WCCTEXT2.DAT"})) failures.Add("message-text-dual-file-mapping-mismatch");
        if (BufferFamilies.SelectMany(x => x.DataFiles).Contains("WCCUSER2.DAT", StringComparer.OrdinalIgnoreCase)) failures.Add("player-file-leaked-into-buffer-cycle");
        foreach (var excluded in new[]{"WCCITOW2.DAT","WCCACTS2.DAT","WCCVACN2.DAT"})
            if (BufferFamilies.SelectMany(x => x.DataFiles).Contains(excluded, StringComparer.OrdinalIgnoreCase)) failures.Add("non-buffer-family-included:" + excluded);
        if (MajorMudDfaTransactionImports.Count != 0) failures.Add("wcc-selected-transaction-import-present");
        if (!ExactPlayerWorldParticipantOrderResolved || !NativeBufferDataParticipantMappingResolved ||
            !MajorMudTransactionGroupingResolved || !ExactOrdinarySaveFlushDurabilityResolved ||
            !ShutdownSaveOrderingResolved || !ExactPartialSaveSemanticsResolved)
            failures.Add("save-coordination-contract-demoted");
        if (CrashReplayRecordFormatResolved) failures.Add("crash-replay-nonclaim-promoted");
        return failures;
    }

    public static void RequireClassicPlayerWorldSaveOrder() { if (!ExactPlayerWorldParticipantOrderResolved) throw new InvalidOperationException("native-player-world-save-order-unresolved"); }
    public static void RequireClassicBufferDataParticipantMapping() { if (!NativeBufferDataParticipantMappingResolved) throw new InvalidOperationException("native-buffer-data-participants-unresolved"); }
    public static void RequireClassicMajorMudTransactionGrouping() { if (!MajorMudTransactionGroupingResolved) throw new InvalidOperationException("native-majormud-save-transaction-grouping-unresolved"); }
    public static void RequireClassicOrdinarySaveFlushDurability() { if (!ExactOrdinarySaveFlushDurabilityResolved) throw new InvalidOperationException("native-ordinary-save-flush-durability-unresolved"); }
    public static void RequireClassicShutdownSaveOrdering() { if (!ShutdownSaveOrderingResolved) throw new InvalidOperationException("native-shutdown-save-order-unresolved"); }
}
