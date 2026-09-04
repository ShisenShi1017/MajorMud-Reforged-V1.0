using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Reforged.Server;

public sealed record ReforgedClientDataRegistration(int Port, string Token);

public sealed class ServerClientDataHost
{
    private readonly NetworkRuntimeOptions _options;
    private readonly RuntimeCommandDispatcher _commands;
    private readonly RuntimeSessionManager _sessions;
    private readonly ConcurrentDictionary<string, RuntimeSession> _sessionsByToken = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _tokenBySession = new(StringComparer.OrdinalIgnoreCase);

    public ServerClientDataHost(
        NetworkRuntimeOptions options,
        RuntimeCommandDispatcher commands,
        RuntimeSessionManager sessions)
    {
        _options = options;
        _commands = commands;
        _sessions = sessions;
    }

    public ReforgedClientDataRegistration Register(RuntimeSession session)
    {
        Unregister(session.Id);
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        _sessionsByToken[token] = session;
        _tokenBySession[session.Id] = token;
        return new ReforgedClientDataRegistration(_options.ClientDataPort, token);
    }

    public void Unregister(string sessionId)
    {
        if (!_tokenBySession.TryRemove(sessionId, out var token)) return;
        _sessionsByToken.TryRemove(token, out _);
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (_options.ClientDataPort == _options.TelnetPort)
            throw new InvalidOperationException("The RealmGate client-data port must be different from the Telnet port.");

        var endpoint = new IPEndPoint(_options.ResolveBindAddress(), _options.ClientDataPort);
        var listener = new TcpListener(endpoint);
        listener.Start();
        Console.WriteLine($"Private client-data listener started on {endpoint.Address}:{endpoint.Port}; terminal output is not used for client panels.");

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                _ = Task.Run(() => HandleClientAsync(client, cancellationToken), CancellationToken.None);
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
        using var connection = client;
        connection.NoDelay = true;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(hostCancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        using var stream = connection.GetStream();
        using var reader = new StreamReader(stream, new UTF8Encoding(false), false, 4096, leaveOpen: true);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, leaveOpen: true)
        {
            AutoFlush = true,
            NewLine = "\n"
        };

        ClientDataResponse response;
        try
        {
            var line = await reader.ReadLineAsync(timeout.Token).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(line) || line.Length > 4096)
            {
                response = ClientDataResponse.Fail("Invalid private client-data request.");
            }
            else
            {
                var request = JsonSerializer.Deserialize<ClientDataRequest>(line);
                response = Execute(request);
            }
        }
        catch (JsonException)
        {
            response = ClientDataResponse.Fail("Malformed private client-data request.");
        }
        catch (OperationCanceledException) when (!hostCancellationToken.IsCancellationRequested)
        {
            response = ClientDataResponse.Fail("Private client-data request timed out.");
        }
        catch (Exception ex) when (ex is IOException or SocketException)
        {
            return;
        }

        try
        {
            await writer.WriteLineAsync(JsonSerializer.Serialize(response)).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
        {
            // The data channel is disposable and read-only; a closed client requires no cleanup.
        }
    }

    private ClientDataResponse Execute(ClientDataRequest? request)
    {
        var token = request?.Token?.Trim() ?? string.Empty;
        var command = request?.Command?.Trim() ?? string.Empty;
        if (token.Length != 64 || command.Length is 0 or > 512)
            return ClientDataResponse.Fail("Invalid private client-data request.");
        if (!_sessionsByToken.TryGetValue(token, out var session))
            return ClientDataResponse.Fail("Private client-data session is no longer valid.");
        if (!_sessions.ActiveSessions.Any(active => active.Id.Equals(session.Id, StringComparison.OrdinalIgnoreCase)))
        {
            Unregister(session.Id);
            return ClientDataResponse.Fail("Private client-data session is no longer active.");
        }
        if (!_commands.TryRenderClientData(session, command, out var data))
            return ClientDataResponse.Fail("Unsupported private client-data request.");
        return new ClientDataResponse(true, data, string.Empty);
    }

    private sealed record ClientDataRequest(string? Token, string? Command);
    private sealed record ClientDataResponse(bool Ok, string Data, string Error)
    {
        public static ClientDataResponse Fail(string error) => new(false, string.Empty, error);
    }
}
