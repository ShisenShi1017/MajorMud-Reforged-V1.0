using System.Net.Sockets;
using System.Text;
using RealmGate.Client.Protocol;
using RealmGate.Client.Terminal;

namespace RealmGate.Client.Transport;

public sealed class ClassicTerminalSession : IAsyncDisposable
{
    private readonly object _sync = new();
    private readonly ClassicTerminalEmulator _terminal = new();
    private readonly RealmGateClientProtocolFilter _protocolFilter = new();
    private readonly StringBuilder _promptTail = new();
    private ClientTransportConnection? _transport;
    private CancellationTokenSource? _readCancellation;
    private Task? _readTask;
    private long _generation;
    private int _columns = 80;
    private int _rows = 25;

    public ClassicTerminalSession()
    {
        Snapshot = _terminal.CreateSnapshot();
    }

    public event Action<ClassicTerminalSnapshot>? SnapshotChanged;
    public event Action<string>? StatusChanged;
    public event Action<bool>? PasswordModeChanged;
    public event Action<RealmGateClientProtocolMessage>? ProtocolMessageReceived;
    public event Action<string>? VisibleTextReceived;

    public ClassicTerminalSnapshot Snapshot { get; private set; }
    public bool IsConnected
    {
        get { lock (_sync) return _transport is not null; }
    }

    public Task ConnectAsync(
        string hostName,
        int port,
        CancellationToken cancellationToken = default) =>
        ConnectAsync(
            hostName,
            port,
            ClientTransportOptions.Default,
            cancellationToken);

    public async Task ConnectAsync(
        string hostName,
        int port,
        ClientTransportOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        await DisconnectAsync(notify: false).ConfigureAwait(false);
        StatusChanged?.Invoke($"Connecting to {hostName}:{port}");

        var transport = await ClientTransportConnection.ConnectAsync(
                hostName,
                port,
                options with
                {
                    Columns = _columns,
                    Rows = _rows
                },
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var readCancellation = new CancellationTokenSource();
        var generation = Interlocked.Increment(ref _generation);

        _promptTail.Clear();
        _protocolFilter.Reset();
        UpdateSnapshot(_terminal.Process("\x1b[2J\x1b[H"));
        PasswordModeChanged?.Invoke(false);

        lock (_sync)
        {
            _transport = transport;
            _readCancellation = readCancellation;
            _readTask = Task.Run(() => ReadLoopAsync(
                transport,
                generation,
                readCancellation.Token));
        }

        StatusChanged?.Invoke($"Connected to {hostName}:{port}");
    }

    public async Task SendLineAsync(
        string value,
        bool sensitive,
        CancellationToken cancellationToken = default)
    {
        var transport = GetConnectedTransport();
        await transport.WriteLineAsync(
                value,
                redactApplicationData: sensitive,
                cancellationToken)
            .ConfigureAwait(false);

        if (sensitive)
            PasswordModeChanged?.Invoke(false);
    }

    public Task SendRawAsync(
        string value,
        CancellationToken cancellationToken = default) =>
        GetConnectedTransport().WriteAsync(value, cancellationToken);

    public void NotePrivateCommand(string command) =>
        _protocolFilter.NoteOutboundCommand(command);

    public async Task SetWindowSizeAsync(
        int columns,
        int rows,
        CancellationToken cancellationToken = default)
    {
        _columns = Math.Clamp(columns, 20, ushort.MaxValue);
        _rows = Math.Clamp(rows, 5, ushort.MaxValue);

        ClientTransportConnection? transport;
        lock (_sync)
            transport = _transport;

        if (transport is not null)
            await transport.SetWindowSizeAsync(_columns, _rows, cancellationToken)
                .ConfigureAwait(false);
    }

    public Task DisconnectAsync() => DisconnectAsync(notify: true);

    private async Task DisconnectAsync(bool notify)
    {
        Interlocked.Increment(ref _generation);

        ClientTransportConnection? transport;
        CancellationTokenSource? readCancellation;
        Task? readTask;
        lock (_sync)
        {
            transport = _transport;
            readCancellation = _readCancellation;
            readTask = _readTask;
            _transport = null;
            _readCancellation = null;
            _readTask = null;
        }

        try { readCancellation?.Cancel(); } catch { }
        if (transport is not null)
        {
            try { await transport.DisposeAsync().ConfigureAwait(false); }
            catch { }
        }

        if (readTask is not null)
        {
            try { await readTask.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false); }
            catch { }
        }

        readCancellation?.Dispose();
        PasswordModeChanged?.Invoke(false);
        if (notify)
            StatusChanged?.Invoke("Disconnected");
    }

    private async Task ReadLoopAsync(
        ClientTransportConnection transport,
        long generation,
        CancellationToken cancellationToken)
    {
        var buffer = new char[4096];
        var disconnectedStatus = "Disconnected by remote host";
        try
        {
            while (!cancellationToken.IsCancellationRequested &&
                   generation == Volatile.Read(ref _generation))
            {
                var count = await transport.ReadAsync(buffer, cancellationToken)
                    .ConfigureAwait(false);
                if (count == 0)
                    break;

                var protocolResult = _protocolFilter.Process(new string(buffer, 0, count));
                foreach (var message in protocolResult.Messages)
                    ProtocolMessageReceived?.Invoke(message);

                if (protocolResult.VisibleText.Length == 0)
                    continue;

                VisibleTextReceived?.Invoke(protocolResult.VisibleText);
                UpdateSnapshot(_terminal.Process(protocolResult.VisibleText));
                UpdatePasswordMode(protocolResult.VisibleText);
            }
        }
        catch (Exception exception) when (exception is IOException or SocketException or OperationCanceledException or ObjectDisposedException)
        {
        }
        catch (Exception exception)
        {
            disconnectedStatus = $"Connection error: {exception.Message}";
        }
        finally
        {
            if (generation == Volatile.Read(ref _generation))
            {
                CancellationTokenSource? readCancellation = null;
                lock (_sync)
                {
                    if (ReferenceEquals(_transport, transport))
                    {
                        readCancellation = _readCancellation;
                        _transport = null;
                        _readCancellation = null;
                        _readTask = null;
                    }
                }

                await transport.DisposeAsync().ConfigureAwait(false);
                readCancellation?.Dispose();
                PasswordModeChanged?.Invoke(false);
                StatusChanged?.Invoke(disconnectedStatus);
            }
        }
    }

    private void UpdateSnapshot(ClassicTerminalSnapshot snapshot)
    {
        Snapshot = snapshot;
        SnapshotChanged?.Invoke(snapshot);
    }

    private void UpdatePasswordMode(string text)
    {
        _promptTail.Append(AnsiTextParser.StripFormatting(text));
        if (_promptTail.Length > 512)
            _promptTail.Remove(0, _promptTail.Length - 512);

        var tail = _promptTail.ToString().TrimEnd();
        var lineStart = Math.Max(
            tail.LastIndexOf('\r'),
            tail.LastIndexOf('\n'));
        var currentLine = tail[(lineStart + 1)..].TrimStart();
        PasswordModeChanged?.Invoke(
            currentLine.EndsWith("Password:", StringComparison.OrdinalIgnoreCase));
    }

    private ClientTransportConnection GetConnectedTransport()
    {
        lock (_sync)
            return _transport ?? throw new InvalidOperationException("The Play terminal is not connected.");
    }

    public async ValueTask DisposeAsync() =>
        await DisconnectAsync(notify: false).ConfigureAwait(false);
}
