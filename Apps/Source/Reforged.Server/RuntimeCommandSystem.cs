namespace Reforged.Server;

public sealed class RuntimeCommandDispatcher
{
    private readonly Func<IReadOnlyDictionary<string, RuntimeWorld>> _worldProvider;
    private readonly RuntimeSessionManager? _sessions;
    private readonly RuntimeStateStore? _state;
    private readonly RuntimeEventDispatcher? _events;
    private readonly RuntimeEntityRegistry? _entities;
    private readonly IRuntimeCharacterRepository? _characters;
    private readonly MagicCastPipelineFoundation? _magicPipeline;
    private readonly NpcLifecycleFoundation? _npcLifecycle;
    private readonly CombatLoopFoundation? _combatLoop;

    public RuntimeCommandDispatcher()
        : this(() => new Dictionary<string, RuntimeWorld>(StringComparer.OrdinalIgnoreCase), null, null, null)
    {
    }

    public RuntimeCommandDispatcher(
        Func<IReadOnlyDictionary<string, RuntimeWorld>> worldProvider,
        RuntimeSessionManager? sessions,
        RuntimeStateStore? state,
        RuntimeEventDispatcher? events,
        RuntimeEntityRegistry? entities = null,
        IRuntimeCharacterRepository? characters = null,
        MagicCastPipelineFoundation? magicPipeline = null,
        NpcLifecycleFoundation? npcLifecycle = null,
        CombatLoopFoundation? combatLoop = null)
    {
        _worldProvider = worldProvider;
        _sessions = sessions;
        _state = state;
        _events = events;
        _entities = entities;
        _characters = characters;
        _magicPipeline = magicPipeline;
        _npcLifecycle = npcLifecycle;
        _combatLoop = combatLoop;
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
                var updatedCharacter = _characters?.UpdateLocation(session.Character, session.CurrentRoom);
                if (updatedCharacter is not null)
                {
                    session.UpdateCharacter(updatedCharacter);
                }

                _state?.UpdateSessionLocation(session);
                _entities?.MoveEntity(session.CharacterId, session.World.Definition.Id, previousRoom.Id, session.CurrentRoom.Id);
                _events?.Publish("movement", "completed", session.World.Definition.Id, session.Id, session.CharacterId, $"{previousRoom.Id}->{session.CurrentRoom.Id}");
            }
            else
            {
                _events?.Publish("movement", "blocked", session.World.Definition.Id, session.Id, session.CharacterId, movementResult);
            }

            return movementResult;
        }

        _events?.Publish("command", "received", session.World.Definition.Id, session.Id, session.CharacterId, command.Name);
        return command.Name switch
        {
            "look" or "l" => RoomRenderer.Render(session.CurrentRoom),
            "where" => $"World={session.World.Definition.Id}; Room={session.CurrentRoom.Id}; Map={session.CurrentRoom.MapId}; Number={session.CurrentRoom.RoomNumber}",
            "world" => RenderWorld(session.World),
            "worlds" => RenderWorlds(),
            "sessions" => _sessions?.RenderStatus() ?? "Session manager is not attached.",
            "state" => _state?.RenderStatus() ?? "Runtime state store is not attached.",
            "events" => _events?.RenderStatus() ?? "Runtime event dispatcher is not attached.",
            "entities" => _entities?.RenderStatus() ?? "Runtime entity registry is not attached.",
            "occupants" => RenderOccupants(session),
            "slots" => RenderSlots(session),
            "npc" => _npcLifecycle?.RenderStatus() ?? "NPC lifecycle foundation is not attached.",
            "combat" => _combatLoop?.RenderStatus() ?? "Combat loop foundation is not attached.",
            "cast" => RenderCastFoundation(session, input),
            "help" => "Commands: look, where, world, worlds, sessions, state, events, entities, occupants, slots, npc, combat, cast, north/south/east/west or n/s/e/w, quit",
            _ => $"Unknown command '{command.Name}'. Type 'help' for available runtime commands."
        };
    }

    private string RenderOccupants(RuntimeSession session)
    {
        var occupants = _entities?.GetRoomOccupants(session.World.Definition.Id, session.CurrentRoom.Id) ?? Array.Empty<RuntimeEntity>();
        if (occupants.Count == 0)
        {
            return "No tracked entities are registered in this room.";
        }

        return string.Join(Environment.NewLine, occupants.Select(entity => $"{entity.EntityType}: {entity.DisplayName} [{entity.Lifecycle}] id={entity.EntityId}"));
    }

    private string RenderSlots(RuntimeSession session)
    {
        var slots = _characters?.ListSaveSlots(session.Account.AccountId, session.World.Definition.Id) ?? Array.Empty<RuntimeSaveSlot>();
        if (slots.Count == 0)
        {
            return "Save slot repository is not attached.";
        }

        return string.Join(Environment.NewLine, slots.Select(slot => $"Slot {slot.SlotNumber}: {slot.Status}{(slot.CharacterId is null ? string.Empty : $" character={slot.CharacterId}")}"));
    }

    private string RenderCastFoundation(RuntimeSession session, string input)
    {
        var request = _magicPipeline?.CreateRejectedRequest(session, input);
        if (request is null)
        {
            return "Magic/cast pipeline foundation is not attached.";
        }

        _events?.Publish("magic", "cast-request-rejected", session.World.Definition.Id, session.Id, session.CharacterId, $"request={request.RequestId}; reason=unsupported-foundation-only");
        return "Magic/cast pipeline foundation is registered, but spell execution is disabled until the magic runtime milestone.";
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
