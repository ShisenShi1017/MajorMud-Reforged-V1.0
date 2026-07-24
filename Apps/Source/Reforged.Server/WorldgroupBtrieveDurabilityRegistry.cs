namespace Reforged.Server;

public enum WorldgroupBtrieveLayer
{
    ApiLink,
    LocalEngine,
    Transport,
    Recovery
}

public sealed record WorldgroupBtrieveArtifact(
    string FileName,
    string Sha256,
    WorldgroupBtrieveLayer Layer,
    string ProvenRole);

public static class WorldgroupBtrieveDurabilityRegistry
{
    public const string ApiModule = "WBTRV32.DLL";
    public const string ApiVersion = "Btrieve API Set Interface DLL v6.20";
    public const string EngineModule = "W32MKDE.EXE";
    public const string EngineIdentity = "BTI-MKDE";
    public const string LocalPipe = @"\\.\PIPE\BMKDE\FUNCTION.PIP";
    public const string TransactionLog = "BTRVLOG.TRN";
    public const string RecoveryLog = "BTRVLOG.RCV";
    public const string EngineTrace = "MKDEWE.TRA";
    public const string EngineMessageLog = "MKDEMSG.LOG";
    public const string TemporaryPattern = "BTR_TMP.$??";

    public static readonly IReadOnlyList<WorldgroupBtrieveArtifact> Artifacts =
    [
        new("WBTRV32.DLL", "3f9cbe699b685f48db550bad43b875bd11ed5ec2b091a0a2d76eb79c3b1e478d", WorldgroupBtrieveLayer.ApiLink,
            "Exports BTRCALL and marshals operations to a local or pipe-backed Btrieve engine session; it is not the physical page engine."),
        new("W32MKDE.EXE", "50453af66f993518cc45172dd0828257b220985a1aaad904f08b9bd11f0bbaa1", WorldgroupBtrieveLayer.LocalEngine,
            "Local MKDE engine importing ReadFile, WriteFile, SetFilePointer, FlushFileBuffers, LockFile, UnlockFile, MoveFileA, DeleteFileA, and SetEndOfFile."),
    ];

    public static readonly IReadOnlyList<string> RecoveryArtifacts =
    [
        TransactionLog,
        RecoveryLog,
        TemporaryPattern,
        EngineTrace,
        EngineMessageLog
    ];
}
