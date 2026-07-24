namespace Reforged.Server;

public static class WorldgroupRoomLatchClosureRegistry
{
    public const int DistinctNativePassiveEffects = 7;
    public const int AffectedExitRows = 12;
    public const bool PromotePlayerCommands = false;
    public const string DefaultUnverifiedState = "closed_hidden";

    public static readonly string[] SharedNativeStateIds =
    {
        "edge-state.map12.2051.u",
        "edge-state.map17.2946.s",
        "edge-state.map17.2949.e",
        "edge-state.map17.2956.n",
        "edge-state.map17.2958.n",
        "edge-state.map17.2970.s",
        "edge-state.map17.3042.n",
    };

    public static bool IsNativePassiveLatch(string? stateId)
        => stateId is not null && Array.IndexOf(SharedNativeStateIds, stateId) >= 0;
}
