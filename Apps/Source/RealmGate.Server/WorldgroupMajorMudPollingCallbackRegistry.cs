namespace RealmGate.Server;

public enum MajorMudPollingCallbackClass
{
    GameplayHeartbeat,
    SessionAsyncWorkflow,
    RecordLookupWorkflow,
    GlobalRecordIterator,
    ProgressThresholdWorkflow,
    ModalValidationWorkflow,
    CharacterStateCleanupWorkflow,
    SecondaryRecordLookupWorkflow
}

public sealed record MajorMudPollingCallbackBinding(
    uint Address,
    MajorMudPollingCallbackClass Classification,
    bool SelfTerminates,
    string ProvenBoundary);

public static class WorldgroupMajorMudPollingCallbackRegistry
{
    public static readonly IReadOnlyList<MajorMudPollingCallbackBinding> Callbacks =
    [
        new(0x00402AAA, MajorMudPollingCallbackClass.GameplayHeartbeat, false,
            "Recurring active-session heartbeat; services per-session flag branches and periodic global maintenance."),
        new(0x00402D12, MajorMudPollingCallbackClass.SessionAsyncWorkflow, true,
            "Session-scoped asynchronous workflow; resolves the active player, polls host operation state, and stops on terminal/error transitions."),
        new(0x0044ED0A, MajorMudPollingCallbackClass.RecordLookupWorkflow, true,
            "Host-backed record/name lookup workflow; advances host search state and stops polling on completion or failure."),
        new(0x0045E4E4, MajorMudPollingCallbackClass.GlobalRecordIterator, true,
            "Global record iterator using retained host state; processes one available record and terminates after exhaustion/error."),
        new(0x0045F63B, MajorMudPollingCallbackClass.ProgressThresholdWorkflow, true,
            "Progress/threshold workflow; compares retained counters, emits terminal output, resets globals, and releases retained state."),
        new(0x0045F7D3, MajorMudPollingCallbackClass.ModalValidationWorkflow, true,
            "Modal validation workflow gated by session state 0x10; performs host-backed lookup and validation before terminal transition."),
        new(0x0045FB56, MajorMudPollingCallbackClass.CharacterStateCleanupWorkflow, true,
            "Modal character-state cleanup workflow gated by session state 0x13; clears owned effects/references in bounded arrays."),
        new(0x0047261D, MajorMudPollingCallbackClass.SecondaryRecordLookupWorkflow, true,
            "Second host-backed lookup workflow with retained cursor/state and explicit completion/failure stop paths.")
    ];

    public static bool IsKnown(uint address) => Callbacks.Any(x => x.Address == address);
}
