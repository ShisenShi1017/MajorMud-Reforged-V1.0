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

    private static readonly IReadOnlySet<string> PolicyCommands = new HashSet<string>(
        RuntimeCommandRuntimeRouteCatalog.Definitions
            .Where(definition => definition.Kind is RuntimeCommandRouteKind.CombatRuntime
                or RuntimeCommandRouteKind.PowerRuntime
                or RuntimeCommandRouteKind.NarrativeRuntime
                or RuntimeCommandRouteKind.ServiceRuntime
                or RuntimeCommandRouteKind.BankRuntime
                or RuntimeCommandRouteKind.RecoveryRuntime)
            .Select(definition => definition.Canonical),
        StringComparer.OrdinalIgnoreCase);

    internal static bool RoutesToPolicy(string commandName) => PolicyCommands.Contains(commandName);

    public string RenderClassicPrompt(RuntimeSession session) =>
        _gameplay?.RenderClassicPrompt(session) ?? ":";

    public bool RequestsSessionExit(RuntimeSession session, string input)
    {
        var command = RuntimeCommand.Parse(input, session.World.CommandCatalog);
        return string.IsNullOrWhiteSpace(command.ResolutionError)
               && command.Name.Equals("quit", StringComparison.OrdinalIgnoreCase);
    }

    public string Dispatch(RuntimeSession session, string input)
    {
        var command = RuntimeCommand.Parse(input, session.World.CommandCatalog);
        session.Touch();
        if (command.Name.Length == 0)
        {
            return _gameplay?.RenderRoom(session) ?? RoomRenderer.Render(session.World, session.CurrentRoom);
        }

        if (!string.IsNullOrWhiteSpace(command.ResolutionError))
        {
            return command.ResolutionError;
        }

        _gameplay?.NotifyCharacterAction(session, command.Name);

        if (command.IsMovement)
        {
            var previousWorld = session.World;
            var previousRoom = session.CurrentRoom;
            try
            {
                if (_gameplay is not null && !_gameplay.CanMove(session, out var movementGuardFailure)) return movementGuardFailure;
                if (_gameplay is not null && !_gameplay.CanTraverseLivingWorld(session, command.Name, out var livingFailure)) return livingFailure;
                if (_gameplay is not null && !_gameplay.CanEnterGangHouse(session, command.Name, out var gangHouseFailure)) return gangHouseFailure;
                var moved = session.TryMove(command.Name, out var movementResult);
                if (moved)
                {
                    _gameplay?.BroadcastDeparture(session, previousRoom.Id, command.Name);
                    var trapResult = _gameplay?.ApplyExitTrap(session, previousRoom, command.Name) ?? string.Empty;
                    _gameplay?.BroadcastArrival(session);
                    movementResult = _gameplay?.RenderRoom(session) ?? movementResult;
                    if (!string.IsNullOrWhiteSpace(trapResult)) movementResult = trapResult + Environment.NewLine + movementResult;
                    _state?.UpdateSessionLocation(session);
                    _events?.Publish("movement", "completed", session.World.Definition.Id, session.Id, session.Actor.Id, $"{previousRoom.Id}->{session.CurrentRoom.Id}");
                    if (_gameplay is not null)
                    {
                        foreach (var follower in _gameplay.ResolveClassicPartyFollowers(session, previousRoom.Id))
                        {
                            string followerFailure;
                            string followerResult = string.Empty;
                            if (!_gameplay.CanMove(follower, out followerFailure) ||
                                !_gameplay.CanTraverseLivingWorld(follower, command.Name, out followerFailure) ||
                                !_gameplay.CanEnterGangHouse(follower, command.Name, out followerFailure) ||
                                !follower.TryMove(command.Name, out followerResult))
                            {
                                follower.EnqueueMessage($"You cannot follow your party leader {command.Name}: {(string.IsNullOrWhiteSpace(followerFailure) ? followerResult : followerFailure)}");
                                continue;
                            }
                            _gameplay.BroadcastDeparture(follower, previousRoom.Id, command.Name);
                            var followerTrap = _gameplay.ApplyExitTrap(follower, previousRoom, command.Name);
                            _gameplay.BroadcastArrival(follower);
                            var followerRoom = _gameplay.RenderRoom(follower);
                            follower.EnqueueMessage($"-- Following your Party leader {command.Name} --\n" +
                                (string.IsNullOrWhiteSpace(followerTrap) ? followerRoom : followerTrap + Environment.NewLine + followerRoom));
                            _state?.UpdateSessionLocation(follower);
                            _events?.Publish("movement", "party-followed", follower.World.Definition.Id, follower.Id, follower.Actor.Id, $"{previousRoom.Id}->{follower.CurrentRoom.Id}");
                        }
                    }
                }
                else
                {
                    _events?.Publish("movement", "blocked", session.World.Definition.Id, session.Id, session.Actor.Id, movementResult);
                }

                return movementResult;
            }
            catch (Exception ex)
            {
                // Movement errors are command-local. Roll back any partially applied room
                // change and keep the Telnet socket alive for MegaMUD and other clients.
                session.MoveToWorld(previousWorld, previousRoom, "movement-command-rollback");
                _state?.UpdateSessionLocation(session);
                _events?.Publish("movement", "failed", previousWorld.Definition.Id, session.Id, session.Actor.Id,
                    $"{previousRoom.Id}:{command.Name}: {ex.GetType().Name}: {ex.Message}");
                return $"Movement failed: {ex.Message}";
            }
        }

        _events?.Publish("command", "received", session.World.Definition.Id, session.Id, session.Actor.Id, command.Name);
        try
        {
            if (!RuntimeCommandRuntimeRouteCatalog.TryGet(command.Name, out var route))
            {
                var contextualResult = ExecuteDataDrivenCommand(session, command);
                _events?.Publish("command", "completed", session.World.Definition.Id, session.Id, session.Actor.Id, command.Name);
                return contextualResult;
            }

            var result = command.Name switch
            {
            "search" => _gameplay?.ExecuteClassicSearch(session) ?? "Live gameplay facade is not attached.",
            "look" => string.IsNullOrWhiteSpace(command.ArgumentText)
                ? (_gameplay?.RenderRoomVerbose(session) ?? RoomRenderer.Render(session.World, session.CurrentRoom))
                : (_gameplay?.LookDirection(session, command.ArgumentText) ?? "Live gameplay facade is not attached."),
            "brief" => _gameplay?.SetRoomDescriptionMode(session, brief: true) ?? "Live gameplay facade is not attached.",
            "verbose" => _gameplay?.SetRoomDescriptionMode(session, brief: false) ?? "Live gameplay facade is not attached.",
            "examine" => _gameplay?.Examine(session, command.ArgumentText) ?? "Live gameplay facade is not attached.",
            "say" => _gameplay?.Say(session, command.ArgumentText) ?? "Live gameplay facade is not attached.",
            "tell" => _gameplay?.Tell(session, command.Arguments) ?? "Live gameplay facade is not attached.",
            "messages" => _gameplay?.DrainMessages(session) ?? string.Empty,
            "score" => _gameplay?.RenderScore(session) ?? "Live gameplay facade is not attached.",
            "character" => _gameplay?.RenderCharacterMenu(session) ?? "Live gameplay facade is not attached.",
            "wealth" => _gameplay?.RenderWealth(session) ?? "Live gameplay facade is not attached.",
            "experience" => _gameplay?.RenderExperience(session) ?? "Live gameplay facade is not attached.",
            "inventory" => _gameplay?.RenderInventory(session) ?? "Live gameplay facade is not attached.",
            "equipment" => string.IsNullOrWhiteSpace(command.ArgumentText)
                ? (_gameplay?.RenderEquipment(session) ?? "Live gameplay facade is not attached.")
                : ExecuteItem(session, RuntimeCommand.Parse($"equip {command.ArgumentText}")),
            "party" => _gameplay?.RenderClassicParty(session) ?? "Live gameplay facade is not attached.",
            "whohome" => _gameplay?.RenderBossHomeStatus(session) ?? "Live gameplay facade is not attached.",
            "librarium" => _gameplay?.RenderLibrarium(session, command) ?? "Live gameplay facade is not attached.",
            "roomdata" => _gameplay?.RenderClientRoomData(session, command) ?? "Live gameplay facade is not attached.",
            "clientdata" => _gameplay?.RenderClientPanelData(session, command) ?? "Live gameplay facade is not attached.",
            "save" => _gameplay?.Save(session) ?? "Live gameplay facade is not attached.",
            "rest" or "meditate" => _gameplay?.ExecuteRegenerationStateCommand(session, command) ?? "Live gameplay facade is not attached.",
            "mudplus" or "description" or "mudreg" or "buylife" or "plusadmin" =>
                _gameplay?.ExecuteMajorMudPlusCommand(session, command) ?? "Live gameplay facade is not attached.",
            "sysop" => _gameplay?.ExecuteWccSysopCommand(session, command) ?? "Live gameplay facade is not attached.",
            "invite" or "uninvite" when command.Arguments.FirstOrDefault()?.Equals("member", StringComparison.OrdinalIgnoreCase) == true => _gameplay?.ExecuteGangCommand(session, command) ?? "Live gameplay facade is not attached.",
            "invite" or "uninvite" or "follow" or "unfollow" or "frontrank" or "midrank" or "backrank" => _gameplay?.ExecuteClassicPartyCommand(session, command) ?? "Live gameplay facade is not attached.",
            "create" or "join" or "leave" or "broadgang" or "disband" or "promote" or "demote" or "topten" => _gameplay?.ExecuteGangCommand(session, command) ?? "Live gameplay facade is not attached.",
            "ready" => _gameplay?.RenderIntegrationStatus(session) ?? "Live gameplay facade is not attached.",
            "audit" => _gameplay?.ExecuteAuditCommand(session, command) ?? "Live gameplay facade is not attached.",
            "put" when _gameplay is not null && _gameplay.TryExecuteDecodedWorldCommand(session, command, out var decodedPut) => decodedPut,
            "get" or "drop" or "put" or "give" or "equip" or "wear" or "wield" or "remove" => ExecuteItem(session, command),
            "open" or "close" or "lock" or "unlock" or "pull" or "push" or "activate" => _gameplay?.ExecuteLivingWorldCommand(session, command) ?? "Live gameplay facade is not attached.",
            "worldstate" => _gameplay?.RenderLivingWorldState(session) ?? "Live gameplay facade is not attached.",
            "services" or "list" => _gameplay?.ExecuteLiveServicesBundleCommand(session, command) ?? "Live gameplay facade is not attached.",
            "trade" => _gameplay?.ExecuteLiveServicesBundleCommand(session, command) ?? "Live gameplay facade is not attached.",
            var policyCommand when RoutesToPolicy(policyCommand) => _gameplay?.ExecutePolicyCommand(session, command) ?? "Live gameplay facade is not attached.",
            "who" => _gameplay?.RenderWho() ?? (_sessions?.RenderStatus() ?? "Session manager is not attached."),
            "where" => $"World={session.World.Definition.Id}; Room={session.CurrentRoom.Id}; Map={session.CurrentRoom.MapId}; Number={session.CurrentRoom.RoomNumber}",
            "world" => RenderWorld(session.World),
            "worlds" => RenderWorlds(),
            "sessions" => _sessions?.RenderStatus() ?? "Session manager is not attached.",
            "state" => _state?.RenderStatus() ?? "Runtime state store is not attached.",
            "events" => _events?.RenderStatus() ?? "Runtime event dispatcher is not attached.",
            "help" or "commands" => RenderHelp(session, command),
            "quit" => "Goodbye.",
                _ => throw new InvalidOperationException($"Registered command '{command.Name}' is assigned to route '{route.Kind}' but has no executable dispatcher branch.")
            };
            _events?.Publish("command", "completed", session.World.Definition.Id, session.Id, session.Actor.Id, command.Name);
            return result;
        }
        catch (Exception ex)
        {
            _events?.Publish("command", "failed", session.World.Definition.Id, session.Id, session.Actor.Id, $"{command.Name}: {ex.GetType().Name}: {ex.Message}");
            return $"Command '{command.Name}' failed: {ex.Message}";
        }
    }

    private static readonly IReadOnlyDictionary<string, string> CommandUsage =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["look"] = "look [direction|object]", ["brief"] = "brief", ["verbose"] = "verbose",
            ["search"] = "search [object]", ["examine"] = "examine <target>", ["score"] = "score",
            ["character"] = "character", ["wealth"] = "wealth",
            ["experience"] = "experience", ["inventory"] = "inventory", ["equipment"] = "equipment",
            ["party"] = "party", ["whohome"] = "whohome", ["librarium"] = "librarium [page <family> <offset> [search text]]",
            ["rest"] = "rest", ["meditate"] = "meditate",
            ["get"] = "get <item>", ["drop"] = "drop <item>", ["put"] = "put <item> <container>",
            ["give"] = "give <item|currency> <target>", ["equip"] = "equip <item>", ["wear"] = "wear <item>",
            ["wield"] = "wield <weapon>", ["remove"] = "remove <item>", ["attack"] = "attack <target>",
            ["kill"] = "kill <target>", ["flee"] = "flee", ["cast"] = "cast <spell> [target]",
            ["use"] = "use <item> [target]", ["spells"] = "spells", ["abilities"] = "abilities", ["skills"] = "skills",
            ["ask"] = "ask <npc> <keyword>", ["talk"] = "talk <npc>", ["say"] = "say <message>", ["tell"] = "tell <player> <message>",
            ["messages"] = "messages", ["services"] = "services", ["list"] = "list", ["buy"] = "buy <item> [quantity]",
            ["follow"] = "follow <player>", ["unfollow"] = "unfollow", ["frontrank"] = "frontrank", ["midrank"] = "midrank", ["backrank"] = "backrank",
            ["sell"] = "sell <item> [quantity]", ["repair"] = "repair <item>", ["train"] = "train [skill|stat]",
            ["bank"] = "bank", ["deposit"] = "deposit <amount> <denomination>",
            ["withdraw"] = "withdraw <amount> <denomination>", ["trade"] = "trade <arguments>",
            ["quest"] = "quest", ["accept"] = "accept <quest>", ["abandon"] = "abandon <quest>",
            ["complete"] = "complete <quest>", ["journal"] = "journal", ["save"] = "save", ["who"] = "who",
            ["where"] = "where", ["help"] = "help [command]", ["commands"] = "commands", ["quit"] = "quit",
            ["mudplus"] = "mudplus", ["description"] = "description [set <line1>|<line2>]",
            ["mudreg"] = "mudreg [name|search <text>|set <field> <value>]", ["buylife"] = "buylife",
            ["plusadmin"] = "plusadmin <operation>", ["sysop"] = "sysop <native-command> [arguments]",
            ["activate"] = "activate <object>", ["assist"] = "assist <character>",
            ["audit"] = "audit [room <room>|spawns]", ["backstab"] = "backstab <target>",
            ["bash"] = "bash <target>", ["close"] = "close <object|direction>",
            ["combat"] = "combat", ["dispel"] = "dispel <effect|target>",
            ["events"] = "events", ["hide"] = "hide", ["lock"] = "lock <object|direction>",
            ["open"] = "open <object|direction>", ["prepare"] = "prepare",
            ["pull"] = "pull <object>", ["push"] = "push <object>", ["ready"] = "ready",
            ["recover"] = "recover", ["respawn"] = "respawn", ["round"] = "round",
            ["sessions"] = "sessions", ["smash"] = "smash <target>", ["sneak"] = "sneak",
            ["punch"] = "punch <target>", ["kick"] = "kick <target>", ["jumpkick"] = "jumpkick <target>",
            ["state"] = "state", ["tat"] = "tat [stat]", ["unlock"] = "unlock <object|direction>",
            ["world"] = "world", ["worlds"] = "worlds", ["worldstate"] = "worldstate",
            ["north"] = "north", ["south"] = "south", ["east"] = "east", ["west"] = "west",
            ["northeast"] = "northeast", ["northwest"] = "northwest",
            ["southeast"] = "southeast", ["southwest"] = "southwest", ["up"] = "up", ["down"] = "down"
        };

    internal static bool HasHelpUsage(string canonical) => CommandUsage.ContainsKey(canonical);
    internal static int HelpUsageCount => CommandUsage.Count;

    private static string RenderHelp(RuntimeSession session, RuntimeCommand command)
    {
        var topic = command.Name.Equals("commands", StringComparison.OrdinalIgnoreCase) ? string.Empty : command.ArgumentText.Trim();
        if (topic.Length == 0)
        {
            var registrations = session.World.CommandCatalog.Registrations;
            var names = registrations
                .Where(r => !r.IsMovement && !IsDiagnosticCommand(r.Canonical) && !IsClientProtocolCommand(r.Canonical))
                .Select(r => r.Canonical)
                .Append("commands")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase);
            var movement = registrations.Where(r => r.IsMovement).Select(r => r.Canonical).OrderBy(x => x, StringComparer.OrdinalIgnoreCase);
            var contextualCommands = registrations
                .Where(r => !RuntimeCommandRuntimeRouteCatalog.TryGet(r.Canonical, out _))
                .Select(r => r.Canonical)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase);
            return "Available commands:" + Environment.NewLine +
                   string.Join(", ", names) + Environment.NewLine +
                   "Movement: " + string.Join(", ", movement) + Environment.NewLine +
                   "Contextual room actions: " + (contextualCommands.Any() ? string.Join(", ", contextualCommands) : "none") + Environment.NewLine +
                   "Type HELP <command> for usage. Type SERVICES while at a service NPC for its live menu.";
        }

        var resolved = RuntimeCommand.Parse(topic, session.World.CommandCatalog);
        if (!string.IsNullOrWhiteSpace(resolved.ResolutionError) || resolved.Name.Length == 0)
            return $"No help topic matches '{topic}'. Type HELP for the command list.";

        var registration = session.World.CommandCatalog.Registrations.FirstOrDefault(r =>
            string.Equals(r.Canonical, resolved.Name, StringComparison.OrdinalIgnoreCase));
        if (registration is null) return $"No help topic matches '{topic}'. Type HELP for the command list.";
        if (IsClientProtocolCommand(registration.Canonical))
            return $"No help topic matches '{topic}'. Type HELP for the command list.";
        var isContextual = !RuntimeCommandRuntimeRouteCatalog.TryGet(registration.Canonical, out var route);
        var usage = CommandUsage.TryGetValue(registration.Canonical, out var defined)
            ? defined
            : isContextual ? registration.Canonical + " <room-specific arguments>" : registration.Canonical;
        var aliases = registration.Aliases.Count > 0 ? string.Join(", ", registration.Aliases) : "none";
        var kind = registration.IsMovement
            ? "Movement"
            : isContextual ? "Contextual room action"
            : route.Kind is RuntimeCommandRouteKind.RuntimeDiagnostics ? "Host/diagnostic"
            : route.Kind is RuntimeCommandRouteKind.SessionExit ? "Session"
            : "Player";
        return $"{registration.Canonical.ToUpperInvariant()}" + Environment.NewLine +
               $"Usage: {usage}" + Environment.NewLine +
               $"Aliases: {aliases}" + Environment.NewLine +
               $"Category: {kind}";
    }

    private static bool IsDiagnosticCommand(string name) => name is
        "audit" or "ready" or "worldstate" or "world" or "worlds" or "sessions" or "state" or "events";

    private static bool IsClientProtocolCommand(string name) => name is
        "clientdata" or "roomdata" or "librarium" or "whohome";

    private string ExecuteDataDrivenCommand(RuntimeSession session, RuntimeCommand command)
    {
        if (_gameplay is not null && _gameplay.TryExecuteMajorMudPlusAction(session, command, out var plusAction))
        {
            return plusAction;
        }
        if (_gameplay is not null && _gameplay.TryExecuteDecodedWorldCommand(session, command, out var result))
        {
            _state?.UpdateSessionLocation(session);
            _events?.Publish("command", "decoded-world-command-completed", session.World.Definition.Id, session.Id, session.Actor.Id, command.Name);
            return result;
        }
        return $"Nothing here responds to '{command.Name}'.";
    }

    private string ExecuteItem(RuntimeSession session, RuntimeCommand command) =>
        _gameplay?.ExecuteItemCommand(session, command) ?? "Live gameplay facade is not attached.";

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
    internal static readonly IReadOnlyList<RuntimeCommandRegistration> DefaultRegistrations =
    [
        new("look", ["l"], 2),
        new("brief", ["br"], 2),
        new("verbose", ["verb"], 3),
        new("search", ["sea"], 3),
        new("examine", ["x"], 2),
        new("score", ["sc", "stat", "stats", "status"], 3),
        new("character", ["char", "sheet"], 3),
        new("wealth", ["money"], 3),
        new("experience", ["exp"], 3),
        new("inventory", ["i", "inv"], 3),
        new("equipment", ["eq", "wearing"], 3),
        new("party", ["par"], 3),
        new("get", ["g"], 3),
        new("drop", [], 3),
        new("put", [], 3),
        new("give", [], 3),
        new("equip", [], 2),
        new("wear", [], 3),
        new("wield", [], 3),
        new("remove", [], 3),
        new("attack", ["a", "att"], 3),
        new("kill", ["k"], 3),
        new("flee", [], 3),
        new("combat", [], 3),
        new("round", [], 3),
        new("assist", [], 3),
        new("cast", [], 3),
        new("prepare", [], 3),
        new("spells", ["spellbook"], 3),
        new("use", [], 3),
        new("dispel", [], 3),
        new("abilities", [], 3),
        new("skills", [], 3),
        new("ask", [], 3),
        new("talk", [], 3),
        new("buy", [], 3),
        new("sell", [], 4),
        new("repair", [], 3),
        new("train", [], 3),
        new("tat", [], 3),
        new("bash", ["ba"], 2),
        new("smash", [], 2),
        new("backstab", ["bs"], 2),
        new("punch", [], 2),
        new("kick", [], 2),
        new("jumpkick", ["jk"], 2),
        new("hide", ["hid"], 2),
        new("sneak", ["sn"], 2),
        new("open", [], 2),
        new("close", [], 3),
        new("lock", [], 3),
        new("unlock", [], 3),
        new("pull", [], 3),
        new("push", [], 3),
        new("activate", [], 3),
        new("worldstate", [], 5),
        new("services", [], 4),
        new("list", [], 3),
        new("bank", [], 3),
        new("deposit", [], 3),
        new("withdraw", [], 3),
        new("trade", [], 3),
        new("quest", [], 3),
        new("accept", [], 3),
        new("abandon", [], 3),
        new("complete", [], 3),
        new("journal", ["qlog"], 3),
        new("say", [], 3),
        new("tell", [], 3),
        new("messages", [], 3),
        new("mudplus", ["mmplus"], 3),
        new("description", ["desc"], 4),
        new("mudreg", ["/mudreg"], 3),
        new("buylife", [], 4),
        new("plusadmin", [], 5),
        new("sysop", ["sys"], 5),
        new("create", [], 3),
        new("invite", [], 3),
        new("uninvite", [], 3),
        new("follow", [], 3),
        new("unfollow", [], 3),
        new("frontrank", ["frontr"], 5),
        new("midrank", ["midr"], 4),
        new("backrank", ["backr"], 4),
        new("join", [], 3),
        new("leave", [], 3),
        new("broadgang", ["bg"], 2),
        new("disband", [], 3),
        new("promote", [], 3),
        new("demote", [], 3),
        new("topten", ["top"], 3),
        new("respawn", [], 3),
        new("recover", [], 3),
        new("ready", [], 3),
        new("audit", [], 3),
        new("save", [], 4),
        new("rest", [], 3),
        new("meditate", ["med"], 3),
        new("who", [], 3),
        new("whohome", ["whoshome"], 4),
        new("librarium", ["library"], 4),
        new("roomdata", [], 8),
        new("clientdata", [], 10),
        new("where", [], 3),
        new("world", [], 5),
        new("worlds", [], 5),
        new("sessions", [], 4),
        new("state", [], 4),
        new("events", [], 3),
        new("help", ["?"], 1),
        new("commands", ["cmds"], 3),
        new("quit", ["logout", "exit"], 3),
        new("north", ["n"], 5, true),
        new("south", ["s"], 5, true),
        new("east", ["e"], 4, true),
        new("west", ["w"], 4, true),
        new("northeast", ["ne"], 9, true),
        new("northwest", ["nw"], 9, true),
        new("southeast", ["se"], 9, true),
        new("southwest", ["sw"], 9, true),
        new("up", ["u"], 2, true),
        new("down", ["d"], 2, true)
    ];

    private static readonly Dictionary<string, RuntimeCommandRegistration> ExactTokens =
        DefaultRegistrations.SelectMany(registration => registration.Tokens.Select(token => (token, registration)))
            .ToDictionary(entry => entry.token, entry => entry.registration, StringComparer.OrdinalIgnoreCase);

    public static readonly IReadOnlySet<string> SupportedCommands = new HashSet<string>(
        DefaultRegistrations.Select(registration => registration.Canonical),
        StringComparer.OrdinalIgnoreCase);

    internal static bool IsDefaultRegistration(string canonical) =>
        DefaultRegistrations.Any(registration => registration.Canonical.Equals(canonical, StringComparison.OrdinalIgnoreCase));

    private RuntimeCommand(string name, IReadOnlyList<string> arguments, string resolutionError = "")
    {
        Name = name;
        Arguments = arguments;
        ResolutionError = resolutionError;
    }

    public string Name { get; }
    public IReadOnlyList<string> Arguments { get; }
    public string ResolutionError { get; }
    public string ArgumentText => string.Join(" ", Arguments);
    public bool IsMovement => ExactTokens.TryGetValue(Name, out var registration) && registration.IsMovement;

    public static RuntimeCommand Parse(string input, RuntimeClassicCommandCatalog? catalog = null)
    {
        var tokens = (input ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0) return new RuntimeCommand(string.Empty, Array.Empty<string>());
        var name = tokens[0].ToLowerInvariant();
        var resolved = catalog is null ? Resolve(name) : catalog.Resolve(name);
        if (resolved.Error.Length > 0) return new RuntimeCommand(name, tokens.Skip(1).ToArray(), resolved.Error);
        return new RuntimeCommand(resolved.Name, tokens.Skip(1).ToArray());
    }

    private static RuntimeCommandResolution Resolve(string token)
    {
        if (ExactTokens.TryGetValue(token, out var exact)) return new RuntimeCommandResolution(exact.Canonical, string.Empty);

        var possibleMatches = DefaultRegistrations
            .Where(registration => registration.Tokens.Any(candidate => candidate.StartsWith(token, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var eligibleMatches = possibleMatches
            .Where(registration => token.Length >= registration.MinimumAbbreviationLength &&
                                   registration.Canonical.StartsWith(token, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var canonicalMatches = eligibleMatches
            .Select(registration => registration.Canonical)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (canonicalMatches.Count == 0 && possibleMatches
                .Select(registration => registration.Canonical)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Skip(1)
                .Any())
        {
            canonicalMatches = possibleMatches
                .Select(registration => registration.Canonical)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return canonicalMatches.Count switch
        {
            0 => new RuntimeCommandResolution(token, string.Empty),
            1 => new RuntimeCommandResolution(canonicalMatches[0], string.Empty),
            _ => new RuntimeCommandResolution(token, $"Ambiguous command '{token}': {string.Join(", ", canonicalMatches)}.")
        };
    }


}
