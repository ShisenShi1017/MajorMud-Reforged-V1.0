namespace Reforged.Server;

public sealed class ServerRuntimeHost
{
    private readonly string _projectRoot;
    private readonly ServerRuntimeOptions _options;
    private readonly RuntimeWorldLoader _worldLoader = new();
    private readonly Dictionary<string, RuntimeWorld> _worlds = new(StringComparer.OrdinalIgnoreCase);
    private readonly RuntimeSessionManager _sessions = new();
    private readonly ServerTickScheduler _scheduler = new();
    private RuntimeCommandDispatcher _commands;

    public ServerRuntimeHost(string projectRoot, ServerRuntimeOptions options)
    {
        _projectRoot = projectRoot;
        _options = options;
        _commands = new RuntimeCommandDispatcher(() => _worlds, _sessions);
    }

    public IReadOnlyDictionary<string, RuntimeWorld> Worlds => _worlds;
    public RuntimeSessionManager Sessions => _sessions;

    public void Bootstrap()
    {
        foreach (var worldDefinition in _options.Worlds.Where(world => world.RuntimeEnabled))
        {
            EnforceClassicBoundaries(worldDefinition);
            var world = _worldLoader.Load(_projectRoot, worldDefinition);
            _worlds[worldDefinition.Id] = world;
        }

        if (_worlds.Count == 0)
        {
            throw new InvalidOperationException("No runtime-enabled worlds are configured.");
        }

        _scheduler.Register("world-tick", TimeSpan.FromMilliseconds(Math.Max(100, _options.WorldTickMilliseconds)), _ => Task.CompletedTask);
        _scheduler.Register("session-heartbeat", TimeSpan.FromSeconds(30), _ => Task.CompletedTask);
    }

    public async Task RunLocalConsoleAsync(CancellationToken cancellationToken)
    {
        if (!_worlds.TryGetValue(_options.DefaultWorldId, out var world))
        {
            world = _worlds.Values.First();
        }

        var session = new RuntimeSession("local-console", world, "LocalConsole");
        _sessions.TryAdd(session, int.MaxValue, out _);
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
        var network = new ServerNetworkHost(_options, () => _worlds, _commands, _sessions);
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
