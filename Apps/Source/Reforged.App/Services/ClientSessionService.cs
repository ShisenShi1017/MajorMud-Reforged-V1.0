using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Sockets;
using System.Text;
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
    Task SendSensitiveCommandAsync(string sessionId, string command, CancellationToken cancellationToken = default);
    Task StartRouteAsync(string sessionId, MegaMudPathRecord route, CancellationToken cancellationToken = default);
    void PauseRoute(string sessionId);
    void ResumeRoute(string sessionId);
    void StopRoute(string sessionId, string reason = "Stopped by user");
    void PrepareRouteRecovery(string sessionId);
    void ClearTerminal(string sessionId);
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

    private static string StripAnsiForPromptDetection(string value) =>
        Regex.Replace(value ?? string.Empty, @"(?:\x1B)?\[[0-9;]*m", string.Empty);

    // Every route and automation action remains a normal Host command; the Host stays authoritative.
    private const string ConfirmedStatus = "Confirmed";
    private const string UnverifiedTopologyStatus = "Confirmed (unverified)";
    private sealed class Runtime
    {
        public TcpClient? Client;
        public StreamReader? Reader;
        public StreamWriter? Writer;
        public CancellationTokenSource? ReadCts;
        public CancellationTokenSource? RouteCts;
        public Task? ReadTask;
        public readonly StringBuilder ReceiveBuffer = new();
        public string PartialDisplayText = string.Empty;
        public long ReceiveRevision;
        public long ConnectionGeneration;
        public Task? RouteTask;
        public bool SuppressReconnect;
        public readonly SemaphoreSlim SendGate = new(1, 1);
        public readonly SemaphoreSlim RouteSignal = new(0, 1);
        public TaskCompletionSource<RouteRoomConfirmation>? RoomConfirmation;
        public string ExpectedPreviousRoom = string.Empty;
        public string ExpectedRoomKey = string.Empty;
        public bool CapturingInventory;
        public readonly List<string> PendingInventory = [];
        public bool CapturingEquipment;
        public readonly List<string> PendingEquipment = [];
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
            var telnetStream = new TelnetNegotiationStream(stream, session.TerminalPreferences.EnableTelnetNegotiation);
            var reader = new StreamReader(telnetStream, new UTF8Encoding(false), true, 4096, leaveOpen: true);
            var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, leaveOpen: true)
            {
                AutoFlush = true,
                NewLine = "\r\n"
            };
            var readCts = new CancellationTokenSource();
            var generation = Interlocked.Increment(ref runtime.ConnectionGeneration);

            runtime.Client = client;
            runtime.Reader = reader;
            runtime.Writer = writer;
            runtime.ReadCts = readCts;

            session.ConnectedAt = DateTimeOffset.Now;
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
        SendCommandCoreAsync(sessionId, command, echoToTerminal: true, cancellationToken);

    public Task SendSensitiveCommandAsync(string sessionId, string command, CancellationToken cancellationToken = default) =>
        SendCommandCoreAsync(sessionId, command, echoToTerminal: false, cancellationToken);

    private async Task SendCommandCoreAsync(string sessionId, string command, bool echoToTerminal, CancellationToken cancellationToken)
    {
        var session = GetSession(sessionId);
        var runtime = _runtime.GetOrAdd(sessionId, _ => new Runtime());
        if (session.ConnectionState != ClientConnectionState.Connected || runtime.Writer is null)
            throw new InvalidOperationException($"{session.DisplayName} is not connected.");

        await runtime.SendGate.WaitAsync(cancellationToken);
        try
        {
            var text = command?.Trim() ?? string.Empty;
            await runtime.Writer.WriteLineAsync(text.AsMemory(), cancellationToken);
            await runtime.Writer.FlushAsync(cancellationToken);

            await OnUiAsync(() =>
            {
                if (echoToTerminal && text.Length > 0)
                    session.CommandHistory.Add(text);

                // The Host owns command echo and the next statline. Do not add
                // an artificial "> command" line to Classic scrollback.
                CommandSent?.Invoke(
                    this,
                    new ClientCommandSentEvent(
                        sessionId,
                        echoToTerminal ? text : "[hidden]",
                        DateTimeOffset.Now));
            });
        }
        finally { runtime.SendGate.Release(); }
    }

    private static async Task ResetSessionTerminalAsync(ClientSessionConnection session, Runtime runtime)
    {
        runtime.ReceiveBuffer.Clear();
        runtime.PartialDisplayText = string.Empty;
        runtime.ReceiveRevision = 0;
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
            session.LastReceivedLine = string.Empty;
            session.LivePromptText = string.Empty;
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
                await SendCommandAsync(session.SessionId, step.Command, token);
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

                runtime.ReceiveBuffer.Append(chars, 0, read);
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
                    foreach (var line in completed)
                    {
                        ParseStatus(session, line);
                        if ((_parsing.IsMatch(ClientParsingRuleKind.MovementRejected, line, session.ServerProfileName, session.WorldName) || MovementRejected().IsMatch(line)) && runtime.RoomConfirmation is not null)
                            runtime.RoomConfirmation.TrySetException(new InvalidOperationException($"Host rejected movement: {line}"));

                        session.LastReceivedLine = line;

                        if (IsClassicStatline(line))
                        {
                            runtime.PartialDisplayText = line;
                            session.LivePromptText = StripAnsiForPromptDetection(line);
                        }
                        else
                        {
                            AddTerminalEntry(session, new ClientTerminalEntry(DateTimeOffset.Now, ClassifyTerminalChannel(line), line));
                            LineReceived?.Invoke(this, new ClientTerminalLineEvent(session.SessionId, line, DateTimeOffset.Now));
                        }
                    }

                    UpdatePartialDisplay(session, runtime, partial);
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

    private static void UpdatePartialDisplay(ClientSessionConnection session, Runtime runtime, string partial)
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

    private static void ParsePartialStatus(ClientSessionConnection session, string partial)
    {
        // Prompts and status ticks often arrive without CR/LF. Preserve the exact
        // text for display and expose it as the latest Host output immediately.
        session.LastReceivedLine = partial;
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
        var vitals = _parsing.MatchFirst(ClientParsingRuleKind.CombinedVitals, prepared, session.ServerProfileName, session.WorldName) ?? VitalPrompt().Match(prepared);
        if (vitals.Success)
        {
            SetVital(session, vitals.Groups[1].Value, vitals.Groups[2].Value, true);
            SetVital(session, vitals.Groups[3].Value, vitals.Groups[4].Value, false);
        }
        else
        {
            var hpPair = _parsing.MatchFirst(ClientParsingRuleKind.Health, prepared, session.ServerProfileName, session.WorldName) ?? HealthPair().Match(prepared);
            if (hpPair.Success) SetVital(session, hpPair.Groups[1].Value, hpPair.Groups[2].Value, true);
            else
            {
                var hp = _parsing.MatchFirst(ClientParsingRuleKind.Health, prepared, session.ServerProfileName, session.WorldName) ?? Health().Match(prepared);
                if (hp.Success && int.TryParse(hp.Groups[1].Value, out var hpValue)) session.HealthPercent = Math.Clamp(hpValue, 0, 100);
            }
            var manaPair = _parsing.MatchFirst(ClientParsingRuleKind.Mana, prepared, session.ServerProfileName, session.WorldName) ?? ManaPair().Match(prepared);
            if (manaPair.Success) SetVital(session, manaPair.Groups[1].Value, manaPair.Groups[2].Value, false);
            else
            {
                var mana = _parsing.MatchFirst(ClientParsingRuleKind.Mana, prepared, session.ServerProfileName, session.WorldName) ?? Mana().Match(prepared);
                if (mana.Success && int.TryParse(mana.Groups[1].Value, out var manaValue)) session.ManaPercent = Math.Clamp(manaValue, 0, 100);
            }
        }
        var cash = _parsing.MatchFirst(ClientParsingRuleKind.Currency, prepared, session.ServerProfileName, session.WorldName) ?? CurrencyTotal().Match(prepared);
        if (cash.Success && long.TryParse(cash.Groups[1].Value.Replace(",", string.Empty), out var cashValue)) session.CarriedCurrency = cashValue;

        var experience = _parsing.MatchFirst(ClientParsingRuleKind.Experience, prepared, session.ServerProfileName, session.WorldName) ?? ExperienceLine().Match(prepared);
        if (experience.Success)
        {
            session.ExperienceCurrent = ParseLong(experience.Groups[1].Value);
            if (experience.Groups[2].Success) session.ExperienceNeeded = ParseLong(experience.Groups[2].Value);
            session.ExperienceUpdatedAt = DateTimeOffset.Now;
        }
        var level = _parsing.MatchFirst(ClientParsingRuleKind.Level, prepared, session.ServerProfileName, session.WorldName) ?? LevelLine().Match(prepared);
        if (level.Success && int.TryParse(level.Groups[1].Value, out var levelValue))
        {
            session.CharacterLevel = levelValue;
            session.ExperienceUpdatedAt = DateTimeOffset.Now;
        }

        if (_runtime.TryGetValue(session.SessionId, out var inventoryRuntime))
            CaptureInventoryAndEquipment(session, inventoryRuntime, prepared);
        if (_parsing.IsMatch(ClientParsingRuleKind.CombatStart, prepared, session.ServerProfileName, session.WorldName) || CombatStart().IsMatch(prepared)) session.InCombat = true;
        if (_parsing.IsMatch(ClientParsingRuleKind.CombatEnd, prepared, session.ServerProfileName, session.WorldName) || CombatEnd().IsMatch(prepared)) session.InCombat = false;
        session.IsResting = _parsing.IsMatch(ClientParsingRuleKind.Resting, prepared, session.ServerProfileName, session.WorldName) || Resting().IsMatch(prepared);
        var roomIdentity = TryParseRoomIdentity(session, prepared);
        if (roomIdentity is not null)
        {
            var previousName = session.CurrentRoomName;
            var previousKey = session.CurrentRoomKey;
            session.CurrentRoomName = roomIdentity.RoomName;
            if (!string.IsNullOrWhiteSpace(roomIdentity.RoomKey)) session.CurrentRoomKey = roomIdentity.RoomKey;
            var roomChanged = !string.Equals(roomIdentity.RoomName, previousName, StringComparison.OrdinalIgnoreCase) ||
                              (!string.IsNullOrWhiteSpace(roomIdentity.RoomKey) && !string.Equals(roomIdentity.RoomKey, previousKey, StringComparison.OrdinalIgnoreCase));
            if (_runtime.TryGetValue(session.SessionId, out var runtime) && runtime.RoomConfirmation is not null && roomChanged &&
                (!string.Equals(roomIdentity.RoomName, runtime.ExpectedPreviousRoom, StringComparison.OrdinalIgnoreCase) ||
                 !string.IsNullOrWhiteSpace(roomIdentity.RoomKey)))
                runtime.RoomConfirmation.TrySetResult(roomIdentity);
        }
    }

    private void CaptureInventoryAndEquipment(ClientSessionConnection session, Runtime runtime, string line)
    {
        if (_parsing.IsMatch(ClientParsingRuleKind.EquipmentHeader, line, session.ServerProfileName, session.WorldName) || EquipmentHeader().IsMatch(line))
        {
            CommitInventory(session, runtime);
            runtime.CapturingEquipment = true;
            runtime.PendingEquipment.Clear();
            return;
        }
        if (_parsing.IsMatch(ClientParsingRuleKind.InventoryHeader, line, session.ServerProfileName, session.WorldName) || InventoryHeader().IsMatch(line))
        {
            CommitEquipment(session, runtime);
            runtime.CapturingInventory = true;
            runtime.PendingInventory.Clear();
            return;
        }

        if (runtime.CapturingInventory)
        {
            if (string.IsNullOrWhiteSpace(line) || _parsing.IsMatch(ClientParsingRuleKind.InventoryFooter, line, session.ServerProfileName, session.WorldName) || InventoryFooter().IsMatch(line) || PromptLine().IsMatch(line))
            {
                CommitInventory(session, runtime);
                return;
            }
            AddCapturedItem(runtime.PendingInventory, line, ClientParsingRuleKind.InventoryItem, session);
            return;
        }

        if (!runtime.CapturingEquipment) return;
        if (string.IsNullOrWhiteSpace(line) || _parsing.IsMatch(ClientParsingRuleKind.EquipmentFooter, line, session.ServerProfileName, session.WorldName) || EquipmentFooter().IsMatch(line) || PromptLine().IsMatch(line))
        {
            CommitEquipment(session, runtime);
            return;
        }
        AddCapturedItem(runtime.PendingEquipment, line, ClientParsingRuleKind.EquipmentItem, session);
    }

    private void AddCapturedItem(List<string> target, string line, ClientParsingRuleKind kind, ClientSessionConnection session)
    {
        var custom = _parsing.MatchFirst(kind, line, session.ServerProfileName, session.WorldName);
        string name;
        if (custom is not null)
        {
            name = custom.Groups.Count > 1 ? custom.Groups[^1].Value.Trim() : custom.Value.Trim();
        }
        else
        {
            var item = InventoryItemLine().Match(line);
            if (!item.Success) return;
            name = EquipmentSlotPrefix().Replace(item.Groups[1].Value.Trim(), string.Empty).Trim();
        }
        name = EquipmentSlotPrefix().Replace(name, string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(name) && !InventoryNoise().IsMatch(name)) target.Add(name);
    }

    private static void CommitInventory(ClientSessionConnection session, Runtime runtime)
    {
        if (!runtime.CapturingInventory) return;
        session.InventoryItems.Clear();
        foreach (var item in runtime.PendingInventory.Distinct(StringComparer.OrdinalIgnoreCase)) session.InventoryItems.Add(item);
        session.InventoryUpdatedAt = DateTimeOffset.Now;
        runtime.PendingInventory.Clear();
        runtime.CapturingInventory = false;
    }

    private static void CommitEquipment(ClientSessionConnection session, Runtime runtime)
    {
        if (!runtime.CapturingEquipment) return;
        session.EquippedItems.Clear();
        foreach (var item in runtime.PendingEquipment.Distinct(StringComparer.OrdinalIgnoreCase)) session.EquippedItems.Add(item);
        session.EquipmentUpdatedAt = DateTimeOffset.Now;
        runtime.PendingEquipment.Clear();
        runtime.CapturingEquipment = false;
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
            if (!string.IsNullOrWhiteSpace(roomName) && !LooksLikeNonRoomOutput(roomName))
                return new RouteRoomConfirmation(roomName, roomKey, "Server parsing profile", !string.IsNullOrWhiteSpace(roomKey));
        }

        if (LooksLikeNonRoomOutput(clean)) return null;
        var room = RoomLine().Match(clean);
        return room.Success ? new RouteRoomConfirmation(room.Groups[1].Value.Trim(), string.Empty, "Room title", false) : null;
    }

    private static bool LooksLikeNonRoomOutput(string line)
    {
        if (line.Length < 3 || line.Length > 140) return true;
        if (line.EndsWith('.') || line.EndsWith('!') || line.EndsWith('?') || line.Contains(" says ", StringComparison.OrdinalIgnoreCase)) return true;
        return NonRoomPrefix().IsMatch(line) || line.StartsWith('>') || line.Contains("HP:", StringComparison.OrdinalIgnoreCase) || line.Contains("MP:", StringComparison.OrdinalIgnoreCase);
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
        var reader = runtime.Reader;
        var writer = runtime.Writer;
        var client = runtime.Client;

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
        try { client?.Dispose(); } catch { }
        try { readCts?.Dispose(); } catch { }
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
        if (Regex.IsMatch(text, @"^(You tell|.+ tells you|From .+:)", RegexOptions.IgnoreCase)) return ClientTerminalChannel.Tell;
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
        foreach (var runtime in _runtime.Values) runtime.SendGate.Dispose();
    }

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
    [GeneratedRegex(@"^(?:You|Your|A |An |The |He |She |It |They |Someone|Something|There |Exits?[: ]|Obvious exits|Also here|You see|You hear|You feel|You are|Current |Status |Combat |Party |Quest |System )", RegexOptions.IgnoreCase)] private static partial Regex NonRoomPrefix();
    [GeneratedRegex(@"\x1B(?:[@-Z\-_]|\[[0-?]*[ -/]*[@-~])")] private static partial Regex StripAnsi();
    [GeneratedRegex(@"can't go|cannot go|no exit|blocked|locked|unable to move|you may not", RegexOptions.IgnoreCase)] private static partial Regex MovementRejected();
}
