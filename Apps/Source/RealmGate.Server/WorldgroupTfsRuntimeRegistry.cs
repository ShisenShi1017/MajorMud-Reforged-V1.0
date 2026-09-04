namespace RealmGate.Server;

public sealed record WorldgroupTfsStateEvidence(int State, string Name, string Semantics);
public sealed record WorldgroupTfsLayoutEvidence(string Field, uint Address, int Offset, int Size, string Confidence, string Semantics);

public static class WorldgroupTfsRuntimeRegistry
{
    public const string Module = "WGSERVER.EXE";
    public const string ModuleSha256 = "4da28d718cf99701aa63c1889770f293bb3ec828aea5e8bea500473d2698c73f";
    public const uint TfsReadLineRva = 0x0003E8D6u;
    public const uint TfsDuplicateRva = 0x0003EA2Au;

    public static readonly IReadOnlyList<WorldgroupTfsStateEvidence> States = new[]
    {
        new WorldgroupTfsStateEvidence(0, "Closed/Exhausted", "No active file or remaining directory match."),
        new WorldgroupTfsStateEvidence(1, "First match staged", "_tfsopn found at least one match; first _tfsrdl call advances to state 2 without returning a line."),
        new WorldgroupTfsStateEvidence(2, "Open pending", "_tfsrdl combines the enumerated name with the derived directory, opens the selected file in text-read mode, and moves to state 3."),
        new WorldgroupTfsStateEvidence(3, "Reading", "_tfsrdl reads at most 0x80 data bytes plus terminator into _tfsbuf, removes a trailing LF, normalizes the line, and retains state 3 until EOF."),
        new WorldgroupTfsStateEvidence(4, "Advance enumeration", "The prior stream reached EOF and was closed; _tfsrdl calls the find-next helper, returning to state 2 on success or state 0 when exhausted."),
    };

    public static readonly IReadOnlyList<WorldgroupTfsLayoutEvidence> Layout = new[]
    {
        new WorldgroupTfsLayoutEvidence("_tfstate", 0x00463DDCu, 0, 4, "proven", "32-bit state discriminator consumed by _tfsopn, _tfsrdl, and _tfsabt."),
        new WorldgroupTfsLayoutEvidence("_tfsbuf", 0x00463DE0u, 0, 0x84, "proven", "Line buffer; _fgets is called with count 0x81 and the buffer is cleared before each state-machine step."),
        new WorldgroupTfsLayoutEvidence("_tfspst", 0x00463E64u, 0, 4, "proven", "Pointer into _tfsbuf set by _tfspfx and consumed by _tfsdpr."),
        new WorldgroupTfsLayoutEvidence("_tfsfb metadata", 0x00463E68u, 0, 0x10, "bounded", "Opaque find metadata owned by the Borland find-first/find-next helpers."),
        new WorldgroupTfsLayoutEvidence("_tfsfb filename", 0x00463E78u, 0x10, 0x104, "proven", "NUL-terminated enumerated filename copied into the derived path before fopen; reservation extends to _tfsfp."),
        new WorldgroupTfsLayoutEvidence("_tfsfp", 0x00463F78u, 0, 4, "proven", "FILE* opened with mode 'rt' and closed on EOF or abort."),
        new WorldgroupTfsLayoutEvidence("_tfspth", 0x00463F7Cu, 0, 0x108, "proven", "Normalized path/pattern buffer passed to fopen and initialized by _tfsopn."),
        new WorldgroupTfsLayoutEvidence("derived path pointer", 0x00464084u, 0, 4, "proven", "Pointer used as the destination for the enumerated filename before opening each match."),
    };

    public static IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (States.Select(x => x.State).Distinct().Count() != 5) errors.Add("tfs-state-count");
        if (Layout.Count != 8) errors.Add("tfs-layout-count");
        if (Layout.Any(x => x.Address == 0 || x.Size <= 0)) errors.Add("invalid-tfs-layout");
        return errors;
    }
}
