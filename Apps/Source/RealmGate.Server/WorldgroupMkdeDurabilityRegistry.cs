namespace RealmGate.Server;

public sealed record WorldgroupMkdeFlushPath(
    string EntryPoint,
    string Trigger,
    string Ordering,
    string Confidence);

public static class WorldgroupMkdeDurabilityRegistry
{
    public const string EngineModule = "W32MKDE.EXE";
    public const string EngineSha256 = "50453af66f993518cc45172dd0828257b220985a1aaad904f08b9bd11f0bbaa1";
    public const string FlushImportIat = "0x0044A39C";
    public const string TransactionLog = "BTRVLOG.TRN";
    public const string RecoveryLog = "BTRVLOG.RCV";

    public static readonly IReadOnlyList<WorldgroupMkdeFlushPath> FlushPaths =
    [
        new("0x004043D0", "Explicit flush wrapper", "Acquire engine critical section; FlushFileBuffers(handle); release critical section.", "PROVEN"),
        new("0x00404D00", "Dirty/flush-required file state", "If file flag bit 0x04 is set: acquire critical section; clear pending-write counter at +0x08; FlushFileBuffers(handle); release critical section.", "PROVEN"),
    ];

    public const string RecoveryEntry = "0x0042C340";
    public const string RecoveryFinding =
        "Builds installation-relative BTRVLOG.RCV and BTRVLOG.TRN paths, probes the recovery artifact first, and enters separate open/validation paths depending on which artifact is present.";
}
