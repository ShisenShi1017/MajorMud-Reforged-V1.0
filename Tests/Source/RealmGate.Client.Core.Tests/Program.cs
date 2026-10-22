using RealmGate.Client.Automation;
using RealmGate.Client.Navigation;
using RealmGate.Client.Profiles;
using RealmGate.Client.Protocol;
using RealmGate.Client.Terminal;
using RealmGate.Client.Transport;
using System.Net;
using System.Net.Sockets;
using System.Text;

var checks = new List<string>();

Require(AnsiTextParser.Parse("plain").Single() == new AnsiTextSpan("plain", AnsiTextStyle.Default), "plain-text");
checks.Add("plain-text");

var red = AnsiTextParser.Parse("\x1b[31mred\x1b[0m normal");
Require(red.Count == 2, "red-reset-span-count");
Require(red[0].Text == "red" && red[0].Style.Foreground == AnsiForegroundColor.Red, "red-foreground");
Require(red[1].Text == " normal" && red[1].Style == AnsiTextStyle.Default, "reset-default");
checks.Add("red-reset");

var bold = AnsiTextParser.Parse("\x1b[1mbold\x1b[22mnormal");
Require(bold.Count == 2 && bold[0].Style.Bold && !bold[1].Style.Bold, "bold-toggle");
checks.Add("bold-toggle");

var bright = AnsiTextParser.Parse("\x1b[97mwhite");
Require(bright.Single().Style.Foreground == AnsiForegroundColor.White, "bright-white");
checks.Add("bright-white");

var csiWithoutEscape = AnsiTextParser.Parse("[32mgreen[0m");
Require(csiWithoutEscape.Count == 1 && csiWithoutEscape[0].Text == "green" && csiWithoutEscape[0].Style.Foreground == AnsiForegroundColor.Green, "legacy-csi-without-escape");
checks.Add("legacy-csi-without-escape");

var stripped = AnsiTextParser.StripFormatting("A\x1b[2JB\x1b[31mC\x1b[0m");
Require(stripped == "ABC", "strip-control-and-sgr");
checks.Add("strip-control-and-sgr");

var resetForeground = AnsiTextParser.Parse("\x1b[34mblue\x1b[39mdefault");
Require(resetForeground.Count == 2 && resetForeground[1].Style.Foreground == AnsiForegroundColor.Default, "foreground-reset");
checks.Add("foreground-reset");

var background = AnsiTextParser.Parse("\x1b[44mblue-bg\x1b[49mdefault-bg");
Require(background.Count == 2, "background-span-count");
Require(background[0].Style.Background == AnsiBackgroundColor.Blue, "background-blue");
Require(background[1].Style.Background == AnsiBackgroundColor.Default, "background-reset");
checks.Add("background-color");

var brightBackground = AnsiTextParser.Parse("\x1b[103mbright-yellow-bg");
Require(brightBackground.Single().Style.Background == AnsiBackgroundColor.Yellow, "bright-background");
checks.Add("bright-background");

var underline = AnsiTextParser.Parse("\x1b[4munder\x1b[24mnormal");
Require(underline.Count == 2 && underline[0].Style.Underline && !underline[1].Style.Underline, "underline-toggle");
checks.Add("underline-toggle");

var blink = AnsiTextParser.Parse("\x1b[5mblink\x1b[25mnormal");
Require(blink.Count == 2 && blink[0].Style.Blink && !blink[1].Style.Blink, "blink-toggle");
checks.Add("blink-toggle");

var reverse = AnsiTextParser.Parse("\x1b[7mreverse\x1b[27mnormal");
Require(reverse.Count == 2 && reverse[0].Style.Reverse && !reverse[1].Style.Reverse, "reverse-toggle");
checks.Add("reverse-toggle");

var combined = AnsiTextParser.Parse("\x1b[1;31;40;4mmajor\x1b[0mmud");
Require(combined.Count == 2, "combined-span-count");
Require(combined[0].Style.Foreground == AnsiForegroundColor.Red &&
        combined[0].Style.Background == AnsiBackgroundColor.Black &&
        combined[0].Style.Bold && combined[0].Style.Underline,
        "combined-style");
Require(combined[1].Style == AnsiTextStyle.Default, "combined-reset");
checks.Add("combined-style");

// Original WCC evidence contract: stateful DOS/ANSI terminal behavior.
var terminal = new ClassicTerminalEmulator();
var initial = terminal.CreateSnapshot();
Require(initial.Columns == 80 && initial.Rows == 25, "wcc-default-80x25");
checks.Add("wcc-default-80x25");

Require(initial.Lines.Count == 25 &&
        initial.Lines.All(line => line.Runs.Sum(run => run.Text.Length) == 80),
        "wcc-snapshot-preserves-blank-cells");
checks.Add("wcc-snapshot-preserves-blank-cells");

terminal.Reset();
var styledCells = terminal.Process("\x1b[7;4;5;8mX");
var styledRun = styledCells.Lines[0].Runs[0];
Require(styledRun.Text.StartsWith("X", StringComparison.Ordinal) &&
        styledRun.Inverse &&
        styledRun.Underline &&
        styledRun.Blink &&
        styledRun.Concealed,
        "wcc-snapshot-preserves-cell-effects");
checks.Add("wcc-snapshot-preserves-cell-effects");

var mapStyle = terminal.Reset();
mapStyle = terminal.Process("\x1b[1;33;41mX");
var mapRun = mapStyle.Lines[0].Runs[0];
Require(mapRun.Text.StartsWith("X", StringComparison.Ordinal) &&
        mapRun.Foreground == ClassicTerminalColor.DarkYellow &&
        mapRun.Background == ClassicTerminalColor.DarkRed &&
        mapRun.Bold,
        "wcc-foreground-background-sgr");
checks.Add("wcc-foreground-background-sgr");

Require(ClassicTerminalPalette.ResolveForeground(mapRun.Foreground, mapRun.Bold) == ClassicTerminalColor.Yellow,
        "wcc-sgr1-bright-foreground");
Require(ClassicTerminalPalette.ResolveForeground(ClassicTerminalColor.DarkRed, false) == ClassicTerminalColor.DarkRed,
        "wcc-normal-red-stays-dark");
checks.Add("wcc-dos-intensity-palette");

terminal.Reset();
terminal.Process("ABC");
var cleared = terminal.Process("\x1b[2J");
Require(PlainLine(cleared, 0).Trim().Length == 0, "wcc-full-screen-clear");
checks.Add("wcc-full-screen-clear");

terminal.Reset();
var erasedLine = terminal.Process("ABCDE\x1b[3D\x1b[K");
Require(PlainLine(erasedLine, 0).StartsWith("AB   ", StringComparison.Ordinal), "wcc-erase-to-end-line");
checks.Add("wcc-erase-to-end-line");

terminal.Reset();
var rewind79 = terminal.Process("\x1b[80GZ\x1b[79DA");
Require(PlainLine(rewind79, 0)[0] == 'A' && PlainLine(rewind79, 0)[79] == 'Z', "wcc-79-column-rewind");
checks.Add("wcc-79-column-rewind");

terminal.Reset();
var positioned = terminal.Process("\x1b[6;1HX");
Require(PlainLine(positioned, 5)[0] == 'X' && positioned.CursorRow == 5 && positioned.CursorColumn == 1,
        "wcc-absolute-cursor-position");
checks.Add("wcc-absolute-cursor-position");

terminal.Reset();
var restored = terminal.Process("\x1b[5;10H\x1b[s\x1b[1;1H!\x1b[uX");
Require(PlainLine(restored, 0)[0] == '!' && PlainLine(restored, 4)[9] == 'X', "wcc-save-restore-cursor");
checks.Add("wcc-save-restore-cursor");

terminal.Reset();
var scrolled = terminal.Process("\x1b[22;24r\x1b[22;1HA\x1b[23;1HB\x1b[24;1HC\n");
Require(PlainLine(scrolled, 21)[0] == 'B' && PlainLine(scrolled, 22)[0] == 'C' && PlainLine(scrolled, 23)[0] == ' ',
        "wcc-scroll-region");
checks.Add("wcc-scroll-region");

terminal.Reset();
var carriageReturn = terminal.Process("ABC\rZ");
Require(PlainLine(carriageReturn, 0).StartsWith("ZBC", StringComparison.Ordinal), "wcc-carriage-return-rewrite");
checks.Add("wcc-carriage-return-rewrite");

terminal.Reset();
var wrapped = terminal.Process(new string('A', 80) + "B");
Require(PlainLine(wrapped, 0) == new string('A', 80) && PlainLine(wrapped, 1)[0] == 'B', "wcc-column-80-autowrap");
checks.Add("wcc-column-80-autowrap");

var cp437 = new CodePage437Encoding();
var decodedMapGlyphs = cp437.GetString(new byte[] { 0xC9, 0xCD, 0xBB, 0xBA, 0xDB, 0xBA, 0xC8, 0xCD, 0xBC });
Require(decodedMapGlyphs == "╔═╗║█║╚═╝", "wcc-cp437-byte-decoding");
checks.Add("wcc-cp437-byte-decoding");

terminal.Reset();
var cp437Glyphs = terminal.Process(decodedMapGlyphs);
Require(PlainLine(cp437Glyphs, 0).StartsWith("╔═╗║█║╚═╝", StringComparison.Ordinal), "wcc-cp437-glyph-preservation");
checks.Add("wcc-cp437-glyph-preservation");

var input = new ClassicTerminalInputBuffer();
Require(input.InsertText("look") && input.Text == "look" && input.CaretIndex == 4,
        "terminal-input-insert");
input.MoveCaret(-2);
input.InsertText("X");
Require(input.Text == "loXok" && input.CaretIndex == 3, "terminal-input-caret-insert");
Require(input.Backspace() && input.Delete() && input.Text == "lok", "terminal-input-delete");
checks.Add("terminal-input-editing");

input.Clear();
input.PasteText("say hello\r\n");
var firstSubmission = input.Submit();
Require(firstSubmission == new ClassicTerminalSubmission("say hello", false) && input.Text.Length == 0,
        "terminal-input-submit");
input.InsertText("north");
input.Submit();
Require(input.RecallHistory(-1) && input.Text == "north" &&
        input.RecallHistory(-1) && input.Text == "say hello" &&
        input.RecallHistory(1) && input.Text == "north" &&
        input.RecallHistory(1) && input.Text.Length == 0,
        "terminal-input-history");
checks.Add("terminal-input-submit-history");

input.SetPasswordMode(true);
input.InsertText("secret");
Require(input.DisplayText == "••••••" && input.CopyableText is null,
        "terminal-input-password-display");
var sensitiveSubmission = input.Submit();
Require(sensitiveSubmission == new ClassicTerminalSubmission("secret", true) &&
        !input.RecallHistory(-1),
        "terminal-input-password-submit");
input.InsertText("discarded");
input.SetPasswordMode(false);
Require(input.Text.Length == 0 && input.CopyableText is null,
        "terminal-input-password-mode-clear");
checks.Add("terminal-input-password");

var inputAutomationProfile = new ClientInputAutomationProfile
{
    AliasesEnabled = true,
    MacrosEnabled = true,
    TextTriggersEnabled = true,
    EmergencyStop = false
};
inputAutomationProfile.Aliases.Add(new ClientCommandAlias
{
    Name = "kk",
    Command = "kill {1};look {0}"
});
var safeMacro = new ClientCommandMacro { Name = "safe" };
safeMacro.Commands.Add("flee");
safeMacro.Commands.Add("look {0}");
inputAutomationProfile.Macros.Add(safeMacro);
inputAutomationProfile.TextTriggers.Add(new ClientTextTrigger
{
    Name = "Retaliate",
    MatchMode = ClientTextTriggerMatchMode.Wildcard,
    MatchText = "{attacker} attacks you*",
    Command = "attack {attacker};health",
    CooldownSeconds = 5,
    Priority = 10
});
var inputAutomation = new ClientInputAutomationRuntime(inputAutomationProfile);
Require(inputAutomation.TryResolveHumanInput("kk giant rat", out var aliasCommands) &&
        aliasCommands.SequenceEqual(["kill giant", "look giant rat"]),
        "input-automation-alias-expansion");
Require(inputAutomation.TryResolveHumanInput("/macro safe north gate", out var macroCommands) &&
        macroCommands.SequenceEqual(["flee", "look north gate"]),
        "input-automation-macro-expansion");
var triggerTime = DateTimeOffset.UtcNow;
var triggerMatch = inputAutomation.EvaluateText(
    "goblin attacks you from the shadows",
    inCombat: false,
    sessionId: "session-one",
    timestamp: triggerTime);
Require(triggerMatch is not null &&
        triggerMatch.Commands.SequenceEqual(["attack goblin", "health"]) &&
        inputAutomation.EvaluateText(
            "goblin attacks you again",
            inCombat: false,
            sessionId: "session-one",
            timestamp: triggerTime.AddSeconds(1)) is null,
        "input-automation-trigger-capture-and-cooldown");
checks.Add("input-automation-runtime");

var protocolPayload = Convert.ToBase64String(Encoding.UTF8.GetBytes("private room data"));
var protocolFrame = $"@@REFORGED-PROTOCOL|ROOMINSPECTION|{protocolPayload}\r\n";
var protocolFilter = new RealmGateClientProtocolFilter();
var protocolFirst = protocolFilter.Process("Visible game text\r\n" + protocolFrame[..17]);
var protocolSecond = protocolFilter.Process(protocolFrame[17..] + "More game text\r\n");
var protocolVisible = protocolFirst.VisibleText + protocolSecond.VisibleText;
Require(protocolVisible.Contains("Visible game text", StringComparison.Ordinal) &&
        protocolVisible.Contains("More game text", StringComparison.Ordinal) &&
        !protocolVisible.Contains("REFORGED-PROTOCOL", StringComparison.Ordinal) &&
        protocolSecond.Messages.Count == 1 &&
        protocolSecond.Messages[0] == new RealmGateClientProtocolMessage(
            "ROOMINSPECTION",
            "private room data"),
        "client-protocol-chunked-frame");

protocolFilter.NoteOutboundCommand("clientdata stats");
var hiddenEchoFirst = protocolFilter.Process("[HP=35/MA=0]: cl");
var hiddenEchoSecond = protocolFilter.Process("ientdata ");
var hiddenEchoThird = protocolFilter.Process("stats\r\nvisible after echo\r\n");
var hiddenEchoVisible = hiddenEchoFirst.VisibleText +
                        hiddenEchoSecond.VisibleText +
                        hiddenEchoThird.VisibleText;
Require(!hiddenEchoVisible.Contains("clientdata stats", StringComparison.OrdinalIgnoreCase) &&
        hiddenEchoVisible.Contains("visible after echo", StringComparison.Ordinal),
        "client-protocol-chunked-private-echo");

var malformedProtocol = protocolFilter.Process(
    "@@REFORGED-PROTOCOL|ROOMINSPECTION|not-base64!\r\nordinary text\r\n");
Require(malformedProtocol.Messages.Count == 0 &&
        malformedProtocol.VisibleText == "ordinary text\r\n",
        "client-protocol-malformed-frame-hidden");
checks.Add("client-protocol-filtering");

var initializationWire = new TestDuplexStream();
await using (var telnet = new TelnetNegotiationStream(
                 initializationWire,
                 negotiate: true,
                 terminalType: "ANSI",
                 columns: 80,
                 rows: 25,
                 captureTraffic: false))
{
    await telnet.InitializeAsync();
    RequireBytes(
        initializationWire.Written,
        [0xFF, 0xFB, 0x18, 0xFF, 0xFB, 0x1F, 0xFF, 0xFB, 0x03, 0xFF, 0xFD, 0x03],
        "telnet-initial-negotiation");

    initializationWire.ClearWritten();
    await telnet.WriteAsync(new byte[] { 0x41, 0xFF, 0x42 });
    RequireBytes(initializationWire.Written, [0x41, 0xFF, 0xFF, 0x42], "telnet-iac-write-escaping");
}
checks.Add("telnet-initial-negotiation-and-escaping");

var decodedWire = new TestDuplexStream([0x41, 0xFF, 0xFF, 0x42]);
await using (var telnet = new TelnetNegotiationStream(
                 decodedWire,
                 negotiate: false,
                 terminalType: "ANSI",
                 columns: 80,
                 rows: 25,
                 captureTraffic: false))
{
    var decoded = new byte[4];
    var count = await telnet.ReadAsync(decoded);
    Require(count == 3 && decoded.AsSpan(0, count).SequenceEqual(new byte[] { 0x41, 0xFF, 0x42 }),
            "telnet-iac-read-decoding");
}
checks.Add("telnet-iac-read-decoding");

var nawsWire = new TestDuplexStream([0xFF, 0xFD, 0x1F]);
await using (var telnet = new TelnetNegotiationStream(
                 nawsWire,
                 negotiate: true,
                 terminalType: "ANSI",
                 columns: 80,
                 rows: 25,
                 captureTraffic: false))
{
    var sink = new byte[1];
    Require(await telnet.ReadAsync(sink) == 0, "telnet-naws-control-only-read");
    RequireBytes(
        nawsWire.Written,
        [0xFF, 0xFB, 0x1F, 0xFF, 0xFA, 0x1F, 0x00, 0x50, 0x00, 0x19, 0xFF, 0xF0],
        "telnet-naws-response");
}
checks.Add("telnet-naws-response");

var token = new string('a', 64);
var gmcpPayload = System.Text.Encoding.ASCII.GetBytes($"Reforged.ClientData 1|2324|{token}");
var gmcpBytes = new byte[gmcpPayload.Length + 5];
gmcpBytes[0] = 0xFF;
gmcpBytes[1] = 0xFA;
gmcpBytes[2] = 0xC9;
gmcpPayload.CopyTo(gmcpBytes, 3);
gmcpBytes[^2] = 0xFF;
gmcpBytes[^1] = 0xF0;
RealmGateClientDataEndpointAnnouncement? announcement = null;
var gmcpWire = new TestDuplexStream(gmcpBytes);
await using (var telnet = new TelnetNegotiationStream(
                 gmcpWire,
                 negotiate: false,
                 terminalType: "ANSI",
                 columns: 80,
                 rows: 25,
                 captureTraffic: false,
                 clientDataAnnouncement: value => announcement = value))
{
    Require(await telnet.ReadAsync(new byte[1]) == 0, "telnet-gmcp-control-only-read");
}
Require(announcement == new RealmGateClientDataEndpointAnnouncement(2324, token),
        "telnet-realmgate-client-data-announcement");
checks.Add("telnet-realmgate-client-data-announcement");

using (var listener = new TcpListener(IPAddress.Loopback, 0))
{
    listener.Start();
    var endpoint = (IPEndPoint)listener.LocalEndpoint;
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    var acceptTask = listener.AcceptTcpClientAsync(timeout.Token);

    await using var transport = await ClientTransportConnection.ConnectAsync(
        IPAddress.Loopback.ToString(),
        endpoint.Port,
        ClientTransportOptions.Default with
        {
            EnableTelnetNegotiation = false,
            UseCodePage437 = true
        },
        cancellationToken: timeout.Token);
    using var peer = await acceptTask;
    await using var peerStream = peer.GetStream();

    await peerStream.WriteAsync(new byte[] { 0xB3 }, timeout.Token);
    await peerStream.FlushAsync(timeout.Token);
    var receivedCharacter = new char[1];
    Require(await transport.ReadAsync(receivedCharacter, timeout.Token) == 1 &&
            receivedCharacter[0] == '\u2502',
            "client-transport-cp437-read");

    await transport.WriteLineAsync(
        "PING",
        redactApplicationData: false,
        timeout.Token);
    var commandBytes = new byte[6];
    var commandLength = 0;
    while (commandLength < commandBytes.Length)
    {
        var read = await peerStream.ReadAsync(
            commandBytes.AsMemory(commandLength),
            timeout.Token);
        if (read == 0)
            break;
        commandLength += read;
    }

    Require(commandLength == commandBytes.Length &&
            commandBytes.AsSpan().SequenceEqual(Encoding.ASCII.GetBytes("PING\r\n")),
            "client-transport-crlf-write");
}
checks.Add("client-transport-loopback-cp437-and-crlf");

using (var listener = new TcpListener(IPAddress.Loopback, 0))
{
    listener.Start();
    var endpoint = (IPEndPoint)listener.LocalEndpoint;
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    var acceptTask = listener.AcceptTcpClientAsync(timeout.Token);
    await using var session = new ClassicTerminalSession();
    var rendered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    var passwordMode = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    var protocolMessage = new TaskCompletionSource<RealmGateClientProtocolMessage>(
        TaskCreationOptions.RunContinuationsAsynchronously);
    session.SnapshotChanged += snapshot =>
    {
        if (snapshot.Lines.Any(line =>
                string.Concat(line.Runs.Select(run => run.Text))
                    .Contains("WELCOME", StringComparison.Ordinal)))
            rendered.TrySetResult(true);
    };
    session.PasswordModeChanged += enabled =>
    {
        if (enabled)
            passwordMode.TrySetResult(true);
    };
    session.ProtocolMessageReceived += message => protocolMessage.TrySetResult(message);

    var connectTask = session.ConnectAsync(
        IPAddress.Loopback.ToString(),
        endpoint.Port,
        timeout.Token);
    using var peer = await acceptTask;
    await using var peerStream = peer.GetStream();
    var negotiation = new byte[12];
    await ReadExactAsync(peerStream, negotiation, timeout.Token);
    RequireBytes(
        negotiation,
        [0xFF, 0xFB, 0x18, 0xFF, 0xFB, 0x1F, 0xFF, 0xFB, 0x03, 0xFF, 0xFD, 0x03],
        "terminal-session-negotiation");
    await connectTask;

    var sessionProtocolPayload = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"room\":2140}"));
    await peerStream.WriteAsync(
        Encoding.ASCII.GetBytes(
            $"\x1b[1;36mWELCOME\x1b[0m\r\n@@REFORGED-PROTOCOL|ROOMDATA|{sessionProtocolPayload}\r\nPassword:"),
        timeout.Token);
    await peerStream.FlushAsync(timeout.Token);
    Require(await rendered.Task.WaitAsync(timeout.Token), "terminal-session-render");
    Require(await passwordMode.Task.WaitAsync(timeout.Token), "terminal-session-password-mode");
    Require(await protocolMessage.Task.WaitAsync(timeout.Token) ==
            new RealmGateClientProtocolMessage("ROOMDATA", "{\"room\":2140}"),
            "terminal-session-private-protocol-event");
    var sessionScreen = string.Join(
        '\n',
        session.Snapshot.Lines.Select(line => string.Concat(line.Runs.Select(run => run.Text))));
    Require(!sessionScreen.Contains("REFORGED-PROTOCOL", StringComparison.Ordinal) &&
            !sessionScreen.Contains("2140", StringComparison.Ordinal),
            "terminal-session-private-protocol-hidden");

    await session.SendLineAsync("secret", sensitive: true, timeout.Token);
    var secret = new byte[8];
    await ReadExactAsync(peerStream, secret, timeout.Token);
    RequireBytes(secret, Encoding.ASCII.GetBytes("secret\r\n"), "terminal-session-send");
    await session.DisconnectAsync();
}
checks.Add("classic-terminal-session-loopback");

var profileTestRoot = Path.Combine(
    Path.GetTempPath(),
    "RealmGate.Client.Core.Tests",
    Guid.NewGuid().ToString("N"));
try
{
    var profilePath = Path.Combine(profileTestRoot, "connection-profiles.json");
    var store = new ClientConnectionProfileCatalogStore(profilePath);
    var catalog = await store.LoadAsync();
    Require(catalog.Profiles.Count == 1 &&
            catalog.Profiles[0].ProfileName == "Local Host Classic" &&
            catalog.Profiles[0].Port == ClientConnectionProfileDefaults.TelnetPort &&
            File.Exists(profilePath),
            "connection-profile-default-catalog");

    var duplicateId = catalog.Profiles[0].ProfileId;
    catalog.Profiles.Add(new ClientConnectionProfile
    {
        ProfileId = duplicateId,
        ProfileName = "Remote Test",
        HostName = "bbs.example.test",
        Port = 4242,
        TerminalType = "ANSI",
        UseCodePage437 = true
    });
    catalog.SessionAssignments.Add(new ClientSessionProfileAssignment
    {
        SessionId = "session-one",
        ProfileId = duplicateId,
        UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-1)
    });
    catalog.SessionAssignments.Add(new ClientSessionProfileAssignment
    {
        SessionId = "session-one",
        ProfileId = duplicateId,
        UpdatedAt = DateTimeOffset.UtcNow
    });
    await store.SaveAsync(catalog);

    var reloaded = await store.LoadAsync();
    Require(reloaded.Profiles.Count == 2 &&
            reloaded.Profiles.Select(profile => profile.ProfileId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() == 2 &&
            reloaded.Profiles.Any(profile =>
                profile.ProfileName == "Remote Test" &&
                profile.HostName == "bbs.example.test" &&
                profile.Port == 4242) &&
            reloaded.SessionAssignments.Count == 1,
            "connection-profile-round-trip-normalization");

    await File.WriteAllTextAsync(profilePath, "{not-json");
    var recovered = await store.LoadAsync();
    Require(recovered.Profiles.Count == 1 &&
            Directory.EnumerateFiles(
                    profileTestRoot,
                    "connection-profiles.json.corrupt-*")
                .Any(),
            "connection-profile-corrupt-recovery");
}
finally
{
    if (Directory.Exists(profileTestRoot))
        Directory.Delete(profileTestRoot, recursive: true);
}
checks.Add("connection-profile-catalog-storage");

var automationTestRoot = Path.Combine(
    Path.GetTempPath(),
    "RealmGate.Client.Core.Tests",
    Guid.NewGuid().ToString("N"));
try
{
    Directory.CreateDirectory(automationTestRoot);
    var automationPath = Path.Combine(
        automationTestRoot,
        "automation-default.json");
    await File.WriteAllTextAsync(
        automationPath,
        """
        {
          "schemaVersion": 3,
          "name": "Default",
          "minimumCommandDelayMs": 925,
          "capabilities": {
            "aliasesEnabled": false,
            "timedEventsEnabled": true,
            "navigationEnabled": true,
            "aliases": []
          },
          "options": {
            "customMarker": "preserve-me",
            "events": {
              "pauseEventsInCombat": true,
              "pauseEventsWhileDisconnected": true,
              "scheduledEvents": [
                { "id": "stored-event", "name": "Stored", "enabled": true, "intervalMinutes": 3, "command": "score" }
              ]
            },
            "navigation": {
              "autoRecoverDesynchronizedRoute": true,
              "maximumRecoveryAttempts": 4,
              "autoRoamDelayMs": 1750,
              "characterLevel": 12,
              "preferences": {
                "avoidDamageTraps": true,
                "avoidRestrictedExits": true,
                "npcLevelBuffer": 6,
                "allowRiskyFallback": false
              }
            }
          }
        }
        """);

    var automationStore = new ClientInputAutomationProfileStore(automationPath);
    var storedAutomation = await automationStore.LoadAsync();
    Require(storedAutomation.TimedEventsEnabled && storedAutomation.NavigationEnabled &&
            storedAutomation.MinimumCommandDelayMs == 925 &&
            storedAutomation.Navigation.CharacterLevel == 12 &&
            storedAutomation.Navigation.MaximumRecoveryAttempts == 4 &&
            storedAutomation.Navigation.Preferences.NpcLevelBuffer == 6 &&
            storedAutomation.Events.ScheduledEvents.Single().Command == "score",
            "input-automation-store-loads-timed-events");
    storedAutomation.AliasesEnabled = true;
    storedAutomation.Aliases.Add(new ClientCommandAlias
    {
        Name = "zz",
        Command = "look {0}"
    });
    await automationStore.SaveAsync(storedAutomation);

    var preserved = System.Text.Json.Nodes.JsonNode.Parse(
        await File.ReadAllTextAsync(automationPath));
    Require(preserved?["capabilities"]?["navigationEnabled"]?.GetValue<bool>() == true &&
            preserved?["options"]?["customMarker"]?.GetValue<string>() == "preserve-me" &&
            preserved?["capabilities"]?["aliasesEnabled"]?.GetValue<bool>() == true &&
            preserved?["capabilities"]?["aliases"]?.AsArray().Count == 1 &&
            preserved?["capabilities"]?["timedEventsEnabled"]?.GetValue<bool>() == true &&
            preserved?["capabilities"]?["navigationEnabled"]?.GetValue<bool>() == true &&
            preserved?["minimumCommandDelayMs"]?.GetValue<int>() == 925 &&
            preserved?["options"]?["events"]?["scheduledEvents"]?.AsArray().Count == 1 &&
            preserved?["options"]?["navigation"]?["characterLevel"]?.GetValue<int>() == 12,
            "input-automation-store-preserves-unmanaged-profile-data");
}
finally
{
    if (Directory.Exists(automationTestRoot))
        Directory.Delete(automationTestRoot, recursive: true);
}
checks.Add("input-automation-profile-storage");

var gameplayState = new ClientGameplayStateTracker();
gameplayState.Append("Password: ");
Require(!gameplayState.GameplayReady && gameplayState.InCombat is null, "gameplay-state-login-fails-closed");
gameplayState.Append("\x1b[36m[HP=41/MA=0]\x1b[0m:");
Require(gameplayState.GameplayReady && gameplayState.InCombat is null, "gameplay-state-prompt-ready-combat-unknown");
gameplayState.Append("\r\n*Combat Engaged*\r\n");
Require(gameplayState.InCombat == true, "gameplay-state-combat-engaged");
gameplayState.Append("*Combat Off*\r\n");
Require(gameplayState.InCombat == false, "gameplay-state-combat-off");
gameplayState.Reset();
Require(!gameplayState.GameplayReady && gameplayState.InCombat is null, "gameplay-state-reset");
checks.Add("gameplay-state-tracker");

var routeGraphJson = """
    {
      "schema": "realmgate_navigation_graph_v2",
      "worldId": "Classic",
      "rooms": [
        { "key": "1/1", "name": "Start", "map": 1, "room": 1 },
        { "key": "1/2", "name": "Danger", "map": 1, "room": 2, "maximumNpcLevel": 50 },
        { "key": "1/3", "name": "Safe", "map": 1, "room": 3 },
        { "key": "2/1", "name": "Finish", "map": 2, "room": 1 },
        { "key": "3/1", "name": "Trap Only", "map": 3, "room": 1 },
        { "key": "9/9", "name": "Isolated", "map": 9, "room": 9 }
      ],
      "edges": [
        { "from": "1/1", "to": "1/2", "command": "e", "gateKind": "open" },
        { "from": "1/1", "to": "1/3", "command": "s", "gateKind": "open" },
        { "from": "1/3", "to": "2/1", "command": "portal", "gateKind": "text_command" },
        { "from": "1/1", "to": "3/1", "command": "n", "gateKind": "damage_trap", "qualifier": "Trap, 45 damage" },
        { "from": "missing", "to": "2/1", "command": "bad" }
      ]
    }
    """;
using (var routeGraphStream = new MemoryStream(Encoding.UTF8.GetBytes(routeGraphJson)))
{
    var routeGraph = ClientRouteGraph.Load(routeGraphStream);
    Require(routeGraph.RoomCount == 6 && routeGraph.HasHazardMetadata && routeGraph.WorldId == "Classic",
        "route-graph-room-count-and-authority");
    Require(ClientRouteGraph.NormalizeRoomKey("room.map1.2140") == "1/2140" &&
            ClientRouteGraph.NormalizeRoomKey("map2:1") == "2/1",
            "route-graph-map-qualified-normalization");
    Require(routeGraph.Search("finish").Single().Key == "2/1", "route-graph-search");
    var routePlan = routeGraph.Plan("map1:1", "room.map2.1");
    Require(routePlan is not null &&
            routePlan.Steps.Select(step => step.Command).SequenceEqual(["s", "portal"]) &&
            routePlan.Steps[1].ToRoomKey == "2/1",
            "route-graph-hazard-aware-plan");
    var strictPreferences = new ClientRoutePreferences();
    Require(routeGraph.Plan("1/1", "3/1", strictPreferences, 1) is null,
        "route-graph-trap-fails-closed");
    strictPreferences.AllowRiskyFallback = true;
    var riskyPlan = routeGraph.Plan("1/1", "3/1", strictPreferences, 1);
    Require(riskyPlan is { UsedRiskyFallback: true, RiskScore: 500 } &&
            riskyPlan.Hazards.Single().Category == "Damage trap",
        "route-graph-explicit-risky-fallback");
    Require(routeGraph.GetSafeRoamChoices("1/1", null, strictPreferences, 1)
                .All(step => step.ToRoomKey != "3/1"),
        "route-graph-auto-roam-never-uses-forbidden-fallback");
    Require(routeGraph.Plan("1/1", "9/9") is null, "route-graph-unreachable-fails-closed");
}
checks.Add("route-graph-search-plan-normalization");
checks.Add("route-graph-hazard-recovery-roam-policy");

var recordedRouteRoot = Path.Combine(
    Path.GetTempPath(),
    "RealmGate.Client.Core.Tests",
    Guid.NewGuid().ToString("N"));
try
{
    var recordedRoutePath = Path.Combine(recordedRouteRoot, "recorded-routes.json");
    var recordedRouteStore = new ClientRecordedRouteStore(recordedRoutePath);
    var recordedCatalog = new ClientRecordedRouteCatalog();
    var recordedRoute = new ClientRecordedRoute
    {
        Name = "  River run  ",
        WorldId = "Custom 1",
        StartRoomKey = "room.map1.1",
        DestinationRoomKey = "map1:3"
    };
    recordedRoute.Steps.Add(new ClientRecordedRouteStep
    {
        FromRoomKey = "room.map1.1",
        ToRoomKey = "1/3",
        Command = "  south  "
    });
    recordedCatalog.Routes.Add(recordedRoute);
    await recordedRouteStore.SaveAsync(recordedCatalog);
    var reloadedRoutes = await recordedRouteStore.LoadAsync();
    var reloadedRoute = reloadedRoutes.Routes.Single();
    Require(reloadedRoute.Name == "River run" &&
            reloadedRoute.WorldId == "Custom1" &&
            reloadedRoute.StartRoomKey == "1/1" &&
            reloadedRoute.DestinationRoomKey == "1/3" &&
            reloadedRoute.Steps.Single().Command == "south",
        "recorded-route-world-qualified-round-trip");
}
finally
{
    if (Directory.Exists(recordedRouteRoot))
        Directory.Delete(recordedRouteRoot, recursive: true);
}
checks.Add("recorded-route-storage");

var bundledRouteGraphPath = Path.Combine(
    Directory.GetCurrentDirectory(),
    "Apps", "Source", "RealmGate.App", "Assets", "Paths", "classic_route_graph.json");
if (File.Exists(bundledRouteGraphPath))
{
    var bundledRouteGraph = ClientRouteGraph.Load(bundledRouteGraphPath);
    Require(bundledRouteGraph.RoomCount == 26668 &&
            bundledRouteGraph.HasHazardMetadata &&
            bundledRouteGraph.WorldId.Equals("Classic", StringComparison.OrdinalIgnoreCase),
        "bundled-route-graph-room-count");
    Require(bundledRouteGraph.Plan("1/1", "1/2") is { Steps.Count: > 0 },
        "bundled-route-graph-live-plan");
    checks.Add("bundled-classic-route-graph");
}

var eventScheduler = new ClientTimedEventScheduler();
var eventOptions = new EventAutomationOptions();
var scheduled = new ClientScheduledEvent
{
    Id = "event", Name = "Status", Enabled = true, IntervalSeconds = 60, Command = "score;inventory"
};
eventOptions.ScheduledEvents.Add(scheduled);
var eventClock = new ClientTimedEventContext("one", DateTimeOffset.Parse("2026-09-19T00:00:00Z"), true, true, false, true);
Require(eventScheduler.Evaluate(eventOptions, eventClock) is null, "events-prime");
Require(eventScheduler.Evaluate(eventOptions, eventClock with { Timestamp = eventClock.Timestamp.AddSeconds(59) }) is null, "events-before-deadline");
Require(eventScheduler.Evaluate(eventOptions, eventClock with { Timestamp = eventClock.Timestamp.AddSeconds(60) })?.Commands.SequenceEqual(["score", "inventory"]) == true, "events-exact-deadline-multistep");
checks.Add("events-deadline-multistep");

var lateClock = eventClock with { Timestamp = eventClock.Timestamp.AddDays(1) };
Require(eventScheduler.Evaluate(eventOptions, lateClock) is not null, "events-late-once");
Require(eventScheduler.Evaluate(eventOptions, lateClock) is null, "events-no-catchup-burst");
Require(eventScheduler.Evaluate(eventOptions, eventClock) is null, "events-clock-backward");
checks.Add("events-no-catchup-or-clock-rollback-burst");

Require(eventScheduler.Evaluate(eventOptions, lateClock with { Connected = false }) is null, "events-disconnected");
Require(eventScheduler.Evaluate(eventOptions, lateClock) is null, "events-reconnect-reprime");
eventOptions.PauseEventsWhileDisconnected = false;
Require(eventScheduler.Evaluate(eventOptions, lateClock with { Connected = false }) is null, "events-never-send-offline-even-legacy-option");
Require(eventScheduler.Evaluate(eventOptions, lateClock) is null, "events-legacy-offline-reprime");
checks.Add("events-disconnect-resets-session");

scheduled.RunImmediately = true;
eventScheduler.Reset();
Require(eventScheduler.Evaluate(eventOptions, eventClock with { GameplayReady = false }) is null, "events-login-blocked");
Require(eventScheduler.Evaluate(eventOptions, eventClock with { Enabled = false }) is null, "events-stop-policy-blocked");
Require(eventScheduler.Evaluate(eventOptions, eventClock) is not null, "events-run-immediately");
Require(eventScheduler.Evaluate(eventOptions, eventClock) is null, "events-immediate-not-every-tick");
checks.Add("events-readiness-emergency-policy");

Require(eventScheduler.Evaluate(eventOptions, eventClock with { SessionId = "two" }) is not null, "events-other-session-independent");
eventScheduler.ResetSession("one");
Require(eventScheduler.Evaluate(eventOptions, eventClock) is not null, "events-reset-session");
Require(eventScheduler.Evaluate(eventOptions, eventClock with { SessionId = "TWO" }) is null, "events-reset-does-not-reset-other-session");
checks.Add("events-session-isolation");

eventScheduler.Reset();
Require(eventScheduler.Evaluate(eventOptions, eventClock with { InCombat = true }) is null, "events-global-combat-pause");
Require(eventScheduler.Evaluate(eventOptions, eventClock with { InCombat = null }) is null, "events-global-unknown-combat-pause");
eventOptions.PauseEventsInCombat = false;
Require(eventScheduler.Evaluate(eventOptions, eventClock with { InCombat = null }) is null, "events-out-of-combat-needs-evidence");
Require(eventScheduler.Evaluate(eventOptions, eventClock with { InCombat = true }) is null, "events-out-of-combat-restriction");
scheduled.OnlyOutOfCombat = false;
scheduled.OnlyInCombat = true;
Require(eventScheduler.Evaluate(eventOptions, eventClock) is null, "events-in-combat-restriction");
Require(eventScheduler.Evaluate(eventOptions, eventClock with { InCombat = null }) is null, "events-in-combat-needs-evidence");
Require(eventScheduler.Evaluate(eventOptions, eventClock with { InCombat = true }) is not null, "events-in-combat-allowed");
checks.Add("events-combat-conditions-fail-closed");

eventScheduler.Reset();
scheduled.OnlyOutOfCombat = true;
Require(eventScheduler.Evaluate(eventOptions, eventClock with { InCombat = true }) is null, "events-contradictory-combat-flags");
scheduled.OnlyInCombat = false;
Require(eventScheduler.Evaluate(eventOptions, eventClock with { MinimumCommandDelayMs = 1000, LastCommandAt = eventClock.Timestamp.AddMilliseconds(-999) }) is null, "events-shared-command-delay");
Require(eventScheduler.Evaluate(eventOptions, eventClock with { MinimumCommandDelayMs = 1000, LastCommandAt = eventClock.Timestamp.AddSeconds(-1) }) is not null, "events-shared-delay-boundary");
checks.Add("events-shared-command-throttle");

eventScheduler.Reset();
eventOptions.ScheduledEvents.Add(new ClientScheduledEvent { Id = "b", Command = "look", Enabled = true, RunImmediately = true, Priority = 1 });
eventOptions.ScheduledEvents.Add(new ClientScheduledEvent { Id = "a", Command = "health", Enabled = true, RunImmediately = true, Priority = 1 });
Require(eventScheduler.Evaluate(eventOptions, eventClock)?.RuleId == "a", "events-priority-stable-id");
Require(eventScheduler.Evaluate(eventOptions, eventClock)?.RuleId == "b", "events-next-due-priority");
Require(eventScheduler.Evaluate(eventOptions, eventClock)?.RuleId == "event", "events-lower-priority");
checks.Add("events-priority-and-id-order");

eventOptions.ScheduledEvents.Clear();
eventOptions.ScheduledEvents.Add(scheduled);
eventScheduler.Reset();
scheduled.RunImmediately = false;
scheduled.IntervalSeconds = 0;
scheduled.IntervalMinutes = 2;
Require(eventScheduler.Evaluate(eventOptions, eventClock) is null, "events-minutes-prime");
Require(eventScheduler.Evaluate(eventOptions, eventClock with { Timestamp = eventClock.Timestamp.AddSeconds(119) }) is null, "events-minutes-before");
Require(eventScheduler.Evaluate(eventOptions, eventClock with { Timestamp = eventClock.Timestamp.AddMinutes(2) }) is not null, "events-minutes-due");
eventScheduler.Reset();
scheduled.IntervalMinutes = int.MaxValue;
Require(eventScheduler.Evaluate(eventOptions, eventClock) is null, "events-large-interval-prime");
Require(eventScheduler.Evaluate(eventOptions, lateClock) is null, "events-large-interval-no-overflow");
checks.Add("events-minute-interval-no-overflow");

var eventJsonOptions = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
var eventJson = System.Text.Json.JsonSerializer.Serialize(eventOptions, eventJsonOptions);
var roundTripEvents = System.Text.Json.JsonSerializer.Deserialize<EventAutomationOptions>(eventJson, eventJsonOptions)!;
Require(roundTripEvents.ScheduledEvents.Single().Command == "score;inventory" && !roundTripEvents.PauseEventsWhileDisconnected,
    "events-json-preserves-existing-schema");
checks.Add("events-shared-model-json-roundtrip");

Console.WriteLine("RealmGate Client Core Regression");
foreach (var check in checks) Console.WriteLine("PASS " + check);
Console.WriteLine($"Passed {checks.Count} checks.");
return 0;

static string PlainLine(ClassicTerminalSnapshot snapshot, int row) =>
    string.Concat(snapshot.Lines[row].Runs.Select(run => run.Text));

static void Require(bool condition, string code)
{
    if (!condition) throw new InvalidOperationException(code);
}

static void RequireBytes(byte[] actual, byte[] expected, string code)
{
    if (!actual.AsSpan().SequenceEqual(expected))
        throw new InvalidOperationException($"{code}: {Convert.ToHexString(actual)} != {Convert.ToHexString(expected)}");
}

static async Task ReadExactAsync(
    Stream stream,
    Memory<byte> buffer,
    CancellationToken cancellationToken)
{
    var total = 0;
    while (total < buffer.Length)
    {
        var read = await stream.ReadAsync(buffer[total..], cancellationToken);
        if (read == 0)
            throw new EndOfStreamException();
        total += read;
    }
}

sealed class TestDuplexStream : Stream
{
    private readonly Queue<byte> _incoming;
    private readonly MemoryStream _written = new();

    public TestDuplexStream(IEnumerable<byte>? incoming = null) =>
        _incoming = new Queue<byte>(incoming ?? []);

    public byte[] Written => _written.ToArray();

    public void ClearWritten() => _written.SetLength(0);

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public override int Read(byte[] buffer, int offset, int count)
    {
        var written = Math.Min(count, _incoming.Count);
        for (var index = 0; index < written; index++)
            buffer[offset + index] = _incoming.Dequeue();
        return written;
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var written = Math.Min(buffer.Length, _incoming.Count);
        for (var index = 0; index < written; index++)
            buffer.Span[index] = _incoming.Dequeue();
        return ValueTask.FromResult(written);
    }

    public override void Write(byte[] buffer, int offset, int count) =>
        _written.Write(buffer, offset, count);

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        _written.Write(buffer.Span);
        return ValueTask.CompletedTask;
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
}
