namespace Reforged.Server;

public sealed record WorldgroupMkdeFlushCaller(
    string Callsite,
    string Wrapper,
    string CallerClass,
    string Ordering,
    string ResultHandling,
    string Confidence);

public static class WorldgroupMkdeFlushCallerRegistry
{
    public const string EngineModule = "W32MKDE.EXE";
    public const string EngineSha256 = "50453af66f993518cc45172dd0828257b220985a1aaad904f08b9bd11f0bbaa1";

    public static readonly IReadOnlyList<WorldgroupMkdeFlushCaller> Callers =
    [
        new("0x00427BBF", "0x004043D0", "Recovery/log serialization finalization", "Runs after a bounded sequence of record writes and before optional follow-on close/cleanup work.", "The 16-bit result is tested and converts the enclosing operation to failure when nonzero.", "PROVEN"),
        new("0x00428336", "0x004043D0", "Recovery/log serialization finalization", "Runs after the final staged write sequence and before temporary-state cleanup and diagnostic handling.", "The direct return is not used by the immediately following instructions.", "PROVEN"),

        new("0x0042BB3C", "0x00404D00", "Cache/page lifecycle", "Runs after cache maintenance and before the enclosing file-state operation returns.", "Wrapper performs its own dirty-bit gate.", "PROVEN"),
        new("0x0042CA2A", "0x00404D00", "Cache/page transfer", "Flushes a local file object before the next page-transfer operation when its pending-write word is nonzero.", "Call is additionally guarded by the caller's 16-bit pending-state test.", "PROVEN"),
        new("0x0042CD0B", "0x00404D00", "Cache/page transfer", "Flushes before the next indexed transfer stage when the local pending-write word is nonzero.", "Call is additionally guarded by the caller's 16-bit pending-state test.", "PROVEN"),
        new("0x0042CE3F", "0x00404D00", "Cache/page transfer finalization", "Flushes after transfer cleanup and before the file object is released.", "Call is guarded by the caller's nonzero pending-state test.", "PROVEN"),
        new("0x0042D01D", "0x00404D00", "Cache/page transfer", "Conditional flush within a page-copy/update helper.", "Wrapper and caller-side state gates bound the operation.", "PROVEN_STRUCTURE"),
        new("0x0042D1FF", "0x00404D00", "Cache/page transfer", "Conditional flush within a page-copy/update helper.", "Wrapper and caller-side state gates bound the operation.", "PROVEN_STRUCTURE"),
        new("0x0042D3CA", "0x00404D00", "Cache/page transfer finalization", "Runs after the final page loop and temporary-buffer release, immediately before return.", "Caller invokes only when the file object's pending-write word at +0x08 is nonzero.", "PROVEN"),

        new("0x00435BFE", "0x00404D00", "Grouped file teardown", "Flushes file object at owner offset +0x1CC before linked cache records are detached.", "Caller guards on the corresponding 16-bit pending counter at +0x1D4.", "PROVEN"),
        new("0x00435C1C", "0x00404D00", "Grouped file teardown", "Flushes file object at owner offset +0x1C0 before linked cache records are detached.", "Caller guards on the corresponding 16-bit pending counter at +0x1C8.", "PROVEN"),
        new("0x00435C3A", "0x00404D00", "Grouped file teardown", "Flushes file object at owner offset +0x1B4 before linked cache records are detached.", "Caller guards on the corresponding 16-bit pending counter at +0x1BC.", "PROVEN"),

        new("0x0043B1E5", "0x00404D00", "Metadata/index mutation", "Flushes after a successful metadata/index write and before the next allocation/update stage.", "Execution reaches the call only after prior operations return success.", "PROVEN"),
        new("0x0043B28F", "0x00404D00", "Metadata/index mutation", "Flushes after insertion into an existing metadata extent.", "Execution reaches the call only after the insertion helper succeeds.", "PROVEN"),
        new("0x0043B345", "0x00404D00", "Metadata/index mutation", "Flushes after creation/population of a new metadata extent and before temporary structures are released.", "Execution reaches the call only after the write helper succeeds.", "PROVEN")
    ];

    public const string DurabilityBoundary =
        "W32MKDE permits ordinary writes to remain pending, but forces synchronous serialized flushes at selected recovery/log finalization, page/cache transition, grouped-close, and metadata/index mutation boundaries.";
}
