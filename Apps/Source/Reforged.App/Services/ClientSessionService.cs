using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using Reforged.App.Models;

namespace Reforged.App.Services;

public interface IClientSessionService : IAsyncDisposable
{
    ObservableCollection<ClientSessionConnection> Sessions { get; }
    event EventHandler<ClientTerminalLineEvent>? LineReceived;
    event EventHandler<ClientCommandSentEvent>? CommandSent;
    event EventHandler<ClientSessionStateEvent>? StateChanged;
    event EventHandler<RouteExecutionEvent>? RouteChanged;
    ClientSessionConnection EnsureSession(string sessionId, string displayName, string characterName = "");
    Task ConnectAsync(string sessionId, CancellationToken cancellationToken = default);
    Task DisconnectAsync(string sessionId, bool suppressReconnect = true);
    Task SendCommandAsync(string sessionId, string command, CancellationToken cancellationToken = default);
    Task SendCommandAsAsync(string sessionId, string command, ClientCommandOrigin origin, CancellationToken cancellationToken = default);
    Task SendSensitiveCommandAsync(string sessionId, string command, CancellationToken cancellationToken = default);
    Task SendSensitiveCommandAsAsync(string sessionId, string command, ClientCommandOrigin origin, CancellationToken cancellationToken = default);
    Task SendClientProtocolCommandAsync(string sessionId, string command, CancellationToken cancellationToken = default);
    Task SendKeyAsync(string sessionId, string sequence, CancellationToken cancellationToken = default);
    void ResizeClassicTerminal(string sessionId, int columns, int rows);
    Task StartRouteAsync(string sessionId, MegaMudPathRecord route, CancellationToken cancellationToken = default);
    void PauseRoute(string sessionId);
    void ResumeRoute(string sessionId);
    void StopRoute(string sessionId, string reason = "Stopped by user");
    void PrepareRouteRecovery(string sessionId);
    void ClearTerminal(string sessionId);
    void ResetAnsi(string sessionId);
    void ResetClientInternals(string sessionId);
    Task<string> ExportTerminalLogAsync(string sessionId, string? destinationPath = null, CancellationToken cancellationToken = default);
}

public sealed partial class ClientSessionService : IClientSessionService
{
    private static readonly Regex ClassicStatlinePattern = new(
        @"^\s*\[(?:HP|H)\s*[=:]\s*[^\]]+\]\s*:?\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static bool IsClassicStatline(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        ClassicStatlinePattern.IsMatch(StripAnsiForPromptDetection(value));

    private static readonly Regex TerminalControlSequence = new(
        @"(?:\x1B\][^\a]*(?:\a|\x1B\\)|\x1B\[[0-?]*[ -/]*[@-~]|\x1B[@-_]|\u009B[0-?]*[ -/]*[@-~])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static string StripAnsiForPromptDetection(string value)
    {
        var withoutSequences = TerminalControlSequence.Replace(
            value ?? string.Empty,
            string.Empty);

        return new string(
            withoutSequences
                .Where(character =>
                    character == '\t' ||
                    !char.IsControl(character))
                .ToArray());
    }

    // Every route and automation action remains a normal Host command; the Host stays authoritative.
    private const string ConfirmedStatus = "Confirmed";
    private const string UnverifiedTopologyStatus = "Confirmed (unverified)";
    private sealed class Runtime
    {
        public TcpClient? Client;
        public TelnetNegotiationStream? TelnetStream;
        public StreamReader? Reader;
        public StreamWriter? Writer;
        public CancellationTokenSource? ReadCts;
        public CancellationTokenSource? RouteCts;
        public Task? ReadTask;
        public readonly StringBuilder ReceiveBuffer = new();
        public string PartialDisplayText = string.Empty;
        public long ReceiveRevision;
        public long ConnectionGeneration;
        public readonly ClassicTerminalEmulator ClassicTerminal = new(80, 25);
        public readonly ClassicPromptStreamScanner PromptScanner = new();
        public readonly ClassicCombatStreamParser CombatParser = new();
        public readonly ClassicRoomDisplayStreamParser RoomParser = new();
        public readonly ClassicStatScreenParser StatParser = new();
        public readonly ClassicInventoryEquipmentParser ItemScreenParser = new();
        public readonly ClassicSpellbookParser SpellbookParser = new();
        public readonly ClassicPartyScreenParser PartyParser = new();
        public readonly ClassicWhoScreenParser WhoParser = new();
        public readonly ClassicBossHomeParser BossHomeParser = new();
        public readonly ClassicLibrariumCatalogParser LibrariumParser = new();
        public readonly ClassicLibrariumPageParser LibrariumPageParser = new();
        public readonly ReforgedClientProtocolFilter ProtocolFilter = new();
        public Task? RouteTask;
        public bool SuppressReconnect;
        public readonly SemaphoreSlim SendGate = new(1, 1);
        public readonly ClientAutomationCommandDeduplicator AutomationDeduplicator = new();
        public readonly SemaphoreSlim TelnetTraceGate = new(1, 1);
        public string TelnetTracePath = string.Empty;
        public readonly SemaphoreSlim RouteSignal = new(0, 1);
        public TaskCompletionSource<RouteRoomConfirmation>? RoomConfirmation;
        public string ExpectedPreviousRoom = string.Empty;
        public string ExpectedRoomKey = string.Empty;
    }

    private readonly ConcurrentDictionary<string, Runtime> _runtime = new(StringComparer.OrdinalIgnoreCase);
    private readonly IClientParsingProfileService _parsing;

    public ClientSessionService(IClientParsingProfileService parsing) => _parsing = parsing;
    public ObservableCollection<ClientSessionConnection> Sessions { get; } = [];
    public event EventHandler<ClientTerminalLineEvent>? LineReceived;
    public event EventHandler<ClientCommandSentEvent>? CommandSent;
    public event EventHandler<ClientSessionStateEvent>? StateChanged;
    public event EventHandler<RouteExecutionEvent>? RouteChanged;

    public ClientSessionConnection EnsureSession(string sessionId, string displayName, string characterName = "")
    {
        var session = Sessions.FirstOrDefault(s => string.Equals(s.SessionId, sessionId, StringComparison.OrdinalIgnoreCase));
        if (session is not null)
        {
            session.DisplayName = displayName;
            session.CharacterName = characterName;
            return session;
        }

        session = new ClientSessionConnection { SessionId = sessionId, DisplayName = displayName, CharacterName = characterName };
        Sessions.Add(session);
        _runtime.TryAdd(sessionId, new Runtime());
        return session;
    }

    public async Task ConnectAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var session = GetSession(sessionId);
        var runtime = _runtime.GetOrAdd(sessionId, _ => new Runtime());
        if (session.ConnectionState is ClientConnectionState.Connected or ClientConnectionState.Connecting) return;

        runtime.SuppressReconnect = false;

        // Fully retire any previous connection before clearing the visible session.
        // This prevents a late read from the old socket from racing the new login.
        await CloseTransportAsync(runtime);
        await ResetSessionTerminalAsync(session, runtime);
        await SetStateAsync(session, ClientConnectionState.Connecting, $"Connecting to {session.HostName}:{session.Port}…");

        try
        {
            var client = new TcpClient { NoDelay = true };
            await client.ConnectAsync(session.HostName, session.Port, cancellationToken);

            var stream = client.GetStream();
            var screen = runtime.ClassicTerminal.CreateSnapshot();
            var encoding = CreateTerminalEncoding(
                session.TerminalPreferences.UseCodePage437);

            runtime.TelnetTracePath = session.TerminalPreferences.CaptureTelnetTraffic
                ? CreateTelnetTracePath(session)
                : string.Empty;

            var telnetStream = new TelnetNegotiationStream(
                stream,
                session.TerminalPreferences.EnableTelnetNegotiation,
                session.TerminalPreferences.TerminalType,
                screen.Columns,
                screen.Rows,
                session.TerminalPreferences.CaptureTelnetTraffic,
                (message, traffic) =>
                    HandleTelnetDiagnostic(
                        session,
                        runtime,
                        message,
                        traffic));

            var reader = new StreamReader(
                telnetStream,
                encoding,
                detectEncodingFromByteOrderMarks: false,
                bufferSize: 4096,
                leaveOpen: true);

            var writer = new StreamWriter(
                telnetStream,
                encoding,
                bufferSize: 4096,
                leaveOpen: true)
            {
                AutoFlush = true,
                NewLine = "\r\n"
            };

            var readCts = new CancellationTokenSource();
            var generation = Interlocked.Increment(
                ref runtime.ConnectionGeneration);

            runtime.Client = client;
            runtime.TelnetStream = telnetStream;
            runtime.Reader = reader;
            runtime.Writer = writer;
            runtime.ReadCts = readCts;

            await OnUiAsync(() =>
            {
                session.TerminalColumns = screen.Columns;
                session.TerminalRows = screen.Rows;
                session.TelnetStatus =
                    $"Negotiating {session.TerminalPreferences.TerminalType} "
                    + $"{screen.Columns}x{screen.Rows}";
            });

            await telnetStream.InitializeAsync(cancellationToken);

            session.ConnectedAt = DateTimeOffset.Now;
            session.SessionExperienceGained = 0;
            session.SessionCombatEventCount = 0;
            session.SessionRoomsObserved = 0;
            session.SessionExperienceSamples.Clear();
            session.SessionExperienceSamples.Add(new ClientExperienceSample(session.ConnectedAt.Value, 0));
            await SetStateAsync(session, ClientConnectionState.Connected, $"Connected to {session.HostName}:{session.Port}");

            var readTask = ReadLoopAsync(session, runtime, reader, generation, readCts.Token);
            runtime.ReadTask = readTask;
        }
        catch (Exception exception) when (exception is SocketException or IOException or OperationCanceledException)
        {
            await CloseTransportAsync(runtime);
            await SetStateAsync(session, ClientConnectionState.Faulted, exception.Message);
            if (!runtime.SuppressReconnect && session.AutoReconnect && !cancellationToken.IsCancellationRequested)
                _ = ReconnectAsync(session, runtime);
        }
    }

    public async Task DisconnectAsync(string sessionId, bool suppressReconnect = true)
    {
        var session = GetSession(sessionId);
        var runtime = _runtime.GetOrAdd(sessionId, _ => new Runtime());
        runtime.SuppressReconnect = suppressReconnect;
        StopRoute(sessionId, "Disconnected");
        await CloseTransportAsync(runtime);
        await ResetSessionTerminalAsync(session, runtime);
        await SetStateAsync(session, ClientConnectionState.Disconnected, "Disconnected");
    }

    public Task SendCommandAsync(string sessionId, string command, CancellationToken cancellationToken = default) =>
        SendCommandCoreAsync(sessionId, command, echoToTerminal: true, ClientCommandOrigin.Human, cancellationToken);

    public Task SendCommandAsAsync(string sessionId, string command, ClientCommandOrigin origin, CancellationToken cancellationToken = default) =>
        SendCommandCoreAsync(sessionId, origin == ClientCommandOrigin.Borin ? FormatBorinCommand(command) : command,
            echoToTerminal: true, origin, cancellationToken);

    public Task SendSensitiveCommandAsync(string sessionId, string command, CancellationToken cancellationToken = default) =>
        SendCommandCoreAsync(sessionId, command, echoToTerminal: false, ClientCommandOrigin.Human, cancellationToken);

    public Task SendSensitiveCommandAsAsync(string sessionId, string command, ClientCommandOrigin origin, CancellationToken cancellationToken = default) =>
        SendCommandCoreAsync(sessionId, command, echoToTerminal: false, origin, cancellationToken);

    private static string FormatBorinCommand(string command)
    {
        var normalized = Regex.Replace(command?.Trim() ?? string.Empty, @"\s+", " ");
        if (normalized.Length == 0) return normalized;
        var split = normalized.IndexOf(' ');
        if (split < 0) return normalized.ToLowerInvariant();
        var root = normalized[..split].ToLowerInvariant();
        var arguments = normalized[(split + 1)..];
        // Preserve Borin's authored speech and character names; ordinary command
        // targets produced by deterministic planners are rendered naturally.
        return root is "say" or "tell" or "telepath" or "group" or "party" or "gos" or "gossip" or "broadgang" or "bg"
            ? $"{root} {arguments}"
            : $"{root} {arguments.ToLowerInvariant()}";
    }

    public Task SendClientProtocolCommandAsync(string sessionId, string command, CancellationToken cancellationToken = default) =>
        SendCommandCoreAsync(sessionId, command, echoToTerminal: false, ClientCommandOrigin.ClientProtocol, cancellationToken);

    public Task SendKeyAsync(string sessionId, string sequence, CancellationToken cancellationToken = default) =>
        SendRawCoreAsync(sessionId, sequence, cancellationToken);

    public void ResizeClassicTerminal(string sessionId, int columns, int rows)
    {
        var session = GetSession(sessionId);
        var runtime = _runtime.GetOrAdd(sessionId, _ => new Runtime());
        var snapshot = runtime.ClassicTerminal.Resize(columns, rows);

        _ = OnUiAsync(() =>
        {
            session.ClassicScreen = snapshot;
            session.TerminalColumns = snapshot.Columns;
            session.TerminalRows = snapshot.Rows;
        });

        if (runtime.TelnetStream is not null)
            _ = UpdateTelnetWindowSizeAsync(
                session,
                runtime,
                snapshot.Columns,
                snapshot.Rows);
    }

    private async Task SendCommandCoreAsync(string sessionId, string command, bool echoToTerminal, ClientCommandOrigin origin, CancellationToken cancellationToken)
    {
        var session = GetSession(sessionId);
        var runtime = _runtime.GetOrAdd(sessionId, _ => new Runtime());
        if (session.ConnectionState != ClientConnectionState.Connected || runtime.Writer is null)
            throw new InvalidOperationException($"{session.DisplayName} is not connected.");

        await runtime.SendGate.WaitAsync(cancellationToken);
        try
        {
            var text = command?.Trim() ?? string.Empty;
            if (!runtime.AutomationDeduplicator.ShouldSend(text, origin, DateTimeOffset.UtcNow)) return;
            if (origin == ClientCommandOrigin.ClientProtocol)
            {
                runtime.ProtocolFilter.NoteOutboundCommand(text);
                var parserCommand = text.StartsWith("clientdata ", StringComparison.OrdinalIgnoreCase)
                    ? text["clientdata ".Length..].Trim()
                    : text;
                runtime.RoomParser.NoteOutboundCommand(parserCommand);
                runtime.StatParser.NoteOutboundCommand(parserCommand);
                runtime.ItemScreenParser.NoteOutboundCommand(parserCommand);
                runtime.SpellbookParser.NoteOutboundCommand(parserCommand);
                runtime.PartyParser.NoteOutboundCommand(parserCommand);
                runtime.WhoParser.NoteOutboundCommand(parserCommand);
                runtime.BossHomeParser.NoteOutboundCommand(text);
                runtime.LibrariumParser.NoteOutboundCommand(text);
                runtime.LibrariumPageParser.NoteOutboundCommand(text);
            }
            else
            {
                runtime.RoomParser.NoteOutboundCommand(text);
                runtime.StatParser.NoteOutboundCommand(text);
                runtime.ItemScreenParser.NoteOutboundCommand(text);
                runtime.SpellbookParser.NoteOutboundCommand(text);
                runtime.PartyParser.NoteOutboundCommand(text);
                runtime.WhoParser.NoteOutboundCommand(text);
            }
            using var traceRedaction = echoToTerminal
                ? null
                : runtime.TelnetStream?.RedactApplicationDataTrace();

            await runtime.Writer.WriteLineAsync(text.AsMemory(), cancellationToken);
            await runtime.Writer.FlushAsync(cancellationToken);

            await OnUiAsync(() =>
            {
                if (origin == ClientCommandOrigin.ClientProtocol)
                    return;
                if (echoToTerminal && text.Length > 0)
                    session.CommandHistory.Add(text);

                // The Host owns command echo and the next statline. Do not add
                // an artificial "> command" line to Classic scrollback.
                var sentAt = DateTimeOffset.Now;
                session.LastCommandText = echoToTerminal ? text : "[hidden]";
                session.LastCommandOrigin = origin;
                session.LastCommandAt = sentAt;
                CommandSent?.Invoke(
                    this,
                    new ClientCommandSentEvent(
                        sessionId,
                        echoToTerminal ? text : "[hidden]",
                        sentAt,
                        origin));
            });
        }
        finally { runtime.SendGate.Release(); }
    }

    private async Task SendRawCoreAsync(string sessionId, string sequence, CancellationToken cancellationToken)
    {
        var session = GetSession(sessionId);
        var runtime = _runtime.GetOrAdd(sessionId, _ => new Runtime());
        if (session.ConnectionState != ClientConnectionState.Connected || runtime.Writer is null)
            throw new InvalidOperationException($"{session.DisplayName} is not connected.");

        if (string.IsNullOrEmpty(sequence))
            return;

        await runtime.SendGate.WaitAsync(cancellationToken);
        try
        {
            await runtime.Writer.WriteAsync(sequence.AsMemory(), cancellationToken);
            await runtime.Writer.FlushAsync(cancellationToken);
        }
        finally { runtime.SendGate.Release(); }
    }

    private static async Task ResetSessionTerminalAsync(ClientSessionConnection session, Runtime runtime)
    {
        runtime.ReceiveBuffer.Clear();
        runtime.PromptScanner.Reset();
        runtime.CombatParser.Reset();
        runtime.RoomParser.Reset();
        runtime.StatParser.Reset();
        runtime.ItemScreenParser.Reset();
        runtime.SpellbookParser.Reset();
        runtime.PartyParser.Reset();
        runtime.WhoParser.Reset();
        runtime.BossHomeParser.Reset();
        runtime.LibrariumParser.Reset();
        runtime.LibrariumPageParser.Reset();
        runtime.ProtocolFilter.Reset();
        runtime.PartialDisplayText = string.Empty;
        runtime.ReceiveRevision = 0;
        var screen = runtime.ClassicTerminal.Reset();
        await OnUiAsync(() =>
        {
            session.TerminalLines.Clear();
            session.TerminalEntries.Clear();
            session.MainEntries.Clear();
            session.CombatEntries.Clear();
            session.ChatEntries.Clear();
            session.TellEntries.Clear();
            session.PartyEntries.Clear();
            session.SystemEntries.Clear();
            session.OnlinePlayers.Clear();
            session.BossHomes.Clear();
            session.ServerCatalogFamilies.Clear();
            session.ServerCatalogWorld = session.ServerCatalogCompileHash = string.Empty;
            session.ServerCatalogUpdatedAt = null;
            session.LibrariumPage = null;
            session.OnlinePlayersUpdatedAt = null;
            session.PvpState = null;
            session.PvpStateUpdatedAt = null;
            session.LastReceivedLine = string.Empty;
            session.LivePromptText = string.Empty;
            session.CurrentRoomName = string.Empty;
            session.CurrentRoomKey = string.Empty;
            session.CurrentRoomDescription = string.Empty;
            session.CurrentRoomCurrency = string.Empty;
            session.CurrentRoomExits.Clear();
            session.CurrentRoomNpcs.Clear();
            session.CurrentRoomNpcDetails.Clear();
            session.CurrentRoomNpcDetailsUpdatedAt = null;
            session.CurrentRoomPlayers.Clear();
            session.CurrentRoomItems.Clear();
            session.CurrentRoomItemDetails.Clear();
            session.CurrentRoomLinks.Clear();
            session.CurrentRoomCommands.Clear();
            session.CurrentRoomCommandDetails.Clear();
            session.CurrentRoomServices.Clear();
            session.CurrentRoomTopologyNodes.Clear();
            session.CurrentRoomTopologyLinks.Clear();
            session.ServerMaps.Clear();
            session.RoomUpdatedAt = null;
            session.ExitsUpdatedAt = null;
            session.PromptUpdatedAt = null;
            session.LastServerActivityAt = null;
            session.InCombat = false;
            session.LastCombatEvent = "None";
            session.LastCombatAmount = 0;
            session.LastCombatEventAt = null;
            session.LastCommandText = string.Empty;
            session.LastCommandOrigin = ClientCommandOrigin.Human;
            session.LastCommandAt = null;
            session.ClassicScreen = screen;
            session.TerminalColumns = screen.Columns;
            session.TerminalRows = screen.Rows;
            session.TelnetStatus = "Not connected";
        });
    }

    public Task StartRouteAsync(string sessionId, MegaMudPathRecord route, CancellationToken cancellationToken = default)
    {
        var session = GetSession(sessionId);
        var runtime = _runtime.GetOrAdd(sessionId, _ => new Runtime());
        if (session.ConnectionState != ClientConnectionState.Connected)
            throw new InvalidOperationException("Connect the selected session before starting a route.");
        if (route.Steps.Count == 0)
            throw new InvalidOperationException("The selected path contains no readable commands.");

        StopRoute(sessionId, "Starting route");
        runtime.RouteCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        session.ActiveRouteName = route.DisplayName;
        session.RouteStep = 0;
        session.RouteStepCount = route.Steps.Count;
        session.RouteDesyncCount = 0;
        session.RouteTrace.Clear();
        session.RoutePlayback.Clear();
        session.RouteMapSegments.Clear();
        session.RouteMapNodes.Clear();
        foreach (var pathStep in route.Steps)
            session.RoutePlayback.Add(new RoutePlaybackNode { Step = pathStep.Number, Command = pathStep.FriendlyCommand, ExpectedRoomKey = pathStep.ExpectedNativeRoomKey, State = RoutePlaybackNodeState.Pending });
        BuildRouteMap(session, route);
        session.LastConfirmedRouteRoom = session.CurrentRoomName;
        session.ExpectedRouteRoomKey = string.Empty;
        session.RouteTopologyStatus = "Waiting for first movement step";
        session.RouteVerifiedStepCount = 0;
        session.RouteUnverifiedStepCount = 0;
        session.RouteRecoveryAvailable = false;
        session.RouteRecoveryStatus = "Route is synchronized";
        session.RouteDestinationKey = route.NativeEndKey;
        session.RouteRecoveryPlan = "No replacement route calculated";
        SetRoute(session, RouteExecutionState.Running, "Route running");
        runtime.RouteTask = RunRouteAsync(session, runtime, route, runtime.RouteCts.Token);
        return Task.CompletedTask;
    }

    public void PauseRoute(string sessionId)
    {
        var session = GetSession(sessionId);
        if (session.RouteState != RouteExecutionState.Running) return;
        SetRoute(session, RouteExecutionState.Paused, "Route paused");
    }

    public void ResumeRoute(string sessionId)
    {
        var session = GetSession(sessionId);
        var runtime = _runtime.GetOrAdd(sessionId, _ => new Runtime());
        if (session.RouteState != RouteExecutionState.Paused) return;
        SetRoute(session, RouteExecutionState.Running, "Route resumed");
        if (runtime.RouteSignal.CurrentCount == 0) runtime.RouteSignal.Release();
    }

    public void StopRoute(string sessionId, string reason = "Stopped by user")
    {
        if (!Sessions.Any(s => string.Equals(s.SessionId, sessionId, StringComparison.OrdinalIgnoreCase))) return;
        var session = GetSession(sessionId);
        var runtime = _runtime.GetOrAdd(sessionId, _ => new Runtime());
        runtime.RouteCts?.Cancel();
        runtime.RouteCts?.Dispose();
        runtime.RouteCts = null;
        if (session.RouteState is RouteExecutionState.Running or RouteExecutionState.Paused)
            SetRoute(session, RouteExecutionState.Stopped, reason);
    }

    public void ClearTerminal(string sessionId)
    {
        var session = GetSession(sessionId);
        session.TerminalLines.Clear();
        session.TerminalEntries.Clear();
        session.MainEntries.Clear();
        session.CombatEntries.Clear();
        session.ChatEntries.Clear();
        session.TellEntries.Clear();
        session.PartyEntries.Clear();
        session.SystemEntries.Clear();
    }

    public void ResetAnsi(string sessionId)
    {
        var session = GetSession(sessionId);
        var runtime = _runtime.GetOrAdd(sessionId, _ => new Runtime());
        runtime.PartialDisplayText = string.Empty;
        runtime.PromptScanner.Reset();
        session.LivePromptText = string.Empty;
        session.ClassicScreen = runtime.ClassicTerminal.Reset();
    }

    public void ResetClientInternals(string sessionId)
    {
        var session = GetSession(sessionId);
        var runtime = _runtime.GetOrAdd(sessionId, _ => new Runtime());
        StopRoute(sessionId, "Client internals reset by user");
        runtime.ReceiveBuffer.Clear();
        runtime.PartialDisplayText = string.Empty;
        runtime.PromptScanner.Reset();
        runtime.CombatParser.Reset();
        runtime.RoomParser.Reset();
        runtime.StatParser.Reset();
        runtime.ItemScreenParser.Reset();
        runtime.SpellbookParser.Reset();
        runtime.PartyParser.Reset();
        runtime.WhoParser.Reset();
        runtime.BossHomeParser.Reset();
        runtime.LibrariumParser.Reset();
        runtime.LibrariumPageParser.Reset();
        runtime.ProtocolFilter.Reset();
        session.LivePromptText = string.Empty;
        session.CurrentRoomName = string.Empty;
        session.CurrentRoomKey = string.Empty;
        session.CurrentRoomDescription = string.Empty;
        session.CurrentRoomCurrency = string.Empty;
        session.CurrentRoomExits.Clear();
        session.CurrentRoomNpcs.Clear();
        session.CurrentRoomNpcDetails.Clear();
        session.CurrentRoomNpcDetailsUpdatedAt = null;
        session.CurrentRoomPlayers.Clear();
        session.CurrentRoomItems.Clear();
        session.CurrentRoomItemDetails.Clear();
        session.CurrentRoomLinks.Clear();
        session.CurrentRoomCommands.Clear();
        session.CurrentRoomCommandDetails.Clear();
        session.CurrentRoomServices.Clear();
        session.CurrentRoomTopologyNodes.Clear();
        session.CurrentRoomTopologyLinks.Clear();
        session.ServerMaps.Clear();
        session.RoomUpdatedAt = null;
        session.ExitsUpdatedAt = null;
        session.InCombat = false;
        session.LastCombatEvent = "None";
        session.LastCombatAmount = 0;
        session.LastCombatEventAt = null;
        session.InventoryItems.Clear();
        session.EquippedItems.Clear();
        session.LearnedSpells.Clear();
        session.LearnedSpellDetails.Clear();
        session.ActiveEffects.Clear();
        session.PartyMembers.Clear();
        session.OnlinePlayers.Clear();
        session.BossHomes.Clear();
        session.ServerCatalogFamilies.Clear();
        session.ServerCatalogWorld = session.ServerCatalogCompileHash = string.Empty;
        session.ServerCatalogUpdatedAt = null;
        session.LibrariumPage = null;
        session.InventoryUpdatedAt = null;
        session.EquipmentUpdatedAt = null;
        session.SpellbookUpdatedAt = null;
        session.ActiveEffectsUpdatedAt = null;
        session.PartyUpdatedAt = null;
        session.OnlinePlayersUpdatedAt = null;
        session.PvpState = null;
        session.PvpStateUpdatedAt = null;
    }

    public async Task<string> ExportTerminalLogAsync(string sessionId, string? destinationPath = null, CancellationToken cancellationToken = default)
    {
        var session = GetSession(sessionId);
        var safeCharacter = string.Concat((string.IsNullOrWhiteSpace(session.CharacterName) ? session.DisplayName : session.CharacterName).Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MajorMudReforged", "ClientLogs");
        Directory.CreateDirectory(folder);
        var path = destinationPath ?? Path.Combine(folder, $"{safeCharacter}-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        var lines = session.TerminalEntries.Select(e => $"[{e.Timestamp:O}] [{e.Channel}] {e.Text}");
        await File.WriteAllLinesAsync(path, lines, new UTF8Encoding(false), cancellationToken);
        return path;
    }

    public void PrepareRouteRecovery(string sessionId)
    {
        var session = GetSession(sessionId);
        if (!session.RouteRecoveryAvailable) return;
        session.RouteRecoveryStatus = $"Recovery prepared from {session.CurrentRoomName}. Select or calculate a replacement route before resuming.";
        session.RouteState = RouteExecutionState.Paused;
    }

    private async Task RunRouteAsync(ClientSessionConnection session, Runtime runtime, MegaMudPathRecord route, CancellationToken token)
    {
        try
        {
            foreach (var step in route.Steps)
            {
                token.ThrowIfCancellationRequested();
                while (session.RouteState == RouteExecutionState.Paused)
                    await runtime.RouteSignal.WaitAsync(token);
                while (session.InCombat)
                {
                    SetRoute(session, RouteExecutionState.Paused, "Paused because combat was detected");
                    await Task.Delay(500, token);
                }
                if (session.RouteState == RouteExecutionState.Paused)
                    SetRoute(session, RouteExecutionState.Running, "Combat ended; route resumed");
                session.RouteStep = step.Number;
                var playbackNode = session.RoutePlayback.FirstOrDefault(n => n.Step == step.Number);
                if (playbackNode is not null) playbackNode.State = RoutePlaybackNodeState.Active;
                SetMapState(session, step.Number, RoutePlaybackNodeState.Active);
                session.IsMoving = step.IsMovement;
                var fromRoom = session.CurrentRoomName;
                SetRoute(session, RouteExecutionState.Running, $"Sending {step.FriendlyCommand}");
                if (step.IsMovement)
                {
                    runtime.ExpectedPreviousRoom = fromRoom;
                    runtime.ExpectedRoomKey = step.ExpectedNativeRoomKey;
                    session.ExpectedRouteRoomKey = step.ExpectedNativeRoomKey;
                    runtime.RoomConfirmation = new TaskCompletionSource<RouteRoomConfirmation>(TaskCreationOptions.RunContinuationsAsynchronously);
                    session.AwaitingRouteConfirmation = true;
                }
                await SendCommandAsAsync(session.SessionId, step.Command, ClientCommandOrigin.RouteAutomation, token);
                if (step.IsMovement)
                {
                    SetRoute(session, RouteExecutionState.WaitingForRoom, $"Waiting for room confirmation after {step.FriendlyCommand}");
                    var confirmed = await WaitForRoomConfirmationAsync(session, runtime, step, fromRoom, token);
                    session.LastConfirmedRouteRoom = confirmed.RoomName;
                    var topologyStatus = VerifyRouteTopology(step.ExpectedNativeRoomKey, confirmed.RoomKey);
                    session.RouteTopologyStatus = $"{confirmed.RecognitionSource}: {topologyStatus}";
                    if (topologyStatus == "Mismatch")
                    {
                        session.RouteDesyncCount++;
                        session.RouteRecoveryAvailable = true;
                        session.RouteRecoveryStatus = $"Unexpected room detected at step {step.Number}. Recovery can begin from {confirmed.RoomName}.";
                        if (playbackNode is not null) { playbackNode.State = RoutePlaybackNodeState.Mismatch; playbackNode.ActualRoomName = confirmed.RoomName; playbackNode.ActualRoomKey = confirmed.RoomKey; }
                        SetMapState(session, step.Number, RoutePlaybackNodeState.Mismatch, confirmed);
                        session.RouteTopologyStatus = $"Expected {step.ExpectedNativeRoomKey}, received {confirmed.RoomKey}";
                        session.RouteTrace.Add(new RouteTraceEntry(step.Number, step.FriendlyCommand, fromRoom, confirmed.RoomName, "Topology mismatch", DateTimeOffset.Now, step.ExpectedNativeRoomKey, confirmed.RoomKey));
                        Trim(session.RouteTrace, 250);
                        throw new InvalidOperationException($"Route topology mismatch at step {step.Number}: expected native room '{step.ExpectedNativeRoomKey}' but Host output identified '{confirmed.RoomKey}'.");
                    }
                    if (topologyStatus == "Verified")
                    {
                        session.RouteVerifiedStepCount++;
                        session.RouteTopologyStatus = $"Verified {confirmed.RoomKey}";
                        if (playbackNode is not null) playbackNode.State = RoutePlaybackNodeState.Verified;
                        SetMapState(session, step.Number, RoutePlaybackNodeState.Verified, confirmed);
                    }
                    else
                    {
                        session.RouteUnverifiedStepCount++;
                        if (playbackNode is not null) playbackNode.State = RoutePlaybackNodeState.Confirmed;
                        SetMapState(session, step.Number, RoutePlaybackNodeState.Confirmed, confirmed);
                        session.RouteTopologyStatus = string.IsNullOrWhiteSpace(step.ExpectedNativeRoomKey)
                            ? "Room change confirmed; route has no expected native key"
                            : "Room change confirmed; Host output did not expose a native room key";
                    }
                    if (playbackNode is not null) { playbackNode.ActualRoomName = confirmed.RoomName; playbackNode.ActualRoomKey = confirmed.RoomKey; }
                    session.RouteTrace.Add(new RouteTraceEntry(step.Number, step.FriendlyCommand, fromRoom, confirmed.RoomName, topologyStatus, DateTimeOffset.Now, step.ExpectedNativeRoomKey, confirmed.RoomKey));
                    Trim(session.RouteTrace, 250);
                    session.AwaitingRouteConfirmation = false;
                    SetRoute(session, RouteExecutionState.Running, $"Confirmed {confirmed.RoomName}");
                    session.IsMoving = false;
                    await Task.Delay(1000, token);
                }
                else
                {
                    await Task.Delay(350, token);
                    if (playbackNode is not null) { playbackNode.State = RoutePlaybackNodeState.Confirmed; playbackNode.ActualRoomName = session.CurrentRoomName; playbackNode.ActualRoomKey = session.CurrentRoomKey; }
                    SetMapState(session, step.Number, RoutePlaybackNodeState.Confirmed, new RouteRoomConfirmation(session.CurrentRoomName, session.CurrentRoomKey, "Non-movement command", false));
                    session.RouteTrace.Add(new RouteTraceEntry(step.Number, step.FriendlyCommand, fromRoom, session.CurrentRoomName, "Command sent", DateTimeOffset.Now));
                    Trim(session.RouteTrace, 250);
                }
                if (session.ConnectionState != ClientConnectionState.Connected)
                    throw new IOException("The session disconnected during route execution.");
            }
            session.IsMoving = false;
            session.RouteRecoveryAvailable = false;
            session.RouteRecoveryStatus = "Route completed without recovery";
            SetRoute(session, RouteExecutionState.Completed, "Route completed");
        }
        catch (OperationCanceledException) { }
        catch (Exception exception)
        {
            session.IsMoving = false;
            session.RouteRecoveryAvailable = true;
            session.RouteRecoveryStatus = $"Route stopped safely. Recovery can begin from {session.CurrentRoomName}.";
            var activeNode = session.RoutePlayback.FirstOrDefault(n => n.State == RoutePlaybackNodeState.Active);
            if (activeNode is not null) { activeNode.State = RoutePlaybackNodeState.Failed; SetMapState(session, activeNode.Step, RoutePlaybackNodeState.Failed, new RouteRoomConfirmation(session.CurrentRoomName, session.CurrentRoomKey, "Route failure", false)); }
            SetRoute(session, RouteExecutionState.Faulted, exception.Message);
        }
    }

    private async Task ReadLoopAsync(ClientSessionConnection session, Runtime runtime, StreamReader reader, long generation, CancellationToken token)
    {
        var chars = new char[4096];
        try
        {
            while (!token.IsCancellationRequested && generation == Volatile.Read(ref runtime.ConnectionGeneration))
            {
                var read = await reader.ReadAsync(chars.AsMemory(), token);
                if (read == 0) break;

                var protocolResult = runtime.ProtocolFilter.Process(new string(chars, 0, read));
                var receivedText = protocolResult.VisibleText;
                if (protocolResult.Messages.Count > 0)
                {
                    await OnUiAsync(() =>
                    {
                        foreach (var message in protocolResult.Messages)
                            ParseClientProtocol(session, message);
                    });
                }
                if (receivedText.Length == 0) continue;
                var classicScreen = runtime.ClassicTerminal.Process(receivedText);
                var promptObservations = runtime.PromptScanner.Append(receivedText);
                await OnUiAsync(() =>
                {
                    session.ClassicScreen = classicScreen;
                    foreach (var observation in promptObservations)
                        ApplyWirePrompt(session, observation);
                });

                runtime.ReceiveBuffer.Append(receivedText);
                var completed = new List<string>();
                var text = runtime.ReceiveBuffer.ToString();
                var start = 0;
                for (var index = 0; index < text.Length; index++)
                {
                    if (text[index] is not ('\r' or '\n')) continue;
                    completed.Add(text[start..index]);
                    if (text[index] == '\r' && index + 1 < text.Length && text[index + 1] == '\n') index++;
                    start = index + 1;
                }

                runtime.ReceiveBuffer.Clear();
                if (start < text.Length) runtime.ReceiveBuffer.Append(text[start..]);
                var partial = runtime.ReceiveBuffer.ToString();

                await OnUiAsync(() =>
                {
                    RemovePartialDisplay(session, runtime);
                    foreach (var rawLine in completed)
                    {
                        var line = StripAnsiForPromptDetection(rawLine);
                        if (string.IsNullOrEmpty(line) &&
                            !string.IsNullOrEmpty(rawLine))
                            continue;

                        session.LastServerActivityAt = DateTimeOffset.Now;
                        ParseStatus(session, line);
                        if ((_parsing.IsMatch(ClientParsingRuleKind.MovementRejected, line, session.ServerProfileName, session.WorldName) || MovementRejected().IsMatch(line)) && runtime.RoomConfirmation is not null)
                            runtime.RoomConfirmation.TrySetException(new InvalidOperationException($"Host rejected movement: {line}"));

                        session.LastReceivedLine = line;

                        if (IsClassicStatline(line))
                        {
                            runtime.PartialDisplayText = line;
                            session.LivePromptText = line;
                            session.PromptUpdatedAt = DateTimeOffset.Now;
                        }
                        else
                        {
                            AddTerminalEntry(session, new ClientTerminalEntry(DateTimeOffset.Now, ClassifyTerminalChannel(line), line));
                            LineReceived?.Invoke(this, new ClientTerminalLineEvent(session.SessionId, line, DateTimeOffset.Now));
                        }
                    }

                    UpdatePartialDisplay(
                        session,
                        runtime,
                        StripAnsiForPromptDetection(partial));
                    runtime.ReceiveRevision++;
                });
            }
        }
        catch (Exception exception) when (exception is IOException or SocketException or OperationCanceledException) { }
        finally
        {
            // A retired loop must never clear prompts or change state for a newer socket.
            if (generation == Volatile.Read(ref runtime.ConnectionGeneration))
            {
                await OnUiAsync(() => RemovePartialDisplay(session, runtime));
                if (!runtime.SuppressReconnect && session.AutoReconnect && !token.IsCancellationRequested)
                {
                    await SetStateAsync(session, ClientConnectionState.Reconnecting, "Connection lost; reconnecting…");
                    _ = ReconnectAsync(session, runtime);
                }
                else if (session.ConnectionState != ClientConnectionState.Disconnected)
                {
                    await SetStateAsync(session, ClientConnectionState.Disconnected, "Disconnected");
                }
            }
        }
    }

    private void UpdatePartialDisplay(ClientSessionConnection session, Runtime runtime, string partial)
    {
        var trimmed = StripAnsiForPromptDetection(partial).Trim();

        if (trimmed == ">")
            return;

        if (string.IsNullOrEmpty(partial))
            return;

        runtime.PartialDisplayText = partial;
        session.LastReceivedLine = partial;
        session.LivePromptText = StripAnsiForPromptDetection(partial);
        ParsePartialStatus(session, partial);
    }

    private void ParsePartialStatus(ClientSessionConnection session, string partial)
    {
        // Prompts and status ticks often arrive without CR/LF. Preserve and parse
        // them immediately so Borin sees the same live state as the terminal.
        var clean = StripAnsiForPromptDetection(partial).Trim();
        session.LastReceivedLine = clean;
        session.LastServerActivityAt = DateTimeOffset.Now;
        if (IsClassicStatline(clean))
        {
            session.LivePromptText = clean;
            session.PromptUpdatedAt = DateTimeOffset.Now;
            ParseStatus(session, clean);
        }
    }

    private static void RemovePartialDisplay(ClientSessionConnection session, Runtime runtime)
    {
        runtime.PartialDisplayText = string.Empty;
    }

    private async Task<RouteRoomConfirmation> WaitForRoomConfirmationAsync(ClientSessionConnection session, Runtime runtime, MegaMudPathStep step, string fromRoom, CancellationToken token)
    {
        if (runtime.RoomConfirmation is null) return new RouteRoomConfirmation(session.CurrentRoomName, session.CurrentRoomKey);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(4));
        try
        {
            return await runtime.RoomConfirmation.Task.WaitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            session.AwaitingRouteConfirmation = false;
            session.RouteDesyncCount++;
            session.RouteRecoveryAvailable = true;
            session.RouteRecoveryStatus = $"No room confirmation. Recovery can begin from {session.CurrentRoomName}.";
            var node = session.RoutePlayback.FirstOrDefault(n => n.Step == step.Number);
            if (node is not null) { node.State = RoutePlaybackNodeState.Failed; node.ActualRoomName = session.CurrentRoomName; node.ActualRoomKey = session.CurrentRoomKey; }
            session.RouteTrace.Add(new RouteTraceEntry(step.Number, step.FriendlyCommand, fromRoom, session.CurrentRoomName, "Unconfirmed", DateTimeOffset.Now, step.ExpectedNativeRoomKey, session.CurrentRoomKey));
            Trim(session.RouteTrace, 250);
            throw new InvalidOperationException($"Route desynchronized at step {step.Number}. No new room was recognized after '{step.FriendlyCommand}'.");
        }
        finally
        {
            runtime.RoomConfirmation = null;
            runtime.ExpectedPreviousRoom = string.Empty;
            runtime.ExpectedRoomKey = string.Empty;
            session.ExpectedRouteRoomKey = string.Empty;
        }
    }

    private async Task ReconnectAsync(ClientSessionConnection session, Runtime runtime)
    {
        if (runtime.SuppressReconnect) return;
        await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(session.ReconnectDelaySeconds, 1, 120)));
        if (!runtime.SuppressReconnect) await ConnectAsync(session.SessionId);
    }

    private void ParseStatus(ClientSessionConnection session, string line)
    {
        session.LastReceivedLine = line;
        var prepared = _parsing.PrepareLine(line, session.ServerProfileName, session.WorldName);
        if (_runtime.TryGetValue(session.SessionId, out var statRuntime) &&
            statRuntime.StatParser.Observe(prepared) is { } stat)
        {
            ApplyStatObservation(session, stat);
        }
        if (_runtime.TryGetValue(session.SessionId, out var spellRuntime) &&
            spellRuntime.SpellbookParser.Observe(prepared) is { } spells)
        {
            session.LearnedSpells.Clear();
            session.LearnedSpellDetails.Clear();
            foreach (var spell in spells)
            {
                session.LearnedSpells.Add(spell.Name);
                session.LearnedSpellDetails.Add(spell);
            }
            session.SpellbookUpdatedAt = DateTimeOffset.Now;
        }
        if (_runtime.TryGetValue(session.SessionId, out var partyRuntime) &&
            partyRuntime.PartyParser.Observe(prepared) is { } party)
        {
            session.PartyMembers.Clear();
            foreach (var member in party)
                session.PartyMembers.Add(new ClientPartyMemberState(member.Name, member.ClassName,
                    member.HealthPercent, member.ResourcePercent, member.ResourceKind,
                    member.Rank, member.State, member.Poisoned));
            session.PartyUpdatedAt = DateTimeOffset.Now;
        }
        if (_runtime.TryGetValue(session.SessionId, out var whoRuntime) &&
            whoRuntime.WhoParser.Observe(prepared) is { } onlinePlayers)
        {
            session.OnlinePlayers.Clear();
            foreach (var player in onlinePlayers) session.OnlinePlayers.Add(player);
            session.OnlinePlayersUpdatedAt = DateTimeOffset.Now;
        }
        var vitals = _parsing.MatchFirst(ClientParsingRuleKind.CombinedVitals, prepared, session.ServerProfileName, session.WorldName) ?? VitalPrompt().Match(prepared);
        if (vitals.Success)
        {
            SetVital(session, vitals.Groups[1].Value, vitals.Groups[2].Value, true);
            SetVital(session, vitals.Groups[3].Value, vitals.Groups[4].Value, false);
        }
        else
        {
            var hpPair = _parsing.MatchFirst(ClientParsingRuleKind.Health, prepared, session.ServerProfileName, session.WorldName) ?? HealthPair().Match(prepared);
            if (hpPair.Success)
            {
                SetVital(session, hpPair.Groups[1].Value, hpPair.Groups[2].Value, true);
            }
            else if (!TryApplyScalarHealthPrompt(session, prepared))
            {
                var hp = _parsing.MatchFirst(ClientParsingRuleKind.Health, prepared, session.ServerProfileName, session.WorldName) ?? Health().Match(prepared);
                if (hp.Success && int.TryParse(hp.Groups[1].Value, out var hpValue))
                    session.HealthPercent = Math.Clamp(hpValue, 0, 100);
            }
            var manaPair = _parsing.MatchFirst(ClientParsingRuleKind.Mana, prepared, session.ServerProfileName, session.WorldName) ?? ManaPair().Match(prepared);
            if (manaPair.Success) SetVital(session, manaPair.Groups[1].Value, manaPair.Groups[2].Value, false);
            else
            {
                var mana = _parsing.MatchFirst(ClientParsingRuleKind.Mana, prepared, session.ServerProfileName, session.WorldName) ?? Mana().Match(prepared);
                if (mana.Success && int.TryParse(mana.Groups[1].Value, out var manaValue)) session.ManaPercent = Math.Clamp(manaValue, 0, 100);
            }
        }
        if (_runtime.TryGetValue(session.SessionId, out var inventoryRuntime) &&
            inventoryRuntime.ItemScreenParser.Observe(prepared) is { } itemScreen)
            ApplyItemScreenObservation(session, itemScreen);
        if (_runtime.TryGetValue(session.SessionId, out var combatRuntime))
        {
            var combat = combatRuntime.CombatParser.Observe(prepared);
            if (combat.Kind is ClassicCombatEventKind.Engaged or ClassicCombatEventKind.Disengaged)
                session.InCombat = combat.InCombat;
            if (combat.Kind != ClassicCombatEventKind.None)
            {
                session.LastCombatEvent = combat.Kind.ToString();
                session.LastCombatAmount = combat.Amount;
                session.LastCombatEventAt = DateTimeOffset.Now;
                session.SessionCombatEventCount++;
            }
            if (combat.Kind == ClassicCombatEventKind.ExperienceReward &&
                combat.Amount > 0 && session.ExperienceUpdatedAt is not null)
            {
                session.ExperienceCurrent = session.ExperienceCurrent > long.MaxValue - combat.Amount
                    ? long.MaxValue
                    : session.ExperienceCurrent + combat.Amount;
                session.SessionExperienceGained = session.SessionExperienceGained > long.MaxValue - combat.Amount
                    ? long.MaxValue
                    : session.SessionExperienceGained + combat.Amount;
                session.SessionExperienceSamples.Add(new ClientExperienceSample(DateTimeOffset.Now, session.SessionExperienceGained));
                if (session.SessionExperienceSamples.Count > 500)
                    session.SessionExperienceSamples.RemoveAt(1);
                session.ExperienceUpdatedAt = DateTimeOffset.Now;
            }
        }
        session.IsResting = _parsing.IsMatch(ClientParsingRuleKind.Resting, prepared, session.ServerProfileName, session.WorldName) || Resting().IsMatch(prepared);

        var roomIdentity = _runtime.TryGetValue(session.SessionId, out var roomRuntime)
            ? roomRuntime.RoomParser.Observe(prepared)
            : null;
        if (roomIdentity is { } roomDisplay)
        {
            var previousName = session.CurrentRoomName;
            var previousKey = session.CurrentRoomKey;
            var roomChanged = !string.Equals(roomDisplay.RoomName, previousName, StringComparison.OrdinalIgnoreCase) ||
                              (!string.IsNullOrWhiteSpace(roomDisplay.RoomKey) && !string.Equals(roomDisplay.RoomKey, previousKey, StringComparison.OrdinalIgnoreCase));
            session.CurrentRoomName = roomDisplay.RoomName;
            if (!string.IsNullOrWhiteSpace(roomDisplay.RoomKey)) session.CurrentRoomKey = roomDisplay.RoomKey;
            session.RoomUpdatedAt = DateTimeOffset.Now;
            if (roomChanged)
                session.SessionRoomsObserved++;
            session.CurrentRoomExits.Clear();
            foreach (var exit in roomDisplay.Exits)
                session.CurrentRoomExits.Add(exit);
            session.CurrentRoomDescription = roomDisplay.Description;
            session.CurrentRoomCurrency = roomDisplay.Currency;
            ReplaceRoomValues(session.CurrentRoomNpcs, roomDisplay.Npcs);
            ReplaceRoomValues(session.CurrentRoomPlayers, roomDisplay.Players);
            ReplaceRoomValues(session.CurrentRoomItems, roomDisplay.Items);
            session.ExitsUpdatedAt = DateTimeOffset.Now;
            if (_runtime.TryGetValue(session.SessionId, out var runtime) && runtime.RoomConfirmation is not null && roomChanged &&
                (!string.Equals(roomDisplay.RoomName, runtime.ExpectedPreviousRoom, StringComparison.OrdinalIgnoreCase) ||
                 !string.IsNullOrWhiteSpace(roomDisplay.RoomKey)))
                runtime.RoomConfirmation.TrySetResult(new RouteRoomConfirmation(
                    roomDisplay.RoomName,
                    roomDisplay.RoomKey,
                    "Completed MajorMUD room display",
                    !string.IsNullOrWhiteSpace(roomDisplay.RoomKey)));
        }
    }

    private void ParseClientProtocol(ClientSessionConnection session, ReforgedClientProtocolMessage message)
    {
        if (!_runtime.TryGetValue(session.SessionId, out var runtime)) return;
        if (message.Channel == "ROOMDATA")
        {
            if (runtime.RoomParser.Observe(message.Payload) is { } room)
                ApplyClientRoomDataObservation(session, room);
            return;
        }
        if (message.Channel == "PANELDATA")
        {
            // Private panel responses preserve the Classic screen layout inside one
            // protocol envelope. Feed that layout to the bounded parsers one line at
            // a time exactly as an ordinary terminal response would arrive. Keeping
            // this path inside the protocol handler prevents the payload from ever
            // appearing in player scrollback.
            foreach (var line in SplitClientPanelLines(message.Payload))
                ParseStatus(session, line);
            return;
        }
        if (message.Channel == "ACTIVEEFFECTS")
        {
            try
            {
                var effects = JsonSerializer.Deserialize<ClientActiveEffectState[]>(message.Payload) ?? [];
                session.ActiveEffectDetails.Clear();
                foreach (var effect in effects) session.ActiveEffectDetails.Add(effect);
                session.ActiveEffectsUpdatedAt = DateTimeOffset.Now;
            }
            catch (JsonException) { }
            return;
        }
        if (message.Channel == "PVPSTATE")
        {
            try
            {
                session.PvpState = JsonSerializer.Deserialize<ClientPvpState>(message.Payload);
                session.PvpStateUpdatedAt = DateTimeOffset.Now;
            }
            catch (JsonException) { }
            return;
        }
        if (message.Channel == "ROOMLINKS")
        {
            try
            {
                var links = JsonSerializer.Deserialize<ClientRoomLinkState[]>(message.Payload) ?? [];
                session.CurrentRoomLinks.Clear();
                foreach (var link in links) session.CurrentRoomLinks.Add(link);
            }
            catch (JsonException) { }
            return;
        }
        if (message.Channel == "ROOMSTATE")
        {
            try
            {
                var state = JsonSerializer.Deserialize<ClientRoomEnvironmentState>(message.Payload);
                if (state is not null && (string.IsNullOrWhiteSpace(session.CurrentRoomKey) ||
                    state.RoomKey.Equals(session.CurrentRoomKey, StringComparison.OrdinalIgnoreCase)))
                {
                    session.CurrentRoomLightLevel = state.EnvironmentLightLevel;
                    session.CurrentRoomEnvironmentUpdatedAt = DateTimeOffset.Now;
                }
            }
            catch (JsonException) { }
            return;
        }
        if (message.Channel == "ROOMNPCS")
        {
            try
            {
                var npcs = JsonSerializer.Deserialize<ClientRoomInspectionNpc[]>(message.Payload) ?? [];
                session.CurrentRoomNpcDetails.Clear();
                foreach (var npc in npcs) session.CurrentRoomNpcDetails.Add(npc);
                session.CurrentRoomNpcDetailsUpdatedAt = DateTimeOffset.Now;
            }
            catch (JsonException) { }
            return;
        }
        if (message.Channel == "ROOMSERVICES")
        {
            try
            {
                var services = JsonSerializer.Deserialize<ClientRoomServiceState[]>(message.Payload) ?? [];
                session.CurrentRoomServices.Clear();
                foreach (var service in services) session.CurrentRoomServices.Add(service);
            }
            catch (JsonException) { }
            return;
        }
        if (message.Channel == "ROOMITEMS")
        {
            try
            {
                var items = JsonSerializer.Deserialize<ClientRoomItemState[]>(message.Payload) ?? [];
                session.CurrentRoomItemDetails.Clear();
                foreach (var item in items) session.CurrentRoomItemDetails.Add(item);
            }
            catch (JsonException) { }
            return;
        }
        if (message.Channel == "ROOMCOMMANDS")
        {
            try
            {
                var commands = JsonSerializer.Deserialize<ClientRoomCommandState[]>(message.Payload) ?? [];
                session.CurrentRoomCommands.Clear();
                session.CurrentRoomCommandDetails.Clear();
                foreach (var command in commands)
                {
                    session.CurrentRoomCommands.Add(command.Command);
                    session.CurrentRoomCommandDetails.Add(command);
                }
            }
            catch (JsonException) { }
            return;
        }
        if (message.Channel == "ROOMTOPOLOGY")
        {
            try
            {
                var topology = JsonSerializer.Deserialize<ClientRoomTopologySnapshot>(message.Payload);
                if (topology is null) return;
                session.CurrentRoomTopologyNodes.Clear();
                session.CurrentRoomTopologyLinks.Clear();
                foreach (var node in topology.Nodes ?? []) session.CurrentRoomTopologyNodes.Add(node);
                foreach (var link in topology.Links ?? []) session.CurrentRoomTopologyLinks.Add(link);
            }
            catch (JsonException) { }
            return;
        }
        if (message.Channel == "ROOMINSPECTION")
        {
            try
            {
                session.InspectedRoom = JsonSerializer.Deserialize<ClientRoomInspectionSnapshot>(message.Payload);
                session.RoomInspectionError = string.Empty;
            }
            catch (JsonException) { session.RoomInspectionError = "The server returned invalid room inspection data."; }
            return;
        }
        if (message.Channel == "MAPCATALOG")
        {
            try
            {
                var maps = JsonSerializer.Deserialize<ClientMapCatalogEntry[]>(message.Payload) ?? [];
                session.ServerMaps.Clear();
                foreach (var map in maps) session.ServerMaps.Add(map);
            }
            catch (JsonException) { }
            return;
        }
        if (message.Channel == "ROOMINSPECTERROR")
        {
            session.RoomInspectionError = message.Payload;
            return;
        }
        if (message.Channel == "BOSSHOME")
        {
            if (runtime.BossHomeParser.Observe(message.Payload) is not { } bosses) return;
            session.BossHomes.Clear();
            foreach (var boss in bosses) session.BossHomes.Add(boss);
            return;
        }
        if (message.Channel != "LIBRARIUM") return;
        if (runtime.LibrariumParser.Observe(message.Payload) is { } catalog)
        {
            session.ServerCatalogFamilies.Clear();
            foreach (var family in catalog.Families) session.ServerCatalogFamilies.Add(family);
            session.ServerCatalogWorld = catalog.World;
            session.ServerCatalogCompileHash = catalog.CompileHash;
            session.ServerCatalogUpdatedAt = catalog.CapturedAt;
        }
        if (runtime.LibrariumPageParser.Observe(message.Payload) is { } page)
            session.LibrariumPage = page;
    }

    private static IEnumerable<string> SplitClientPanelLines(string payload)
    {
        using var reader = new StringReader(payload ?? string.Empty);
        while (reader.ReadLine() is { } line)
            yield return line;
    }

    private static void ApplyClientRoomDataObservation(ClientSessionConnection session, ClassicRoomDisplayObservation room)
    {
        var roomChanged = (!string.IsNullOrWhiteSpace(room.RoomKey) &&
                           !room.RoomKey.Equals(session.CurrentRoomKey, StringComparison.OrdinalIgnoreCase)) ||
                          (string.IsNullOrWhiteSpace(room.RoomKey) &&
                           !room.RoomName.Equals(session.CurrentRoomName, StringComparison.OrdinalIgnoreCase));
        if (roomChanged)
        {
            session.CurrentRoomNpcDetails.Clear();
            session.CurrentRoomServices.Clear();
            session.CurrentRoomItemDetails.Clear();
            session.CurrentRoomNpcDetailsUpdatedAt = null;
        }
        session.CurrentRoomName = room.RoomName;
        if (!string.IsNullOrWhiteSpace(room.RoomKey)) session.CurrentRoomKey = room.RoomKey;
        session.CurrentRoomDescription = room.Description;
        session.CurrentRoomCurrency = room.Currency;
        ReplaceRoomValues(session.CurrentRoomExits, room.Exits);
        ReplaceRoomValues(session.CurrentRoomNpcs, room.Npcs);
        ReplaceRoomValues(session.CurrentRoomPlayers, room.Players);
        ReplaceRoomValues(session.CurrentRoomItems, room.Items);
        session.RoomUpdatedAt = DateTimeOffset.Now;
        session.ExitsUpdatedAt = session.RoomUpdatedAt;
    }

    private static void ReplaceRoomValues(ObservableCollection<string> target, IEnumerable<string> values)
    {
        target.Clear();
        foreach (var value in values) target.Add(value);
    }

    private static void ApplyItemScreenObservation(ClientSessionConnection session, ClassicItemScreenObservation observation)
    {
        var now = DateTimeOffset.Now;
        if (observation.Currency is { } currency)
        {
            session.CarriedCurrency = currency;
            session.CurrencyUpdatedAt = now;
        }
        if (observation.CarriedWeight is { } weight && observation.CarryingCapacity is { } capacity && observation.EncumbrancePercent is { } percent)
        {
            session.CarriedWeight = weight;
            session.CarryingCapacity = capacity;
            session.EncumbrancePercent = percent;
            session.EncumbranceBand = observation.EncumbranceBand;
            session.EncumbranceUpdatedAt = now;
        }
        if (observation.Kind == ClassicItemScreenKind.Wealth) return;
        var target = observation.Kind == ClassicItemScreenKind.Inventory ? session.InventoryItems : session.EquippedItems;
        target.Clear();
        foreach (var item in observation.Entries) target.Add(item.DisplayText);
        if (observation.Kind == ClassicItemScreenKind.Inventory) session.InventoryUpdatedAt = now;
        else session.EquipmentUpdatedAt = now;
    }

    internal static bool TryApplyScalarHealthPromptForTest(ClientSessionConnection session, string line) =>
        TryApplyScalarHealthPrompt(session, line);

    private static bool TryApplyScalarHealthPrompt(ClientSessionConnection session, string line)
    {
        var match = HealthScalarPrompt().Match(line ?? string.Empty);
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out var current))
            return false;

        session.HealthCurrent = Math.Max(0, current);
        if (session.HealthMaximum > 0)
        {
            session.HealthPercent = Math.Clamp(
                (int)Math.Round(session.HealthCurrent * 100d / session.HealthMaximum),
                0,
                100);
        }
        else
        {
            // A scalar MajorMUD prompt such as [HP=51]: proves current HP, but
            // not maximum HP or a safe percentage. Keep percentage unknown so
            // autonomous play cannot mistake an unverified maximum for full health.
            session.HealthPercent = 0;
        }

        return true;
    }

    private static void ApplyWirePrompt(
        ClientSessionConnection session,
        ClassicPromptObservation observation)
    {
        session.HealthCurrent = Math.Max(0, observation.Health);
        if (session.HealthMaximum > 0)
        {
            session.HealthPercent = Math.Clamp(
                (int)Math.Round(session.HealthCurrent * 100d / session.HealthMaximum),
                0,
                100);
        }

        if (!string.IsNullOrEmpty(observation.ResourceKind))
        {
            session.ManaCurrent = Math.Max(0, observation.Resource);
            if (session.ManaMaximum > 0)
            {
                session.ManaPercent = Math.Clamp(
                    (int)Math.Round(session.ManaCurrent * 100d / session.ManaMaximum),
                    0,
                    100);
            }
        }
        else
        {
            session.ManaCurrent = 0;
            session.ManaPercent = 0;
        }

        session.IsResting = observation.IsResting || observation.IsMeditating;
        session.LivePromptText = observation.ResourceKind.Length == 0
            ? $"[HP={observation.Health}]:"
            : $"[HP={observation.Health}/{observation.ResourceKind}={observation.Resource}]:";
        session.PromptUpdatedAt = DateTimeOffset.Now;
        session.LastServerActivityAt = DateTimeOffset.Now;
    }

    private static void ApplyStatObservation(
        ClientSessionConnection session,
        ClassicStatObservation observation)
    {
        if (!string.IsNullOrWhiteSpace(observation.CharacterName))
            session.CharacterName = observation.CharacterName;
        if (observation.HealthCurrent is { } health) session.HealthCurrent = health;
        if (observation.HealthMaximum is { } maximumHealth) session.HealthMaximum = maximumHealth;
        if (observation.ManaCurrent is { } mana) session.ManaCurrent = mana;
        if (observation.ManaMaximum is { } maximumMana) session.ManaMaximum = maximumMana;
        if (session.HealthMaximum > 0)
            session.HealthPercent = Math.Clamp((int)Math.Round(session.HealthCurrent * 100d / session.HealthMaximum), 0, 100);
        if (session.ManaMaximum > 0)
            session.ManaPercent = Math.Clamp((int)Math.Round(session.ManaCurrent * 100d / session.ManaMaximum), 0, 100);
        else if (observation.ManaMaximum == 0)
            session.ManaPercent = 0;
        if (observation.Experience is { } experience) session.ExperienceCurrent = experience;
        if (observation.ExperienceNeeded is { } needed) session.ExperienceNeeded = needed;
        if (observation.ExperienceRemaining is { } remaining) session.ExperienceRemaining = remaining;
        if (observation.Level is { } level) session.CharacterLevel = level;
        if (observation.Lives is { } lives) session.Lives = lives;
        if (observation.CharacterPoints is { } characterPoints) session.CharacterPoints = characterPoints;
        if (observation.Experience is not null || observation.ExperienceNeeded is not null || observation.Level is not null)
            session.ExperienceUpdatedAt = DateTimeOffset.Now;
        if (observation.ScreenComplete)
        {
            session.ActiveEffects.Clear();
            foreach (var effect in observation.ActiveEffects ?? Array.Empty<string>()) session.ActiveEffects.Add(effect);
            session.ActiveEffectsUpdatedAt = DateTimeOffset.Now;
        }
    }

    internal static IReadOnlyList<string> ParseExits(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Array.Empty<string>();

        return Regex.Split(value, @"(?:\s+and\s+)|[,;/\s]+", RegexOptions.IgnoreCase)
            .Select(part => NormalizeExit(part))
            .Where(part => part.Length > 0 && !part.Equals("none", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string NormalizeExit(string value)
    {
        var clean = Regex.Replace(value.Trim().Trim('.', ':', ';'), @"[^A-Za-z]", string.Empty).ToLowerInvariant();
        return clean switch
        {
            "n" or "north" => "north",
            "ne" or "northeast" => "northeast",
            "e" or "east" => "east",
            "se" or "southeast" => "southeast",
            "s" or "south" => "south",
            "sw" or "southwest" => "southwest",
            "w" or "west" => "west",
            "nw" or "northwest" => "northwest",
            "u" or "up" => "up",
            "d" or "down" => "down",
            "in" or "inside" => "in",
            "out" or "outside" => "out",
            _ => clean,
        };
    }

    private static long ParseLong(string value) => long.TryParse(value.Replace(",", string.Empty), out var parsed) ? Math.Max(0, parsed) : 0;

    private static void BuildRouteMap(ClientSessionConnection session, MegaMudPathRecord route)
    {
        double x = 360, y = 110;
        session.RouteMapNodes.Add(new RouteMapNode
        {
            Step = 0, X = x, Y = y, Command = "Route start", IsStart = true,
            ActualRoomName = session.CurrentRoomName, ActualRoomKey = session.CurrentRoomKey,
            State = RoutePlaybackNodeState.Confirmed
        });
        foreach (var step in route.Steps.Where(item => item.IsMovement))
        {
            var (dx, dy) = DirectionVector(step.Command);
            var nextX = x + dx * 72;
            var nextY = y + dy * 72;
            session.RouteMapSegments.Add(new RouteMapSegment { Step = step.Number, X1 = x, Y1 = y, X2 = nextX, Y2 = nextY, Command = step.FriendlyCommand, State = RoutePlaybackNodeState.Pending });
            session.RouteMapNodes.Add(new RouteMapNode { Step = step.Number, X = nextX, Y = nextY, Command = step.FriendlyCommand, ExpectedRoomKey = step.ExpectedNativeRoomKey, State = RoutePlaybackNodeState.Pending });
            x = nextX; y = nextY;
        }
    }

    private static void SetMapState(ClientSessionConnection session, int step, RoutePlaybackNodeState state, RouteRoomConfirmation? confirmation = null)
    {
        var segment = session.RouteMapSegments.FirstOrDefault(item => item.Step == step);
        if (segment is not null) segment.State = state;
        var node = session.RouteMapNodes.FirstOrDefault(item => item.Step == step);
        if (node is null) return;
        node.State = state;
        if (confirmation is not null)
        {
            node.ActualRoomName = confirmation.RoomName;
            node.ActualRoomKey = confirmation.RoomKey;
        }
    }

    private static (int X, int Y) DirectionVector(string command) => command.Trim().ToLowerInvariant() switch
    {
        "n" => (0, -1), "ne" => (1, -1), "e" => (1, 0), "se" => (1, 1), "s" => (0, 1),
        "sw" => (-1, 1), "w" => (-1, 0), "nw" => (-1, -1), "u" => (0, -1), "d" => (0, 1), _ => (0, 0),
    };

    private RouteRoomConfirmation? TryParseRoomIdentity(ClientSessionConnection session, string line)
    {
        var clean = _parsing.PrepareLine(line, session.ServerProfileName, session.WorldName);
        if (string.IsNullOrWhiteSpace(clean)) return null;

        var keyed = KeyedRoomLine().Match(clean);
        if (keyed.Success)
            return new RouteRoomConfirmation(keyed.Groups[2].Value.Trim(), NormalizeRoomKey(keyed.Groups[1].Value), "Native room key", true);

        var mapRoom = MapRoomLine().Match(clean);
        if (mapRoom.Success)
            return new RouteRoomConfirmation(mapRoom.Groups[3].Value.Trim(), $"{mapRoom.Groups[1].Value}:{mapRoom.Groups[2].Value}", "Map and room number", true);

        var numbered = NumberedRoomLine().Match(clean);
        if (numbered.Success)
            return new RouteRoomConfirmation(numbered.Groups[3].Value.Trim(), $"{numbered.Groups[1].Value}:{numbered.Groups[2].Value}", "Numbered room heading", true);

        var suffix = RoomKeySuffixLine().Match(clean);
        if (suffix.Success)
            return new RouteRoomConfirmation(suffix.Groups[1].Value.Trim(), NormalizeRoomKey(suffix.Groups[2].Value), "Room-key suffix", true);

        var hash = HashRoomLine().Match(clean);
        if (hash.Success)
            return new RouteRoomConfirmation(hash.Groups[2].Value.Trim(), NormalizeRoomKey(hash.Groups[1].Value), "Room number", true);

        var custom = _parsing.MatchFirst(ClientParsingRuleKind.RoomTitle, clean, session.ServerProfileName, session.WorldName);
        if (custom is not null)
        {
            var roomName = custom.Groups.Count > 2 ? custom.Groups[^1].Value.Trim() : custom.Groups[1].Value.Trim();
            var roomKey = custom.Groups.Count > 2 ? NormalizeRoomKey(custom.Groups[1].Value) : string.Empty;
            if (!string.IsNullOrWhiteSpace(roomName) && LooksLikeRoomTitle(roomName))
                return new RouteRoomConfirmation(roomName, roomKey, "Server parsing profile", !string.IsNullOrWhiteSpace(roomKey));
        }

        if (!LooksLikeRoomTitle(clean)) return null;
        var room = RoomLine().Match(clean);
        return room.Success ? new RouteRoomConfirmation(room.Groups[1].Value.Trim(), string.Empty, "Room title", false) : null;
    }

    private static bool LooksLikeNonRoomOutput(string line)
    {
        if (line.Length < 3 || line.Length > 140) return true;
        if (line.EndsWith('.') || line.EndsWith('!') || line.EndsWith('?') || line.Contains(" says ", StringComparison.OrdinalIgnoreCase)) return true;
        return NonRoomPrefix().IsMatch(line) || line.StartsWith('>') || line.Contains("HP:", StringComparison.OrdinalIgnoreCase) || line.Contains("MP:", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsPlausibleRoomTitleForTest(string line) => LooksLikeRoomTitle(line);

    private static bool LooksLikeRoomTitle(string line)
    {
        var clean = Regex.Replace(line?.Trim() ?? string.Empty, @"\s+", " ");
        if (LooksLikeNonRoomOutput(clean))
            return false;

        var words = Regex.Matches(clean, @"[A-Za-z][A-Za-z'-]*");
        if (words.Count == 0 || words.Count > 9)
            return false;

        // Wrapped room prose often arrives as a capitalized fragment without its
        // final punctuation. Leading articles are valid in room titles, so reject
        // descriptive verbs and sentence markers rather than blanket-rejecting
        // titles beginning with A, An, or The.
        if (Regex.IsMatch(
                clean,
                @"\b(?:you|your|there|this|these|those|is|are|was|were|has|have|can|could|would|surrounds?|leads?|stands?|contains?|protects?|against|appears?|seems?|notice|see|hear|feel|winds?|extends?|stretches?|opens?|lies?)\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            return false;
        }

        return true;
    }

    private static string NormalizeRoomKey(string value) => value.Trim().Trim('[', ']', '(', ')').ToUpperInvariant();

    private static string VerifyRouteTopology(string expectedRoomKey, string actualRoomKey)
    {
        if (string.IsNullOrWhiteSpace(expectedRoomKey) || string.IsNullOrWhiteSpace(actualRoomKey)) return UnverifiedTopologyStatus;
        return string.Equals(NormalizeRoomKey(expectedRoomKey), NormalizeRoomKey(actualRoomKey), StringComparison.OrdinalIgnoreCase) ? "Verified" : "Mismatch";
    }

    private static void SetVital(ClientSessionConnection session, string currentText, string maximumText, bool health)
    {
        if (!int.TryParse(currentText, out var current) || !int.TryParse(maximumText, out var maximum) || maximum <= 0) return;
        var percent = (int)Math.Round(current * 100d / maximum);
        if (health)
        {
            session.HealthCurrent = current; session.HealthMaximum = maximum; session.HealthPercent = percent;
        }
        else
        {
            session.ManaCurrent = current; session.ManaMaximum = maximum; session.ManaPercent = percent;
        }
    }

    private ClientSessionConnection GetSession(string sessionId) => Sessions.FirstOrDefault(s => string.Equals(s.SessionId, sessionId, StringComparison.OrdinalIgnoreCase))
        ?? throw new KeyNotFoundException($"Client session '{sessionId}' was not found.");

    private async Task SetStateAsync(ClientSessionConnection session, ClientConnectionState state, string message)
    {
        await OnUiAsync(() =>
        {
            session.ConnectionState = state;
            session.ConnectionMessage = message;
            StateChanged?.Invoke(this, new ClientSessionStateEvent(session.SessionId, state, message));
        });
    }

    private void SetRoute(ClientSessionConnection session, RouteExecutionState state, string message)
    {
        _ = OnUiAsync(() =>
        {
            session.RouteState = state;
            session.RouteMessage = message;
            RouteChanged?.Invoke(this, new RouteExecutionEvent(session.SessionId, state, session.RouteStep, session.RouteStepCount, message));
        });
    }

    private static async Task CloseTransportAsync(Runtime runtime)
    {
        // Invalidate the current loop before a new reader can be installed.
        Interlocked.Increment(ref runtime.ConnectionGeneration);

        var readCts = runtime.ReadCts;
        var readTask = runtime.ReadTask;
        var telnetStream = runtime.TelnetStream;
        var reader = runtime.Reader;
        var writer = runtime.Writer;
        var client = runtime.Client;

        runtime.TelnetStream = null;
        runtime.Reader = null;
        runtime.Writer = null;
        runtime.Client = null;
        runtime.ReadCts = null;
        runtime.ReadTask = null;

        try { readCts?.Cancel(); } catch { }
        try { client?.Close(); } catch { }
        if (readTask is not null)
        {
            try { await readTask.WaitAsync(TimeSpan.FromSeconds(2)); } catch { }
        }

        try { reader?.Dispose(); } catch { }
        try { writer?.Dispose(); } catch { }
        try { telnetStream?.Dispose(); } catch { }
        try { client?.Dispose(); } catch { }
        try { readCts?.Dispose(); } catch { }
    }

    private static Encoding CreateTerminalEncoding(bool useCodePage437)
    {
        if (!useCodePage437)
            return new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false,
                throwOnInvalidBytes: false);

        return new CodePage437Encoding();
    }

    private static string CreateTelnetTracePath(
        ClientSessionConnection session)
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "MajorMudReforged",
            "TelnetLogs");

        Directory.CreateDirectory(folder);

        var sourceName = string.IsNullOrWhiteSpace(session.CharacterName)
            ? session.DisplayName
            : session.CharacterName;

        var safeName = string.Concat(
            sourceName.Select(character =>
                Path.GetInvalidFileNameChars().Contains(character)
                    ? '_'
                    : character));

        return Path.Combine(
            folder,
            $"{safeName}-{DateTime.Now:yyyyMMdd-HHmmss}.telnet.log");
    }

    private static void HandleTelnetDiagnostic(
        ClientSessionConnection session,
        Runtime runtime,
        string message,
        bool traffic)
    {
        if (!traffic)
            _ = OnUiAsync(() => session.TelnetStatus = message);

        if (!string.IsNullOrWhiteSpace(runtime.TelnetTracePath))
            _ = AppendTelnetTraceAsync(runtime, message);
    }

    private static async Task AppendTelnetTraceAsync(
        Runtime runtime,
        string message)
    {
        await runtime.TelnetTraceGate.WaitAsync();
        try
        {
            await File.AppendAllTextAsync(
                runtime.TelnetTracePath,
                $"[{DateTimeOffset.Now:O}] {message}"
                + Environment.NewLine);
        }
        catch
        {
        }
        finally
        {
            runtime.TelnetTraceGate.Release();
        }
    }

    private static async Task UpdateTelnetWindowSizeAsync(
        ClientSessionConnection session,
        Runtime runtime,
        int columns,
        int rows)
    {
        try
        {
            var telnetStream = runtime.TelnetStream;
            if (telnetStream is null)
                return;

            await telnetStream.SetWindowSizeAsync(columns, rows);
        }
        catch (Exception exception)
            when (exception is IOException
                or SocketException
                or ObjectDisposedException)
        {
            await OnUiAsync(() =>
                session.TelnetStatus =
                    $"NAWS update failed: {exception.Message}");
        }
    }

    private static Task OnUiAsync(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess()) { action(); return Task.CompletedTask; }
        return dispatcher.InvokeAsync(action).Task;
    }

    private static void Trim<T>(ObservableCollection<T> items, int maximum)
    {
        while (items.Count > maximum) items.RemoveAt(0);
    }

    private static readonly Regex AnsiSequence = new("\u001B\\[[0-?]*[ -/]*[@-~]", RegexOptions.Compiled);

    private static ClientTerminalChannel ClassifyTerminalChannel(string line)
    {
        var text = AnsiSequence.Replace(line, string.Empty).Trim();
        if (Regex.IsMatch(text, @"^(You tell|.+ tells you|From .+:|.+ telepaths(?: to you)?\s*:)", RegexOptions.IgnoreCase)) return ClientTerminalChannel.Tell;
        if (Regex.IsMatch(text, @"^(Party|Group|\[Party\]|\[Group\])", RegexOptions.IgnoreCase)) return ClientTerminalChannel.Party;
        if (Regex.IsMatch(text, @"^(You say|.+ says|.+ chats|Chat:)", RegexOptions.IgnoreCase)) return ClientTerminalChannel.Chat;
        if (Regex.IsMatch(text, @"(attacks|hits|misses|damage|combat|killed|slain|flee)", RegexOptions.IgnoreCase)) return ClientTerminalChannel.Combat;
        if (Regex.IsMatch(text, @"^(Connected|Disconnected|Reconnecting|System|Warning|Error)", RegexOptions.IgnoreCase)) return ClientTerminalChannel.System;
        return ClientTerminalChannel.Main;
    }

    private static void AddTerminalEntry(ClientSessionConnection session, ClientTerminalEntry entry)
    {
        session.TerminalEntries.Add(entry);
        var target = entry.Channel switch
        {
            ClientTerminalChannel.Combat => session.CombatEntries,
            ClientTerminalChannel.Chat => session.ChatEntries,
            ClientTerminalChannel.Tell => session.TellEntries,
            ClientTerminalChannel.Party => session.PartyEntries,
            ClientTerminalChannel.System => session.SystemEntries,
            _ => session.MainEntries
        };
        target.Add(entry);
        var display = entry.DisplayText(session.TerminalPreferences.ShowTimestamps);
        session.TerminalLines.Add(display);
        var limit = session.TerminalPreferences.MaximumScrollbackLines;
        Trim(session.TerminalEntries, limit);
        Trim(session.TerminalLines, limit);
        Trim(session.MainEntries, limit);
        Trim(session.CombatEntries, limit);
        Trim(session.ChatEntries, limit);
        Trim(session.TellEntries, limit);
        Trim(session.PartyEntries, limit);
        Trim(session.SystemEntries, limit);
        if (session.TerminalPreferences.LoggingEnabled)
            _ = AppendLiveLogAsync(session, entry);
    }

    private static async Task AppendLiveLogAsync(ClientSessionConnection session, ClientTerminalEntry entry)
    {
        try
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MajorMudReforged", "ClientLogs");
            Directory.CreateDirectory(folder);
            var safe = string.Concat((string.IsNullOrWhiteSpace(session.CharacterName) ? session.DisplayName : session.CharacterName).Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            var path = Path.Combine(folder, $"{safe}-{DateTime.Now:yyyyMMdd}.log");
            await File.AppendAllTextAsync(path, $"[{entry.Timestamp:O}] [{entry.Channel}] {entry.Text}{Environment.NewLine}");
        }
        catch { }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var session in Sessions.ToArray()) await DisconnectAsync(session.SessionId);
        foreach (var runtime in _runtime.Values)
        {
            runtime.SendGate.Dispose();
            runtime.TelnetTraceGate.Dispose();
        }
    }

    [GeneratedRegex(@"^\s*\[(?:HP|H)\s*[=:]\s*(\d+)\]\s*:?\s*$", RegexOptions.IgnoreCase)] private static partial Regex HealthScalarPrompt();
    [GeneratedRegex(@"(?:HP|Health)\s*[:=]?\s*(\d{1,3})%", RegexOptions.IgnoreCase)] private static partial Regex Health();
    [GeneratedRegex(@"(?:HP|Health)\s*[:=]?\s*(\d+)\s*/\s*(\d+)", RegexOptions.IgnoreCase)] private static partial Regex HealthPair();
    [GeneratedRegex(@"(?:HP|H)\s*[:=]?\s*(\d+)\s*/\s*(\d+).*?(?:MP|M|Mana)\s*[:=]?\s*(\d+)\s*/\s*(\d+)", RegexOptions.IgnoreCase)] private static partial Regex VitalPrompt();
    [GeneratedRegex(@"(?:MP|Mana)\s*[:=]?\s*(\d{1,3})%", RegexOptions.IgnoreCase)] private static partial Regex Mana();
    [GeneratedRegex(@"(?:MP|Mana)\s*[:=]?\s*(\d+)\s*/\s*(\d+)", RegexOptions.IgnoreCase)] private static partial Regex ManaPair();
    [GeneratedRegex(@"(?:Cash|Coins|Gold)\s*[:=]?\s*([0-9,]+)", RegexOptions.IgnoreCase)] private static partial Regex CurrencyTotal();
    [GeneratedRegex(@"(?:Experience|Exp|XP)\s*[:=]?\s*([0-9,]+)(?:\s*(?:/|of|needed(?:\s+for\s+next\s+level)?[:=]?)\s*([0-9,]+))?", RegexOptions.IgnoreCase)] private static partial Regex ExperienceLine();
    [GeneratedRegex(@"(?:Level|Lvl)\s*[:=]?\s*(\d+)", RegexOptions.IgnoreCase)] private static partial Regex LevelLine();
    [GeneratedRegex(@"^(?:You are carrying|Inventory|Items carried|You have the following items)\s*:?$", RegexOptions.IgnoreCase)] private static partial Regex InventoryHeader();
    [GeneratedRegex(@"^(?:Total weight|Encumbrance|You are carrying nothing|End of inventory)\b", RegexOptions.IgnoreCase)] private static partial Regex InventoryFooter();
    [GeneratedRegex(@"^(?:Equipment|Worn items|You are wearing|Equipped items)\s*:?$", RegexOptions.IgnoreCase)] private static partial Regex EquipmentHeader();
    [GeneratedRegex(@"^(?:End of equipment|Total armor|Armor class|You are wearing nothing)\b", RegexOptions.IgnoreCase)] private static partial Regex EquipmentFooter();
    [GeneratedRegex(@"^\s*(?:<[^>]+>|\[[^\]]+\]|(?:Head|Body|Hands|Feet|Waist|Back|Neck|Finger|Wrist|Weapon|Shield|Held|Light)\s*:)\s*", RegexOptions.IgnoreCase)] private static partial Regex EquipmentSlotPrefix();
    [GeneratedRegex(@"^(?:HP|H)\s*[:=]|^>\s*$|^Command\s*[:>]", RegexOptions.IgnoreCase)] private static partial Regex PromptLine();
    [GeneratedRegex(@"^(?:Obvious\s+exits?|Exits?)\s*:\s*(.+?)\s*$", RegexOptions.IgnoreCase)] private static partial Regex ExitLine();
    [GeneratedRegex(@"^\s*(?:[-*]\s*)?(?:\d+\s*[xX]\s*)?(.+?)\s*$")] private static partial Regex InventoryItemLine();
    [GeneratedRegex(@"^(?:Inventory|Items carried|You are carrying|Nothing|Total|Weight|Encumbrance|Gold|Coins|Cash)\b", RegexOptions.IgnoreCase)] private static partial Regex InventoryNoise();
    [GeneratedRegex(@"attacks you|you attack|combat begins|engage in combat", RegexOptions.IgnoreCase)] private static partial Regex CombatStart();
    [GeneratedRegex(@"combat ends|no longer fighting|you have defeated|is dead", RegexOptions.IgnoreCase)] private static partial Regex CombatEnd();
    [GeneratedRegex(@"you (?:sit|rest|meditate)|you are resting", RegexOptions.IgnoreCase)] private static partial Regex Resting();
    [GeneratedRegex(@"^\[([0-9A-F]{8}|\d+:\d+)\]\s*([A-Z][^.!?]{2,120})$", RegexOptions.IgnoreCase)] private static partial Regex KeyedRoomLine();
    [GeneratedRegex(@"^(?:Room\s+)?(?:Map\s*)?(\d+)\s*[-:/]\s*(\d+)\s*[:|-]\s*([A-Z][^.!?]{2,120})$", RegexOptions.IgnoreCase)] private static partial Regex NumberedRoomLine();
    [GeneratedRegex(@"^(?:\[[^\]]+\]\s*)?([A-Z][^.!?]{2,120})$")] private static partial Regex RoomLine();
    [GeneratedRegex(@"^(?:Map\s*)?(\d+)\s+(?:Room\s*)?(\d+)\s*[:|\-]\s*(.+)$", RegexOptions.IgnoreCase)] private static partial Regex MapRoomLine();
    [GeneratedRegex(@"^(.+?)\s*[\[(]([0-9A-F]{8}|\d+[:/]\d+)[\])]$", RegexOptions.IgnoreCase)] private static partial Regex RoomKeySuffixLine();
    [GeneratedRegex(@"^(?:Room\s*)?#?(\d+)\s*[:|\-]\s*(.+)$", RegexOptions.IgnoreCase)] private static partial Regex HashRoomLine();
    [GeneratedRegex(@"^(?:You|Your|He |She |It |They |Someone|Something|There |Exits?[: ]|Obvious exits|Also here|You see|You hear|You feel|You are|Current |Status |Combat |Party |Quest |System )", RegexOptions.IgnoreCase)] private static partial Regex NonRoomPrefix();
    [GeneratedRegex(@"\x1B(?:[@-Z\-_]|\[[0-?]*[ -/]*[@-~])")] private static partial Regex StripAnsi();
    [GeneratedRegex(@"can't go|cannot go|no exit|blocked|locked|unable to move|you may not", RegexOptions.IgnoreCase)] private static partial Regex MovementRejected();
}
