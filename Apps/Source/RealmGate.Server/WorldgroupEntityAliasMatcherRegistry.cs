namespace RealmGate.Server;

/// <summary>
/// Evidence-backed contract for the MajorMUD entity/alias matcher at 0x0046ABB4.
/// The routine scans the current player's bounded candidate-id list and resolves
/// an entity by primary/alternate text, with optional fallback matching.
/// </summary>
public static class WorldgroupEntityAliasMatcherRegistry
{
    public const uint FunctionAddress = 0x0046ABB4;
    public const uint EndAddress = 0x0046AE00;
    public const int ArgumentCount = 5;
    public const int MaximumCandidateIds = 100;
    public const int MaximumPasses = 50;

    public static readonly string[] ProvenContract =
    {
        "resolve the caller session/player record",
        "scan the player's bounded 16-bit candidate-id array in ascending order",
        "resolve each candidate id to an entity record",
        "compare the mutable input against primary and alternate entity text",
        "optionally permit the fallback matcher",
        "track ambiguity and matched-word count through caller-owned outputs",
        "return the resolved entity record or null"
    };

    public static bool IsCommandTableLookup => false;
    public static bool IsRoomBroadcast => false;
}
