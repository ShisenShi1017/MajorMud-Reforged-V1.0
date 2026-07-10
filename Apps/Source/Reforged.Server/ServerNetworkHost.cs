using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Reforged.Server;

public sealed class ServerNetworkHost
{
    private readonly ServerRuntimeOptions _options;
    private readonly Func<IReadOnlyDictionary<string, RuntimeWorld>> _worldProvider;
    private readonly RuntimeCommandDispatcher _commands;
    private readonly RuntimeSessionManager _sessions;

    public ServerNetworkHost(
        ServerRuntimeOptions options,
        Func<IReadOnlyDictionary<string, RuntimeWorld>> worldProvider,
        RuntimeCommandDispatcher commands,
        RuntimeSessionManager sessions)
    {
        _options = options;
        _worldProvider = worldProvider;
        _commands = commands;
        _sessions = sessions;
    }

    public async Task RunTelnetAsync(CancellationToken cancellationToken)
    {
        var endpoint = new IPEndPoint(_options.Network.ResolveBindAddress(), _options.Network.TelnetPort);
        var listener = new TcpListener(endpoint);
        listener.Start();
        Console.WriteLine($"Telnet listener started on {endpoint.Address}:{endpoint.Port}; maxSessions={_options.Network.MaxSessions}");

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                _ = Task.Run(() => HandleClientAsync(client, cancellationToken), cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown.
        }
        finally
        {
            listener.Stop();
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken hostCancellationToken)
    {
        var sessionId = $"telnet-{Guid.NewGuid():N}"[..20];
        using var connection = client;
        using var stream = connection.GetStream();
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true, NewLine = "\r\n" };

        using var loginTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(10, _options.Network.LoginTimeoutSeconds)));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(hostCancellationToken, loginTimeout.Token);

        try
        {
            await WriteBannerAsync(writer).ConfigureAwait(false);
            await writer.WriteAsync("Name: ").ConfigureAwait(false);
            var playerName = await ReadLineAsync(reader, linked.Token).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(playerName))
            {
                await writer.WriteLineAsync("Connection closed: blank names are not accepted.").ConfigureAwait(false);
                return;
            }

            var selectedWorld = await SelectWorldAsync(reader, writer, linked.Token).ConfigureAwait(false);
            if (selectedWorld is null)
            {
                await writer.WriteLineAsync("Connection closed: no world selected.").ConfigureAwait(false);
                return;
            }

            var session = _sessions.CreateSession(sessionId, selectedWorld, playerName);
            if (!_sessions.TryAdd(session, Math.Max(1, _options.Network.MaxSessions), out var message))
            {
                await writer.WriteLineAsync($"Connection refused: {message}").ConfigureAwait(false);
                return;
            }

            try
            {
                await writer.WriteLineAsync(selectedWorld.Definition.Motd).ConfigureAwait(false);
                await writer.WriteLineAsync(RoomRenderer.Render(session.CurrentRoom)).ConfigureAwait(false);
                while (!hostCancellationToken.IsCancellationRequested && connection.Connected)
                {
                    await writer.WriteAsync("> ").ConfigureAwait(false);
                    var input = await ReadLineAsync(reader, hostCancellationToken).ConfigureAwait(false);
                    if (input is null)
                    {
                        break;
                    }

                    if (string.Equals(input.Trim(), "quit", StringComparison.OrdinalIgnoreCase) || string.Equals(input.Trim(), "exit", StringComparison.OrdinalIgnoreCase))
                    {
                        await writer.WriteLineAsync("Goodbye.").ConfigureAwait(false);
                        break;
                    }

                    var response = _commands.Dispatch(session, input);
                    if (!string.IsNullOrWhiteSpace(response))
                    {
                        await writer.WriteLineAsync(response).ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                _sessions.Remove(session.Id);
            }
        }
        catch (OperationCanceledException)
        {
            // Client login timeout or host shutdown.
        }
        catch (IOException)
        {
            // Remote endpoint disconnected.
        }
        catch (SocketException)
        {
            // Remote endpoint disconnected.
        }
    }

    private async Task WriteBannerAsync(StreamWriter writer)
    {
        await writer.WriteLineAsync(_options.HostName).ConfigureAwait(false);
        await writer.WriteLineAsync("MajorMUD Reforged Runtime Milestone 1").ConfigureAwait(false);
        await writer.WriteLineAsync("Host authoritative. Classic-first. No client-side state mutation.").ConfigureAwait(false);
        await writer.WriteLineAsync().ConfigureAwait(false);
    }

    private async Task<RuntimeWorld?> SelectWorldAsync(StreamReader reader, StreamWriter writer, CancellationToken cancellationToken)
    {
        var worlds = _worldProvider();
        if (worlds.Count == 0)
        {
            return null;
        }

        if (!_options.Network.AllowWorldSelection || worlds.Count == 1)
        {
            if (worlds.TryGetValue(_options.DefaultWorldId, out var defaultWorld))
            {
                return defaultWorld;
            }

            return worlds.Values.First();
        }

        await writer.WriteLineAsync("Available worlds:").ConfigureAwait(false);
        foreach (var world in worlds.Values.OrderBy(world => world.Definition.Id))
        {
            await writer.WriteLineAsync($"- {world.Definition.Id}: {world.Definition.Name} [{world.Definition.Type}]").ConfigureAwait(false);
        }

        await writer.WriteAsync($"World [{_options.DefaultWorldId}]: ").ConfigureAwait(false);
        var requestedWorld = await ReadLineAsync(reader, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(requestedWorld))
        {
            requestedWorld = _options.DefaultWorldId;
        }

        return worlds.TryGetValue(requestedWorld.Trim(), out var selected)
            ? selected
            : null;
    }

    private static async Task<string?> ReadLineAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var readTask = reader.ReadLineAsync();
        var completed = await Task.WhenAny(readTask, Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken)).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return completed == readTask ? await readTask.ConfigureAwait(false) : null;
    }
}
