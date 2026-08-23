using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Reforged.App.Services;

internal sealed class TelnetNegotiationStream : Stream
{
    private const byte Iac = 255;
    private const byte Dont = 254;
    private const byte Do = 253;
    private const byte Wont = 252;
    private const byte Will = 251;
    private const byte Sb = 250;
    private const byte Ga = 249;
    private const byte El = 248;
    private const byte Ec = 247;
    private const byte Ayt = 246;
    private const byte Ao = 245;
    private const byte Ip = 244;
    private const byte Break = 243;
    private const byte Dm = 242;
    private const byte Nop = 241;
    private const byte Se = 240;

    private const byte OptionBinary = 0;
    private const byte OptionEcho = 1;
    private const byte OptionSuppressGoAhead = 3;
    private const byte OptionTerminalType = 24;
    private const byte OptionNaws = 31;
    private const byte OptionLineMode = 34;

    private const byte TerminalTypeIs = 0;
    private const byte TerminalTypeSend = 1;

    private readonly Stream _inner;
    private readonly bool _negotiate;
    private readonly bool _captureTraffic;
    private readonly Action<string, bool>? _diagnostic;
    private readonly Queue<byte> _decoded = new();
    private readonly byte[] _rawBuffer = new byte[8192];
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly HashSet<byte> _localEnabled = [];
    private readonly HashSet<byte> _remoteEnabled = [];
    private readonly List<byte> _subnegotiation = [];

    private TelnetReadState _state;
    private byte _pendingCommand;
    private byte _subnegotiationOption;
    private bool _hasSubnegotiationOption;
    private string _terminalType;
    private int _columns;
    private int _rows;
    private int _applicationTraceRedactionDepth;
    private bool _disposed;

    private enum TelnetReadState
    {
        Data,
        Iac,
        NegotiationOption,
        Subnegotiation,
        SubnegotiationIac
    }

    public TelnetNegotiationStream(
        Stream inner,
        bool negotiate,
        string terminalType,
        int columns,
        int rows,
        bool captureTraffic,
        Action<string, bool>? diagnostic = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _negotiate = negotiate;
        _captureTraffic = captureTraffic;
        _diagnostic = diagnostic;
        _terminalType = NormalizeTerminalType(terminalType);
        _columns = Math.Clamp(columns, 20, ushort.MaxValue);
        _rows = Math.Clamp(rows, 5, ushort.MaxValue);
    }

    public override bool CanRead => !_disposed && _inner.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => !_disposed && _inner.CanWrite;
    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (!_negotiate)
        {
            Trace("Telnet control parsing enabled; option replies disabled.", false);
            return;
        }

        Trace(
            $"Offering terminal {_terminalType}, NAWS {_columns}x{_rows}, and suppress-go-ahead.",
            false);

        await SendNegotiationAsync(Will, OptionTerminalType, cancellationToken)
            .ConfigureAwait(false);
        await SendNegotiationAsync(Will, OptionNaws, cancellationToken)
            .ConfigureAwait(false);
        await SendNegotiationAsync(Will, OptionSuppressGoAhead, cancellationToken)
            .ConfigureAwait(false);
        await SendNegotiationAsync(Do, OptionSuppressGoAhead, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task SetWindowSizeAsync(
        int columns,
        int rows,
        CancellationToken cancellationToken = default)
    {
        _columns = Math.Clamp(columns, 20, ushort.MaxValue);
        _rows = Math.Clamp(rows, 5, ushort.MaxValue);

        if (_negotiate && _localEnabled.Contains(OptionNaws))
            await SendNawsAsync(cancellationToken).ConfigureAwait(false);
    }

    public override void Flush()
    {
        ThrowIfDisposed();
        _inner.Flush();
    }

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        return _inner.FlushAsync(cancellationToken);
    }

    public override int Read(byte[] buffer, int offset, int count) =>
        ReadAsync(buffer.AsMemory(offset, count))
            .AsTask()
            .GetAwaiter()
            .GetResult();

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (buffer.Length == 0)
            return 0;

        while (_decoded.Count == 0)
        {
            var rawCount = await _inner
                .ReadAsync(
                    _rawBuffer.AsMemory(0, _rawBuffer.Length),
                    cancellationToken)
                .ConfigureAwait(false);

            if (rawCount == 0)
                return 0;

            TraceTraffic("RX", _rawBuffer, rawCount);

            for (var index = 0; index < rawCount; index++)
                await DecodeByteAsync(_rawBuffer[index], cancellationToken)
                    .ConfigureAwait(false);
        }

        var written = Math.Min(buffer.Length, _decoded.Count);
        var decoded = new byte[written];

        for (var index = 0; index < written; index++)
            decoded[index] = _decoded.Dequeue();

        decoded.AsMemory().CopyTo(buffer);
        return written;
    }

    private async ValueTask DecodeByteAsync(
        byte value,
        CancellationToken cancellationToken)
    {
        switch (_state)
        {
            case TelnetReadState.Data:
                if (value == Iac)
                    _state = TelnetReadState.Iac;
                else
                    _decoded.Enqueue(value);
                break;

            case TelnetReadState.Iac:
                if (value == Iac)
                {
                    _decoded.Enqueue(Iac);
                    _state = TelnetReadState.Data;
                }
                else if (value == Sb)
                {
                    _subnegotiation.Clear();
                    _hasSubnegotiationOption = false;
                    _state = TelnetReadState.Subnegotiation;
                }
                else if (value is Do or Dont or Will or Wont)
                {
                    _pendingCommand = value;
                    _state = TelnetReadState.NegotiationOption;
                }
                else
                {
                    HandleTelnetCommand(value);
                    _state = TelnetReadState.Data;
                }
                break;

            case TelnetReadState.NegotiationOption:
                await RespondToNegotiationAsync(
                        _pendingCommand,
                        value,
                        cancellationToken)
                    .ConfigureAwait(false);
                _pendingCommand = 0;
                _state = TelnetReadState.Data;
                break;

            case TelnetReadState.Subnegotiation:
                if (!_hasSubnegotiationOption)
                {
                    _subnegotiationOption = value;
                    _hasSubnegotiationOption = true;
                }
                else if (value == Iac)
                {
                    _state = TelnetReadState.SubnegotiationIac;
                }
                else
                {
                    _subnegotiation.Add(value);
                }
                break;

            case TelnetReadState.SubnegotiationIac:
                if (value == Iac)
                {
                    _subnegotiation.Add(Iac);
                    _state = TelnetReadState.Subnegotiation;
                }
                else if (value == Se)
                {
                    await HandleSubnegotiationAsync(cancellationToken)
                        .ConfigureAwait(false);
                    _state = TelnetReadState.Data;
                }
                else
                {
                    _state = TelnetReadState.Subnegotiation;
                }
                break;
        }
    }

    private async ValueTask RespondToNegotiationAsync(
        byte command,
        byte option,
        CancellationToken cancellationToken)
    {
        Trace(
            $"RX {CommandName(command)} {OptionName(option)} ({option}).",
            false);

        if (!_negotiate)
            return;

        switch (command)
        {
            case Do:
                if (SupportsLocalOption(option))
                {
                    if (_localEnabled.Add(option))
                        await SendNegotiationAsync(Will, option, cancellationToken)
                            .ConfigureAwait(false);

                    if (option == OptionNaws)
                        await SendNawsAsync(cancellationToken)
                            .ConfigureAwait(false);
                }
                else
                {
                    await SendNegotiationAsync(Wont, option, cancellationToken)
                        .ConfigureAwait(false);
                }
                break;

            case Dont:
                if (_localEnabled.Remove(option))
                    await SendNegotiationAsync(Wont, option, cancellationToken)
                        .ConfigureAwait(false);
                break;

            case Will:
                if (SupportsRemoteOption(option))
                {
                    if (_remoteEnabled.Add(option))
                        await SendNegotiationAsync(Do, option, cancellationToken)
                            .ConfigureAwait(false);
                }
                else
                {
                    await SendNegotiationAsync(Dont, option, cancellationToken)
                        .ConfigureAwait(false);
                }
                break;

            case Wont:
                if (_remoteEnabled.Remove(option))
                    await SendNegotiationAsync(Dont, option, cancellationToken)
                        .ConfigureAwait(false);
                break;
        }
    }

    private async ValueTask HandleSubnegotiationAsync(
        CancellationToken cancellationToken)
    {
        if (!_hasSubnegotiationOption)
            return;

        Trace(
            $"RX SB {OptionName(_subnegotiationOption)} "
            + $"{FormatBytes(_subnegotiation.ToArray(), _subnegotiation.Count)}.",
            false);

        if (_subnegotiationOption == OptionTerminalType &&
            _subnegotiation.Count > 0 &&
            _subnegotiation[0] == TerminalTypeSend)
        {
            var typeBytes = Encoding.ASCII.GetBytes(_terminalType);
            var payload = new byte[typeBytes.Length + 1];
            payload[0] = TerminalTypeIs;
            typeBytes.CopyTo(payload, 1);

            await SendSubnegotiationAsync(
                    OptionTerminalType,
                    payload,
                    cancellationToken)
                .ConfigureAwait(false);

            Trace($"Sent terminal type {_terminalType}.", false);
        }

        _subnegotiation.Clear();
        _hasSubnegotiationOption = false;
    }

    private async Task SendNawsAsync(CancellationToken cancellationToken)
    {
        var payload = new byte[4];
        payload[0] = (byte)(_columns >> 8);
        payload[1] = (byte)(_columns & 0xFF);
        payload[2] = (byte)(_rows >> 8);
        payload[3] = (byte)(_rows & 0xFF);

        await SendSubnegotiationAsync(
                OptionNaws,
                payload,
                cancellationToken)
            .ConfigureAwait(false);

        Trace($"Sent NAWS {_columns}x{_rows}.", false);
    }

    private async Task SendNegotiationAsync(
        byte command,
        byte option,
        CancellationToken cancellationToken)
    {
        var bytes = new[] { Iac, command, option };
        await WriteProtocolAsync(bytes, cancellationToken).ConfigureAwait(false);
        Trace(
            $"TX {CommandName(command)} {OptionName(option)} ({option}).",
            false);
    }

    private async Task SendSubnegotiationAsync(
        byte option,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken)
    {
        var source = payload.ToArray();
        var expandedLength = 5;
        foreach (var value in source)
            expandedLength += value == Iac ? 2 : 1;

        var bytes = new byte[expandedLength];
        var index = 0;
        bytes[index++] = Iac;
        bytes[index++] = Sb;
        bytes[index++] = option;

        foreach (var value in source)
        {
            bytes[index++] = value;
            if (value == Iac)
                bytes[index++] = Iac;
        }

        bytes[index++] = Iac;
        bytes[index] = Se;

        await WriteProtocolAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    private async Task WriteProtocolAsync(
        ReadOnlyMemory<byte> bytes,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        var source = bytes.ToArray();
        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            TraceTraffic("TX-PROTOCOL", source, source.Length);
            await _inner.WriteAsync(source, cancellationToken).ConfigureAwait(false);
            await _inner.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    internal IDisposable RedactApplicationDataTrace()
    {
        ThrowIfDisposed();
        Interlocked.Increment(ref _applicationTraceRedactionDepth);
        return new TraceRedactionScope(this);
    }

    public override void Write(byte[] buffer, int offset, int count) =>
        WriteAsync(buffer.AsMemory(offset, count))
            .AsTask()
            .GetAwaiter()
            .GetResult();

    public override async ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var source = buffer.ToArray();
        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        byte[]? rented = null;

        try
        {
            if (_captureTraffic &&
                Volatile.Read(ref _applicationTraceRedactionDepth) > 0)
            {
                Trace($"TX-DATA [REDACTED] ({source.Length} bytes)", true);
            }
            else
            {
                TraceTraffic("TX-DATA", source, source.Length);
            }

            var escapedLength = source.Length;
            foreach (var value in source)
            {
                if (value == Iac)
                    escapedLength++;
            }

            if (escapedLength == source.Length)
            {
                await _inner.WriteAsync(source, cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                rented = ArrayPool<byte>.Shared.Rent(escapedLength);
                var written = 0;
                foreach (var value in source)
                {
                    rented[written++] = value;
                    if (value == Iac)
                        rented[written++] = Iac;
                }

                await _inner
                    .WriteAsync(rented.AsMemory(0, written), cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        finally
        {
            if (rented is not null)
                ArrayPool<byte>.Shared.Return(rented);

            _writeGate.Release();
        }
    }

    private void HandleTelnetCommand(byte command)
    {
        var name = command switch
        {
            Ga => "GA",
            El => "EL",
            Ec => "EC",
            Ayt => "AYT",
            Ao => "AO",
            Ip => "IP",
            Break => "BREAK",
            Dm => "DM",
            Nop => "NOP",
            _ => $"COMMAND-{command.ToString(CultureInfo.InvariantCulture)}"
        };

        Trace($"RX IAC {name}.", false);
    }

    private static bool SupportsLocalOption(byte option) =>
        option is OptionBinary
            or OptionSuppressGoAhead
            or OptionTerminalType
            or OptionNaws;

    private static bool SupportsRemoteOption(byte option) =>
        option is OptionBinary
            or OptionEcho
            or OptionSuppressGoAhead;

    private static string CommandName(byte command) =>
        command switch
        {
            Do => "DO",
            Dont => "DONT",
            Will => "WILL",
            Wont => "WONT",
            _ => command.ToString(CultureInfo.InvariantCulture)
        };

    private static string OptionName(byte option) =>
        option switch
        {
            OptionBinary => "BINARY",
            OptionEcho => "ECHO",
            OptionSuppressGoAhead => "SUPPRESS-GO-AHEAD",
            OptionTerminalType => "TERMINAL-TYPE",
            OptionNaws => "NAWS",
            OptionLineMode => "LINEMODE",
            _ => $"OPTION-{option.ToString(CultureInfo.InvariantCulture)}"
        };

    private static string NormalizeTerminalType(string value)
    {
        var prepared = string.IsNullOrWhiteSpace(value)
            ? "ANSI"
            : value.Trim().ToUpperInvariant();

        var ascii = new string(
            prepared
                .Where(value => value is >= ' ' and <= '~')
                .Take(40)
                .ToArray());

        return string.IsNullOrWhiteSpace(ascii) ? "ANSI" : ascii;
    }


    private void TraceTraffic(
        string direction,
        byte[] bytes,
        int byteCount)
    {
        if (!_captureTraffic)
            return;

        Trace(
            $"{direction} {FormatBytes(bytes, byteCount)}",
            true);
    }

    private static string FormatBytes(
        byte[] bytes,
        int byteCount)
    {
        const int maximum = 256;
        var available = Math.Clamp(byteCount, 0, bytes.Length);
        var count = Math.Min(available, maximum);
        var builder = new StringBuilder(count * 3 + 32);

        for (var index = 0; index < count; index++)
        {
            if (index > 0)
                builder.Append(' ');

            builder.Append(
                bytes[index].ToString(
                    "X2",
                    CultureInfo.InvariantCulture));
        }

        if (available > maximum)
            builder.Append($" … (+{available - maximum} bytes)");

        return builder.ToString();
    }

    private void Trace(string message, bool traffic) =>
        _diagnostic?.Invoke(message, traffic);

    private void EndApplicationTraceRedaction() =>
        Interlocked.Decrement(ref _applicationTraceRedactionDepth);

    private sealed class TraceRedactionScope : IDisposable
    {
        private TelnetNegotiationStream? _owner;

        public TraceRedactionScope(TelnetNegotiationStream owner) =>
            _owner = owner;

        public void Dispose()
        {
            var owner = Interlocked.Exchange(ref _owner, null);
            owner?.EndApplicationTraceRedaction();
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(
                nameof(TelnetNegotiationStream));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            _writeGate.Dispose();
            _inner.Dispose();
        }

        base.Dispose(disposing);
    }

    public override long Seek(long offset, SeekOrigin origin) =>
        throw new NotSupportedException();

    public override void SetLength(long value) =>
        throw new NotSupportedException();
}
