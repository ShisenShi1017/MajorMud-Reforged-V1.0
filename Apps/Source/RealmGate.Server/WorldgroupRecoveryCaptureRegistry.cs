namespace RealmGate.Server;

public static class WorldgroupRecoveryCaptureRegistry
{
    public const string EvidenceUpdate = "worldgroup-recovery-capture";
    public const string CaptureManifestSchema = "majormud.worldgroup-recovery-capture.v1";
    public const string ArtifactDiffSchema = "majormud.worldgroup-recovery-artifact-diff.v1";
    public const bool AltersRuntimeBehavior = false;
    public const bool ParsesUnknownRecoveryRecords = false;
    public const bool RequiresOriginalBinaryRedistribution = false;
    public const int DefaultStabilityPasses = 2;

    public static readonly string[] CandidateArtifactPatterns =
    {
        "BTRVLOG.TRN",
        "BTRVLOG.RCV",
        "BTR_TMP.$??",
        "????????.LOG",
        "MKDEWE.TRA",
        "MKDEMSG.LOG"
    };
}
