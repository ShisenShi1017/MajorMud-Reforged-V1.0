using System.Net.Sockets;
using System.Text;
using RealmGate.Client.Terminal;

namespace RealmGate.Client.Transport;

public sealed record ClientTransportOptions(
    bool EnableTelnetNegotiation,
    string TerminalType,
    int Columns,
    int Rows,
    bool CaptureTraffic,
    bool UseCodePage437)
{
    public static ClientTransportOptions Default { get; } = new(
        EnableTelnetNegotiation: true,
        TerminalType: "ANSI",
        Columns: 80,
        Rows: 25,
        CaptureTraffic: false,
        UseCodePage437: true);
}

/// <summary>
/// UI-neutral owner for one TCP/Telnet client connection. Session parsing,
/// automation, and presentation remain with their respective consumers.
/// </summary>
public sealed class ClientTransportConnection : IAsyncDisposable
{
    private readonly TcpClient _client;
    private readonly TelnetNegotiationStream _telnetStream;
    private readonly StreamReader _reader;
    private readonly StreamWriter _writer;
    private int _disposed;

    private ClientTransportConnection(
        TcpClient client,
        TelnetNegotiationStream telnetStream,
        StreamReader reader,
        StreamWriter writer)
    {
        _client = client;
        _telnetStream = telnetStream;
        _reader = reader;
        _writer = writer;
    }

    public static async Task<ClientTransportConnection> ConnectAsync(
        string hostName,
        int port,
        ClientTransportOptions options,
        Action<string, bool>? diagnostic = null,
        Action<RealmGateClientDataEndpointAnnouncement>? clientDataAnnouncement = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostName);
        ArgumentOutOfRangeException.ThrowIfLessThan(port, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, 65535);
        ArgumentNullException.ThrowIfNull(options);

        var client = new TcpClient { NoDelay = true };
        TelnetNegotiationStream? telnetStream = null;
        StreamReader? reader = null;
        StreamWriter? writer = null;

        try
        {
            await client.ConnectAsync(hostName, port, cancellationToken)
                .ConfigureAwait(false);

            telnetStream = new TelnetNegotiationStream(
                client.GetStream(),
                options.EnableTelnetNegotiation,
                options.TerminalType,
                options.Columns,
                options.Rows,
                options.CaptureTraffic,
                diagnostic,
                clientDataAnnouncement);

            var encoding = CreateEncoding(options.UseCodePage437);
            reader = new StreamReader(
                telnetStream,
                encoding,
                detectEncodingFromByteOrderMarks: false,
                bufferSize: 4096,
                leaveOpen: true);
            writer = new StreamWriter(
                telnetStream,
                encoding,
                bufferSize: 4096,
                leaveOpen: true)
            {
                AutoFlush = true,
                NewLine = "\r\n"
            };

            await telnetStream.InitializeAsync(cancellationToken)
                .ConfigureAwait(false);

            return new ClientTransportConnection(
                client,
                telnetStream,
                reader,
                writer);
        }
        catch
        {
            try { writer?.Dispose(); } catch { }
            try { reader?.Dispose(); } catch { }
            try { telnetStream?.Dispose(); } catch { }
            try { client.Dispose(); } catch { }
            throw;
        }
    }

    public ValueTask<int> ReadAsync(
        Memory<char> buffer,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return _reader.ReadAsync(buffer, cancellationToken);
    }

    public async Task WriteLineAsync(
        string value,
        bool redactApplicationData,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        using var traceRedaction = redactApplicationData
            ? _telnetStream.RedactApplicationDataTrace()
            : null;
        await _writer.WriteLineAsync(value.AsMemory(), cancellationToken)
            .ConfigureAwait(false);
        await _writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task WriteAsync(
        string value,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _writer.WriteAsync(value.AsMemory(), cancellationToken)
            .ConfigureAwait(false);
        await _writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task SetWindowSizeAsync(
        int columns,
        int rows,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return _telnetStream.SetWindowSizeAsync(
            columns,
            rows,
            cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return ValueTask.CompletedTask;

        try { _client.Close(); } catch { }
        try { _reader.Dispose(); } catch { }
        try { _writer.Dispose(); } catch { }
        try { _telnetStream.Dispose(); } catch { }
        try { _client.Dispose(); } catch { }
        return ValueTask.CompletedTask;
    }

    private static Encoding CreateEncoding(bool useCodePage437) =>
        useCodePage437
            ? new CodePage437Encoding()
            : new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false,
                throwOnInvalidBytes: false);

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(ClientTransportConnection));
    }
}
