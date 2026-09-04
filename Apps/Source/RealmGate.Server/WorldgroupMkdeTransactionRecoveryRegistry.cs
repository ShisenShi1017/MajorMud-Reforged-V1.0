namespace RealmGate.Server;

public sealed record WorldgroupMkdeRecoveryArtifact(string Name, string Owner, string ProvenUse, string Confidence);

public static class WorldgroupMkdeTransactionRecoveryRegistry
{
    public const string EngineModule = "W32MKDE.EXE";
    public const string EngineSha256 = "50453af66f993518cc45172dd0828257b220985a1aaad904f08b9bd11f0bbaa1";
    public const string RecoveryEntry = "0x0042C340";

    public static readonly IReadOnlyList<WorldgroupMkdeRecoveryArtifact> Artifacts =
    [
        new("BTRVLOG.RCV", "W32MKDE recovery subsystem", "Installation-relative recovery artifact is constructed and probed before the transaction log during startup recovery.", "PROVEN"),
        new("BTRVLOG.TRN", "W32MKDE transaction-log subsystem", "Installation-relative transaction log is constructed, opened and validated by a distinct branch after the recovery-artifact probe.", "PROVEN"),
        new("BTR_TMP.$??", "W32MKDE background subsystem", "Temporary-file wildcard belongs to the background/cache maintenance component; exact rename role remains bounded.", "PROVEN_OWNERSHIP"),
        new("%8X.LOG", "W32MKDE transaction-log subsystem", "Per-identifier hexadecimal log naming template is owned by TRANSLOG.C; exact rotation semantics remain unresolved.", "PROVEN_OWNERSHIP")
    ];

    public const string StartupOrdering =
        "Startup recovery constructs and probes BTRVLOG.RCV before BTRVLOG.TRN, then enters separate validation/replay branches; explicit serialized flushes protect selected recovery/log finalization paths.";

    public const string DurabilityBoundary =
        "Writes not crossing a proven explicit or conditional flush boundary remain write-back cache state; static evidence does not prove power-loss durability before a later flush, close, metadata transition, or recovery/log finalization.";
}
