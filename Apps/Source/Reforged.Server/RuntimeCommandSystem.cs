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

    public bool TryRenderClientData(RuntimeSession session, string input, out string result)
    {
        result = string.Empty;
        if (_gameplay is null || string.IsNullOrWhiteSpace(input)) return false;

        var command = RuntimeCommand.Parse(input, session.World.CommandCatalog);
        if (!string.IsNullOrWhiteSpace(command.ResolutionError)) return false;

        result = command.Name switch
        {
            "clientdata" => _gameplay.RenderClientPanelData(session, command),
            "roomdata" => _gameplay.RenderClientRoomData(session, command),
            "librarium" => _gameplay.RenderLibrarium(session, command),
            "whohome" => _gameplay.RenderBossHomeStatus(session),
            _ => string.Empty
        };
        return result.Length > 0;
    }

    public void HandleCharacterRerollCleanup(RuntimeSession session, string oldCharacterId) =>
        _gameplay?.HandleCharacterRerollCleanup(session, oldCharacterId);

    public bool ClassicSuicideRequiresPassword(RuntimeSession session) =>
        _gameplay?.ClassicSuicideRequiresPassword(session) ?? false;

    public bool ValidateClassicSuicidePassword(RuntimeSession session, string password) =>
        _gameplay?.ValidateClassicSuicidePassword(session, password) ?? false;

    public bool ClassicSuicideIsFinalLife(RuntimeSession session) =>
        _gameplay?.ClassicSuicideIsFinalLife(session) ?? false;

    public string ExecuteClassicSuicide(RuntimeSession session) =>
        _gameplay?.ExecuteClassicSuicide(session) ?? "Live gameplay facade is not attached.";

    public bool RequestsSessionExit(RuntimeSession session, string input)
    {
        // Worldgroup modal states consume the next line as modal input; "QUIT" at state 0x88
        // is therefore a cancel reply, not a session-exit command.
        if (session.HasClassicModalInput) return false;
        var command = RuntimeCommand.Parse(input, session.World.CommandCatalog);
        var requested = string.IsNullOrWhiteSpace(command.ResolutionError)
                        && command.Name.Equals("quit", StringComparison.OrdinalIgnoreCase);
        if (requested) _gameplay?.PrepareClassicQuit(session);
        return requested;
    }

    public RuntimeClassicLifecycleCleanupResult? CompleteGracefulSessionExit(RuntimeSession session) =>
        _gameplay?.CompleteClassicGracefulLogoff(session);

    public RuntimeClassicLifecycleCleanupResult? CompleteUnexpectedSessionExit(RuntimeSession session) =>
        _gameplay?.CompleteClassicUnexpectedHangup(session);

    public string Dispatch(RuntimeSession session, string input)
    {
        session.Touch();
        if (_gameplay is not null && _gameplay.TryHandleClassicModalInput(session, input, out var modalResult))
            return modalResult;

        // Native command admission checks the live-user delay byte (+0x6BB) before normal
        // gameplay parsing. While non-zero it routes the raw input line through the per-user
        // pending-command owner at 0x450FA1 instead of discarding it. Modal input remains above
        // this guard because Worldgroup owns that state.
        if (session.ClassicCommandDelay > 0) return session.QueueClassicPendingCommand(input);

        var command = RuntimeCommand.Parse(input, session.World.CommandCatalog);
        if (command.Name.Length == 0)
        {
            return _gameplay?.RenderBlankCommand(session) ?? RoomRenderer.Render(session.World, session.CurrentRoom);
        }

        // Classic MajorMUD first admits explicit speech shortcuts and resolved commands/actions.
        // Unrecognized text falls through to SAY only while TALK FAST is active; TALK SLOW returns
        // the native command-not-recognized response unless the line used the configured speech prefix.
        // Custom/AI retain Reforged's permissive command-to-SAY fallback.
        var registeredCommand = string.IsNullOrWhiteSpace(command.ResolutionError)
            && session.World.CommandCatalog.IsRegistered(command.Name);
        if (!registeredCommand
            && _gameplay is not null
            && _gameplay.TryExecuteMajorMudSpeechShortcut(session, command, out var speechShortcut))
        {
            _events?.Publish("communication", "speech-shortcut", session.World.Definition.Id, session.Id, session.Actor.Id, command.Name);
            return speechShortcut;
        }

        if (!registeredCommand)
        {
            if (string.IsNullOrWhiteSpace(command.ResolutionError)
                && _gameplay is not null
                && TryExecuteUnregisteredAction(session, command, out var actionResult))
            {
                return actionResult;
            }

            var classicWorld = session.World.Definition.Type.Equals("Classic", StringComparison.OrdinalIgnoreCase);
            if (!classicWorld || (_gameplay?.IsMajorMudTalkFast(session) ?? true))
            {
                _events?.Publish("communication", "command-fallback-say", session.World.Definition.Id, session.Id, session.Actor.Id, input ?? string.Empty);
                return _gameplay?.Say(session, input ?? string.Empty) ?? "Live gameplay facade is not attached.";
            }

            _events?.Publish("communication", "command-not-recognized-slow-talk", session.World.Definition.Id, session.Id, session.Actor.Id, input ?? string.Empty);
            return RuntimeClassicCommunicationRenderingAuthority.CommandNotRecognized;
        }

        _gameplay?.NotifyCharacterAction(session, command.Name, command.IsMovement);

        if (command.IsMovement)
        {
            var previousWorld = session.World;
            var previousRoom = session.CurrentRoom;
            try
            {
                if (_gameplay is not null && !_gameplay.CanMove(session, out var movementGuardFailure)) return movementGuardFailure;
                if (_gameplay is not null && !_gameplay.CanTraverseLivingWorld(session, command.Name, out var livingFailure)) return livingFailure;
                if (_gameplay is not null && !_gameplay.CanEnterGangHouse(session, command.Name, out var gangHouseFailure)) return gangHouseFailure;

                // Native _move_user executes active type-9/type-24 trap effects before the
                // successful room transition. A lethal trap therefore leaves the requested move
                // uncommitted (ordinary death may relocate the victim through the death engine).
                var trapResult = _gameplay?.ApplyExitTrap(session, previousRoom, command.Name) ?? string.Empty;
                if (_gameplay is not null && _gameplay.ClassicTrapStoppedMovement(session, previousRoom))
                {
                    _state?.UpdateSessionLocation(session);
                    _events?.Publish("movement", "trap-stopped", session.World.Definition.Id, session.Id, session.Actor.Id, $"{previousRoom.Id}:{command.Name}");
                    return trapResult;
                }

                var moved = session.TryMove(command.Name, out var movementResult);
                if (moved)
                {
                    _gameplay?.BroadcastDeparture(session, previousRoom.Id, command.Name);
                    _gameplay?.CaptureClassicPracticeArenaEntry(session, previousRoom);
                    _gameplay?.BroadcastArrival(session, command.Name);
                    _gameplay?.AdmitClassicRoomEntryNpcRespawns(session);
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
                                !_gameplay.CanEnterGangHouse(follower, command.Name, out followerFailure))
                            {
                                // Native _fast_update_character calls _move_user(..., mode 3) for
                                // a follower and clears +0x6aa whenever that move returns false.
                                _gameplay.StopClassicPartyFollowAfterFailedMove(follower);
                                follower.EnqueueMessage($"You cannot follow your party leader {command.Name}: {followerFailure}");
                                continue;
                            }

                            var followerTrap = _gameplay.ApplyExitTrap(follower, previousRoom, command.Name);
                            if (_gameplay.ClassicTrapStoppedMovement(follower, previousRoom))
                            {
                                _gameplay.StopClassicPartyFollowAfterFailedMove(follower);
                                follower.EnqueueMessage($"-- Following your Party leader {command.Name} --" +
                                    (string.IsNullOrWhiteSpace(followerTrap) ? string.Empty : Environment.NewLine + followerTrap));
                                _state?.UpdateSessionLocation(follower);
                                _events?.Publish("movement", "party-trap-stopped", follower.World.Definition.Id, follower.Id, follower.Actor.Id, $"{previousRoom.Id}:{command.Name}");
                                continue;
                            }

                            if (!follower.TryMove(command.Name, out followerResult))
                            {
                                _gameplay.StopClassicPartyFollowAfterFailedMove(follower);
                                follower.EnqueueMessage($"You cannot follow your party leader {command.Name}: {followerResult}");
                                continue;
                            }
                            _gameplay.BroadcastDeparture(follower, previousRoom.Id, command.Name);
                            _gameplay.CaptureClassicPracticeArenaEntry(follower, previousRoom);
                            _gameplay.BroadcastArrival(follower, command.Name);
                            _gameplay.AdmitClassicRoomEntryNpcRespawns(follower);
                            var followerRoom = _gameplay.ShouldRenderClassicPartyFollowRoom(follower)
                                ? _gameplay.RenderRoom(follower)
                                : string.Empty;
                            var followerDetails = string.Join(Environment.NewLine, new[] { followerTrap, followerRoom }
                                .Where(value => !string.IsNullOrWhiteSpace(value)));
                            follower.EnqueueMessage($"-- Following your Party leader {command.Name} --" +
                                (followerDetails.Length == 0 ? string.Empty : Environment.NewLine + followerDetails));
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
                return ex.Message;
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

            // Native room/textblock commands can deliberately reuse a normal command verb
            // (for example BUY HEALING and command-triggered room relocations). Resolve an
            // exact current-room decoded command before the generic command owner so the
            // retained room script, not a synthetic shop/command fallback, owns that phrase.
            if (_gameplay is not null && _gameplay.TryExecuteDecodedWorldCommand(session, command, out var decodedRoomCommand))
            {
                _state?.UpdateSessionLocation(session);
                _events?.Publish("command", "decoded-world-command-completed", session.World.Definition.Id, session.Id, session.Actor.Id, command.Name);
                return decodedRoomCommand;
            }

            var result = command.Name switch
            {
            "search" => _gameplay?.ExecuteClassicSearch(session) ?? "Live gameplay facade is not attached.",
            "look" => string.IsNullOrWhiteSpace(command.ArgumentText)
                ? (_gameplay?.RenderRoomVerbose(session) ?? RoomRenderer.Render(session.World, session.CurrentRoom))
                : (_gameplay?.LookDirection(session, command.ArgumentText) ?? "Live gameplay facade is not attached."),
            "brief" => _gameplay?.SetRoomDescriptionMode(session, brief: true) ?? "Live gameplay facade is not attached.",
            "verbose" => _gameplay?.SetRoomDescriptionMode(session, brief: false) ?? "Live gameplay facade is not attached.",
            "statline" => _gameplay?.ConfigureClassicStatline(session, command.ArgumentText, invokedThroughSet: false) ?? "Live gameplay facade is not attached.",
            "set" => _gameplay?.ConfigureClassicSetting(session, command.ArgumentText) ?? "Live gameplay facade is not attached.",
            "examine" => _gameplay?.Examine(session, command.ArgumentText) ?? "Live gameplay facade is not attached.",
            "exits" => _gameplay?.RenderObviousExits(session) ?? "Live gameplay facade is not attached.",
            "say" => _gameplay?.Say(session, command.ArgumentText) ?? "Live gameplay facade is not attached.",
            "talk" => _gameplay?.ConfigureMajorMudTalkMode(session, command.ArgumentText) ?? "Live gameplay facade is not attached.",
            "gossip" => _gameplay?.Gossip(session, command.ArgumentText) ?? "Live gameplay facade is not attached.",
            "auction" => _gameplay?.Auction(session, command.ArgumentText) ?? "Live gameplay facade is not attached.",
            "broadcast" => _gameplay?.Broadcast(session, command.ArgumentText) ?? "Live gameplay facade is not attached.",
            "forget" => _gameplay?.ForgetTelepath(session, command.ArgumentText) ?? "Live gameplay facade is not attached.",
            "messages" => _gameplay?.DrainMessages(session) ?? string.Empty,
            "page" => _gameplay?.ConfigureClassicPage(session) ?? "Live gameplay facade is not attached.",
            "score" => _gameplay?.RenderScore(session) ?? "Live gameplay facade is not attached.",
            "health" => _gameplay?.RenderHealth(session) ?? "Live gameplay facade is not attached.",
            "character" => _gameplay?.RenderCharacterMenu(session) ?? "Live gameplay facade is not attached.",
            "wealth" => _gameplay?.RenderWealth(session) ?? "Live gameplay facade is not attached.",
            "experience" => _gameplay?.RenderExperience(session) ?? "Live gameplay facade is not attached.",
            "inventory" => _gameplay?.RenderInventory(session) ?? "Live gameplay facade is not attached.",
            "keys" => _gameplay?.RenderKeys(session) ?? "Live gameplay facade is not attached.",
            "equipment" => string.IsNullOrWhiteSpace(command.ArgumentText)
                ? (_gameplay?.RenderEquipment(session) ?? "Live gameplay facade is not attached.")
                : ExecuteItem(session, RuntimeCommand.Parse($"equip {command.ArgumentText}")),
            "party" => _gameplay?.RenderClassicParty(session) ?? "Live gameplay facade is not attached.",
            "profile" => _gameplay?.RenderClassicProfile(session) ?? "Live gameplay facade is not attached.",
            "scan" => _gameplay?.RenderClassicUserScan(session) ?? "Live gameplay facade is not attached.",
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
            "join" when command.Arguments.FirstOrDefault()?.Equals("gang", StringComparison.OrdinalIgnoreCase) == true => _gameplay?.ExecuteGangCommand(session, command) ?? "Live gameplay facade is not attached.",
            "leave" when command.Arguments.FirstOrDefault()?.Equals("gang", StringComparison.OrdinalIgnoreCase) == true => _gameplay?.ExecuteGangCommand(session, command) ?? "Live gameplay facade is not attached.",
            "invite" or "uninvite" or "follow" or "frontrank" or "midrank" or "backrank" or "join" or "leave" => _gameplay?.ExecuteClassicPartyCommand(session, command) ?? "Live gameplay facade is not attached.",
            "share" => _gameplay?.ExecuteClassicShare(session, command) ?? "Live gameplay facade is not attached.",
            "stock" or "unstock" or "markup" => _gameplay?.ExecuteClassicGangShopCommand(session, command) ?? "Live gameplay facade is not attached.",
            "create" or "broadgang" or "disband" or "promote" or "demote" or "topten" => _gameplay?.ExecuteGangCommand(session, command) ?? "Live gameplay facade is not attached.",
            var nativeEvidenceCommand when route.Kind is RuntimeCommandRouteKind.NativeEvidenceGate => ExecuteEvidenceGatedNativeCommand(session, command),
            "audit" => _gameplay?.ExecuteAuditCommand(session, command) ?? "Live gameplay facade is not attached.",
            "get" or "drop" or "put" or "give" or "equip" or "wear" or "wield" or "ready" or "remove" => ExecuteItem(session, command),
            "open" or "close" or "lock" or "unlock" or "pull" or "push" or "activate" or "picklock" or "disarm" => _gameplay?.ExecuteLivingWorldCommand(session, command) ?? "Live gameplay facade is not attached.",
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
            "reroll" when route.Kind is RuntimeCommandRouteKind.SessionLifecycle => "REROLL requires an interactive character-creation session.",
            "suicide" when route.Kind is RuntimeCommandRouteKind.SessionLifecycle => "SUICIDE requires an interactive password/death session.",
            "quit" => "Goodbye.",
                _ => throw new InvalidOperationException($"Registered command '{command.Name}' is assigned to route '{route.Kind}' but has no executable dispatcher branch.")
            };
            _events?.Publish("command", "completed", session.World.Definition.Id, session.Id, session.Actor.Id, command.Name);
            return result;
        }
        catch (Exception ex)
        {
            _events?.Publish("command", "failed", session.World.Definition.Id, session.Id, session.Actor.Id, $"{command.Name}: {ex.GetType().Name}: {ex.Message}");
            return ex.Message;
        }
    }

    private static readonly IReadOnlyDictionary<string, string> CommandUsage =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["look"] = "look [direction|object]", ["brief"] = "brief", ["verbose"] = "verbose",
            ["statline"] = "statline on|off|full", ["set"] = "set statline [full] custom <format> | set suicide <password> | set receive [on|off] | set follow [blind|normal] | set speech [fast|slow] | set messages [low|medium|high] | set entrances [on|off] | set telepath [on|off] | set gossip [on|off] | set auction [on|off] | set page [on|ok|off] | set response [brief|verbose] | set maxpages|maxgossip|maxauction <n>",
            ["search"] = "search [object]", ["examine"] = "examine <target>", ["score"] = "score",
            ["character"] = "character", ["wealth"] = "wealth",
            ["experience"] = "experience", ["inventory"] = "inventory", ["keys"] = "keys", ["equipment"] = "equipment",
            ["party"] = "party", ["whohome"] = "whohome", ["librarium"] = "librarium [page <family> <offset> [search text]]",
            ["rest"] = "rest", ["meditate"] = "meditate",
            ["get"] = "get <item>", ["drop"] = "drop <item>", ["put"] = "put <item> <container>",
            ["give"] = "give <item> <player>", ["equip"] = "equip <item>", ["wear"] = "wear <item>",
            ["wield"] = "wield <weapon>", ["remove"] = "remove <item>", ["attack"] = "attack <target>", ["break"] = "break",
            ["kill"] = "kill <target>", ["flee"] = "flee", ["cast"] = "cast <spell> [target]",
            ["use"] = "use <item> [target]", ["spells"] = "spells", ["powers"] = "powers", ["abilities"] = "abilities", ["skills"] = "skills",
            ["ask"] = "ask <npc> <keyword>", ["greet"] = "greet <npc>", ["talk"] = "talk fast|slow", ["say"] = "say <message>",
            ["gossip"] = "gossip [message]", ["auction"] = "auction [message]", ["forget"] = "forget [player]",
            ["messages"] = "messages", ["services"] = "services", ["list"] = "list", ["buy"] = "buy <item> [quantity]",
            ["follow"] = "follow [player]", ["join"] = "join <player>|<broadcast-group>|gang <gang>", ["leave"] = "leave [gang]", ["frontrank"] = "frontrank", ["midrank"] = "midrank", ["backrank"] = "backrank",
            ["create"] = "create gang <name>", ["invite"] = "invite <player> | invite member <character>",
            ["uninvite"] = "uninvite <player> | uninvite member <character>", ["broadgang"] = "broadgang [message]",
            ["disband"] = "disband gang", ["promote"] = "promote <character>", ["demote"] = "demote <character>",
            ["sell"] = "sell <item> [quantity]", ["repair"] = "repair <item>", ["train"] = "train [skill|stat]",
            ["bank"] = "bank", ["deposit"] = "deposit <amount> <denomination>",
            ["withdraw"] = "withdraw <amount> <denomination>", ["trade"] = "trade <arguments>",
            ["quest"] = "quest", ["accept"] = "accept <quest>", ["abandon"] = "abandon <quest>",
            ["complete"] = "complete <quest>", ["journal"] = "journal", ["save"] = "save", ["who"] = "who",
            ["drag"] = "drag <character>", ["go"] = "go <location>", ["health"] = "health", ["map"] = "map", ["profile"] = "profile", ["track"] = "track <user>",
            ["aid"] = "aid", ["light"] = "light <item>", ["stash"] = "stash <item>", ["mana"] = "mana", ["invoke"] = "invoke <kai power>", ["read"] = "read <scroll>",
            ["suicide"] = "suicide [now]", ["reroll"] = "reroll", ["appraise"] = "appraise <item>", ["eat"] = "eat <item>", ["drink"] = "drink <item>", ["rob"] = "rob <target>",
            ["ready"] = "ready <item>", ["exits"] = "exits", ["picklock"] = "picklock <direction>", ["disarm"] = "disarm trap <direction>", ["page"] = "page", ["broadcast"] = "broadcast [message]", ["share"] = "share <amount> <currency> [with room|party]",
            ["scan"] = "scan", ["quiet"] = "quiet", ["topten"] = "topten | top <n> <class> | topten gangs | top <n> gangs", ["stock"] = "stock <item> [price] [currency]", ["unstock"] = "unstock <item>", ["markup"] = "markup <percent>",
            ["where"] = "where", ["help"] = "help [command]", ["commands"] = "commands", ["quit"] = "quit",
            ["roomdata"] = "roomdata", ["clientdata"] = "clientdata <panel>",
            ["mudplus"] = "mudplus", ["description"] = "description [set <line1>|<line2>]",
            ["mudreg"] = "mudreg [name|search <text>|set <field> <value>]", ["buylife"] = "buylife",
            ["plusadmin"] = "plusadmin <operation>", ["sysop"] = "sysop <native-command> [arguments]",
            ["activate"] = "activate <object>", ["assist"] = "assist <character>",
            ["audit"] = "audit [room <room>|spawns|megamud|parity|timers|combat]", ["backstab"] = "backstab <target>",
            ["bash"] = "bash <target|direction>", ["close"] = "close <object|direction>",
            ["combat"] = "combat", ["dispel"] = "dispel <effect|target>",
            ["events"] = "events", ["hide"] = "hide", ["lock"] = "lock <object|direction>",
            ["open"] = "open <object|direction>", ["prepare"] = "prepare",
            ["pull"] = "pull <object>", ["push"] = "push <object>",
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

    private string RenderHelp(RuntimeSession session, RuntimeCommand command)
    {
        var topic = command.Name.Equals("commands", StringComparison.OrdinalIgnoreCase) ? "commands" : command.ArgumentText.Trim();
        if (_gameplay is not null && _gameplay.TryRenderMajorMudNativeHelp(topic, out var nativeHelp))
            return nativeHelp;

        var classicWorld = session.World.Definition.Type.Equals("Classic", StringComparison.OrdinalIgnoreCase);
        // CodeUpdate0369 recovers the native prefix/first-match resolver. A failed
        // static WCCMMHLP lookup uses the retained native no-help sentence rather than
        // exposing Reforged's synthetic command documentation in Classic.
        if (classicWorld)
            return _gameplay?.RenderMajorMudNativeNoHelp() ?? "Sorry, no help is available for that topic!\r";

        if (topic.Length == 0)
        {
            var registrations = session.World.CommandCatalog.Registrations;
            var names = registrations
                .Where(r => !r.IsMovement && !IsDiagnosticCommand(r.Canonical) && !IsClientProtocolCommand(r.Canonical)
                    && RuntimeClassicExtensionBoundaryAuthority.IsCommandAvailable(session.World.Definition.Type, r.Canonical))
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
        if (!RuntimeClassicExtensionBoundaryAuthority.IsCommandAvailable(session.World.Definition.Type, registration.Canonical))
            return $"No help topic matches '{topic}'. Type HELP for the command list.";
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
            : route.Kind is RuntimeCommandRouteKind.SessionExit or RuntimeCommandRouteKind.SessionLifecycle ? "Session"
            : "Player";
        return $"{registration.Canonical.ToUpperInvariant()}" + Environment.NewLine +
               $"Usage: {usage}" + Environment.NewLine +
               $"Aliases: {aliases}" + Environment.NewLine +
               $"Category: {kind}";
    }

    private static bool IsDiagnosticCommand(string name) => name is
        "audit" or "worldstate" or "world" or "worlds" or "sessions" or "state" or "events";

    private static bool IsClientProtocolCommand(string name) => name is
        "clientdata" or "roomdata" or "librarium" or "whohome";

    private string ExecuteDataDrivenCommand(RuntimeSession session, RuntimeCommand command)
    {
        return TryExecuteUnregisteredAction(session, command, out var result)
            ? result
            : $"Nothing here responds to '{command.Name}'.";
    }

    private bool TryExecuteUnregisteredAction(RuntimeSession session, RuntimeCommand command, out string result)
    {
        result = string.Empty;
        if (_gameplay is null) return false;

        // MegaMUD BBS profiles can issue the configured ANSI command when a
        // session begins. Reforged is always ANSI-capable, so acknowledge the
        // stock "=a" command silently instead of leaking it into SAY text.
        if (command.RawInput.Trim().Equals("=a", StringComparison.OrdinalIgnoreCase))
            return true;

        if (_gameplay.TryExecuteMajorMudPowerShortcut(session, command, out result)) return true;
        if (_gameplay.TryExecuteMajorMudPlusAction(session, command, out result)) return true;
        if (_gameplay.TryExecuteDecodedWorldCommand(session, command, out result))
        {
            _state?.UpdateSessionLocation(session);
            _events?.Publish("command", "decoded-world-command-completed", session.World.Definition.Id, session.Id, session.Actor.Id, command.Name);
            return true;
        }
        return false;
    }

    private string ExecuteEvidenceGatedNativeCommand(RuntimeSession session, RuntimeCommand command)
    {
        if (TryExecuteUnregisteredAction(session, command, out var contextual)) return contextual;
        return $"Native command '{command.Name.ToUpperInvariant()}' is recognized, but its exact runtime behavior is not yet recovered.";
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
        new("brief", [], 5),
        new("verbose", ["verb"], 3),
        new("statline", [], 4),
        new("set", [], 3),
        new("search", ["sea"], 3),
        new("examine", ["x"], 2),
        new("score", ["sc", "st", "stat", "stats", "status"], 3),
        new("character", ["char", "sheet"], 3),
        new("wealth", ["money"], 3),
        new("experience", ["exp"], 3),
        new("inventory", ["i", "inv"], 3),
        new("keys", [], 3),
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
        new("break", [], 3),
        new("cast", [], 3),
        new("prepare", [], 3),
        new("spells", ["spellbook", "sp"], 2),
        new("powers", ["pow"], 3),
        new("use", [], 3),
        new("dispel", [], 3),
        new("abilities", [], 3),
        new("skills", [], 3),
        new("ask", [], 3),
        new("greet", [], 3),
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
        new("gossip", ["gos"], 3),
        new("auction", ["auc"], 3),
        new("forget", [], 6),
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
        new("drag", [], 4),
        new("go", [], 2),
        new("health", [], 6),
        new("map", [], 3),
        new("profile", [], 7),
        new("track", [], 5),
        new("aid", [], 3),
        new("light", [], 5),
        new("stash", [], 5),
        new("mana", [], 4),
        new("invoke", [], 6),
        new("read", [], 4),
        new("suicide", [], 7),
        new("reroll", [], 6),
        new("appraise", [], 8),
        new("eat", [], 3),
        new("drink", [], 5),
        new("rob", [], 3),
        new("exits", [], 5),
        new("picklock", [], 8),
        new("disarm", [], 6),
        new("page", [], 4),
        new("broadcast", ["br"], 9),
        new("share", [], 5),
        new("scan", [], 4),
        new("quiet", [], 5),
        new("stock", [], 5),
        new("unstock", [], 7),
        new("markup", [], 6),
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

    private RuntimeCommand(string name, IReadOnlyList<string> arguments, string resolutionError = "", string rawInput = "")
    {
        Name = name;
        Arguments = arguments;
        ResolutionError = resolutionError;
        RawInput = rawInput;
    }

    public string Name { get; }
    public IReadOnlyList<string> Arguments { get; }
    public string ResolutionError { get; }
    public string RawInput { get; }
    public string ArgumentText => string.Join(" ", Arguments);
    public bool IsMovement => ExactTokens.TryGetValue(Name, out var registration) && registration.IsMovement;

    public static RuntimeCommand Parse(string input, RuntimeClassicCommandCatalog? catalog = null)
    {
        var rawInput = input ?? string.Empty;
        var tokens = rawInput.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0) return new RuntimeCommand(string.Empty, Array.Empty<string>(), rawInput: rawInput);
        var name = tokens[0].ToLowerInvariant();
        var resolved = catalog is null ? Resolve(name) : catalog.Resolve(name);
        if (resolved.Error.Length > 0) return new RuntimeCommand(name, tokens.Skip(1).ToArray(), resolved.Error, rawInput);
        return new RuntimeCommand(resolved.Name, tokens.Skip(1).ToArray(), rawInput: rawInput);
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
