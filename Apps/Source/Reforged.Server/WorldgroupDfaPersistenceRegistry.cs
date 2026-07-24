namespace Reforged.Server;

public enum WorldgroupDfaOperationClass
{
    ContextSelection,
    LockAcquire,
    AbsoluteLockAcquire,
    StepLock,
    Query,
    QueryNoPosition,
    Insert,
    Update,
    Delete,
    Reset,
    OpenClose,
    ModeOrCount
}

public sealed record WorldgroupDfaOperationBinding(
    string Symbol,
    WorldgroupDfaOperationClass Classification,
    int DirectCallsites,
    string ProvenBoundary);

public static class WorldgroupDfaPersistenceRegistry
{
    public static readonly IReadOnlyList<WorldgroupDfaOperationBinding> Operations =
    [
        new("_dfaSetBlk", WorldgroupDfaOperationClass.ContextSelection, 154, "Selects the active DFA block/context before record operations; caller-side arity varies because setup can be inherited from retained host state."),
        new("_dfaAcqLock", WorldgroupDfaOperationClass.LockAcquire, 156, "Five-position lock-acquisition call shape dominates MajorMUD callers; lock-mode constants include 5, 8, 9, 10, 12, and 13."),
        new("_dfaAcqAbsLock", WorldgroupDfaOperationClass.AbsoluteLockAcquire, 20, "Acquires an absolute-record lock using caller-provided record identity/state."),
        new("_dfaGetAbsLock", WorldgroupDfaOperationClass.AbsoluteLockAcquire, 24, "Reads or resolves the current absolute lock identity."),
        new("_dfaStepLock", WorldgroupDfaOperationClass.StepLock, 94, "Advances a retained lock/query cursor; stable caller constants include 0x18 and 0x21."),
        new("_dfaQuery", WorldgroupDfaOperationClass.Query, 48, "Queries through the active DFA context and retained cursor state."),
        new("_dfaQueryNP", WorldgroupDfaOperationClass.QueryNoPosition, 14, "No-position query path; MajorMUD callers consistently pass 0x38."),
        new("_dfaInsertV", WorldgroupDfaOperationClass.Insert, 12, "Variable-length insert path used by MMUD/MMPLS persistent records."),
        new("_dfaInsertDup", WorldgroupDfaOperationClass.Insert, 48, "Duplicate-key insert path with caller-provided record data."),
        new("_dfaUpdateV", WorldgroupDfaOperationClass.Update, 4, "Variable-length update path observed in MMPLS persistence."),
        new("_dfaUpdateDup", WorldgroupDfaOperationClass.Update, 46, "Duplicate-key update path; one explicit record/data argument at proven callers."),
        new("_dfaDelete", WorldgroupDfaOperationClass.Delete, 36, "Deletes the current record selected by retained DFA context."),
        new("_dfaRstBlk", WorldgroupDfaOperationClass.Reset, 164, "Resets/releases the active DFA block and associated retained operation state."),
        new("_dfaAbs", WorldgroupDfaOperationClass.Reset, 104, "Terminates or resets absolute-position state; caller forms include zero-argument and two-argument variants."),
        new("_dfaOpen", WorldgroupDfaOperationClass.OpenClose, 36, "Opens a DFA data source; two callers provide the proven 0x7F0 record-size constant."),
        new("_dfaClose", WorldgroupDfaOperationClass.OpenClose, 40, "Closes the caller-selected DFA data source."),
        new("_dfaCountRec", WorldgroupDfaOperationClass.ModeOrCount, 4, "Returns the current context record count."),
        new("_dfaMode", WorldgroupDfaOperationClass.ModeOrCount, 2, "Sets or queries DFA mode; observed MajorMUD caller passes zero.")
    ];

    public static bool IsPersistentMutation(string symbol) => symbol is
        "_dfaInsertV" or "_dfaInsertDup" or "_dfaUpdateV" or "_dfaUpdateDup" or "_dfaDelete";
}
