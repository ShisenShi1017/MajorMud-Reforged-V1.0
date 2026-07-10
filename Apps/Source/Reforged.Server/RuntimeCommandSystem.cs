namespace Reforged.Server;

public sealed class RuntimeCommandDispatcher
{
    private readonly Func<IReadOnlyDictionary<string, RuntimeWorld>> _worldProvider;
    private readonly RuntimeSessionManager? _sessions;
    private readonly RuntimeStateStore? _state;
    private readonly RuntimeEventDispatcher? _events;

    public RuntimeCommandDispatcher()
        : this(() => new Dictionary<string, RuntimeWorld>(StringComparer.OrdinalIgnoreCase), null, null, null)
    {
    }

    public RuntimeCommandDispatcher(
        Func<IReadOnlyDictionary<string, RuntimeWorld>> worldProvider,
        RuntimeSessionManager? sessions,
        RuntimeStateStore? state,
        RuntimeEventDispatcher? events)
    {
        _worldProvider = worldProvider;
        _sessions = sessions;
        _state = state;
        _events = events;
    }

    public string Dispatch(RuntimeSession session, string input)
    {
        var command = RuntimeCommand.Parse(input);
        session.Touch();
        if (command.Name.Length == 0)
        {
            return string.Empty;
        }

        if (command.IsMovement)
        {
            var previousRoom = session.CurrentRoom;
            var moved = session.TryMove(command.Name, out var movementResult);
            if (moved)
            {
                _state?.UpdateSessionLocation(session);
                _events?.Publish("movement", "completed", session.World.Definition.Id, session.Id, session.Actor.Id, $"{previousRoom.Id}->{session.CurrentRoom.Id}");
            }
            else
            {
                _events?.Publish("movement", "blocked", session.World.Definition.Id, session.Id, session.Actor.Id, movementResult);
            }

            return movementResult;
        }

        _events?.Publish("command", "received", session.World.Definition.Id, session.Id, session.Actor.Id, command.Name);
        return command.Name switch
        {
            "look" or "l" => RoomRenderer.Render(session.CurrentRoom),
            "where" => $"World={session.World.Definition.Id}; Room={session.CurrentRoom.Id}; Map={session.CurrentRoom.MapId}; Number={session.CurrentRoom.RoomNumber}",
            "world" => RenderWorld(session.World),
            "worlds" => RenderWorlds(),
            "sessions" => _sessions?.RenderStatus() ?? "Session manager is not attached.",
            "state" => _state?.RenderStatus() ?? "Runtime state store is not attached.",
            "events" => _events?.RenderStatus() ?? "Runtime event dispatcher is not attached.",
            "help" => "Commands: look, where, world, worlds, sessions, state, events, north/south/east/west or n/s/e/w, quit",
            _ => $"Unknown command '{command.Name}'. Type 'help' for available runtime commands."
        };
    }

    private string RenderWorlds()
    {
        var worlds = _worldProvider();
        if (worlds.Count == 0)
        {
            return "No worlds are loaded.";
        }

        return string.Join(Environment.NewLine, worlds.Values
            .OrderBy(world => world.Definition.Id)
            .Select(world => $"{world.Definition.Id}: {world.Definition.Name} [{world.Definition.Type}] rooms={world.RoomCount}; rules={world.Definition.RulesProfile}; ai={world.Definition.AiMode}; protected={world.Definition.ClassicProtected}"));
    }

    private static string RenderWorld(RuntimeWorld world)
    {
        return $"{world.Definition.Name} ({world.Definition.Type}) rooms={world.RoomCount}; rules={world.Definition.RulesProfile}; ai={world.Definition.AiMode}; classicProtected={world.Definition.ClassicProtected}; transfersAllowed={world.Definition.TransfersAllowed}; evidence={world.EvidencePath}";
    }
}

public sealed class RuntimeCommand
{
    private static readonly HashSet<string> MovementCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "n", "s", "e", "w", "ne", "nw", "se", "sw", "u", "d",
        "north", "south", "east", "west", "northeast", "northwest", "southeast", "southwest", "up", "down"
    };

    private RuntimeCommand(string name)
    {
        Name = name;
    }

    public string Name { get; }
    public bool IsMovement => MovementCommands.Contains(Name);

    public static RuntimeCommand Parse(string input)
    {
        var trimmed = input.Trim();
        if (trimmed.Length == 0)
        {
            return new RuntimeCommand(string.Empty);
        }

        var firstToken = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0].ToLowerInvariant();
        return new RuntimeCommand(firstToken);
    }
}
