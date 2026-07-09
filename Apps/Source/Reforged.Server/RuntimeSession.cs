namespace Reforged.Server;

public sealed class RuntimeSession
{
    public RuntimeSession(string id, RuntimeWorld world, string? playerName = null)
        : this(id, world,
            new RuntimeAccountIdentity($"account:{RuntimeIdentity.NormalizeToken(playerName ?? id, "player")}", string.IsNullOrWhiteSpace(playerName) ? id : playerName.Trim(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
            new RuntimeCharacterIdentity(RuntimeIdentity.CreateCharacterId($"account:{RuntimeIdentity.NormalizeToken(playerName ?? id, "player")}", world.Definition.Id, 1), $"account:{RuntimeIdentity.NormalizeToken(playerName ?? id, "player")}", world.Definition.Id, 1, string.IsNullOrWhiteSpace(playerName) ? id : playerName.Trim(), world.GetStartRoom().Id, world.GetStartRoom().RoomNumber, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow))
    {
    }

    public RuntimeSession(string id, RuntimeWorld world, RuntimeAccountIdentity account, RuntimeCharacterIdentity character)
    {
        Id = id;
        World = world;
        Account = account;
        Character = character;
        PlayerName = string.IsNullOrWhiteSpace(character.DisplayName) ? account.DisplayName : character.DisplayName;
        CharacterId = character.CharacterId;
        CurrentRoom = world.FindRoomByNumber(character.CurrentRoomNumber) ?? world.GetStartRoom();
        ConnectedAtUtc = DateTimeOffset.UtcNow;
        LastCommandAtUtc = ConnectedAtUtc;
    }

    public string Id { get; }
    public string PlayerName { get; }
    public RuntimeAccountIdentity Account { get; private set; }
    public RuntimeCharacterIdentity Character { get; private set; }
    public string CharacterId { get; private set; }
    public RuntimeWorld World { get; private set; }
    public RuntimeRoom CurrentRoom { get; private set; }
    public DateTimeOffset ConnectedAtUtc { get; }
    public DateTimeOffset LastCommandAtUtc { get; private set; }

    public void Touch()
    {
        LastCommandAtUtc = DateTimeOffset.UtcNow;
    }

    public void MoveToWorld(RuntimeWorld world)
    {
        World = world;
        Character = Character with
        {
            WorldId = world.Definition.Id,
            CurrentRoomId = world.GetStartRoom().Id,
            CurrentRoomNumber = world.GetStartRoom().RoomNumber,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        CharacterId = Character.CharacterId;
        CurrentRoom = world.GetStartRoom();
        Touch();
    }

    public void UpdateCharacter(RuntimeCharacterIdentity character)
    {
        Character = character;
        CharacterId = character.CharacterId;
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
        Character = Character with
        {
            CurrentRoomId = CurrentRoom.Id,
            CurrentRoomNumber = CurrentRoom.RoomNumber,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        message = RoomRenderer.Render(CurrentRoom);
        return true;
    }

    private static string NormalizeDirection(string direction)
    {
        return direction.Trim().ToUpperInvariant() switch
        {
            "NORTH" => "N",
            "SOUTH" => "S",
            "EAST" => "E",
            "WEST" => "W",
            "NORTHEAST" => "NE",
            "NORTHWEST" => "NW",
            "SOUTHEAST" => "SE",
            "SOUTHWEST" => "SW",
            "UP" => "U",
            "DOWN" => "D",
            _ => direction.Trim().ToUpperInvariant()
        };
    }

}

public static class RoomRenderer
{
    public static string Render(RuntimeRoom room)
    {
        var exits = room.Exits.Count == 0
            ? "none"
            : string.Join(", ", room.Exits.Select(exit => exit.Direction).Where(value => !string.IsNullOrWhiteSpace(value)).OrderBy(value => value));

        return $"{room.Name}\n{room.Description}\n\nExits: {exits}";
    }
}
