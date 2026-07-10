namespace Reforged.Server;

public sealed class RuntimeSession
{
    public RuntimeSession(
        string id,
        RuntimeWorld world,
        RuntimeRoom initialRoom,
        IRuntimeSessionActor actor,
        string spawnSource)
    {
        Id = id;
        World = world;
        CurrentRoom = initialRoom;
        Actor = actor;
        SpawnSource = spawnSource;
        ConnectedAtUtc = DateTimeOffset.UtcNow;
        LastCommandAtUtc = ConnectedAtUtc;
    }

    public string Id { get; }
    public IRuntimeSessionActor Actor { get; }
    public RuntimeWorld World { get; private set; }
    public RuntimeRoom CurrentRoom { get; private set; }
    public string SpawnSource { get; private set; }
    public DateTimeOffset ConnectedAtUtc { get; }
    public DateTimeOffset LastCommandAtUtc { get; private set; }

    public void Touch() => LastCommandAtUtc = DateTimeOffset.UtcNow;

    public void MoveToWorld(RuntimeWorld world, RuntimeRoom room, string source)
    {
        World = world;
        CurrentRoom = room;
        SpawnSource = source;
        Touch();
    }

    public bool TryMove(string direction, out string message)
    {
        Touch();
        var normalized = NormalizeDirection(direction);
        var exit = CurrentRoom.Exits.FirstOrDefault(candidate => string.Equals(candidate.Direction, normalized, StringComparison.OrdinalIgnoreCase));
        if (exit is null)
        {
            message = "You cannot go that way.";
            return false;
        }

        var target = World.FindRoomByNumber(exit.TargetRoomNumber);
        if (target is null)
        {
            message = $"Exit {normalized} points to unresolved room number {exit.TargetRoomNumber}. Movement blocked until evidence is reconciled.";
            return false;
        }

        CurrentRoom = target;
        message = RoomRenderer.Render(CurrentRoom);
        return true;
    }

    private static string NormalizeDirection(string direction) => direction.Trim().ToUpperInvariant() switch
    {
        "NORTH" => "N", "SOUTH" => "S", "EAST" => "E", "WEST" => "W",
        "NORTHEAST" => "NE", "NORTHWEST" => "NW", "SOUTHEAST" => "SE", "SOUTHWEST" => "SW",
        "UP" => "U", "DOWN" => "D", _ => direction.Trim().ToUpperInvariant()
    };
}

public static class RoomRenderer
{
    public static string Render(RuntimeRoom room)
    {
        var exits = room.Exits.Count == 0 ? "none" : string.Join(", ", room.Exits.Select(exit => exit.Direction).Where(value => !string.IsNullOrWhiteSpace(value)).OrderBy(value => value));
        return $"{room.Name}\n{room.Description}\n\nExits: {exits}";
    }
}
