using System.Text;
using System.Text.Json;
using RealmGate.Server;

var root = args.Length > 0 ? Path.GetFullPath(args[0]) : Directory.GetCurrentDirectory();
var options = ServerRuntimeOptions.Load(root);
var classic = options.Worlds.Single(world => world.Id.Equals("Classic", StringComparison.OrdinalIgnoreCase));
var world = new RuntimeWorldLoader().Load(root, classic);
var testState = Path.Combine(Path.GetTempPath(), "realmgate-room-commands-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(testState);
var checks = 0;
try
{
    var characters = new RuntimeCharacterService(root, testState);
    var facade = new RuntimeLiveGameplayFacade(characters, new RuntimeSessionManager(), testState);
    var dispatcher = new RuntimeCommandDispatcher(() => new Dictionary<string, RuntimeWorld> { [classic.Id] = world }, null, null, null, facade);
    var auditLiving = new RuntimeLivingWorldService(root, world, Path.Combine(testState, "gate-audit"));
    var gates = auditLiving.CaptureGates();
    var newhaven = world.FindRoom(1, 2149) ?? throw new InvalidOperationException("Newhaven dock missing");
    var pier = world.FindRoom(1, 2335) ?? throw new InvalidOperationException("River pier missing");
    var silvermere = world.FindRoom(1, 296) ?? throw new InvalidOperationException("Silvermere dock missing");
    var session = new RuntimeSession("room-command-audit", world, newhaven, new RuntimeDeveloperAvatar("room-command-audit", "Audit"), "smoke");
    var crossing = world.FindExit(newhaven, "S") ?? throw new InvalidOperationException("Ferryman exit missing");
    var crossingGate = gates.Single(gate => gate.ExitId.Equals(crossing.Id, StringComparison.OrdinalIgnoreCase));
    Require(crossing.GateKind.Equals("text_command", StringComparison.OrdinalIgnoreCase)
        && crossingGate.Commands.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(["borrow skiff", "go skiff", "row skiff"])
        && crossing.TargetRoomId.Equals(pier.Id, StringComparison.OrdinalIgnoreCase), "ferryman-source-exit-and-aliases");

    var dialogue = RuntimeClassicNpcDialogueAuthority.Load(root);
    var graph = RuntimeClassicTextblockGraphAuthority.Load(root);
    Require(dialogue.GetRoots(81).Any(link => link.RootTextblockId == 77), "ferryman-dialogue-root-77");
    Require(graph.Require(77).Commands.Select(command => command.Keyword).ToHashSet(StringComparer.OrdinalIgnoreCase)
        .SetEquals(["borrow", "skiff", "boat", "ferry", "silvermere"]), "ferryman-dialogue-keywords-distinct-from-exit-aliases");

    var roomData = facade.RenderClientRoomData(session, false);
    var commandPayload = roomData.Split('\n').Where(line => line.StartsWith("@@REFORGED-PROTOCOL|ROOMCOMMANDS|", StringComparison.Ordinal))
        .Select(line => Encoding.UTF8.GetString(Convert.FromBase64String(line.TrimEnd('\r').Split('|')[2])))
        .Single();
    using (var commands = JsonDocument.Parse(commandPayload))
    {
        var shown = commands.RootElement.EnumerateArray().Select(command => command.GetProperty("Command").GetString()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Require(crossingGate.Commands.All(shown.Contains), "ferryman-aliases-visible-in-client-roomcommands");
        Require(shown.Contains("ask npc.81 borrow") && shown.Contains("ask npc.81 silvermere"),
            "ferryman-dialogue-visible-as-npc-specific-ask-actions");
    }

    foreach (var alias in crossingGate.Commands)
    {
        session.MoveToWorld(world, newhaven, "test-reset");
        _ = dispatcher.Dispatch(session, alias);
        Require(session.CurrentRoom.Id.Equals(pier.Id, StringComparison.OrdinalIgnoreCase), "ferryman-dispatch:" + alias);
    }
    _ = dispatcher.Dispatch(session, "south");
    Require(session.CurrentRoom.Id.Equals(silvermere.Id, StringComparison.OrdinalIgnoreCase), "pier-to-silvermere");
    _ = dispatcher.Dispatch(session, "north");
    Require(session.CurrentRoom.Id.Equals(pier.Id, StringComparison.OrdinalIgnoreCase), "silvermere-to-pier");
    _ = dispatcher.Dispatch(session, "north");
    Require(session.CurrentRoom.Id.Equals(newhaven.Id, StringComparison.OrdinalIgnoreCase), "pier-to-newhaven");

    var account = characters.GetOrCreateAccount("RoomCommandAudit");
    var catalog = characters.GetCatalog(classic.Id);
    var character = characters.CreateCharacter(account, world, "FerryAudit", "Male", catalog.Races.First().Id, catalog.Classes.First().Id);
    var pricedState = RuntimeBundleCInventoryAuthority.WriteCurrency(
        character.ItemState ?? new RuntimeItemState([], 0, new Dictionary<string, string>(), []),
        new RuntimeClassicInventoryCurrency(0, 0, 0, 0, 1000));
    var priceContext = new RuntimeQuestDialogueContext(
        character with { ItemState = pricedState },
        RuntimeItemCatalog.Load(root, classic.Id));
    priceContext.DebitCopper(375);
    Require(RuntimeBundleCInventoryAuthority.ReadCurrency(priceContext.ToCharacterRecord().ItemState!).CopperValue == 625,
        "decoded-price-debit-persists-physical-denominations");
    var persistentSession = new RuntimeSessionManager().CreateCharacterSession("ferryman-persistence", world, character);
    persistentSession.MoveToWorld(world, newhaven, "ferryman-persistence-setup");
    _ = dispatcher.Dispatch(persistentSession, "borrow skiff");
    Require(persistentSession.CurrentRoom.Id.Equals(pier.Id, StringComparison.OrdinalIgnoreCase), "persistent-ferryman-crossing");
    Require(characters.AutosaveSessions([persistentSession], DateTimeOffset.UtcNow.AddDays(1), TimeSpan.FromMilliseconds(1)) == 1,
        "persistent-ferryman-autosave");
    var saved = characters.LoadCharacter(character.CharacterId) ?? throw new InvalidOperationException("Ferryman character save missing");
    Require(saved.RoomId.Equals(pier.Id, StringComparison.OrdinalIgnoreCase), "persistent-ferryman-room-saved");
    var reconnected = new RuntimeSessionManager().CreateCharacterSession("ferryman-reconnect", world, saved);
    Require(reconnected.CurrentRoom.Id.Equals(pier.Id, StringComparison.OrdinalIgnoreCase), "persistent-ferryman-room-restored");

    var textGates = gates.Where(gate => gate.GateKind.Equals("text_command", StringComparison.OrdinalIgnoreCase)).ToArray();
    Require(textGates.Length == 249 && textGates.Sum(gate => gate.Commands.Count) == 675, "all-decoded-text-gates-and-aliases-loaded");
    foreach (var gate in textGates.Where(gate => gate.Commands.Count > 0))
    {
        var source = world.FindRoomById(gate.RoomId) ?? throw new InvalidOperationException($"Gate source room missing: {gate.ExitId}");
        session.MoveToWorld(world, source, "gate-admission-audit");
        foreach (var alias in gate.Commands)
            Require(facade.IsExactContextualCommand(session, RuntimeCommand.Parse(alias)), $"gate-alias-admitted:{gate.ExitId}:{alias}");
    }
    var unresolvedTextGates = textGates.Where(gate => gate.Commands.Count == 0
        || world.FindRoomById(gate.RoomId) is not { } source
        || world.FindExit(source, gate.Direction) is not { } exit
        || world.FindRoomById(exit.TargetRoomId) is null).ToArray();
    Require(unresolvedTextGates.Length == 1
        && unresolvedTextGates[0].ExitId.Equals("exit.map11.40.w", StringComparison.OrdinalIgnoreCase)
        && unresolvedTextGates[0].Commands.Count == 0,
        "only-source-empty-text-gate-remains-unresolved");

    var actionRooms = graph.AllRoomActionRooms();
    var supportedActions = actionRooms.SelectMany(room => facade.GetExecutableRoomActionCommands(classic.Id, room.MapNumber, room.RoomNumber)
        .Select(action => new { RoomId = $"room.map{room.MapNumber}.{room.RoomNumber}", action.TextblockId, action.Command })).ToArray();
    Require(supportedActions.Length > 0, "executable-room-actions-project-to-client-source");
    foreach (var action in supportedActions)
    {
        var source = world.FindRoomById(action.RoomId) ?? throw new InvalidOperationException($"Action source room missing: {action.RoomId}");
        session.MoveToWorld(world, source, "room-action-admission-audit");
        Require(facade.IsExactContextualCommand(session, RuntimeCommand.Parse(action.Command)),
            $"room-action-admitted:{action.TextblockId}:{action.RoomId}:{action.Command}");
    }
    var objectCommands = auditLiving.CaptureWorldObjectDefinitions().SelectMany(definition =>
        (definition.Metadata.TryGetValue("commands", out var encoded)
            ? encoded.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : Array.Empty<string>())
        .Select(command => new { RoomId = definition.RoomId, SourceId = definition.Id, Command = command })).ToArray();
    var unresolvedObjectRooms = objectCommands.Where(command => world.FindRoomById(command.RoomId) is null)
        .Select(command => command.RoomId).Distinct(StringComparer.OrdinalIgnoreCase).ToHashSet(StringComparer.OrdinalIgnoreCase);
    Require(unresolvedObjectRooms.SetEquals(["room.map1.164"]), "only-sysop-support-object-room-is-not-playable");
    foreach (var room in objectCommands.Where(command => world.FindRoomById(command.RoomId) is not null)
                 .GroupBy(command => command.RoomId, StringComparer.OrdinalIgnoreCase))
    {
        var source = world.FindRoomById(room.Key) ?? throw new InvalidOperationException($"Object source room missing: {room.Key}");
        session.MoveToWorld(world, source, "room-object-admission-audit");
        foreach (var command in room)
            Require(facade.IsExactContextualCommand(session, RuntimeCommand.Parse(command.Command)),
                $"room-object-admitted:{command.SourceId}:{command.Command}");
    }

    var expectedByRoom = gates.Where(gate => world.FindRoomById(gate.RoomId) is { } room
            && world.FindExit(room, gate.Direction) is { } exit
            && !exit.GateKind.Equals("hidden", StringComparison.OrdinalIgnoreCase))
        .SelectMany(gate => gate.Commands.Select(command => (gate.RoomId, Command: command)))
        .Concat(objectCommands.Where(command => world.FindRoomById(command.RoomId) is not null)
            .Select(command => (command.RoomId, command.Command)))
        .Concat(supportedActions.Select(command => (command.RoomId, command.Command)))
        .GroupBy(command => command.RoomId, StringComparer.OrdinalIgnoreCase);
    foreach (var room in expectedByRoom)
    {
        var source = world.FindRoomById(room.Key) ?? throw new InvalidOperationException($"Client projection source room missing: {room.Key}");
        session.MoveToWorld(world, source, "client-command-projection-audit");
        var shown = facade.GetClientRoomCommands(session).Select(command => command.Command).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var expected in room.Select(command => command.Command).Distinct(StringComparer.OrdinalIgnoreCase))
            Require(shown.Contains(expected), $"client-command-visible:{room.Key}:{expected}");
    }
    session.MoveToWorld(world, newhaven, "wrong-room-admission-audit");
    Require(!facade.IsExactContextualCommand(session, RuntimeCommand.Parse("hurl egg")), "room-action-unavailable-outside-source-room");

    var dialogueExecution = RuntimeClassicNpcDialogueExecutionAuthority.Load(root, classic.Id);
    var dispatcherContracts = new RuntimeClassicNarrativeOperationDispatcher().All;
    Require(dispatcherContracts.All(contract => RuntimeLiveGameplayFacade.IsDecodedNarrativeOpcodeLiveBound(contract.Opcode)),
        "all-decoded-opcode-contracts-have-production-live-bindings");
    var dialogueRoutes = dialogueExecution.AllRoots.SelectMany(link => graph.Require(link.RootTextblockId).Commands
            .Where(command => command.TargetTextblockId > 0)
            .Select(command => new { link.NpcId, link.RootTextblockId, command.Keyword, command.TargetTextblockId }))
        .ToArray();
    foreach (var route in dialogueRoutes)
        Require(dialogueExecution.TryResolve(new(route.NpcId, route.Keyword), out var resolved)
                && resolved.Match.RootTextblockId == route.RootTextblockId
                && resolved.Match.TargetTextblockId == route.TargetTextblockId,
            $"npc-dialogue-live-plan:{route.NpcId}:{route.RootTextblockId}:{route.Keyword}");
    var directDialogueRoutes = dialogueExecution.AllRoots
        .Where(link => graph.Require(link.RootTextblockId).Commands.Count == 0)
        .ToArray();
    Require(directDialogueRoutes.Select(route => route.RootTextblockId).Distinct().Count() == 19,
        "all-19-source-authorized-direct-dialogue-roots-covered");
    foreach (var route in directDialogueRoutes)
        Require(dialogueExecution.TryResolve(new(route.NpcId, string.Empty), out var resolved)
                && resolved.Match.RootTextblockId == route.RootTextblockId,
            $"npc-dialogue-direct-greet-plan:{route.NpcId}:{route.RootTextblockId}");
    var runtimeIndex = args.Length > 1
        ? Path.GetFullPath(args[1])
        : Path.Combine(Path.GetTempPath(), "realmgate-room-command-runtime-index.json");
    var gateAliases = gates.SelectMany(gate => gate.Commands.Select(command => new { RoomId = gate.RoomId, SourceId = gate.ExitId, Command = command })).ToArray();
    File.WriteAllText(runtimeIndex, JsonSerializer.Serialize(new { SupportedRoomActions = supportedActions, GateAliases = gateAliases, ObjectCommands = objectCommands, DialogueRoutes = dialogueRoutes, DirectDialogueRoutes = directDialogueRoutes }));
    Console.WriteLine($"PASS {checks} checks; text gates={textGates.Length}; gate aliases={textGates.Sum(gate => gate.Commands.Count)}; object aliases={objectCommands.Length}; action rooms={actionRooms.Count}; executable action aliases={supportedActions.Length}; dialogue routes={dialogueRoutes.Length}; direct dialogue routes={directDialogueRoutes.Length}");
    return 0;
}
finally
{
    if (Directory.Exists(testState)) Directory.Delete(testState, recursive: true);
}

void Require(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException(name);
    checks++;
}
