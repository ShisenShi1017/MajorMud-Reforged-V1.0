namespace Reforged.Server;

public static class NativeWccKnowsGateRegistry
{
    public const string EvidenceId = "native_wcc_knows_gate_0032";
    public const string WccSentinel = "wccknows.sys";
    public const string WbbSentinel = "wbbknows.sys";

    // Native helper semantics: access(path, 0) == 0 means present.
    public static bool SentinelExists(int accessResult) => accessResult == 0;

    // The only recovered caller inverts the helper at this branch.
    public static bool ValidationBranchResult(bool sentinelExists) => !sentinelExists;

    public static bool ReadsSentinelContents => false;
    public static int ProvenStaticCallsitesPerModule => 1;
}
