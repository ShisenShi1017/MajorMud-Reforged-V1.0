namespace Reforged.Server;

public sealed partial class ServerRuntimeHost
{
    private readonly string _projectRoot;
    private readonly ServerRuntimeOptions _options;
    private readonly RuntimeWorldLoader _worldLoader = new();
    private readonly Dictionary<string, RuntimeWorld> _worlds = new(StringComparer.OrdinalIgnoreCase);
    private readonly RuntimeStateStore _state = new();
    private readonly RuntimeEventDispatcher _events = new();
    private readonly RuntimeSessionManager _sessions;
    private readonly RuntimeCharacterService _characters;
    private readonly ServerTickScheduler _scheduler = new();
    private readonly RuntimeLiveGameplayFacade _gameplay;
    private readonly List<RuntimeClassicAutoCleanupScheduler> _classicAutoCleanup = new();
    private RuntimeCommandDispatcher _commands;

    public ServerRuntimeHost(string projectRoot, ServerRuntimeOptions options)
    {
        _projectRoot = projectRoot;
        _options = options;
        _characters = new RuntimeCharacterService(projectRoot);
        _sessions = new RuntimeSessionManager(_state, _events, _options.Development, _characters);
        _gameplay = new RuntimeLiveGameplayFacade(_characters, _sessions);
        _commands = new RuntimeCommandDispatcher(() => _worlds, _sessions, _state, _events, _gameplay);
        _gameplay.BindClassicPendingCommandExecutor((session, input) => _commands.Dispatch(session, input));
    }

    public IReadOnlyDictionary<string, RuntimeWorld> Worlds => _worlds;
    public RuntimeSessionManager Sessions => _sessions;
    public RuntimeStateStore State => _state;
    public RuntimeEventDispatcher Events => _events;
    public RuntimeCharacterService Characters => _characters;

    public GeneratedWorldSmokeReport CreateSmokeReport()
    {
        return new GeneratedWorldSmokeReport
        {
            WorldCount = _worlds.Count,
            Worlds = _worlds.Values.Select(world =>
            {
                var startRoom = world.ResolveGeneratedCharacterStartRoom() ?? world.ResolveDiagnosticRoom();
                var diagnostics = world.DataCatalog.Diagnostics.ToList();
                diagnostics.AddRange(world.CommandCatalog.Diagnostics);
                if (world.CommandCatalog.QuarantinedGeneratedCommandCount > 0)
                {
                    diagnostics.Add($"{world.CommandCatalog.QuarantinedGeneratedCommandCount} generated command records remain evidence-only or lack an executable dispatch owner.");
                }
                diagnostics.AddRange(world.StatusEffectCatalog.Diagnostics.Select(x => $"status-effect:{x.Code}:{x.Message}"));
                if (world.StatusEffectCatalog.QuarantinedCount > 0)
                {
                    diagnostics.Add($"{world.StatusEffectCatalog.QuarantinedCount} generated status-effect records remain relationship-only pending native effect semantics.");
                }
                diagnostics.AddRange(world.LootRuleCatalog.Diagnostics.Select(x => $"loot-rule:{x.Code}:{x.Message}"));
                if (world.LootRuleCatalog.QuarantinedCount > 0)
                {
                    diagnostics.Add($"{world.LootRuleCatalog.QuarantinedCount} generated loot-rule records remain catalog-only pending executable native drop semantics.");
                }
                if (startRoom is null)
                {
                    diagnostics.Add("No explicit generated character-start room is available and no diagnostic room is configured.");
                }
                return new GeneratedWorldSmokeWorld
                {
                    Id = world.Definition.Id,
                    Name = world.Definition.Name,
                    Type = world.Definition.Type,
                    RoomCount = world.RoomCount,
                    DataRoot = world.DataCatalog.DataRoot,
                    FamilyCount = world.DataCatalog.Families.Count,
                    TotalGeneratedRecords = world.DataCatalog.Families.Values.Sum(family => Math.Max(0, family.RecordCount)),
                    StartRoomId = startRoom?.Id ?? string.Empty,
                    StartRoomName = startRoom?.Name ?? string.Empty,
                    StartRoomNumber = startRoom?.RoomNumber ?? 0,
                    StartRoomExitCount = startRoom?.Exits.Count ?? 0,
                    StartRoomResolved = startRoom is not null && !string.Equals(startRoom.Name, "Unknown Room", StringComparison.OrdinalIgnoreCase),
                    StartRoomExits = startRoom?.Exits.Select(exit => $"{exit.Direction}->{exit.TargetRoomNumber}").ToArray() ?? Array.Empty<string>(),
                    Diagnostics = diagnostics.ToArray()
                };
            }).ToArray()
        };
    }



    public RuntimeNpcSmokeReport CreateNpcSmokeReport()
    {
        return new RuntimeNpcSmokeReport
        {
            Worlds = _worlds.Values.Select(world =>
            {
                var availablePrimaryRooms = world.Spawns.Count(spawn => spawn.PrimaryRoomAvailable);
                var unavailablePrimaryRooms = world.SpawnCount - availablePrimaryRooms;
                var missingMapIdentity = world.Spawns.Count(spawn => spawn.SourceMapId <= 0 || spawn.SourceRoomNumber <= 0);
                var linkedPopulationRecords = world.Spawns.Count(spawn => spawn.HasLinkedPopulationArea);
                var mapQualifiedLinkedPopulationRecords = world.Spawns.Count(spawn => spawn.HasMapQualifiedPopulationArea);
                var missingLinkedMapContext = linkedPopulationRecords - mapQualifiedLinkedPopulationRecords;
                var diagnostics = new List<string>();
                if (world.UnresolvedGroupCount > 0)
                {
                    diagnostics.Add($"{world.UnresolvedGroupCount} spawn records reference monster group IDs without generated NPC group members.");
                }
                if (world.UnresolvedBossCount > 0)
                {
                    diagnostics.Add($"{world.UnresolvedBossCount} spawn records reference boss legacy IDs without generated NPC definitions.");
                }
                if (missingMapIdentity > 0)
                {
                    diagnostics.Add($"{missingMapIdentity} spawn records lack map-qualified primary room identity.");
                }
                if (missingLinkedMapContext > 0)
                {
                    diagnostics.Add($"{missingLinkedMapContext} linked population records lack map-qualified population identity.");
                }
                if (unavailablePrimaryRooms > 0)
                {
                    diagnostics.Add($"{unavailablePrimaryRooms} map-qualified spawn rooms are outside the currently generated room slice; their identities remain valid and will attach when those maps are emitted.");
                }

                return new RuntimeNpcSmokeWorld
                {
                    Id = world.Definition.Id,
                    NpcDefinitionCount = world.NpcCount,
                    SpawnCount = world.SpawnCount,
                    GroupSpawnCount = world.GroupSpawnCount,
                    BossSpawnCount = world.BossSpawnCount,
                    ResolvedSpawnCount = world.ResolvedSpawnCount,
                    UnresolvedSpawnCount = world.UnresolvedSpawnCount,
                    UnresolvedGroupCount = world.UnresolvedGroupCount,
                    UnresolvedBossCount = world.UnresolvedBossCount,
                    SpawnRoomCount = world.SpawnsByRoom.Count,
                    AvailablePrimaryRoomCount = availablePrimaryRooms,
                    UnavailablePrimaryRoomCount = unavailablePrimaryRooms,
                    MissingMapIdentityCount = missingMapIdentity,
                    LinkedPopulationRecordCount = linkedPopulationRecords,
                    MapQualifiedLinkedPopulationCount = mapQualifiedLinkedPopulationRecords,
                    MissingLinkedMapContextCount = missingLinkedMapContext,
                    Diagnostics = diagnostics
                };
            }).ToArray()
        };
    }

    public RuntimeRoomExitSmokeReport CreateRoomExitSmokeReport()
    {
        var worlds = _worlds.Values.Select(world =>
        {
            var startRoom = world.ResolveDiagnosticRoom() ?? world.ResolveGeneratedCharacterStartRoom();
            var movementChecks = new List<RuntimeRoomExitMovementCheck>();
            foreach (var exit in startRoom?.Exits ?? Array.Empty<RuntimeExit>())
            {
                var target = exit.HasBlockedDestination ? null : world.FindRoom(exit.TargetMapId, exit.TargetRoomNumber);
                movementChecks.Add(new RuntimeRoomExitMovementCheck
                {
                    Direction = exit.Direction,
                    TargetRoomNumber = exit.TargetRoomNumber,
                    TargetRoomId = target?.Id ?? string.Empty,
                    TargetRoomName = target?.Name ?? string.Empty,
                    Resolved = target is not null && !string.Equals(target.Name, "Unknown Room", StringComparison.OrdinalIgnoreCase)
                });
            }

            return new RuntimeRoomExitSmokeWorld
            {
                Id = world.Definition.Id,
                Name = world.Definition.Name,
                StartRoomId = startRoom?.Id ?? string.Empty,
                StartRoomName = startRoom?.Name ?? string.Empty,
                StartRoomNumber = startRoom?.RoomNumber ?? 0,
                StartRoomExitCount = startRoom?.Exits.Count ?? 0,
                MovementChecks = movementChecks.ToArray()
            };
        }).ToArray();

        return new RuntimeRoomExitSmokeReport { Worlds = worlds };
    }

    public void Bootstrap()
    {
        foreach (var worldDefinition in _options.Worlds.Where(world => world.RuntimeEnabled))
        {
            EnforceClassicBoundaries(worldDefinition);
            var world = _worldLoader.Load(_projectRoot, worldDefinition);
            _worlds[worldDefinition.Id] = world;
            _state.EnsureWorld(world);
            _events.Publish("world", "loaded", world.Definition.Id, null, null, $"rooms={world.RoomCount}; npcs={world.NpcCount}; spawns={world.SpawnCount}; generatedFamilies={world.DataCatalog.Families.Count}; rules={world.Definition.RulesProfile}; ai={world.Definition.AiMode}");
        }

        if (_worlds.Count == 0)
        {
            throw new InvalidOperationException("No runtime-enabled worlds are configured.");
        }

        _scheduler.Register("world-tick", TimeSpan.FromMilliseconds(Math.Max(100, _options.WorldTickMilliseconds)), token => _gameplay.TickLivingWorldsAsync(token));
        _scheduler.Register("combat-tick", TimeSpan.FromMilliseconds(Math.Max(100, _options.CombatTickMilliseconds)), token => _gameplay.TickLiveCombatsAsync(token));
        _scheduler.Register("spell-system-tick", TimeSpan.FromMilliseconds(RuntimeLiveGameplayFacade.ClassicSpellSystemRoundMilliseconds), token => _gameplay.TickLiveStatusesAsync(token));
        _classicAutoCleanup.Clear();
        var cleanupBootLocal = DateTimeOffset.Now;
        foreach (var classicWorld in _worlds.Values.Where(world => string.Equals(world.Definition.Type, "Classic", StringComparison.OrdinalIgnoreCase)))
            _classicAutoCleanup.Add(new RuntimeClassicAutoCleanupScheduler(
                _characters,
                classicWorld.Definition.Id,
                cleanupBootLocal,
                gangHouseWorldCleanup: () => _gameplay.ApplyClassicGangHouseDailyCleanup(classicWorld)));
        _scheduler.Register("classic-mcurou-probe", TimeSpan.FromMinutes(1), _ =>
        {
            var nowLocal = DateTimeOffset.Now;
            foreach (var cleanup in _classicAutoCleanup) cleanup.TryRun(nowLocal);
            return Task.CompletedTask;
        });

        _scheduler.Register("session-heartbeat", TimeSpan.FromSeconds(30), _ => Task.CompletedTask);
        _scheduler.Register("character-autosave", RuntimeClassicTimingAuthority.PlayerDataSaveInterval, _ =>
        {
            _characters.AutosaveSessions(_sessions.ActiveSessions, DateTimeOffset.UtcNow, RuntimeClassicTimingAuthority.PlayerDataSaveInterval);
            return Task.CompletedTask;
        });
    }

    public async Task RunLocalConsoleAsync(CancellationToken cancellationToken)
    {
        if (!_worlds.TryGetValue(_options.DefaultWorldId, out var world))
        {
            world = _worlds.Values.First();
        }

        var session = _sessions.CreateSession("local-console", world, "LocalConsole");
        _sessions.TryAdd(session, int.MaxValue, out _);
        _events.Publish("runtime", "local-console-started", world.Definition.Id, session.Id, session.Actor.Id, "host=authoritative");
        Console.WriteLine(_options.HostName);
        Console.WriteLine($"World: {world.Definition.Name} [{world.Definition.Type}] rules={world.Definition.RulesProfile} ai={world.Definition.AiMode}");
        Console.WriteLine("Local console session. Host is authoritative. Type 'help' or 'quit'.");
        Console.WriteLine($"Actor: {session.Actor.DisplayName} [{session.Actor.Kind}] persistentCharacter={session.Actor.IsPersistentCharacter} spawn={session.SpawnSource}");
        Console.WriteLine(RoomRenderer.Render(session.World, session.CurrentRoom));

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var schedulerTask = _scheduler.RunAsync(linked.Token);
        var gracefulSessionExit = false;

        try
        {
            while (!linked.IsCancellationRequested)
            {
                if (!session.HasClassicModalInput) Console.Write("> ");
                var input = Console.ReadLine();
                if (input is null) break;
                if (_commands.RequestsSessionExit(session, input))
                {
                    gracefulSessionExit = true;
                    break;
                }

                var response = _commands.Dispatch(session, input);
                if (!string.IsNullOrWhiteSpace(response))
                {
                    if (session.HasClassicModalInput) Console.Write(response);
                    else Console.WriteLine(response);
                }

                await Task.Yield();
            }
        }
        finally
        {
            if (_sessions.ActiveSessions.Any(active => active.Id.Equals(session.Id, StringComparison.OrdinalIgnoreCase)))
            {
                if (gracefulSessionExit) _commands.CompleteGracefulSessionExit(session);
                else _commands.CompleteUnexpectedSessionExit(session);
            }
            linked.Cancel();
            try
            {
                await schedulerTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected during local console shutdown.
            }
        }
    }

    public async Task RunNetworkAsync(CancellationToken cancellationToken)
    {
        if (!_options.Network.Enabled)
        {
            Console.WriteLine("Network runtime is disabled in Config/server.worlds.json. Starting anyway because --run-network was explicitly requested.");
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            linked.Cancel();
        };

        var schedulerTask = _scheduler.RunAsync(linked.Token);
        var network = new ServerNetworkHost(_options, () => _worlds, _commands, _sessions, _characters);
        var networkTask = network.RunTelnetAsync(linked.Token);

        Console.WriteLine("Runtime network host is running. Press Ctrl+C to stop.");
        try
        {
            await Task.WhenAny(schedulerTask, networkTask).ConfigureAwait(false);
            linked.Cancel();
            await Task.WhenAll(schedulerTask, networkTask).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected during host shutdown.
        }
    }

    private static void EnforceClassicBoundaries(WorldRuntimeDefinition definition)
    {
        if (!string.Equals(definition.Type, "Classic", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (definition.TransfersAllowed)
        {
            throw new InvalidOperationException($"Classic world '{definition.Id}' cannot allow character transfers.");
        }

        if (!definition.ClassicProtected)
        {
            throw new InvalidOperationException($"Classic world '{definition.Id}' must remain protected.");
        }

        if (!string.Equals(definition.AiMode, "Off", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Classic world '{definition.Id}' cannot enable AI runtime modes during classic-first implementation.");
        }
    }
}
