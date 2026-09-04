namespace RealmGate.Server;

/// <summary>
/// Read-only summary of the projected world room index used by Server bootstrap diagnostics.
/// </summary>
public sealed class ServerWorldProjectionAuditReport
{
    public string? ProjectRoot { get; internal set; }

    public string? WorldPath { get; internal set; }

    public string? RoomsPath { get; internal set; }

    public string? StartRoom { get; internal set; }

    public bool StartRoomExists { get; internal set; }

    public int WorldRoomCount { get; internal set; }

    public int UniqueWorldRoomCount { get; internal set; }

    public int CanonicalRoomCount { get; internal set; }

    public IReadOnlyList<string> MissingRoomIds { get; internal set; } = Array.Empty<string>();

    public IReadOnlyList<string> DuplicateWorldRoomIds { get; internal set; } = Array.Empty<string>();

    public int MissingRoomCount => MissingRoomIds.Count;

    public int DuplicateWorldRoomCount => DuplicateWorldRoomIds.Count;

    public bool ProjectionReady =>
        WorldRoomCount > 0 &&
        UniqueWorldRoomCount == WorldRoomCount &&
        MissingRoomCount == 0 &&
        DuplicateWorldRoomCount == 0 &&
        StartRoomExists;

    public string Status => ProjectionReady ? "projection-ready" : "projection-has-gaps";
}
