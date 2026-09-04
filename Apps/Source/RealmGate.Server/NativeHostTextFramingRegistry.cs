namespace RealmGate.Server;

public sealed record NativeHostTextFramingEvidence(
    string Symbol, IReadOnlyList<string> LineTerminationForms, int RecordsEndingCr,
    int RecordsEndingLf, int RecordsEndingCrLf, int RecordsWithTabs);

public static class NativeHostTextFramingRegistry
{
    public const string EvidenceId = "FinalNativeClosure0019";
    public const string Scope = "Proven caller-owned text framing only; host translation and output ordering remain fail-closed.";
    public static readonly IReadOnlyList<NativeHostTextFramingEvidence> Proven = new NativeHostTextFramingEvidence[]
    {
        new("_c2bcpy", new string[] { "none" }, 0, 0, 0, 0),
        new("_catastro", new string[] { "none" }, 0, 0, 0, 0),
        new("_findtvar", new string[] { "none" }, 0, 0, 0, 0),
        new("_fsdxan", new string[] { "none" }, 0, 0, 0, 0),
        new("_gmdnam", new string[] { "none" }, 0, 0, 0, 0),
        new("_haskey", new string[] { "none" }, 0, 0, 0, 0),
        new("_msgscan", new string[] { "none" }, 0, 0, 0, 0),
        new("_opnmsg", new string[] { "none" }, 0, 0, 0, 0),
        new("_prat", new string[] { "none" }, 0, 0, 0, 0),
        new("_prf", new string[] { "cr", "crlf", "lf", "none" }, 46, 28, 24, 0),
        new("_prfmsg", new string[] { "none" }, 0, 0, 0, 0),
        new("_sameas", new string[] { "none" }, 0, 0, 0, 0),
        new("_samepatu", new string[] { "none" }, 0, 0, 0, 0),
        new("_sameto", new string[] { "none" }, 0, 0, 0, 0),
        new("_senddpk", new string[] { "none" }, 0, 0, 0, 0),
        new("_shocst", new string[] { "none" }, 0, 0, 0, 0),
        new("_spr", new string[] { "cr", "none" }, 2, 0, 0, 0),
        new("_stzcpy", new string[] { "none" }, 0, 0, 0, 0),
        new("_tfsopn", new string[] { "none" }, 0, 0, 0, 0),
        new("_tokopt", new string[] { "none" }, 0, 0, 0, 0),
        new("_vdaoff", new string[] { "none" }, 0, 0, 0, 0),
    };
}
