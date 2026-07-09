namespace Reforged.Server;

public sealed class ServerRuntimeHost
{
    private readonly string _projectRoot;
    private readonly ServerRuntimeOptions _options;
    private readonly RuntimeWorldLoader _worldLoader = new();
    private readonly Dictionary<string, RuntimeWorld> _worlds = new(StringComparer.OrdinalIgnoreCase);
    private readonly RuntimeStateStore _state = new();
    private readonly RuntimeEventDispatcher _events = new();
    private readonly RuntimeEntityRegistry _entities = new();
    private readonly IRuntimeAccountRepository _accounts = new InMemoryRuntimeAccountRepository();
    private readonly IRuntimeCharacterRepository _characters = new InMemoryRuntimeCharacterRepository();
    private readonly SpawnSchedulingFoundation _spawnScheduling = new();
    private readonly RuntimeEventSchedulingFoundation _eventScheduling = new();
    private readonly NpcLifecycleFoundation _npcLifecycle = new();
    private readonly CombatLoopFoundation _combatLoop = new();
    private readonly MagicCastPipelineFoundation _magicPipeline = new();
    private readonly RuntimeSessionManager _sessions;
    private readonly ServerTickScheduler _scheduler = new();
    private RuntimeCommandDispatcher _commands;

    public ServerRuntimeHost(string projectRoot, ServerRuntimeOptions options)
    {
        _projectRoot = projectRoot;
        _options = options;
        _sessions = new RuntimeSessionManager(_state, _events, _entities);
        _commands = new RuntimeCommandDispatcher(() => _worlds, _sessions, _state, _events, _entities, _characters, _magicPipeline, _npcLifecycle, _combatLoop);
    }

    public IReadOnlyDictionary<string, RuntimeWorld> Worlds => _worlds;
    public RuntimeSessionManager Sessions => _sessions;
    public RuntimeStateStore State => _state;
    public RuntimeEventDispatcher Events => _events;
    public RuntimeEntityRegistry Entities => _entities;

    public void Bootstrap()
    {
        foreach (var worldDefinition in _options.Worlds.Where(world => world.RuntimeEnabled))
        {
            EnforceClassicBoundaries(worldDefinition);
            var world = _worldLoader.Load(_projectRoot, worldDefinition);
            _worlds[worldDefinition.Id] = world;
            _state.EnsureWorld(world);
            _events.Publish("world", "loaded", world.Definition.Id, null, null, $"rooms={world.RoomCount}; rules={world.Definition.RulesProfile}; ai={world.Definition.AiMode}");
        }

        if (_worlds.Count == 0)
        {
            throw new InvalidOperationException("No runtime-enabled worlds are configured.");
        }

        _scheduler.Register("world-tick", TimeSpan.FromMilliseconds(Math.Max(100, _options.WorldTickMilliseconds)), _ => Task.CompletedTask);
        _scheduler.Register("session-heartbeat", TimeSpan.FromSeconds(30), _ => Task.CompletedTask);
        _scheduler.Register("event-scheduler-foundation", TimeSpan.FromSeconds(10), token => _eventScheduling.TickAsync(_events, _worlds, token));
        _scheduler.Register("spawn-scheduler-foundation", TimeSpan.FromSeconds(15), token => _spawnScheduling.TickAsync(_events, _worlds, token));
        _scheduler.Register("npc-lifecycle-foundation", TimeSpan.FromSeconds(20), token => _npcLifecycle.TickAsync(_events, _worlds, _entities, token));
        _scheduler.Register("combat-loop-foundation", TimeSpan.FromSeconds(20), token => _combatLoop.TickAsync(_events, _worlds, token));
    }

    public async Task RunLocalConsoleAsync(CancellationToken cancellationToken)
    {
        if (!_worlds.TryGetValue(_options.DefaultWorldId, out var world))
        {
            world = _worlds.Values.First();
        }

        var account = _accounts.GetOrCreateAccount("LocalConsole");
        var character = _characters.GetOrCreateCharacter(account, world, 1, "LocalConsole");
        var session = new RuntimeSession("local-console", world, account, character);
        _sessions.TryAdd(session, int.MaxValue, out _);
        _events.Publish("runtime", "local-console-started", world.Definition.Id, session.Id, session.CharacterId, "host=authoritative");
        Console.WriteLine(_options.HostName);
        Console.WriteLine($"World: {world.Definition.Name} [{world.Definition.Type}] rules={world.Definition.RulesProfile} ai={world.Definition.AiMode}");
        Console.WriteLine("Local console session. Host is authoritative. Type 'help' or 'quit'.");
        Console.WriteLine(RoomRenderer.Render(session.CurrentRoom));

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var schedulerTask = _scheduler.RunAsync(linked.Token);

        try
        {
            while (!linked.IsCancellationRequested)
            {
                Console.Write("> ");
                var input = Console.ReadLine();
                if (input is null || string.Equals(input.Trim(), "quit", StringComparison.OrdinalIgnoreCase) || string.Equals(input.Trim(), "exit", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                var response = _commands.Dispatch(session, input);
                if (!string.IsNullOrWhiteSpace(response))
                {
                    Console.WriteLine(response);
                }

                await Task.Yield();
            }
        }
        finally
        {
            _sessions.Remove(session.Id);
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
        var network = new ServerNetworkHost(_options, () => _worlds, _commands, _sessions, _accounts, _characters);
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
