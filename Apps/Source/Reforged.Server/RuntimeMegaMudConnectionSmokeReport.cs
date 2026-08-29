using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeMegaMudConnectionSmokeReport(
    bool Passed,
    IReadOnlyList<string> Checks)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

    public static async Task<RuntimeMegaMudConnectionSmokeReport> RunAsync()
    {
        var checks = new List<string>();
        if (!ServerNetworkHost.IsMegaMudLaunchCommand("/go majormud") ||
            !ServerNetworkHost.IsMegaMudLaunchCommand("go majormud") ||
            !ServerNetworkHost.IsMegaMudLaunchCommand("majormud") ||
            ServerNetworkHost.IsMegaMudLaunchCommand("/go somethingelse"))
            throw new InvalidOperationException("MegaMUD direct-login launch token recognition regressed.");
        checks.Add("Recovered MegaMUD `/go majormud` auto-login launch token is recognized explicitly.");

        if (!ServerNetworkHost.TryParseMegaMudLaunchCommand("/go majormud 2", out var numericSelector) ||
            !string.Equals(numericSelector, "2", StringComparison.Ordinal) ||
            !ServerNetworkHost.TryParseMegaMudLaunchCommand("/go majormud Shisen Shi", out var namedSelector) ||
            !string.Equals(namedSelector, "Shisen Shi", StringComparison.Ordinal))
            throw new InvalidOperationException("MegaMUD character-selector launch parsing regressed.");

        var characterNames = new[] { "Alpha", "Shisen Shi", "Gamma" };
        if (ServerNetworkHost.ResolveMegaMudCharacterSelectorIndex(characterNames, "1") != 0 ||
            ServerNetworkHost.ResolveMegaMudCharacterSelectorIndex(characterNames, "2") != 1 ||
            ServerNetworkHost.ResolveMegaMudCharacterSelectorIndex(characterNames, "shisen shi") != 1 ||
            ServerNetworkHost.ResolveMegaMudCharacterSelectorIndex(characterNames, "4") != -1 ||
            ServerNetworkHost.ResolveMegaMudCharacterSelectorIndex(characterNames, "missing") != -1)
            throw new InvalidOperationException("MegaMUD deterministic character selection regressed.");
        checks.Add("MegaMUD direct login supports deterministic 1-based character slots or exact case-insensitive character names.");

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;

        using var client = new TcpClient();
        var connectTask = client.ConnectAsync(IPAddress.Loopback, endpoint.Port, timeout.Token);
        using var accepted = await listener.AcceptTcpClientAsync(timeout.Token).ConfigureAwait(false);
        await connectTask.ConfigureAwait(false);
        using var clientStream = client.GetStream();
        await using var telnet = new TelnetConnection(accepted.GetStream());

        await telnet.NegotiateAsync(timeout.Token).ConfigureAwait(false);
        var negotiation = await ReadExactAsync(clientStream, 9, timeout.Token).ConfigureAwait(false);
        RequireSequence(negotiation, new byte[]
        {
            255, 251, 3,   // IAC WILL SUPPRESS-GO-AHEAD
            255, 251, 1,   // IAC WILL ECHO
            255, 251, 201  // IAC WILL GMCP; legacy clients may reject this option
        }, "initial Telnet negotiation");
        checks.Add("Telnet negotiation keeps classic server echo/SGA and offers the Reforged-only GMCP option without requiring it.");

        // Recovered MegaMUD is a legacy Telnet client. Rejecting the unknown GMCP option must
        // keep the private Reforged client-data channel disabled while ordinary line input works.
        var lineTask = telnet.ReadLineAsync(hideInput: false, timeout.Token);
        var echoReassert = await ReadExactAsync(clientStream, 3, timeout.Token).ConfigureAwait(false);
        RequireSequence(echoReassert, new byte[] { 255, 251, 1 }, "server echo reassertion");

        await clientStream.WriteAsync(new byte[] { 255, 254, 201 }, timeout.Token).ConfigureAwait(false); // IAC DONT GMCP
        var gmcpReject = await ReadExactAsync(clientStream, 3, timeout.Token).ConfigureAwait(false);
        RequireSequence(gmcpReject, new byte[] { 255, 252, 201 }, "GMCP rejection acknowledgement");

        var commandBytes = Encoding.ASCII.GetBytes("set statline full\r\n");
        await clientStream.WriteAsync(commandBytes, timeout.Token).ConfigureAwait(false);
        var line = await lineTask.ConfigureAwait(false);
        if (!string.Equals(line, "set statline full", StringComparison.Ordinal))
            throw new InvalidOperationException($"MegaMUD line input changed: '{line ?? "<null>"}'.");
        if (telnet.ReforgedClientDataEnabled)
            throw new InvalidOperationException("Legacy MegaMUD negotiation incorrectly enabled the private Reforged client-data channel.");

        var echoed = await ReadExactAsync(clientStream, commandBytes.Length, timeout.Token).ConfigureAwait(false);
        RequireSequence(echoed, commandBytes, "classic server-side input echo");
        checks.Add("A legacy client may reject GMCP and still send normal MajorMUD commands; no private client-data token is enabled.");

        await telnet.WriteAsync("é\n", timeout.Token).ConfigureAwait(false);
        var cp437 = await ReadExactAsync(clientStream, 3, timeout.Token).ConfigureAwait(false);
        RequireSequence(cp437, new byte[] { 0x82, 13, 10 }, "CP437/CRLF application output");
        checks.Add("Application output uses CP437 bytes and CRLF network line endings.");

        listener.Stop();
        return new RuntimeMegaMudConnectionSmokeReport(true, checks);
    }

    private static async Task<byte[]> ReadExactAsync(NetworkStream stream, int count, CancellationToken cancellationToken)
    {
        var buffer = new byte[count];
        var offset = 0;
        while (offset < count)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, count - offset), cancellationToken).ConfigureAwait(false);
            if (read == 0) throw new IOException($"Connection closed while reading {count} bytes; received {offset}.");
            offset += read;
        }
        return buffer;
    }

    private static void RequireSequence(IReadOnlyList<byte> actual, IReadOnlyList<byte> expected, string label)
    {
        if (actual.Count == expected.Count && actual.SequenceEqual(expected)) return;
        throw new InvalidOperationException(
            $"Unexpected {label}: actual={Convert.ToHexString(actual.ToArray())}; expected={Convert.ToHexString(expected.ToArray())}.");
    }
}
