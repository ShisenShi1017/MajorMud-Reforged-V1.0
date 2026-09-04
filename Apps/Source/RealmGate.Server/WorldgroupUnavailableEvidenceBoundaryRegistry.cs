namespace RealmGate.Server;

public static class WorldgroupUnavailableEvidenceBoundaryRegistry
{
    public const string EvidenceUpdate = "worldgroup-unavailable-evidence-boundary";
    public const string BoundaryId = "worldgroup.btrieve.recovery-format.live-capture-unavailable";
    public const string Status = "ConclusiveExternalEvidenceBoundary";
    public const bool RequiresLiveWorldgroup = true;
    public const bool LiveWorldgroupAvailable = false;
    public const bool BlocksRuntimeImplementation = false;
    public const bool BlocksCompatibility = false;
    public const bool BlocksSemanticExhaustion = false;
    public const bool SpeculativeParserAllowed = false;

    public static readonly string[] UnavailableArtifacts =
    {
        "BTRVLOG.TRN",
        "BTRVLOG.RCV",
        "BTR_TMP.$??",
        "????????.LOG"
    };

    public static readonly string[] RequiredEvidenceToReopen =
    {
        "A byte-preserving controlled recovery capture from a compatible live Worldgroup installation",
        "Equivalent vendor symbols or source defining the recovery record layouts",
        "Representative transaction and recovery artifacts with reproducible before/after provenance"
    };
}
