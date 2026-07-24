namespace Reforged.Server;

public sealed record WorldgroupMkdeFileOperation(string Entry, string Operation, string Fallback, string ResultContract, string Confidence);

public static class WorldgroupMkdeLogArtifactLifecycleRegistry
{
    public const string EngineModule = "W32MKDE.EXE";
    public const string EngineSha256 = "50453af66f993518cc45172dd0828257b220985a1aaad904f08b9bd11f0bbaa1";

    public static readonly IReadOnlyList<WorldgroupMkdeFileOperation> FileOperations =
    [
        new("0x00404820", "DeleteFileA", "Retry with GetFullPathNameA-expanded path", "Returns Btrieve-style status; zero on success and mapped nonzero status on failure", "PROVEN"),
        new("0x004049A0", "MoveFileA", "Retry after expanding both source and destination through GetFullPathNameA", "Returns Btrieve-style status; zero on success and mapped nonzero status on failure", "PROVEN")
    ];

    public const string ArtifactOrderingBoundary =
        "The engine contains dedicated delete and move wrappers suitable for transaction/recovery artifact cleanup and promotion, but supplied static evidence does not bind a specific BTRVLOG.TRN or BTRVLOG.RCV callsite to either wrapper.";

    public const string FormatBoundary =
        "No BTRVLOG.TRN, BTRVLOG.RCV, per-transaction hexadecimal LOG, or BTR_TMP sample artifact is present in the supplied installation. Exact record layouts, undo/redo image payloads, checksums, and replay opcodes therefore remain unprovable from this baseline.";
}
