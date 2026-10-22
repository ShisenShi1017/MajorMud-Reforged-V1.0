using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Windows.Input;
using Avalonia.Threading;
using RealmGate.Client.Automation;
using RealmGate.Client.Navigation;
using RealmGate.Client.Profiles;
using RealmGate.Client.Terminal;
using RealmGate.Client.Transport;
using RealmGate.Desktop.Services;
using RealmGate.Server;

namespace RealmGate.Desktop.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged, IAsyncDisposable
{
    private readonly EmbeddedServerHostController _host = new();
    private readonly ClassicTerminalSession _terminalSession = new();
    private readonly ClientConnectionProfileCatalogStore _profileStore =
        ClientConnectionProfileCatalogStore.CreateForCurrentUser();
    private readonly ClientInputAutomationProfileStore _automationStore =
        ClientInputAutomationProfileStore.CreateForCurrentUser();
    private readonly ClientRecordedRouteStore _recordedRouteStore =
        ClientRecordedRouteStore.CreateForCurrentUser();
    private readonly ClientInputAutomationRuntime _inputAutomation = new();
    private readonly ClientTimedEventScheduler _timedEventScheduler = new();
    private readonly ClientGameplayStateTracker _gameplayState = new();
    private readonly SemaphoreSlim _triggerGate = new(1, 1);
    private readonly CancellationTokenSource _automationClockCancellation = new();
    private readonly StringBuilder _triggerReceiveBuffer = new();
    private readonly object _routeSync = new();
    private readonly Action<string> _hostLogHandler;
    private ClientConnectionProfileCatalog _profileCatalog = new();
    private ClientInputAutomationProfile _automationProfile = new();
    private ClientRecordedRouteCatalog _recordedRouteCatalog = new();
    private ClientConnectionProfile? _selectedConnectionProfile;
    private ClientCommandAlias? _selectedAlias;
    private ClientCommandMacro? _selectedMacro;
    private ClientTextTrigger? _selectedTrigger;
    private ClientScheduledEvent? _selectedScheduledEvent;
    private ClientRouteGraph? _routeGraph;
    private ClientRouteRoom? _selectedNavigationRoom;
    private ClientRecordedRoute? _selectedRecordedRoute;
    private readonly List<ClientRecordedRouteStep> _recordingRouteSteps = [];
    private CancellationTokenSource? _routeCancellation;
    private TaskCompletionSource<string>? _routeRoomConfirmation;
    private string _currentRoomKey = string.Empty;
    private string _navigationSearchText = string.Empty;
    private string _navigationStatus = "Navigation graph not loaded.";
    private bool _isAutoRoaming;
    private bool _isRecordingRoute;
    private string _recordedRouteName = "Recorded Route";
    private string _recordingStartRoomKey = string.Empty;
    private string _macroCommandsText = string.Empty;
    private string _automationStatus = "Automation profile not loaded.";
    private string _bindAddress = RealmGateNetworkBinding.AllIpv4InterfacesAddress;
    private string _portText = RealmGateNetworkBinding.DefaultTelnetPort.ToString();
    private string _maximumSessionsText = "64";
    private string _statusMessage = "Stopped";
    private string _listenerText = "Not listening";
    private string _lanEndpointsText = "No LAN addresses discovered yet.";
    private string _logText = "Avalonia desktop host initialized.";
    private string _clientHostName = RealmGateNetworkBinding.LocalOnlyAddress;
    private string _clientPortText = RealmGateNetworkBinding.DefaultTelnetPort.ToString();
    private string _clientStatus = "Disconnected";
    private bool _isTerminalPasswordMode;
    private DateTimeOffset? _lastAutomationCommandAt;
    private long _clientConnectionGeneration;
    private long _navigationLoadGeneration;
    private readonly Task _automationClockTask;
    private ClassicTerminalSnapshot _terminalSnapshot;

    public MainWindowViewModel()
    {
        _terminalSnapshot = _terminalSession.Snapshot;
        _terminalSession.SnapshotChanged += snapshot =>
            Dispatcher.UIThread.Post(() => TerminalSnapshot = snapshot);
        _terminalSession.StatusChanged += HandleTerminalStatusChanged;
        _terminalSession.PasswordModeChanged += enabled =>
            Dispatcher.UIThread.Post(() => IsTerminalPasswordMode = enabled);
        _terminalSession.VisibleTextReceived += text =>
        {
            var wasReady = _gameplayState.GameplayReady;
            _gameplayState.Append(text);
            var generation = Volatile.Read(ref _clientConnectionGeneration);
            _ = ProcessTriggerTextAsync(text, generation);
            if (!wasReady && _gameplayState.GameplayReady && SupportsPrivateRoomState())
                _ = RequestRoomStateAsync(generation, CancellationToken.None);
        };
        _terminalSession.ProtocolMessageReceived += message =>
        {
            if (message.Channel.Equals("ROOMDATA", StringComparison.OrdinalIgnoreCase))
                _gameplayState.MarkGameplayReady();
            if (message.Channel.Equals("ROOMSTATE", StringComparison.OrdinalIgnoreCase))
                ObserveRoomState(message.Payload);
        };

        _hostLogHandler = message => Dispatcher.UIThread.Post(() => AppendLog(message));
        _host.Log += _hostLogHandler;

        UseLocalOnlyCommand = new RelayCommand(() =>
        {
            BindAddress = RealmGateNetworkBinding.LocalOnlyAddress;
            RefreshLanEndpoints();
        });
        UseRemoteHostingCommand = new RelayCommand(() =>
        {
            BindAddress = RealmGateNetworkBinding.AllIpv4InterfacesAddress;
            RefreshLanEndpoints();
        });
        RefreshLanCommand = new RelayCommand(RefreshLanEndpoints);
        StartServerCommand = new AsyncCommand(StartServerAsync);
        StopServerCommand = new AsyncCommand(StopServerAsync);
        ConnectClientCommand = new AsyncCommand(ConnectClientAsync);
        DisconnectClientCommand = new AsyncCommand(DisconnectClientAsync);
        SaveClientProfileCommand = new AsyncCommand(SaveClientProfileAsync);
        SaveAutomationCommand = new AsyncCommand(SaveAutomationAsync);
        AddAliasCommand = new RelayCommand(AddAlias);
        DeleteAliasCommand = new RelayCommand(DeleteAlias);
        AddMacroCommand = new RelayCommand(AddMacro);
        DeleteMacroCommand = new RelayCommand(DeleteMacro);
        AddTriggerCommand = new RelayCommand(AddTrigger);
        DeleteTriggerCommand = new RelayCommand(DeleteTrigger);
        AddScheduledEventCommand = new RelayCommand(AddScheduledEvent);
        DeleteScheduledEventCommand = new RelayCommand(DeleteScheduledEvent);
        StartNavigationCommand = new AsyncCommand(StartNavigationAsync);
        StartAutoRoamCommand = new AsyncCommand(StartAutoRoamAsync);
        StopNavigationCommand = new RelayCommand(() => StopNavigation("Route stopped."));
        StartRouteRecordingCommand = new RelayCommand(StartRouteRecording);
        FinishRouteRecordingCommand = new AsyncCommand(FinishRouteRecordingAsync);
        CancelRouteRecordingCommand = new RelayCommand(CancelRouteRecording);
        RunRecordedRouteCommand = new AsyncCommand(RunRecordedRouteAsync);
        DeleteRecordedRouteCommand = new AsyncCommand(DeleteRecordedRouteAsync);
        RefreshLanEndpoints();
        _ = LoadConnectionProfilesAsync();
        _ = LoadAutomationAsync();
        _ = LoadNavigationAsync("Classic");
        _ = LoadRecordedRoutesAsync();
        _automationClockTask = RunAutomationClockAsync(_automationClockCancellation.Token);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand UseLocalOnlyCommand { get; }
    public ICommand UseRemoteHostingCommand { get; }
    public ICommand RefreshLanCommand { get; }
    public ICommand StartServerCommand { get; }
    public ICommand StopServerCommand { get; }
    public ICommand ConnectClientCommand { get; }
    public ICommand DisconnectClientCommand { get; }
    public ICommand SaveClientProfileCommand { get; }
    public ICommand SaveAutomationCommand { get; }
    public ICommand AddAliasCommand { get; }
    public ICommand DeleteAliasCommand { get; }
    public ICommand AddMacroCommand { get; }
    public ICommand DeleteMacroCommand { get; }
    public ICommand AddTriggerCommand { get; }
    public ICommand DeleteTriggerCommand { get; }
    public ICommand AddScheduledEventCommand { get; }
    public ICommand DeleteScheduledEventCommand { get; }
    public ICommand StartNavigationCommand { get; }
    public ICommand StartAutoRoamCommand { get; }
    public ICommand StopNavigationCommand { get; }
    public ICommand StartRouteRecordingCommand { get; }
    public ICommand FinishRouteRecordingCommand { get; }
    public ICommand CancelRouteRecordingCommand { get; }
    public ICommand RunRecordedRouteCommand { get; }
    public ICommand DeleteRecordedRouteCommand { get; }

    public ClientInputAutomationProfile AutomationProfile => _automationProfile;
    public ObservableCollection<ClientCommandAlias> Aliases => _automationProfile.Aliases;
    public ObservableCollection<ClientCommandMacro> Macros => _automationProfile.Macros;
    public ObservableCollection<ClientTextTrigger> TextTriggers => _automationProfile.TextTriggers;
    public ObservableCollection<ClientScheduledEvent> ScheduledEvents =>
        _automationProfile.Events.ScheduledEvents;
    public ClientNavigationAutomationOptions NavigationOptions =>
        _automationProfile.Navigation;
    public ObservableCollection<ClientRouteRoom> VisibleNavigationRooms { get; } = [];
    public ObservableCollection<ClientRecordedRoute> RecordedRoutes { get; } = [];
    public IReadOnlyList<ClientTextTriggerMatchMode> TriggerMatchModes { get; } =
        Enum.GetValues<ClientTextTriggerMatchMode>();

    public ClientCommandAlias? SelectedAlias
    {
        get => _selectedAlias;
        set => Set(ref _selectedAlias, value);
    }

    public ClientCommandMacro? SelectedMacro
    {
        get => _selectedMacro;
        set
        {
            if (ReferenceEquals(_selectedMacro, value))
                return;
            ApplyMacroCommandsText(_selectedMacro);
            if (Set(ref _selectedMacro, value))
                MacroCommandsText = value is null
                    ? string.Empty
                    : string.Join(" ; ", value.Commands);
        }
    }

    public ClientTextTrigger? SelectedTrigger
    {
        get => _selectedTrigger;
        set => Set(ref _selectedTrigger, value);
    }

    public ClientScheduledEvent? SelectedScheduledEvent
    {
        get => _selectedScheduledEvent;
        set => Set(ref _selectedScheduledEvent, value);
    }

    public ClientRouteRoom? SelectedNavigationRoom
    {
        get => _selectedNavigationRoom;
        set => Set(ref _selectedNavigationRoom, value);
    }

    public ClientRecordedRoute? SelectedRecordedRoute
    {
        get => _selectedRecordedRoute;
        set => Set(ref _selectedRecordedRoute, value);
    }

    public string NavigationSearchText
    {
        get => _navigationSearchText;
        set
        {
            if (Set(ref _navigationSearchText, value)) RefreshNavigationRooms();
        }
    }

    public string CurrentRoomKey
    {
        get => _currentRoomKey;
        private set => Set(ref _currentRoomKey, value);
    }

    public string NavigationStatus
    {
        get => _navigationStatus;
        private set => Set(ref _navigationStatus, value);
    }

    public bool IsAutoRoaming
    {
        get => _isAutoRoaming;
        private set => Set(ref _isAutoRoaming, value);
    }

    public bool IsRecordingRoute
    {
        get => _isRecordingRoute;
        private set => Set(ref _isRecordingRoute, value);
    }

    public string RecordedRouteName
    {
        get => _recordedRouteName;
        set => Set(ref _recordedRouteName, value);
    }

    public string MacroCommandsText
    {
        get => _macroCommandsText;
        set => Set(ref _macroCommandsText, value);
    }

    public string AutomationStatus
    {
        get => _automationStatus;
        private set => Set(ref _automationStatus, value);
    }

    public ObservableCollection<ClientConnectionProfile> ConnectionProfiles =>
        _profileCatalog.Profiles;

    public ClientConnectionProfile? SelectedConnectionProfile
    {
        get => _selectedConnectionProfile;
        set
        {
            if (!Set(ref _selectedConnectionProfile, value) || value is null)
                return;

            ClientHostName = value.HostName;
            ClientPortText = value.Port.ToString();
            StopNavigation("Route stopped because the selected profile changed.");
            CurrentRoomKey = string.Empty;
            _ = LoadNavigationAsync(value.WorldName);
            RefreshRecordedRoutes();
        }
    }

    public ClassicTerminalSnapshot TerminalSnapshot
    {
        get => _terminalSnapshot;
        private set => Set(ref _terminalSnapshot, value);
    }

    public string ClientHostName
    {
        get => _clientHostName;
        set => Set(ref _clientHostName, value);
    }

    public string ClientPortText
    {
        get => _clientPortText;
        set => Set(ref _clientPortText, value);
    }

    public string ClientStatus
    {
        get => _clientStatus;
        private set => Set(ref _clientStatus, value);
    }

    public bool IsTerminalPasswordMode
    {
        get => _isTerminalPasswordMode;
        private set => Set(ref _isTerminalPasswordMode, value);
    }

    public string BindAddress
    {
        get => _bindAddress;
        set => Set(ref _bindAddress, value);
    }

    public string PortText
    {
        get => _portText;
        set
        {
            if (Set(ref _portText, value)) RefreshLanEndpoints();
        }
    }

    public string MaximumSessionsText
    {
        get => _maximumSessionsText;
        set => Set(ref _maximumSessionsText, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => Set(ref _statusMessage, value);
    }

    public string ListenerText
    {
        get => _listenerText;
        private set => Set(ref _listenerText, value);
    }

    public string LanEndpointsText
    {
        get => _lanEndpointsText;
        private set => Set(ref _lanEndpointsText, value);
    }

    public string LogText
    {
        get => _logText;
        private set => Set(ref _logText, value);
    }

    private async Task StartServerAsync()
    {
        try
        {
            if (!int.TryParse(PortText, out var port))
                throw new InvalidOperationException("Port must be a number from 1 through 65535.");
            if (!int.TryParse(MaximumSessionsText, out var maximumSessions))
                throw new InvalidOperationException("Maximum sessions must be a number.");

            StatusMessage = "Starting";
            ListenerText = $"Requested: {BindAddress}:{port}";
            await _host.StartAsync(new DesktopHostOptions(null, BindAddress, port, maximumSessions));
            StatusMessage = _host.StatusMessage;
            ListenerText = $"Listening on {BindAddress}:{port}";
            RefreshLanEndpoints();
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
            ListenerText = "Not listening";
            AppendLog(exception.Message);
        }
    }

    private async Task StopServerAsync()
    {
        try
        {
            StatusMessage = "Stopping";
            await _host.StopAsync();
            StatusMessage = _host.StatusMessage;
            ListenerText = "Not listening";
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
            AppendLog(exception.Message);
        }
    }

    private async Task ConnectClientAsync()
    {
        try
        {
            if (!int.TryParse(ClientPortText, out var port) || port is < 1 or > 65535)
                throw new InvalidOperationException("Client port must be a number from 1 through 65535.");

            var profile = SelectedConnectionProfile;
            var options = new ClientTransportOptions(
                profile?.EnableTelnetNegotiation ?? true,
                profile?.TerminalType ?? "ANSI",
                profile?.TerminalColumns ?? 80,
                profile?.TerminalRows ?? 25,
                profile?.CaptureTelnetTraffic ?? false,
                profile?.UseCodePage437 ?? true);
            await ResetConnectionAutomationAsync();
            await _terminalSession.ConnectAsync(
                ClientHostName,
                port,
                options);
        }
        catch (Exception exception)
        {
            ClientStatus = exception.Message;
        }
    }

    private async Task LoadConnectionProfilesAsync()
    {
        try
        {
            var catalog = await _profileStore.LoadAsync();
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _profileCatalog = catalog;
                Raise(nameof(ConnectionProfiles));
                SelectedConnectionProfile = ConnectionProfiles.FirstOrDefault();
            });
        }
        catch (Exception exception)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
                ClientStatus = $"Profile load failed: {exception.Message}");
        }
    }

    private async Task SaveClientProfileAsync()
    {
        try
        {
            if (!int.TryParse(ClientPortText, out var port) ||
                port is < 1 or > 65535)
            {
                throw new InvalidOperationException(
                    "Client port must be a number from 1 through 65535.");
            }

            var profile = SelectedConnectionProfile;
            if (profile is null)
            {
                profile = ClientConnectionProfileCatalogStore
                    .CreateLocalClassicProfile();
                profile.ProfileName = MakeUniqueProfileName("New BBS Profile");
                ConnectionProfiles.Add(profile);
                SelectedConnectionProfile = profile;
            }

            profile.HostName = ClientHostName;
            profile.Port = port;
            profile.UpdatedAt = DateTimeOffset.Now;
            await _profileStore.SaveAsync(_profileCatalog);
            ClientStatus = $"Saved profile '{profile.ProfileName}'";
        }
        catch (Exception exception)
        {
            ClientStatus = $"Profile save failed: {exception.Message}";
        }
    }

    private string MakeUniqueProfileName(string requestedName)
    {
        var candidate = requestedName;
        var suffix = 2;
        while (ConnectionProfiles.Any(profile =>
                   string.Equals(
                       profile.ProfileName,
                       candidate,
                       StringComparison.OrdinalIgnoreCase)))
        {
            candidate = $"{requestedName} {suffix++}";
        }

        return candidate;
    }

    private async Task DisconnectClientAsync()
    {
        try
        {
            await _terminalSession.DisconnectAsync();
            await ResetConnectionAutomationAsync();
        }
        catch (Exception exception)
        {
            ClientStatus = exception.Message;
        }
    }

    public async Task SendTerminalCommandAsync(string command, bool sensitive)
    {
        if (!sensitive && IsMovementCommand(command) && IsRecordingRoute)
        {
            await RecordManualMovementAsync(command);
            return;
        }
        if (!sensitive && IsMovementCommand(command))
            StopNavigation("Route stopped by manual movement.");
        await _triggerGate.WaitAsync();
        try
        {
            if (!sensitive &&
                !_automationProfile.EmergencyStop &&
                _inputAutomation.TryResolveHumanInput(command, out var commands))
            {
                await SendAutomationCommandsAsync(
                    commands,
                    Volatile.Read(ref _clientConnectionGeneration),
                    CancellationToken.None);
                ClientStatus = commands.Count == 1
                    ? $"Expanded automation command: {commands[0]}"
                    : $"Sent {commands.Count} macro commands";
                return;
            }

            await _terminalSession.SendLineAsync(command, sensitive);
            _lastAutomationCommandAt = DateTimeOffset.UtcNow;
        }
        catch (Exception exception)
        {
            ClientStatus = exception.Message;
        }
        finally
        {
            _triggerGate.Release();
        }
    }

    public async Task SendTerminalKeyAsync(string sequence)
    {
        try
        {
            await _terminalSession.SendRawAsync(sequence);
        }
        catch (Exception exception)
        {
            ClientStatus = exception.Message;
        }
    }

    public async Task ResizeTerminalAsync(int columns, int rows)
    {
        try
        {
            await _terminalSession.SetWindowSizeAsync(columns, rows);
        }
        catch (Exception exception)
        {
            ClientStatus = exception.Message;
        }
    }

    private async Task LoadAutomationAsync()
    {
        try
        {
            var profile = await _automationStore.LoadAsync();
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _automationProfile = profile;
                Raise(nameof(AutomationProfile));
                Raise(nameof(Aliases));
                Raise(nameof(Macros));
                Raise(nameof(TextTriggers));
                Raise(nameof(ScheduledEvents));
                Raise(nameof(NavigationOptions));
                SelectedAlias = Aliases.FirstOrDefault();
                SelectedMacro = Macros.FirstOrDefault();
                SelectedTrigger = TextTriggers.FirstOrDefault();
                SelectedScheduledEvent = ScheduledEvents.FirstOrDefault();
                AutomationStatus = "Automation profile loaded.";
            });
            _inputAutomation.UpdateProfile(CloneAutomationProfile(profile));
        }
        catch (Exception exception)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
                AutomationStatus = $"Automation load failed: {exception.Message}");
        }
    }

    private async Task SaveAutomationAsync()
    {
        try
        {
            ApplyMacroCommandsText(SelectedMacro);
            ValidateAutomationProfile(_automationProfile);
            await _automationStore.SaveAsync(_automationProfile);
            _inputAutomation.UpdateProfile(
                CloneAutomationProfile(_automationProfile));
            _timedEventScheduler.Reset();
            AutomationStatus = "Aliases, macros, triggers, events, and navigation settings saved.";

            // Refresh list labels after names are edited in the detail pane.
            var alias = SelectedAlias;
            var macro = SelectedMacro;
            var trigger = SelectedTrigger;
            var scheduledEvent = SelectedScheduledEvent;
            SelectedAlias = null;
            SelectedMacro = null;
            SelectedTrigger = null;
            SelectedScheduledEvent = null;
            SelectedAlias = alias;
            SelectedMacro = macro;
            SelectedTrigger = trigger;
            SelectedScheduledEvent = scheduledEvent;
        }
        catch (Exception exception)
        {
            AutomationStatus = $"Automation save failed: {exception.Message}";
        }
    }

    private void AddAlias()
    {
        var alias = new ClientCommandAlias
        {
            Name = MakeUniqueAutomationName("alias", Aliases.Select(item => item.Name)),
            Command = "look"
        };
        Aliases.Add(alias);
        SelectedAlias = alias;
    }

    private void DeleteAlias()
    {
        if (SelectedAlias is null)
            return;
        var index = Aliases.IndexOf(SelectedAlias);
        Aliases.Remove(SelectedAlias);
        SelectedAlias = Aliases.Count == 0
            ? null
            : Aliases[Math.Clamp(index, 0, Aliases.Count - 1)];
    }

    private void AddMacro()
    {
        var macro = new ClientCommandMacro
        {
            Name = MakeUniqueAutomationName("macro", Macros.Select(item => item.Name))
        };
        macro.Commands.Add("look");
        Macros.Add(macro);
        SelectedMacro = macro;
    }

    private void DeleteMacro()
    {
        if (SelectedMacro is null)
            return;
        var index = Macros.IndexOf(SelectedMacro);
        Macros.Remove(SelectedMacro);
        _selectedMacro = null;
        Raise(nameof(SelectedMacro));
        SelectedMacro = Macros.Count == 0
            ? null
            : Macros[Math.Clamp(index, 0, Macros.Count - 1)];
    }

    private void AddTrigger()
    {
        var trigger = new ClientTextTrigger
        {
            Name = MakeUniqueAutomationName(
                "trigger",
                TextTriggers.Select(item => item.Name)),
            MatchText = "appears",
            Command = "look",
            OnlyOutOfCombat = true
        };
        TextTriggers.Add(trigger);
        SelectedTrigger = trigger;
    }

    private void DeleteTrigger()
    {
        if (SelectedTrigger is null)
            return;
        var index = TextTriggers.IndexOf(SelectedTrigger);
        TextTriggers.Remove(SelectedTrigger);
        SelectedTrigger = TextTriggers.Count == 0
            ? null
            : TextTriggers[Math.Clamp(index, 0, TextTriggers.Count - 1)];
    }

    private void AddScheduledEvent()
    {
        var scheduledEvent = new ClientScheduledEvent
        {
            Name = MakeUniqueAutomationName(
                "event",
                ScheduledEvents.Select(item => item.Name)),
            Command = "score"
        };
        ScheduledEvents.Add(scheduledEvent);
        SelectedScheduledEvent = scheduledEvent;
    }

    private void DeleteScheduledEvent()
    {
        if (SelectedScheduledEvent is null)
            return;
        var index = ScheduledEvents.IndexOf(SelectedScheduledEvent);
        ScheduledEvents.Remove(SelectedScheduledEvent);
        SelectedScheduledEvent = ScheduledEvents.Count == 0
            ? null
            : ScheduledEvents[Math.Clamp(index, 0, ScheduledEvents.Count - 1)];
    }

    private async Task ProcessTriggerTextAsync(string text, long generation)
    {
        await _triggerGate.WaitAsync();
        try
        {
            if (generation != Volatile.Read(ref _clientConnectionGeneration) ||
                !_terminalSession.IsConnected)
                return;
            _triggerReceiveBuffer.Append(text);
            var buffered = _triggerReceiveBuffer.ToString();
            var start = 0;
            for (var index = 0; index < buffered.Length; index++)
            {
                if (buffered[index] is not ('\r' or '\n'))
                    continue;

                var line = buffered[start..index];
                if (buffered[index] == '\r' &&
                    index + 1 < buffered.Length &&
                    buffered[index + 1] == '\n')
                {
                    index++;
                }

                start = index + 1;
                await ExecuteMatchingTriggerAsync(line, generation);
            }

            _triggerReceiveBuffer.Clear();
            if (start < buffered.Length)
                _triggerReceiveBuffer.Append(buffered[start..]);
            if (_triggerReceiveBuffer.Length > 8192)
                _triggerReceiveBuffer.Remove(0, _triggerReceiveBuffer.Length - 8192);
        }
        catch (Exception exception)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
                AutomationStatus = $"Trigger execution failed: {exception.Message}");
        }
        finally
        {
            _triggerGate.Release();
        }
    }

    private async Task ExecuteMatchingTriggerAsync(string line, long generation)
    {
        if (_automationProfile.EmergencyStop || !_gameplayState.GameplayReady)
            return;
        var match = _inputAutomation.EvaluateText(
            AnsiTextParser.StripFormatting(line),
            inCombat: _gameplayState.InCombat,
            sessionId: SelectedConnectionProfile?.ProfileId ?? "play",
            timestamp: DateTimeOffset.UtcNow);
        if (match is null)
            return;

        await SendAutomationCommandsAsync(
            match.Commands,
            generation,
            CancellationToken.None);
        await Dispatcher.UIThread.InvokeAsync(() =>
            AutomationStatus = $"Trigger '{match.RuleName}' sent {match.Commands.Count} command(s)." );
    }

    private async Task RunAutomationClockAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(250));
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await _triggerGate.WaitAsync(cancellationToken);
                try
                {
                    var generation = Volatile.Read(ref _clientConnectionGeneration);
                    var connected = _terminalSession.IsConnected;
                    var now = DateTimeOffset.UtcNow;
                    var match = _timedEventScheduler.Evaluate(
                        _automationProfile.Events,
                        new ClientTimedEventContext(
                            SelectedConnectionProfile?.ProfileId ?? "play",
                            now,
                            connected,
                            _gameplayState.GameplayReady,
                            _gameplayState.InCombat,
                            _automationProfile.TimedEventsEnabled &&
                            !_automationProfile.EmergencyStop,
                            _automationProfile.MinimumCommandDelayMs,
                            _lastAutomationCommandAt));
                    if (match is null)
                        continue;

                    await SendAutomationCommandsAsync(
                        match.Commands,
                        generation,
                        cancellationToken);
                    await Dispatcher.UIThread.InvokeAsync(() =>
                        AutomationStatus = $"Event '{match.RuleName}' sent {match.Commands.Count} command(s)." );
                }
                finally
                {
                    _triggerGate.Release();
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
                AutomationStatus = $"Event scheduler stopped: {exception.Message}");
        }
    }

    private async Task SendAutomationCommandsAsync(
        IReadOnlyList<string> commands,
        long generation,
        CancellationToken cancellationToken)
    {
        foreach (var command in commands.Take(ClientAutomationExpansionEngine.MaximumCommandSteps))
        {
            if (_automationProfile.EmergencyStop ||
                generation != Volatile.Read(ref _clientConnectionGeneration) ||
                !_terminalSession.IsConnected)
                break;

            await _terminalSession.SendLineAsync(command, sensitive: false, cancellationToken);
            _lastAutomationCommandAt = DateTimeOffset.UtcNow;
            if (commands.Count > 1)
                await Task.Delay(
                    Math.Max(500, _automationProfile.MinimumCommandDelayMs),
                    cancellationToken);
        }
    }

    private async Task ResetConnectionAutomationAsync()
    {
        StopNavigation("Route stopped because the connection changed.");
        await _triggerGate.WaitAsync();
        try
        {
            Interlocked.Increment(ref _clientConnectionGeneration);
            _gameplayState.Reset();
            _timedEventScheduler.Reset();
            _triggerReceiveBuffer.Clear();
            _lastAutomationCommandAt = null;
            await Dispatcher.UIThread.InvokeAsync(() => CurrentRoomKey = string.Empty);
        }
        finally
        {
            _triggerGate.Release();
        }
    }

    private void HandleTerminalStatusChanged(string status)
    {
        if (status.StartsWith("Disconnected", StringComparison.OrdinalIgnoreCase))
        {
            StopNavigation("Route stopped because the connection was lost.");
            Interlocked.Increment(ref _clientConnectionGeneration);
            _gameplayState.Reset();
            _timedEventScheduler.Reset();
            _lastAutomationCommandAt = null;
            Dispatcher.UIThread.Post(() => CurrentRoomKey = string.Empty);
        }
        Dispatcher.UIThread.Post(() => ClientStatus = status);
    }

    private async Task LoadNavigationAsync(string worldName)
    {
        var generation = Interlocked.Increment(ref _navigationLoadGeneration);
        var worldId = NormalizeWorldId(worldName);
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            _routeGraph = null;
            VisibleNavigationRooms.Clear();
            SelectedNavigationRoom = null;
            NavigationStatus = $"Loading {worldId} navigation graph...";
        });
        try
        {
            var path = ResolveNavigationGraphPath(worldId);
            if (path is null)
                throw new FileNotFoundException(
                    $"No generated navigation graph was found for world '{worldId}'. Generate that world before using navigation.");

            var graph = await Task.Run(() => ClientRouteGraph.Load(path));
            if (graph.WorldId.Length > 0 &&
                !NormalizeWorldId(graph.WorldId).Equals(worldId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Navigation graph world '{graph.WorldId}' does not match selected world '{worldId}'.");
            }
            if (generation != Volatile.Read(ref _navigationLoadGeneration)) return;
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _routeGraph = graph;
                RefreshNavigationRooms();
                var evidence = graph.HasHazardMetadata ? "decoded hazard metadata" : "legacy topology only";
                NavigationStatus = $"Loaded {graph.RoomCount:N0} {worldId} rooms with {evidence}.";
            });
        }
        catch (Exception exception)
        {
            if (generation != Volatile.Read(ref _navigationLoadGeneration)) return;
            await Dispatcher.UIThread.InvokeAsync(() =>
                NavigationStatus = $"Navigation unavailable: {exception.Message}");
        }
    }

    private static string? ResolveNavigationGraphPath(string worldId)
    {
        var candidates = new List<string>
        {
            Path.Combine(AppContext.BaseDirectory, "Data", worldId, "navigation_graph.json"),
            Path.Combine(AppContext.BaseDirectory, "navigation_graph.json")
        };
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            candidates.Add(Path.Combine(directory.FullName, "Data", worldId, "navigation_graph.json"));
        }
        if (worldId.Equals("Classic", StringComparison.OrdinalIgnoreCase))
        {
            candidates.Add(Path.Combine(
                AppContext.BaseDirectory,
                "Assets",
                "Paths",
                "classic_route_graph.json"));
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
                 directory is not null;
                 directory = directory.Parent)
            {
                candidates.Add(Path.Combine(
                    directory.FullName,
                    "Apps",
                    "Source",
                    "RealmGate.App",
                    "Assets",
                    "Paths",
                    "classic_route_graph.json"));
            }
        }
        return candidates.FirstOrDefault(File.Exists);
    }

    private static string NormalizeWorldId(string? worldName)
    {
        var normalized = new string((worldName ?? string.Empty)
            .Where(char.IsLetterOrDigit)
            .ToArray());
        return normalized.Length == 0 ? "Classic" : normalized;
    }

    private void RefreshNavigationRooms()
    {
        if (_routeGraph is null) return;
        var selectedKey = SelectedNavigationRoom?.Key;
        VisibleNavigationRooms.Clear();
        foreach (var room in _routeGraph.Search(NavigationSearchText))
            VisibleNavigationRooms.Add(room);
        SelectedNavigationRoom = selectedKey is null
            ? VisibleNavigationRooms.FirstOrDefault()
            : VisibleNavigationRooms.FirstOrDefault(room =>
                  room.Key.Equals(selectedKey, StringComparison.OrdinalIgnoreCase))
              ?? VisibleNavigationRooms.FirstOrDefault();
    }

    private async Task LoadRecordedRoutesAsync()
    {
        try
        {
            var catalog = await _recordedRouteStore.LoadAsync();
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _recordedRouteCatalog = catalog;
                RefreshRecordedRoutes();
            });
        }
        catch (Exception exception)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
                NavigationStatus = $"Recorded routes unavailable: {exception.Message}");
        }
    }

    private void RefreshRecordedRoutes()
    {
        var worldId = NormalizeWorldId(SelectedConnectionProfile?.WorldName);
        var selectedId = SelectedRecordedRoute?.Id;
        RecordedRoutes.Clear();
        foreach (var route in _recordedRouteCatalog.Routes
                     .Where(route => NormalizeWorldId(route.WorldId)
                         .Equals(worldId, StringComparison.OrdinalIgnoreCase))
                     .OrderBy(route => route.Name, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(route => route.RecordedAtUtc))
        {
            RecordedRoutes.Add(route);
        }
        SelectedRecordedRoute = selectedId is null
            ? RecordedRoutes.FirstOrDefault()
            : RecordedRoutes.FirstOrDefault(route => route.Id.Equals(
                  selectedId,
                  StringComparison.OrdinalIgnoreCase))
              ?? RecordedRoutes.FirstOrDefault();
    }

    private void StartRouteRecording()
    {
        if (!_terminalSession.IsConnected || !_gameplayState.GameplayReady || CurrentRoomKey.Length == 0)
        {
            NavigationStatus = "Connect, enter gameplay, and obtain an exact room before recording a route.";
            return;
        }
        if (_routeGraph is null || !_routeGraph.ContainsRoom(CurrentRoomKey))
        {
            NavigationStatus = "The current room is not present in the selected world's navigation graph.";
            return;
        }

        StopNavigation("Route stopped for recording.");
        _recordingRouteSteps.Clear();
        _recordingStartRoomKey = CurrentRoomKey;
        IsRecordingRoute = true;
        NavigationStatus = $"Recording from {_recordingStartRoomKey}. Use normal movement commands, then Finish.";
    }

    private async Task RecordManualMovementAsync(string command)
    {
        if (!IsRecordingRoute || _routeGraph is null) return;
        var from = CurrentRoomKey;
        if (!_routeGraph.ContainsRoom(from))
        {
            CancelRouteRecording();
            NavigationStatus = "Recording stopped because the current room is outside the selected graph.";
            return;
        }

        var generation = Volatile.Read(ref _clientConnectionGeneration);
        try
        {
            var actual = await SendObservedMovementAsync(command, generation, CancellationToken.None);
            if (actual.Equals(from, StringComparison.OrdinalIgnoreCase))
            {
                NavigationStatus = $"'{command}' did not change rooms; no recorded step was added.";
                return;
            }
            if (!_routeGraph.ContainsRoom(actual))
            {
                CancelRouteRecording();
                NavigationStatus = $"Recording stopped because confirmed room {actual} is outside the selected graph.";
                return;
            }

            _recordingRouteSteps.Add(new ClientRecordedRouteStep
            {
                FromRoomKey = from,
                ToRoomKey = actual,
                Command = command.Trim()
            });
            NavigationStatus = $"Recorded {_recordingRouteSteps.Count} step(s); now at {actual}.";
        }
        catch (Exception exception)
        {
            CancelRouteRecording();
            NavigationStatus = $"Recording stopped: {exception.Message}";
        }
    }

    private async Task FinishRouteRecordingAsync()
    {
        if (!IsRecordingRoute)
        {
            NavigationStatus = "No route recording is active.";
            return;
        }
        IsRecordingRoute = false;
        if (_recordingRouteSteps.Count == 0)
        {
            _recordingStartRoomKey = string.Empty;
            NavigationStatus = "Recording ended without any confirmed movement.";
            return;
        }

        var route = new ClientRecordedRoute
        {
            Name = string.IsNullOrWhiteSpace(RecordedRouteName)
                ? "Recorded Route"
                : RecordedRouteName.Trim(),
            WorldId = NormalizeWorldId(SelectedConnectionProfile?.WorldName),
            StartRoomKey = _recordingStartRoomKey,
            DestinationRoomKey = _recordingRouteSteps[^1].ToRoomKey
        };
        foreach (var step in _recordingRouteSteps)
        {
            route.Steps.Add(new ClientRecordedRouteStep
            {
                FromRoomKey = step.FromRoomKey,
                ToRoomKey = step.ToRoomKey,
                Command = step.Command
            });
        }
        _recordedRouteCatalog.Routes.Add(route);
        try
        {
            await _recordedRouteStore.SaveAsync(_recordedRouteCatalog);
            _recordingRouteSteps.Clear();
            _recordingStartRoomKey = string.Empty;
            RefreshRecordedRoutes();
            SelectedRecordedRoute = route;
            NavigationStatus = $"Saved '{route.Name}' with {route.Steps.Count} confirmed steps.";
        }
        catch (Exception exception)
        {
            _recordedRouteCatalog.Routes.Remove(route);
            NavigationStatus = $"Recorded route was not saved: {exception.Message}";
        }
    }

    private void CancelRouteRecording()
    {
        IsRecordingRoute = false;
        _recordingRouteSteps.Clear();
        _recordingStartRoomKey = string.Empty;
        NavigationStatus = "Route recording cancelled.";
    }

    private async Task RunRecordedRouteAsync()
    {
        var route = SelectedRecordedRoute;
        if (route is null)
        {
            NavigationStatus = "Select a recorded route first.";
            return;
        }
        if (!_automationProfile.NavigationEnabled || _automationProfile.EmergencyStop ||
            !_terminalSession.IsConnected || !_gameplayState.GameplayReady)
        {
            NavigationStatus = "Enable navigation, clear Emergency Stop, connect, and enter gameplay first.";
            return;
        }
        var worldId = NormalizeWorldId(SelectedConnectionProfile?.WorldName);
        if (!NormalizeWorldId(route.WorldId).Equals(worldId, StringComparison.OrdinalIgnoreCase))
        {
            NavigationStatus = $"Recorded route world '{route.WorldId}' does not match selected world '{worldId}'.";
            return;
        }
        if (!CurrentRoomKey.Equals(route.StartRoomKey, StringComparison.OrdinalIgnoreCase))
        {
            NavigationStatus = $"Recorded route requires exact start {route.StartRoomKey}; current room is {CurrentRoomKey}.";
            return;
        }

        var cancellation = BeginNavigation("Replacing the previous route.");
        var generation = Volatile.Read(ref _clientConnectionGeneration);
        try
        {
            var current = CurrentRoomKey;
            for (var index = 0; index < route.Steps.Count; index++)
            {
                var recorded = route.Steps[index];
                if (!current.Equals(recorded.FromRoomKey, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        $"Recorded route stopped: step {index + 1} expects {recorded.FromRoomKey}, current room is {current}.");
                var step = new ClientRouteStep(
                    index + 1,
                    recorded.FromRoomKey,
                    recorded.ToRoomKey,
                    recorded.Command);
                var actual = await SendVerifiedMovementAsync(
                    step,
                    route.Steps.Count,
                    generation,
                    cancellation.Token,
                    $"; recorded route '{route.Name}'");
                if (!actual.Equals(recorded.ToRoomKey, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        $"Recorded route stopped: expected {recorded.ToRoomKey}, received {actual}.");
                current = actual;
            }
            NavigationStatus = $"Recorded route '{route.Name}' completed at {current}.";
        }
        catch (OperationCanceledException)
        {
            NavigationStatus = "Recorded route stopped.";
        }
        catch (Exception exception)
        {
            NavigationStatus = exception.Message;
        }
        finally
        {
            CompleteNavigation(cancellation);
        }
    }

    private async Task DeleteRecordedRouteAsync()
    {
        var route = SelectedRecordedRoute;
        if (route is null) return;
        _recordedRouteCatalog.Routes.Remove(route);
        try
        {
            await _recordedRouteStore.SaveAsync(_recordedRouteCatalog);
            RefreshRecordedRoutes();
            NavigationStatus = $"Deleted recorded route '{route.Name}'.";
        }
        catch (Exception exception)
        {
            _recordedRouteCatalog.Routes.Add(route);
            RefreshRecordedRoutes();
            SelectedRecordedRoute = route;
            NavigationStatus = $"Recorded route was not deleted: {exception.Message}";
        }
    }

    private async Task StartNavigationAsync()
    {
        if (_routeGraph is null || SelectedNavigationRoom is null)
        {
            NavigationStatus = "Select a destination after the route graph loads.";
            return;
        }
        if (!_routeGraph.HasHazardMetadata)
        {
            NavigationStatus = "Automatic routing requires a generated v2 graph with decoded hazard metadata.";
            return;
        }
        if (!_automationProfile.NavigationEnabled || _automationProfile.EmergencyStop)
        {
            NavigationStatus = "Enable navigation and clear Emergency Stop first.";
            return;
        }
        if (!_terminalSession.IsConnected || !_gameplayState.GameplayReady)
        {
            NavigationStatus = "Connect and enter gameplay before starting a route.";
            return;
        }
        if (CurrentRoomKey.Length == 0)
        {
            NavigationStatus = SupportsPrivateRoomState()
                ? "Waiting for a map-qualified room update."
                : "Verified navigation requires a RealmGate room-state connection.";
            if (SupportsPrivateRoomState())
                await RequestRoomStateAsync(
                    Volatile.Read(ref _clientConnectionGeneration),
                    CancellationToken.None);
            return;
        }

        var destination = SelectedNavigationRoom.Key;
        var plan = _routeGraph.Plan(
            CurrentRoomKey,
            destination,
            NavigationOptions.Preferences,
            NavigationOptions.CharacterLevel);
        if (plan is null)
        {
            NavigationStatus = NavigationOptions.Preferences.AllowRiskyFallback
                ? $"No connected route was found from {CurrentRoomKey} to {destination}."
                : "No route satisfies the current hazard rules. Enable risky fallback only if you accept the decoded hazards.";
            return;
        }
        if (plan.Steps.Count == 0)
        {
            NavigationStatus = "You are already at that destination.";
            return;
        }

        var cancellation = BeginNavigation("Replacing the previous route.");
        var generation = Volatile.Read(ref _clientConnectionGeneration);
        try
        {
            var current = CurrentRoomKey;
            var recoveryAttempts = 0;
            while (!current.Equals(destination, StringComparison.OrdinalIgnoreCase))
            {
                cancellation.Token.ThrowIfCancellationRequested();
                plan = _routeGraph.Plan(
                    current,
                    destination,
                    NavigationOptions.Preferences,
                    NavigationOptions.CharacterLevel);
                if (plan is null || plan.Steps.Count == 0)
                    throw new InvalidOperationException(
                        $"No verified route remains from {current} to {destination}.");

                var replan = false;
                foreach (var step in plan.Steps)
                {
                    var risk = plan.UsedRiskyFallback
                        ? $"; risky fallback score {plan.RiskScore}"
                        : string.Empty;
                    var actual = await SendVerifiedMovementAsync(
                        step,
                        plan.Steps.Count,
                        generation,
                        cancellation.Token,
                        risk);
                    if (actual.Equals(step.ToRoomKey, StringComparison.OrdinalIgnoreCase))
                    {
                        current = actual;
                        continue;
                    }

                    if (!NavigationOptions.AutoRecoverDesynchronizedRoute ||
                        recoveryAttempts >= NavigationOptions.MaximumRecoveryAttempts ||
                        !_routeGraph.ContainsRoom(actual))
                    {
                        throw new InvalidOperationException(
                            $"Route stopped: expected {step.ToRoomKey}, received {actual}.");
                    }

                    recoveryAttempts++;
                    current = actual;
                    NavigationStatus =
                        $"Route changed at {actual}; calculating recovery {recoveryAttempts}/{NavigationOptions.MaximumRecoveryAttempts}.";
                    replan = true;
                    break;
                }
                if (!replan && current.Equals(destination, StringComparison.OrdinalIgnoreCase))
                    break;
                if (!replan)
                    throw new InvalidOperationException(
                        $"Route ended at {current} before reaching {destination}.");
            }
            NavigationStatus = $"Route completed at {destination}.";
        }
        catch (OperationCanceledException)
        {
            NavigationStatus = "Route stopped.";
        }
        catch (Exception exception)
        {
            NavigationStatus = exception.Message;
        }
        finally
        {
            CompleteNavigation(cancellation);
        }
    }

    private async Task StartAutoRoamAsync()
    {
        if (_routeGraph is null)
        {
            NavigationStatus = "Load the selected world's navigation graph before starting Auto-Roam.";
            return;
        }
        if (!_routeGraph.HasHazardMetadata)
        {
            NavigationStatus = "Auto-Roam requires a generated v2 graph with decoded hazard metadata.";
            return;
        }
        if (!_automationProfile.NavigationEnabled || _automationProfile.EmergencyStop)
        {
            NavigationStatus = "Enable navigation and clear Emergency Stop first.";
            return;
        }
        if (!_terminalSession.IsConnected || !_gameplayState.GameplayReady || CurrentRoomKey.Length == 0)
        {
            NavigationStatus = "Connect, enter gameplay, and obtain an exact room before starting Auto-Roam.";
            return;
        }

        var cancellation = BeginNavigation("Replacing the previous route.");
        var generation = Volatile.Read(ref _clientConnectionGeneration);
        IsAutoRoaming = true;
        try
        {
            var current = CurrentRoomKey;
            var previous = string.Empty;
            while (true)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                var choices = _routeGraph.GetSafeRoamChoices(
                    current,
                    previous,
                    NavigationOptions.Preferences,
                    NavigationOptions.CharacterLevel);
                if (choices.Count == 0)
                    throw new InvalidOperationException(
                        $"Auto-Roam stopped: no transition from {current} satisfies the current hazard rules.");

                var choicePool = Math.Min(3, choices.Count);
                var step = choices[Random.Shared.Next(choicePool)];
                var actual = await SendVerifiedMovementAsync(
                    step,
                    1,
                    generation,
                    cancellation.Token,
                    "; Auto-Roam");
                if (!actual.Equals(step.ToRoomKey, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        $"Auto-Roam stopped: expected {step.ToRoomKey}, received {actual}.");

                previous = current;
                current = actual;
                NavigationStatus = $"Auto-Roam at {current}. Stop to take manual control.";
                await Task.Delay(
                    Math.Max(NavigationOptions.AutoRoamDelayMs, _automationProfile.MinimumCommandDelayMs),
                    cancellation.Token);
            }
        }
        catch (OperationCanceledException)
        {
            NavigationStatus = "Auto-Roam stopped.";
        }
        catch (Exception exception)
        {
            NavigationStatus = exception.Message;
        }
        finally
        {
            IsAutoRoaming = false;
            CompleteNavigation(cancellation);
        }
    }

    private async Task<string> SendVerifiedMovementAsync(
        ClientRouteStep step,
        int totalSteps,
        long generation,
        CancellationToken cancellationToken,
        string suffix)
    {
        while (_gameplayState.InCombat == true)
        {
            NavigationStatus = $"Paused in combat before step {step.Number}/{totalSteps}.";
            await Task.Delay(500, cancellationToken);
        }

        var confirmation = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_routeSync) _routeRoomConfirmation = confirmation;
        try
        {
            NavigationStatus =
                $"Step {step.Number}/{totalSteps}: {step.Command} to {step.ToRoomKey}{suffix}";
            await _triggerGate.WaitAsync(cancellationToken);
            try
            {
                if (generation != Volatile.Read(ref _clientConnectionGeneration) ||
                    !_terminalSession.IsConnected)
                {
                    throw new IOException("The connection changed during route execution.");
                }
                await _terminalSession.SendLineAsync(step.Command, false, cancellationToken);
                _lastAutomationCommandAt = DateTimeOffset.UtcNow;
            }
            finally
            {
                _triggerGate.Release();
            }

            await Task.Delay(250, cancellationToken);
            await RequestRoomStateAsync(generation, cancellationToken);
            var actual = await confirmation.Task.WaitAsync(
                TimeSpan.FromSeconds(12),
                cancellationToken);
            await Task.Delay(
                Math.Max(500, _automationProfile.MinimumCommandDelayMs),
                cancellationToken);
            return actual;
        }
        finally
        {
            lock (_routeSync)
            {
                if (ReferenceEquals(_routeRoomConfirmation, confirmation))
                    _routeRoomConfirmation = null;
            }
        }
    }

    private async Task<string> SendObservedMovementAsync(
        string command,
        long generation,
        CancellationToken cancellationToken)
    {
        if (!SupportsPrivateRoomState())
            throw new InvalidOperationException(
                "Route recording requires a RealmGate room-state connection.");

        var confirmation = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_routeSync) _routeRoomConfirmation = confirmation;
        try
        {
            await _triggerGate.WaitAsync(cancellationToken);
            try
            {
                if (generation != Volatile.Read(ref _clientConnectionGeneration) ||
                    !_terminalSession.IsConnected)
                {
                    throw new IOException("The connection changed during route recording.");
                }
                await _terminalSession.SendLineAsync(command, false, cancellationToken);
                _lastAutomationCommandAt = DateTimeOffset.UtcNow;
            }
            finally
            {
                _triggerGate.Release();
            }

            await Task.Delay(250, cancellationToken);
            await RequestRoomStateAsync(generation, cancellationToken);
            return await confirmation.Task.WaitAsync(
                TimeSpan.FromSeconds(12),
                cancellationToken);
        }
        finally
        {
            lock (_routeSync)
            {
                if (ReferenceEquals(_routeRoomConfirmation, confirmation))
                    _routeRoomConfirmation = null;
            }
        }
    }

    private CancellationTokenSource BeginNavigation(string replacementMessage)
    {
        StopNavigation(replacementMessage);
        var cancellation = new CancellationTokenSource();
        lock (_routeSync) _routeCancellation = cancellation;
        return cancellation;
    }

    private void CompleteNavigation(CancellationTokenSource cancellation)
    {
        lock (_routeSync)
        {
            if (ReferenceEquals(_routeCancellation, cancellation))
            {
                _routeCancellation = null;
                _routeRoomConfirmation = null;
            }
        }
        cancellation.Dispose();
    }

    private void StopNavigation(string message)
    {
        CancellationTokenSource? cancellation;
        lock (_routeSync) cancellation = _routeCancellation;
        cancellation?.Cancel();
        if (cancellation is not null)
        {
            IsAutoRoaming = false;
            NavigationStatus = message;
        }
    }

    private void ObserveRoomState(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            if (!document.RootElement.TryGetProperty("RoomKey", out var value) &&
                !document.RootElement.TryGetProperty("roomKey", out value))
                return;
            var roomKey = ClientRouteGraph.NormalizeRoomKey(value.GetString());
            if (roomKey.Length == 0) return;
            _gameplayState.MarkGameplayReady();
            Dispatcher.UIThread.Post(() => CurrentRoomKey = roomKey);

            TaskCompletionSource<string>? confirmation;
            lock (_routeSync) confirmation = _routeRoomConfirmation;
            confirmation?.TrySetResult(roomKey);
        }
        catch (JsonException)
        {
        }
    }

    private async Task RequestRoomStateAsync(long generation, CancellationToken cancellationToken)
    {
        if (!SupportsPrivateRoomState() ||
            generation != Volatile.Read(ref _clientConnectionGeneration) ||
            !_terminalSession.IsConnected)
            return;

        await _triggerGate.WaitAsync(cancellationToken);
        try
        {
            if (generation != Volatile.Read(ref _clientConnectionGeneration) ||
                !_terminalSession.IsConnected)
                return;
            _terminalSession.NotePrivateCommand("roomdata");
            await _terminalSession.SendLineAsync("roomdata", false, cancellationToken);
        }
        finally
        {
            _triggerGate.Release();
        }
    }

    private bool SupportsPrivateRoomState()
    {
        var profile = SelectedConnectionProfile;
        if (profile is null) return false;
        var identity = $"{profile.ProfileName} {profile.ServerProfileName}";
        if (identity.Contains("RealmGate", StringComparison.OrdinalIgnoreCase) ||
            identity.Contains("Reforged", StringComparison.OrdinalIgnoreCase))
            return true;
        return identity.Contains("Local Host", StringComparison.OrdinalIgnoreCase) &&
               (profile.HostName.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                profile.HostName.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
                profile.HostName.Equals("::1", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsMovementCommand(string command) =>
        command.Trim().ToLowerInvariant() is
            "n" or "north" or "ne" or "northeast" or "e" or "east" or
            "se" or "southeast" or "s" or "south" or "sw" or "southwest" or
            "w" or "west" or "nw" or "northwest" or "u" or "up" or "d" or "down";

    private void ApplyMacroCommandsText(ClientCommandMacro? macro)
    {
        if (macro is null)
            return;
        macro.Commands.Clear();
        foreach (var command in ClientAutomationExpansionEngine.SplitCommandSteps(
                     MacroCommandsText))
        {
            macro.Commands.Add(command);
        }
    }

    private static void ValidateAutomationProfile(
        ClientInputAutomationProfile profile)
    {
        ValidateUniqueNames(
            profile.Aliases.Select(alias => alias.Name),
            "alias");
        foreach (var alias in profile.Aliases)
        {
            var conflict = ClientAutomationExpansionEngine
                .AliasNameConflictReason(alias.Name);
            if (conflict is not null)
                throw new InvalidOperationException(conflict);
            if (ClientAutomationExpansionEngine
                    .SplitCommandSteps(alias.Command).Count == 0)
            {
                throw new InvalidOperationException(
                    $"Alias '{alias.Name}' has no command.");
            }
        }

        ValidateUniqueNames(
            profile.Macros.Select(macro => macro.Name),
            "macro");
        foreach (var macro in profile.Macros)
        {
            if (ClientAutomationExpansionEngine
                    .ExpandMacro(macro.Commands, string.Empty).Count == 0)
            {
                throw new InvalidOperationException(
                    $"Macro '{macro.Name}' has no command steps.");
            }
        }

        foreach (var trigger in profile.TextTriggers)
        {
            var error = ClientAutomationExpansionEngine.ValidateTriggerPattern(
                trigger.MatchMode,
                trigger.MatchText,
                trigger.CaseSensitive);
            if (error is not null)
            {
                throw new InvalidOperationException(
                    $"Trigger '{trigger.Name}': {error}");
            }

            if (ClientAutomationExpansionEngine
                    .SplitCommandSteps(trigger.Command).Count == 0)
            {
                throw new InvalidOperationException(
                    $"Trigger '{trigger.Name}' has no response command.");
            }
        }

        foreach (var scheduledEvent in profile.Events.ScheduledEvents)
        {
            if (string.IsNullOrWhiteSpace(scheduledEvent.Name))
                throw new InvalidOperationException("An event name is blank.");
            if (ClientAutomationExpansionEngine
                    .SplitCommandSteps(scheduledEvent.Command).Count == 0)
            {
                throw new InvalidOperationException(
                    $"Event '{scheduledEvent.Name}' has no command.");
            }
        }
    }

    private static void ValidateUniqueNames(
        IEnumerable<string> names,
        string kind)
    {
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in names)
        {
            var normalized = name?.Trim() ?? string.Empty;
            if (normalized.Length == 0)
                throw new InvalidOperationException($"A {kind} name is blank.");
            if (!found.Add(normalized))
                throw new InvalidOperationException(
                    $"Duplicate {kind} name '{normalized}'.");
        }
    }

    private static string MakeUniqueAutomationName(
        string prefix,
        IEnumerable<string> names)
    {
        var used = names.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var suffix = 1;
        var candidate = prefix;
        while (used.Contains(candidate))
            candidate = $"{prefix}{++suffix}";
        return candidate;
    }

    private static ClientInputAutomationProfile CloneAutomationProfile(
        ClientInputAutomationProfile source)
    {
        var clone = new ClientInputAutomationProfile
        {
            AliasesEnabled = source.AliasesEnabled,
            MacrosEnabled = source.MacrosEnabled,
            TextTriggersEnabled = source.TextTriggersEnabled,
            TimedEventsEnabled = source.TimedEventsEnabled,
            NavigationEnabled = source.NavigationEnabled,
            MinimumCommandDelayMs = source.MinimumCommandDelayMs,
            EmergencyStop = source.EmergencyStop
        };
        foreach (var alias in source.Aliases)
        {
            clone.Aliases.Add(new ClientCommandAlias
            {
                Id = alias.Id,
                Enabled = alias.Enabled,
                Name = alias.Name,
                Command = alias.Command
            });
        }
        foreach (var macro in source.Macros)
        {
            var copied = new ClientCommandMacro
            {
                Id = macro.Id,
                Enabled = macro.Enabled,
                Name = macro.Name
            };
            foreach (var command in macro.Commands)
                copied.Commands.Add(command);
            clone.Macros.Add(copied);
        }
        foreach (var trigger in source.TextTriggers)
        {
            clone.TextTriggers.Add(new ClientTextTrigger
            {
                Id = trigger.Id,
                Name = trigger.Name,
                Enabled = trigger.Enabled,
                MatchMode = trigger.MatchMode,
                CaseSensitive = trigger.CaseSensitive,
                MatchText = trigger.MatchText,
                Command = trigger.Command,
                CooldownSeconds = trigger.CooldownSeconds,
                Priority = trigger.Priority,
                OnlyOutOfCombat = trigger.OnlyOutOfCombat,
                OnlyInCombat = trigger.OnlyInCombat
            });
        }
        clone.Events.PauseEventsInCombat = source.Events.PauseEventsInCombat;
        clone.Events.PauseEventsWhileDisconnected = source.Events.PauseEventsWhileDisconnected;
        clone.Navigation.AutoRecoverDesynchronizedRoute = source.Navigation.AutoRecoverDesynchronizedRoute;
        clone.Navigation.MaximumRecoveryAttempts = source.Navigation.MaximumRecoveryAttempts;
        clone.Navigation.AutoRoamDelayMs = source.Navigation.AutoRoamDelayMs;
        clone.Navigation.CharacterLevel = source.Navigation.CharacterLevel;
        clone.Navigation.Preferences.AvoidDamageTraps = source.Navigation.Preferences.AvoidDamageTraps;
        clone.Navigation.Preferences.AvoidSpellTraps = source.Navigation.Preferences.AvoidSpellTraps;
        clone.Navigation.Preferences.AvoidRestrictedExits = source.Navigation.Preferences.AvoidRestrictedExits;
        clone.Navigation.Preferences.AvoidHiddenExits = source.Navigation.Preferences.AvoidHiddenExits;
        clone.Navigation.Preferences.AllowOrdinaryDoors = source.Navigation.Preferences.AllowOrdinaryDoors;
        clone.Navigation.Preferences.AvoidHighLevelNpcRooms = source.Navigation.Preferences.AvoidHighLevelNpcRooms;
        clone.Navigation.Preferences.NpcLevelBuffer = source.Navigation.Preferences.NpcLevelBuffer;
        clone.Navigation.Preferences.AvoidRoomSpellEffects = source.Navigation.Preferences.AvoidRoomSpellEffects;
        clone.Navigation.Preferences.AllowRiskyFallback = source.Navigation.Preferences.AllowRiskyFallback;
        foreach (var scheduledEvent in source.Events.ScheduledEvents)
        {
            clone.Events.ScheduledEvents.Add(new ClientScheduledEvent
            {
                Id = scheduledEvent.Id,
                Name = scheduledEvent.Name,
                Enabled = scheduledEvent.Enabled,
                IntervalMinutes = scheduledEvent.IntervalMinutes,
                IntervalSeconds = scheduledEvent.IntervalSeconds,
                Command = scheduledEvent.Command,
                RunImmediately = scheduledEvent.RunImmediately,
                OnlyOutOfCombat = scheduledEvent.OnlyOutOfCombat,
                OnlyInCombat = scheduledEvent.OnlyInCombat,
                Priority = scheduledEvent.Priority
            });
        }
        return clone;
    }

    private void RefreshLanEndpoints()
    {
        var port = int.TryParse(PortText, out var parsed) && parsed is >= 1 and <= 65535
            ? parsed
            : RealmGateNetworkBinding.DefaultTelnetPort;
        var endpoints = RealmGateNetworkBinding.DiscoverLanEndpoints(port);
        LanEndpointsText = endpoints.Count == 0
            ? "No active non-loopback IPv4 address was discovered."
            : string.Join(Environment.NewLine, endpoints);
    }

    private void AppendLog(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        var line = $"[{DateTimeOffset.Now:HH:mm:ss}] {message}";
        LogText = string.IsNullOrWhiteSpace(LogText) ? line : LogText + Environment.NewLine + line;
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    private void Raise([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public async ValueTask DisposeAsync()
    {
        _host.Log -= _hostLogHandler;
        StopNavigation("Route stopped because RealmGate is closing.");
        if (IsRecordingRoute) CancelRouteRecording();
        _automationClockCancellation.Cancel();
        try { await _automationClockTask; }
        catch (OperationCanceledException) { }
        _automationClockCancellation.Dispose();
        await _terminalSession.DisposeAsync();
        await _host.DisposeAsync();
    }

    private sealed class RelayCommand(Action execute) : ICommand
    {
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => execute();
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }

    private sealed class AsyncCommand(Func<Task> execute) : ICommand
    {
        private int _running;
        public bool CanExecute(object? parameter) => Volatile.Read(ref _running) == 0;
        public event EventHandler? CanExecuteChanged;

        public async void Execute(object? parameter)
        {
            if (Interlocked.Exchange(ref _running, 1) != 0) return;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            try { await execute(); }
            finally
            {
                Volatile.Write(ref _running, 0);
                CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
