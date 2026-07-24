namespace Reforged.Server;

public sealed record NativeHostCallerCleanupEvidence(
    string Symbol,
    int DirectCallsiteCount,
    int CleanupBytes,
    int CleanupSlotCount,
    bool ExactBoundedPushMatch);

public static class NativeHostCallerCleanupRegistry
{
    public const string EvidenceId = "FinalNativeClosure0010";
    public const string Scope = "Observed direct-call caller cleanup only; native parameter types and host implementation remain fail-closed.";

    public static readonly IReadOnlyList<NativeHostCallerCleanupEvidence> Proven = new NativeHostCallerCleanupEvidence[]
    {
        new("_sameas", 394, 8, 2, true),
        new("_dfaStepLock", 94, 12, 3, true),
        new("_numopt", 78, 12, 3, true),
        new("_dfaQuery", 48, 12, 3, true),
        new("_alcblok", 24, 8, 2, true),
        new("_begin_polling", 24, 8, 2, true),
        new("_rtkick", 18, 8, 2, true),
        new("_fsdxan", 12, 8, 2, true),
        new("_odedcrd", 10, 16, 4, true),
        new("_otstcrd", 10, 12, 3, true),
        new("_vfyadn", 10, 8, 2, true),
        new("_fsdroom", 8, 12, 3, true),
        new("_listing", 8, 8, 2, true),
        new("_lngopt", 8, 12, 3, true),
        new("_samein", 8, 8, 2, true),
        new("_c2bcpy", 6, 12, 3, true),
        new("_echsec", 6, 8, 2, true),
        new("_b2ccpy", 4, 12, 3, true),
        new("_getFileTm", 4, 12, 3, true),
        new("_msgscan", 4, 8, 2, true),
        new("_rsp2read", 4, 16, 4, true),
        new("_fsdapr", 2, 12, 3, true),
        new("_fsdego", 2, 8, 2, true),
        new("_locate", 2, 8, 2, true),
        new("_register_textvar", 2, 8, 2, true),
        new("_samepatu", 2, 12, 3, true),
        new("_sortstgs", 2, 8, 2, true),
        new("_tokopt", 2, 24, 6, true),
        new("_sameto", 694, 8, 2, false),
        new("_genrdn", 292, 8, 2, false),
        new("_dfaAcqLock", 266, 20, 5, false),
        new("_lngrnd", 84, 8, 2, false),
        new("_dfaGetAbsLock", 68, 16, 4, false),
        new("_dfaOpen", 36, 12, 3, false),
        new("_ptrblok", 24, 8, 2, false),
        new("_dfaAcqAbsLock", 20, 16, 4, false),
        new("_dfaInsertV", 12, 8, 2, false),
        new("_stzcpy", 12, 12, 3, false),
        new("_rsp2write", 8, 16, 4, false),
        new("_dfaUpdateV", 4, 8, 2, false),
        new("_senddpk", 2, 28, 7, false),
        new("_vtmsend", 2, 12, 3, false),
    };

    public static bool TryGet(string symbol, out NativeHostCallerCleanupEvidence evidence)
    {
        evidence = Proven.FirstOrDefault(x => string.Equals(x.Symbol, symbol, StringComparison.Ordinal))!;
        return evidence is not null;
    }
}
