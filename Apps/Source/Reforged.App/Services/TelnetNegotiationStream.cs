using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Reforged.App.Services;

internal sealed class TelnetNegotiationStream : System.IO.Stream
{
    private const byte Iac = 255;
    private const byte Dont = 254;
    private const byte Do = 253;
    private const byte Wont = 252;
    private const byte Will = 251;
    private const byte Sb = 250;
    private const byte Se = 240;

    private readonly System.IO.Stream _inner;
    private readonly bool _negotiate;
    private readonly Queue<byte> _decoded = new();
    private readonly byte[] _rawBuffer = new byte[4096];

    private TelnetReadState _state;
    private byte _pendingCommand;

    private enum TelnetReadState
    {
        Data,
        Iac,
        NegotiationOption,
        Subnegotiation,
        SubnegotiationIac
    }

    public TelnetNegotiationStream(System.IO.Stream inner, bool negotiate)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _negotiate = negotiate;
    }

    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => _inner.CanWrite;
    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush() => _inner.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken) =>
        _inner.FlushAsync(cancellationToken);

    public override int Read(byte[] buffer, int offset, int count) =>
        ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        if (buffer.Length == 0)
            return 0;

        while (_decoded.Count == 0)
        {
            var rawCount = await _inner
                .ReadAsync(_rawBuffer.AsMemory(0, _rawBuffer.Length), cancellationToken)
                .ConfigureAwait(false);

            if (rawCount == 0)
                return 0;

            for (var index = 0; index < rawCount; index++)
                await DecodeByteAsync(_rawBuffer[index], cancellationToken).ConfigureAwait(false);
        }

        var written = 0;
        while (written < buffer.Length && _decoded.Count > 0)
            buffer.Span[written++] = _decoded.Dequeue();

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
                    _state = TelnetReadState.Subnegotiation;
                }
                else if (value is Do or Dont or Will or Wont)
                {
                    _pendingCommand = value;
                    _state = TelnetReadState.NegotiationOption;
                }
                else
                {
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
                if (value == Iac)
                    _state = TelnetReadState.SubnegotiationIac;
                break;

            case TelnetReadState.SubnegotiationIac:
                _state = value == Se
                    ? TelnetReadState.Data
                    : TelnetReadState.Subnegotiation;
                break;
        }
    }

    private async ValueTask RespondToNegotiationAsync(
        byte command,
        byte option,
        CancellationToken cancellationToken)
    {
        if (!_negotiate)
            return;

        // Conservative prompt-safe behavior: consume Telnet negotiation bytes
        // and reject unsupported options without enabling unexpected local echo.
        var response = command is Do or Dont ? Wont : Dont;
        var reply = new byte[] { Iac, response, option };

        await _inner
            .WriteAsync(reply.AsMemory(0, reply.Length), cancellationToken)
            .ConfigureAwait(false);
        await _inner.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public override void Write(byte[] buffer, int offset, int count) =>
        _inner.Write(buffer, offset, count);

    public override ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default) =>
        _inner.WriteAsync(buffer, cancellationToken);

    public override long Seek(long offset, System.IO.SeekOrigin origin) =>
        throw new NotSupportedException();

    public override void SetLength(long value) =>
        throw new NotSupportedException();
}
