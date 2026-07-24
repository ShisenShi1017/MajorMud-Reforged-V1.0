using System.Net.Sockets;
using System.Text;

namespace Reforged.Server;

public sealed class TelnetConnection : IAsyncDisposable
{
    private const byte Iac = 255;
    private const byte Se = 240;
    private const byte Sb = 250;
    private const byte Will = 251;
    private const byte Wont = 252;
    private const byte Do = 253;
    private const byte Dont = 254;
    private const byte Echo = 1;
    private const byte SuppressGoAhead = 3;

    private readonly NetworkStream _stream;
    private readonly byte[] _single = new byte[1];
    private bool _skipNextLineFeed;

    public TelnetConnection(NetworkStream stream) => _stream = stream;

    public async Task NegotiateAsync(CancellationToken cancellationToken)
    {
        // The server performs echo itself. This is reliable with MegaMUD profiles
        // that do not enable local echo, while still allowing password input to stay hidden.
        await WriteBytesAsync(new byte[]
        {
            Iac, Will, SuppressGoAhead,
            Iac, Will, Echo
        }, cancellationToken).ConfigureAwait(false);
    }

    public Task WriteAsync(string text, CancellationToken cancellationToken) =>
        WriteBytesAsync(Encoding.UTF8.GetBytes(NormalizeNetworkNewLines(text)), cancellationToken);

    private static string NormalizeNetworkNewLines(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Replace("\n", "\r\n", StringComparison.Ordinal);
    }

    public Task WriteLineAsync(string text, CancellationToken cancellationToken) =>
        WriteAsync(text + "\r\n", cancellationToken);

    public Task WriteLineAsync(CancellationToken cancellationToken) => WriteAsync("\r\n", cancellationToken);

    public async Task<string?> ReadLineAsync(bool hideInput, CancellationToken cancellationToken)
    {
        // Reassert server echo mode before every prompt because some clients change
        // their local echo state during automatic login handling.
        await WriteBytesAsync(new byte[] { Iac, Will, Echo }, cancellationToken).ConfigureAwait(false);

        var buffer = new List<byte>(128);
        while (true)
        {
            var value = await ReadDataByteAsync(cancellationToken).ConfigureAwait(false);
            if (value < 0)
            {
                return buffer.Count == 0 ? null : Encoding.UTF8.GetString(buffer.ToArray());
            }

            if (value is 10 or 13)
            {
                if (value == 13) _skipNextLineFeed = true;
                await WriteAsync("\r\n", cancellationToken).ConfigureAwait(false);
                return Encoding.UTF8.GetString(buffer.ToArray());
            }

            if (value is 8 or 127)
            {
                if (buffer.Count > 0)
                {
                    buffer.RemoveAt(buffer.Count - 1);
                    await WriteAsync("\b \b", cancellationToken).ConfigureAwait(false);
                }
                continue;
            }

            if ((value >= 32 || value == 9) && buffer.Count < 128)
            {
                buffer.Add((byte)value);
                if (hideInput)
                    await WriteAsync("*", cancellationToken).ConfigureAwait(false);
                else
                    await WriteBytesAsync(new[] { (byte)value }, cancellationToken).ConfigureAwait(false);
            }
        }
    }


    public async Task<string?> ReadKeyAsync(CancellationToken cancellationToken)
    {
        await WriteBytesAsync(new byte[] { Iac, Will, Echo }, cancellationToken).ConfigureAwait(false);
        var value = await ReadDataByteAsync(cancellationToken).ConfigureAwait(false);
        if (value < 0) return null;
        if (value is 10 or 13)
        {
            if (value == 13) _skipNextLineFeed = true;
            return "ENTER";
        }
        if (value == 27)
        {
            var next = await ReadDataByteAsync(cancellationToken).ConfigureAwait(false);
            if (next == '[')
            {
                var code = await ReadDataByteAsync(cancellationToken).ConfigureAwait(false);
                return code switch { 65 => "UP", 66 => "DOWN", 67 => "RIGHT", 68 => "LEFT", _ => "ESC" };
            }
            return "ESC";
        }
        if (value == 32) return "SPACE";
        if (value is 8 or 127) return "BACKSPACE";
        return ((char)value).ToString();
    }

    private async Task<int> ReadDataByteAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var value = await ReadRawByteAsync(cancellationToken).ConfigureAwait(false);
            if (_skipNextLineFeed)
            {
                _skipNextLineFeed = false;
                if (value is 0 or 10) continue;
            }
            if (value != Iac) return value;

            var command = await ReadRawByteAsync(cancellationToken).ConfigureAwait(false);
            if (command < 0) return -1;
            if (command == Iac) return Iac;

            if (command == Sb)
            {
                await DiscardSubNegotiationAsync(cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (command is Will or Wont or Do or Dont)
            {
                var option = await ReadRawByteAsync(cancellationToken).ConfigureAwait(false);
                if (option < 0) return -1;

                byte response;
                if (option == SuppressGoAhead)
                {
                    response = command is Will or Wont ? Do : Will;
                }
                else if (option == Echo)
                {
                    // We are the echoing endpoint. Accept DO ECHO and reject client WILL ECHO.
                    response = command is Do ? Will : command is Dont ? Wont : Dont;
                }
                else
                {
                    response = command is Will or Wont ? Dont : Wont;
                }

                await WriteBytesAsync(new[] { Iac, response, (byte)option }, cancellationToken).ConfigureAwait(false);
                continue;
            }

            // Ignore simple Telnet commands such as NOP and GA.
        }
    }

    private async Task DiscardSubNegotiationAsync(CancellationToken cancellationToken)
    {
        var previousWasIac = false;
        while (true)
        {
            var value = await ReadRawByteAsync(cancellationToken).ConfigureAwait(false);
            if (value < 0) return;
            if (previousWasIac && value == Se) return;
            previousWasIac = value == Iac && !previousWasIac;
        }
    }

    private async Task<int> ReadRawByteAsync(CancellationToken cancellationToken)
    {
        var read = await _stream.ReadAsync(_single.AsMemory(0, 1), cancellationToken).ConfigureAwait(false);
        return read == 0 ? -1 : _single[0];
    }

    private async Task WriteBytesAsync(byte[] bytes, CancellationToken cancellationToken)
    {
        await _stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
