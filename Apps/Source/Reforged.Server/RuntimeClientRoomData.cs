namespace Reforged.Server;

public sealed partial class RuntimeLiveGameplayFacade
{
    public string RenderClientRoomData(RuntimeSession session, RuntimeCommand command)
    {
        var includeHidden = command.Arguments.Any(argument => argument.Equals("hidden", StringComparison.OrdinalIgnoreCase));
        if (command.Arguments.FirstOrDefault()?.Equals("map", StringComparison.OrdinalIgnoreCase) == true)
        {
            if (command.Arguments.Count < 2 || !int.TryParse(command.Arguments[1], out var requestedMap))
                return ReforgedClientProtocol.Encode("ROOMINSPECTERROR", "Enter a decoded map number.");
            var root = session.World.AllRooms().Where(room => room.MapId == requestedMap).OrderBy(room => room.RoomNumber).FirstOrDefault();
            if (root is null) return ReforgedClientProtocol.Encode("ROOMINSPECTERROR", $"Map {requestedMap} does not exist.");
            return ReforgedClientProtocol.Encode("ROOMTOPOLOGY", System.Text.Json.JsonSerializer.Serialize(BuildClientRoomTopology(session, root, 64)));
        }
        if (command.Arguments.FirstOrDefault()?.Equals("inspect", StringComparison.OrdinalIgnoreCase) != true)
            return RenderClientRoomData(session, includeHidden);
        var key = command.Arguments.Skip(1).FirstOrDefault(argument => !argument.Equals("hidden", StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
        var parts = key.Split(':', '/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !int.TryParse(parts[0], out var mapId) || !int.TryParse(parts[1], out var roomNumber) ||
            session.World.FindRoom(mapId, roomNumber) is not { } room)
            return ReforgedClientProtocol.Encode("ROOMINSPECTERROR", "Enter an exact room key as map:room, for example 1:2189.");
        return RenderClientRoomInspection(session, room, includeHidden);
    }

    public string RenderClientRoomData(RuntimeSession session, bool includeHiddenObjects)
    {
        var living = LivingNpcsInRoom(session).ToArray();
        var npcDetails = living.Select(npc => BuildClientNpcState(session, npc)).ToArray();
        var lines = new List<string>
        {
            $"Map {session.CurrentRoom.MapId} Room {session.CurrentRoom.RoomNumber} - {session.CurrentRoom.Name}",
            session.CurrentRoom.Description
        };
        var npcNames = living.Select(npc => npc.Name).Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
        if (npcNames.Length > 0) lines.Add($"Also here: {string.Join(", ", npcNames)}.");
        var playerNames = _sessions.InRoom(session.World.Definition.Id, session.CurrentRoom.Id)
            .Where(other => !other.Id.Equals(session.Id, StringComparison.OrdinalIgnoreCase))
            .Select(other => other.Actor.DisplayName).Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
        if (playerNames.Length > 0) lines.Add($"Players: {string.Join(", ", playerNames)}");

        var items = LoadClientMapGround(session, includeHiddenObjects).ToArray();
        var itemCatalog = RuntimeItemCatalog.Load(_characters.ProjectRoot, session.World.Definition.Id);
        var itemDetails = Living(session).GroundItems(session.CurrentRoom.Id)
            .Where(item => includeHiddenObjects || !RuntimeClassicHiddenSearchService.IsHidden(item))
            .GroupBy(item => item.InstanceId, StringComparer.OrdinalIgnoreCase).Select(group => group.First())
            .Select(item =>
            {
                var definition = itemCatalog.Require(item.CanonicalItemId);
                return new
                {
                    item.InstanceId, item.CanonicalItemId, definition.Name, definition.Weight, definition.Gettable,
                    Quantity = item.StackQuantity, Hidden = RuntimeClassicHiddenSearchService.IsHidden(item)
                };
            }).ToArray();
        if (items.Length > 0) lines.Add($"Items: {string.Join(", ", items)}");
        var currency = RuntimeClassicRoomCurrencyStore.Get(_characters.ProjectRoot, session.World.Definition.Id, session.CurrentRoom.Id);
        if (!currency.IsEmpty) lines.Add($"You notice {RuntimeClassicRoomCurrencyStore.Format(currency)} here.");
        var exits = session.CurrentRoom.Exits.Where(exit => CanCharacterSeeExit(session, exit))
            .Select(exit => exit.Direction).Where(direction => !string.IsNullOrWhiteSpace(direction)).ToArray();
        lines.Add($"Obvious exits: {(exits.Length == 0 ? "none" : string.Join(", ", exits))}");
        var links = session.CurrentRoom.Exits
            .Where(exit => CanCharacterSeeExit(session, exit) && !exit.HasBlockedDestination)
            .Select(exit =>
            {
                var target = session.World.FindRoomById(exit.TargetRoomId) ?? session.World.FindRoom(exit.TargetMapId, exit.TargetRoomNumber);
                return new
                {
                    Direction = exit.Direction,
                    RoomKey = $"{exit.TargetMapId}:{exit.TargetRoomNumber}",
                    RoomName = target?.Name ?? "Unknown destination"
                };
            }).ToArray();
        var roomObjectCommands = Living(session).CaptureWorldObjectDefinitions()
            .Where(definition => definition.RoomId.Equals(session.CurrentRoom.Id, StringComparison.OrdinalIgnoreCase))
            .SelectMany(definition => (definition.Metadata.TryGetValue("commands", out var encoded)
                    ? encoded.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    : Array.Empty<string>())
                .Select(command => new
                {
                    Command = command,
                    Source = definition.Id,
                    InteractionType = definition.Kind,
                    Requirements = DescribeRoomCommandRequirements(definition.Metadata)
                }));
        var roomGateCommands = Living(session).CaptureGates()
                .Where(gate => gate.RoomId.Equals(session.CurrentRoom.Id, StringComparison.OrdinalIgnoreCase))
                .SelectMany(gate => gate.Commands.Select(command => new
                {
                    Command = command,
                    Source = gate.ExitId,
                    InteractionType = gate.GateKind,
                    Requirements = DescribeRoomGateRequirements(gate)
                }));
        var roomCommands = roomObjectCommands.Concat(roomGateCommands)
            .GroupBy(entry => entry.Command, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(entry => entry.Command, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var topology = BuildClientRoomTopology(session, session.CurrentRoom, 64);
        var mapCatalog = session.World.AllRooms().GroupBy(room => room.MapId).OrderBy(group => group.Key)
            .Select(group => new { MapId = group.Key, RoomCount = group.Count() }).ToArray();
        var roomServices = RuntimeServiceCatalog.Load(_characters.ProjectRoot, session.World.Definition.Id).Services
            .Where(service => TryReadRoomNumber(service.GeneratedRecord, out var roomNumber) && roomNumber == session.CurrentRoom.RoomNumber)
            .Select(service => new { service.Id, Kind = ReadServiceKind(service), Name = ReadServiceName(service) }).ToArray();
        return ReforgedClientProtocol.Encode("ROOMDATA", string.Join(Environment.NewLine, lines)) + Environment.NewLine +
               ReforgedClientProtocol.Encode("ROOMSTATE", System.Text.Json.JsonSerializer.Serialize(new
               {
                   RoomKey = $"{session.CurrentRoom.MapId}:{session.CurrentRoom.RoomNumber}",
                   session.CurrentRoom.EnvironmentLightLevel,
                   session.CurrentRoom.EnvironmentRoomType
               })) + Environment.NewLine +
               ReforgedClientProtocol.Encode("ROOMNPCS", System.Text.Json.JsonSerializer.Serialize(npcDetails)) + Environment.NewLine +
               ReforgedClientProtocol.Encode("ROOMSERVICES", System.Text.Json.JsonSerializer.Serialize(roomServices)) + Environment.NewLine +
               ReforgedClientProtocol.Encode("ROOMITEMS", System.Text.Json.JsonSerializer.Serialize(itemDetails)) + Environment.NewLine +
               ReforgedClientProtocol.Encode("ROOMLINKS", System.Text.Json.JsonSerializer.Serialize(links)) + Environment.NewLine +
               ReforgedClientProtocol.Encode("ROOMCOMMANDS", System.Text.Json.JsonSerializer.Serialize(roomCommands)) + Environment.NewLine +
               ReforgedClientProtocol.Encode("ROOMTOPOLOGY", System.Text.Json.JsonSerializer.Serialize(topology)) + Environment.NewLine +
               ReforgedClientProtocol.Encode("MAPCATALOG", System.Text.Json.JsonSerializer.Serialize(mapCatalog));
    }

    private string RenderClientRoomInspection(RuntimeSession session, RuntimeRoom room, bool includeHidden)
    {
        var living = Living(session);
        var npcs = living.Capture().Npcs.Where(npc => npc.RoomId.Equals(room.Id, StringComparison.OrdinalIgnoreCase))
            .Select(npc => BuildClientNpcState(session, npc)).ToArray();
        var players = _sessions.InRoom(session.World.Definition.Id, room.Id).Select(active => active.Actor.DisplayName)
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
        var catalog = RuntimeItemCatalog.Load(_characters.ProjectRoot, session.World.Definition.Id);
        var items = living.GroundItems(room.Id).Where(item => includeHidden || !RuntimeClassicHiddenSearchService.IsHidden(item))
            .Select(item =>
            {
                string name;
                try { name = catalog.Require(item.CanonicalItemId).Name; } catch { name = item.CanonicalItemId; }
                return RuntimeClassicHiddenSearchService.IsHidden(item) ? $"[hidden] {name}" : name;
            }).ToArray();
        var currency = RuntimeClassicRoomCurrencyStore.Get(_characters.ProjectRoot, session.World.Definition.Id, room.Id);
        var exits = room.Exits.Select(exit => new
        {
            exit.Direction, RoomKey = $"{exit.TargetMapId}:{exit.TargetRoomNumber}",
            RoomName = (session.World.FindRoomById(exit.TargetRoomId) ?? session.World.FindRoom(exit.TargetMapId, exit.TargetRoomNumber))?.Name ?? "Unknown",
            exit.GateKind, exit.HasBlockedDestination
        }).ToArray();
        var expectedSpawns = session.World.Spawns.Where(spawn => spawn.ResolvedRuntimeRoomId.Equals(room.Id, StringComparison.OrdinalIgnoreCase))
            .Select(spawn => new { spawn.DisplayName, spawn.RespawnMinutes, spawn.MaximumAreaPopulation }).ToArray();
        var commands = living.CaptureWorldObjectDefinitions().Where(definition => definition.RoomId.Equals(room.Id, StringComparison.OrdinalIgnoreCase))
            .SelectMany(definition => definition.Metadata.TryGetValue("commands", out var encoded)
                ? encoded.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) : Array.Empty<string>())
            .Concat(living.CaptureGates().Where(gate => gate.RoomId.Equals(room.Id, StringComparison.OrdinalIgnoreCase)).SelectMany(gate => gate.Commands))
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();
        var snapshot = new
        {
            RoomKey = $"{room.MapId}:{room.RoomNumber}", room.Name, room.Description,
            Npcs = npcs, Players = players, Items = items,
            Currency = currency.IsEmpty ? string.Empty : RuntimeClassicRoomCurrencyStore.Format(currency),
            Exits = exits, Commands = commands, ExpectedSpawns = expectedSpawns,
            room.ImportedShopNumber, room.EnvironmentLightLevel, room.EnvironmentRoomType,
            CapturedAt = DateTimeOffset.UtcNow
        };
        return ReforgedClientProtocol.Encode("ROOMINSPECTION", System.Text.Json.JsonSerializer.Serialize(snapshot));
    }

    private static object BuildClientNpcState(RuntimeSession session, RuntimeLivingNpcState npc)
    {
        var definition = session.World.Npcs.Values.FirstOrDefault(candidate =>
            candidate.Id.Equals(npc.NpcId, StringComparison.OrdinalIgnoreCase));
        return new
        {
            npc.InstanceId,
            CanonicalNpcId = npc.NpcId,
            LegacyNpcId = definition?.LegacyId ?? 0,
            npc.Name,
            npc.CurrentHitPoints,
            npc.MaximumHitPoints,
            npc.Alive,
            npc.RespawnAtUtc,
            Disposition = ResolveLiveNpcDisposition(npc),
            InteractionModel = npc.InteractionModel,
            Level = definition?.Level ?? 0,
            ExperienceReward = definition?.ExperienceReward ?? 0L
        };
    }

    private object BuildClientRoomTopology(RuntimeSession session, RuntimeRoom rootRoom, int maximumDepth)
    {
        var gates = Living(session).CaptureGates();
        var livingSnapshot = Living(session).Capture();
        var objectDefinitions = Living(session).CaptureWorldObjectDefinitions();
        var nodes = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        var links = new List<object>();
        var queue = new Queue<(RuntimeRoom Room, int Depth)>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { rootRoom.Id };
        queue.Enqueue((rootRoom, 0));
        while (queue.Count > 0 && nodes.Count < 600)
        {
            var (room, depth) = queue.Dequeue();
            var roomPlayers = _sessions.InRoom(session.World.Definition.Id, room.Id)
                .Where(active => !active.Id.Equals(session.Id, StringComparison.OrdinalIgnoreCase))
                .Select(active => active.Actor.DisplayName).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var liveNpcs = livingSnapshot.Npcs.Where(npc => npc.RoomId.Equals(room.Id, StringComparison.OrdinalIgnoreCase) && npc.Alive)
                .Select(npc => npc.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var roomSpawns = session.World.Spawns.Where(spawn => spawn.ResolvedRuntimeRoomId.Equals(room.Id, StringComparison.OrdinalIgnoreCase)).ToArray();
            var expectedMonsters = roomSpawns.SelectMany(spawn => spawn.BossDefinition is not null
                    ? new[] { spawn.BossDefinition.Name }
                    : spawn.GroupDefinitions.Select(definition => definition.Name))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var bossNames = roomSpawns.Where(spawn => spawn.BossDefinition is not null).Select(spawn => spawn.BossDefinition!.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var bossAlive = bossNames.Any(name => liveNpcs.Contains(name, StringComparer.OrdinalIgnoreCase));
            var hasCommands = objectDefinitions.Any(definition => definition.RoomId.Equals(room.Id, StringComparison.OrdinalIgnoreCase) && definition.Metadata.ContainsKey("commands")) ||
                              gates.Any(gate => gate.RoomId.Equals(room.Id, StringComparison.OrdinalIgnoreCase) && gate.Commands.Count > 0);
            nodes[room.Id] = new
            {
                RoomKey = $"{room.MapId}:{room.RoomNumber}", RoomName = room.Name, Depth = depth, Current = room.Id.Equals(session.CurrentRoom.Id, StringComparison.OrdinalIgnoreCase),
                Players = roomPlayers, LiveNpcs = liveNpcs, ExpectedMonsters = expectedMonsters, BossNames = bossNames,
                BossAlive = bossAlive, IsBossLair = bossNames.Length > 0 || room.EnvironmentLairId > 0,
                HasShop = room.ImportedShopNumber is > 0, HasRoomCommand = hasCommands,
                RoomType = room.EnvironmentRoomType
            };
            if (depth >= maximumDepth) continue;
            foreach (var exit in room.Exits)
            {
                var target = session.World.FindRoomById(exit.TargetRoomId) ?? session.World.FindRoom(exit.TargetMapId, exit.TargetRoomNumber);
                var targetKey = $"{exit.TargetMapId}:{exit.TargetRoomNumber}";
                var gate = gates.FirstOrDefault(candidate => candidate.RoomId.Equals(room.Id, StringComparison.OrdinalIgnoreCase) &&
                                                            candidate.Direction.Equals(exit.Direction, StringComparison.OrdinalIgnoreCase));
                var reverse = target?.Exits.Any(candidate => candidate.TargetRoomId.Equals(room.Id, StringComparison.OrdinalIgnoreCase)) == true;
                links.Add(new
                {
                    SourceRoomKey = $"{room.MapId}:{room.RoomNumber}", TargetRoomKey = targetKey,
                    Direction = exit.Direction, GateKind = gate?.GateKind ?? exit.GateKind,
                    Hidden = (gate?.GateKind ?? exit.GateKind).Equals("hidden", StringComparison.OrdinalIgnoreCase),
                    OneWay = !reverse, Blocked = exit.HasBlockedDestination
                });
                if (target is not null && visited.Add(target.Id) && target.MapId == rootRoom.MapId)
                    queue.Enqueue((target, depth + 1));
            }
        }
        return new { Nodes = nodes.Values.ToArray(), Links = links.ToArray() };
    }

    private static string DescribeRoomCommandRequirements(IReadOnlyDictionary<string, string> metadata)
    {
        var values = metadata
            .Where(entry => entry.Key.StartsWith("required_", StringComparison.OrdinalIgnoreCase) ||
                            entry.Key.Equals("skill_modifier", StringComparison.OrdinalIgnoreCase))
            .Select(entry => $"{entry.Key}={entry.Value}")
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return values.Length == 0 ? "None decoded" : string.Join("; ", values);
    }

    private static string DescribeRoomGateRequirements(RuntimeExitGateDefinition gate)
    {
        var values = new List<string>();
        if (!string.IsNullOrWhiteSpace(gate.Qualifier)) values.Add(gate.Qualifier);
        if (!string.IsNullOrWhiteSpace(gate.RequiredItemId)) values.Add($"item={gate.RequiredItemId}");
        if (gate.TollAmount > 0) values.Add($"toll={gate.TollAmount}");
        if (gate.Damage > 0) values.Add($"damage={gate.Damage}");
        if (gate.SpellId > 0) values.Add($"spell={gate.SpellId}");
        return values.Count == 0 ? "None decoded" : string.Join("; ", values);
    }

    private IEnumerable<string> LoadClientMapGround(RuntimeSession session, bool includeHiddenObjects)
    {
        var catalog = RuntimeItemCatalog.Load(_characters.ProjectRoot, session.World.Definition.Id);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        IEnumerable<RuntimeItemInstance> candidates = Living(session).GroundItems(session.CurrentRoom.Id);
        foreach (var active in _sessions.ActiveSessions.Where(active =>
                     active.World.Definition.Id.Equals(session.World.Definition.Id, StringComparison.OrdinalIgnoreCase)))
        {
            if (active.Actor is not RuntimeCharacterActor actor) continue;
            var state = _characters.LoadCharacter(actor.Id)?.ItemState;
            if (state is null) continue;
            candidates = candidates.Concat(state.Items.Where(item =>
                !RuntimeClassicRoomCurrencyStore.IsLegacyCurrency(item) &&
                item.LocationKind.Equals(RuntimeItemLocations.Room, StringComparison.OrdinalIgnoreCase) &&
                item.LocationId.Equals(session.CurrentRoom.Id, StringComparison.OrdinalIgnoreCase)));
        }
        foreach (var item in candidates.Where(item => seen.Add(item.InstanceId)))
        {
            var hidden = RuntimeClassicHiddenSearchService.IsHidden(item);
            if (!includeHiddenObjects && hidden) continue;
            string name;
            try { name = catalog.Require(item.CanonicalItemId).Name; }
            catch { continue; }
            yield return hidden ? $"[hidden] {name}" : name;
        }
    }
}
