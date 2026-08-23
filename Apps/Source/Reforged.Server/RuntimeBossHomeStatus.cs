namespace Reforged.Server;

public sealed partial class RuntimeLiveGameplayFacade
{
    public string RenderBossHomeStatus(RuntimeSession session)
    {
        var living = Living(session).Capture().Npcs;
        var rows = session.World.Spawns
            .Where(spawn => spawn.BossDefinition is { } definition &&
                            (definition.GameLimit == 1 || definition.RegenTime >= 1))
            .GroupBy(spawn => spawn.BossDefinition!.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var definition = group.First().BossDefinition!;
                var spawnIds = group.Select(spawn => spawn.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var instances = living.Where(npc => spawnIds.Contains(npc.SpawnId)).ToArray();
                var alive = instances.Where(npc => npc.Alive).ToArray();
                var currentRooms = alive.Select(npc => FormatRoom(session.World, npc.RoomId))
                    .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();
                var homeRooms = group.Select(spawn => FormatRoom(session.World, spawn.ResolvedRuntimeRoomId))
                    .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();
                var respawnAt = instances.Where(npc => !npc.Alive && npc.RespawnAtUtc is not null)
                    .Select(npc => npc.RespawnAtUtc!.Value).OrderBy(value => value).FirstOrDefault();
                var state = alive.Length > 0 ? "ALIVE" : "DEAD";
                var current = alive.Length > 0 ? string.Join("; ", currentRooms) : "-";
                var deadline = alive.Length > 0 || respawnAt == default ? "-" : respawnAt.ToUniversalTime().ToString("O");
                return new { definition.Name, State = state, Current = current, Home = string.Join("; ", homeRooms), Deadline = deadline };
            })
            .OrderBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var lines = new List<string> { "Boss Home Status", "Boss | State | Current room | Home room | Respawn UTC" };
        lines.AddRange(rows.Select(row => $"{row.Name} | {row.State} | {row.Current} | {row.Home} | {row.Deadline}"));
        lines.Add($"Total bosses: {rows.Length}");
        return ReforgedClientProtocol.Encode("BOSSHOME", string.Join(Environment.NewLine, lines));
    }

    private static string FormatRoom(RuntimeWorld world, string roomId)
    {
        var room = world.FindRoomById(roomId);
        return room is null ? roomId : $"Map {room.MapId} Room {room.RoomNumber} - {room.Name}";
    }
}
