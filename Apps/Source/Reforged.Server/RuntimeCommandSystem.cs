namespace Reforged.Server;

public sealed class RuntimeCommandDispatcher
{
    private readonly Func<IReadOnlyDictionary<string, RuntimeWorld>> _worldProvider;
    private readonly RuntimeSessionManager? _sessions;
    private readonly RuntimeStateStore? _state;
    private readonly RuntimeEventDispatcher? _events;
    private readonly RuntimeLiveGameplayFacade? _gameplay;

    public RuntimeCommandDispatcher()
        : this(() => new Dictionary<string, RuntimeWorld>(StringComparer.OrdinalIgnoreCase), null, null, null, null)
    {
    }

    public RuntimeCommandDispatcher(
        Func<IReadOnlyDictionary<string, RuntimeWorld>> worldProvider,
        RuntimeSessionManager? sessions,
        RuntimeStateStore? state,
        RuntimeEventDispatcher? events,
        RuntimeLiveGameplayFacade? gameplay = null)
    {
        _worldProvider = worldProvider;
        _sessions = sessions;
        _state = state;
        _events = events;
        _gameplay = gameplay;
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
            if (_gameplay is not null && !_gameplay.CanTraverseLivingWorld(session, command.Name, out var livingFailure)) return livingFailure;
            var moved = session.TryMove(command.Name, out var movementResult);
            if (moved)
            {
                _gameplay?.BroadcastDeparture(session, previousRoom.Id, command.Name);
                _gameplay?.BroadcastArrival(session);
                movementResult = _gameplay?.RenderRoom(session) ?? movementResult;
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
            "look" => _gameplay?.RenderRoom(session) ?? RoomRenderer.Render(session.World, session.CurrentRoom),
            "examine" => _gameplay?.Examine(session, command.ArgumentText) ?? "Live gameplay facade is not attached.",
            "say" => _gameplay?.Say(session, command.ArgumentText) ?? "Live gameplay facade is not attached.",
            "tell" => _gameplay?.Tell(session, command.Arguments) ?? "Live gameplay facade is not attached.",
            "messages" => _gameplay?.DrainMessages(session) ?? string.Empty,
            "score" => _gameplay?.RenderScore(session) ?? "Live gameplay facade is not attached.",
            "inventory" => _gameplay?.RenderInventory(session) ?? "Live gameplay facade is not attached.",
            "equipment" => _gameplay?.RenderEquipment(session) ?? "Live gameplay facade is not attached.",
            "save" => _gameplay?.Save(session) ?? "Live gameplay facade is not attached.",
            "ready" => _gameplay?.RenderIntegrationStatus(session) ?? "Live gameplay facade is not attached.",
            "get" or "drop" or "put" or "give" or "wear" or "wield" or "remove" => ExecuteItem(session, command),
            "open" or "close" or "lock" or "unlock" or "pull" or "push" or "activate" => _gameplay?.ExecuteLivingWorldCommand(session, command) ?? "Live gameplay facade is not attached.",
            "worldstate" => _gameplay?.RenderLivingWorldState(session) ?? "Live gameplay facade is not attached.",
            "services" or "list" => _gameplay?.ExecuteLiveServicesBundleCommand(session, command) ?? "Live gameplay facade is not attached.",
            "trade" => _gameplay?.ExecuteLiveServicesBundleCommand(session, command) ?? "Live gameplay facade is not attached.",
            "attack" or "kill" or "flee" or "combat" or "round" or "assist" or "cast" or "prepare" or "use" or "dispel" or "abilities" or "skills" or "talk" or "buy" or "sell" or "repair" or "train" or "tat" or "stats" or "bank" or "deposit" or "withdraw" or "quest" or "accept" or "abandon" or "complete" or "journal" or "respawn" or "recover" => _gameplay?.ExecutePolicyCommand(session, command) ?? "Live gameplay facade is not attached.",
            "who" => _gameplay?.RenderWho() ?? (_sessions?.RenderStatus() ?? "Session manager is not attached."),
            "where" => $"World={session.World.Definition.Id}; Room={session.CurrentRoom.Id}; Map={session.CurrentRoom.MapId}; Number={session.CurrentRoom.RoomNumber}",
            "world" => RenderWorld(session.World),
            "worlds" => RenderWorlds(),
            "sessions" => _sessions?.RenderStatus() ?? "Session manager is not attached.",
            "state" => _state?.RenderStatus() ?? "Runtime state store is not attached.",
            "events" => _events?.RenderStatus() ?? "Runtime event dispatcher is not attached.",
            "help" => "Commands: look, examine, say, tell, messages, score, inventory, equipment, get, drop, put, give, wear, wield, remove, attack, combat, round, flee, cast, use, abilities, skills, talk, services, list, buy, sell, repair, train, bank, deposit, withdraw, trade, quest, accept, abandon, complete, journal, respawn, recover, ready, save, who, movement, quit",
            _ => $"Unknown command '{command.Name}'. Type 'help' for available runtime commands."
        };
    }

    private string ExecuteItem(RuntimeSession session, RuntimeCommand command)
    {
        try { return _gameplay?.ExecuteItemCommand(session, command) ?? "Live gameplay facade is not attached."; }
        catch (Exception ex) { return ex.Message; }
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

    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["l"] = "look", ["x"] = "examine", ["sc"] = "score", ["i"] = "inventory", ["inv"] = "inventory",
        ["eq"] = "equipment", ["wearing"] = "equipment", ["who"] = "who", ["k"] = "kill", ["att"] = "attack", ["qlog"] = "journal",
        ["logout"] = "quit", ["exit"] = "quit"
    };

    public static readonly IReadOnlySet<string> SupportedCommands = new HashSet<string>(new[]
    {
        "look", "examine", "score", "inventory", "equipment", "get", "drop", "put", "give", "wear", "wield", "remove",
        "attack", "kill", "flee", "combat", "round", "assist", "cast", "prepare", "use", "dispel", "abilities", "skills", "talk", "buy", "sell",
        "repair", "train", "tat", "stats", "open", "close", "lock", "unlock", "pull", "push", "activate", "worldstate", "services", "list", "bank", "deposit", "withdraw", "trade", "quest", "accept", "abandon", "complete", "journal",
        "say", "tell", "messages", "respawn", "recover", "ready", "save", "who", "where", "world", "worlds", "sessions", "state", "events", "help", "quit"
    }, StringComparer.OrdinalIgnoreCase);

    private RuntimeCommand(string name, IReadOnlyList<string> arguments)
    {
        Name = name;
        Arguments = arguments;
    }

    public string Name { get; }
    public IReadOnlyList<string> Arguments { get; }
    public string ArgumentText => string.Join(" ", Arguments);
    public bool IsMovement => MovementCommands.Contains(Name);

    public static RuntimeCommand Parse(string input)
    {
        var tokens = (input ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0) return new RuntimeCommand(string.Empty, Array.Empty<string>());
        var name = tokens[0].ToLowerInvariant();
        if (Aliases.TryGetValue(name, out var canonical)) name = canonical;
        return new RuntimeCommand(name, tokens.Skip(1).ToArray());
    }
}
