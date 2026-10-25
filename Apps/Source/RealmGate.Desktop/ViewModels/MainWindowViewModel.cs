using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Windows.Input;
using Avalonia.Threading;
using RealmGate.Client.Automation;
using RealmGate.Client.Gameplay;
using RealmGate.Client.Inventory;
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
    private readonly ClientSavedDestinationStore _savedDestinationStore =
        ClientSavedDestinationStore.CreateForCurrentUser();
    private readonly ClientInventoryAutomationProfileStore _inventoryStore =
        ClientInventoryAutomationProfileStore.CreateForCurrentUser();
    private readonly ClientGameplayAutomationProfileStore _gameplayStore =
        ClientGameplayAutomationProfileStore.CreateForCurrentUser();
    private readonly ClientNavigationGraphTransferAssembler _navigationTransfer = new();
    private readonly object _navigationTransferSync = new();
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
    private ClientSavedDestinationCatalog _savedDestinationCatalog = new();
    private ClientInventoryAutomationCatalog _inventoryCatalog = new();
    private ClientInventoryAutomationProfile _inventoryProfile = new();
    private ClientInventoryStateSnapshot? _inventoryState;
    private ClientGameplayAutomationCatalog _gameplayCatalog = new();
    private ClientGameplayAutomationProfile _gameplayProfile = new();
    private ClientGameplayAutomationSnapshot? _gameplayAutomationState;
    private ClientCharacterState? _characterState;
    private ClientWorkshopItemSearch? _workshopItemSearch;
    private ClientWorkshopItem? _selectedWorkshopItem;
    private ClientWorkshopItem? _selectedPlannedWorkshopItem;
    private ClientConnectionProfile? _selectedConnectionProfile;
    private ClientCommandAlias? _selectedAlias;
    private ClientCommandMacro? _selectedMacro;
    private ClientTextTrigger? _selectedTrigger;
    private ClientScheduledEvent? _selectedScheduledEvent;
    private ClientRouteGraph? _routeGraph;
    private ClientRouteRoom? _selectedNavigationRoom;
    private ClientRecordedRoute? _selectedRecordedRoute;
    private ClientSavedDestination? _selectedSavedDestination;
    private ClientInventoryItemState? _selectedInventoryItem;
    private ClientInventoryRule? _selectedInventoryRule;
    private ClientEquipmentSet? _selectedEquipmentSet;
    private ClientBuffRule? _selectedBuffRule;
    private readonly List<ClientRecordedRouteStep> _recordingRouteSteps = [];
    private CancellationTokenSource? _routeCancellation;
    private TaskCompletionSource<string>? _routeRoomConfirmation;
    private string _currentRoomKey = string.Empty;
    private string _navigationSearchText = string.Empty;
    private string _navigationStatus = "Navigation graph not loaded.";
    private bool _isAutoRoaming;
    private bool _isRecordingRoute;
    private string _recordedRouteName = "Recorded Route";
    private string _savedDestinationName = string.Empty;
    private string _equipmentSetName = "Equipment Set";
    private string _inventoryStatus = "Inventory state not loaded.";
    private string _inventoryPlanText = "Refresh inventory to begin.";
    private string _gameplayAssistantStatus = "Gameplay assistant state not loaded.";
    private string _characterStatus = "Connect a RealmGate character to view live details.";
    private string _characterIdentityText = string.Empty;
    private string _characterVitalsText = string.Empty;
    private string _characterProgressText = string.Empty;
    private string _characterAttributesText = string.Empty;
    private string _characterSkillsText = string.Empty;
    private string _workshopSearchText = string.Empty;
    private string _workshopSearchStatus = "Search the selected world's items after connecting a character.";
    private string _workshopItemDetails = string.Empty;
    private string _workshopComparisonText = "Search items and add a candidate to compare loadouts.";
    private bool _workshopPlanInitialized;
    private string _gameplayPlanText = "Refresh live state to begin.";
    private string _communicationTarget = string.Empty;
    private string _communicationText = string.Empty;
    private bool _isGameplayAssistantRunning;
    private DateTimeOffset? _lastGameplayActionAt;
    private DateTimeOffset? _lastGameplayStateRequestAt;
    private string _lastGameplayCommand = string.Empty;
    private int _repeatedGameplayCommands;
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
    private int _navigationGraphRequested;
    private readonly Task _automationClockTask;
    private readonly Task _inventoryLoadTask;
    private readonly Task _gameplayLoadTask;
    private readonly Task _gameplayAssistantClockTask;
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
            {
                _ = RequestRoomStateAsync(generation, CancellationToken.None);
                _ = RequestNavigationGraphAsync(generation, CancellationToken.None);
            }
        };
        _terminalSession.ProtocolMessageReceived += message =>
        {
            if (message.Channel.Equals("ROOMDATA", StringComparison.OrdinalIgnoreCase))
            {
                _gameplayState.MarkGameplayReady();
                _ = RequestNavigationGraphAsync(
                    Volatile.Read(ref _clientConnectionGeneration),
                    CancellationToken.None);
                _ = RequestInventoryStateAsync(
                    Volatile.Read(ref _clientConnectionGeneration),
                    CancellationToken.None);
                _ = RequestGameplayAutomationStateAsync(
                    Volatile.Read(ref _clientConnectionGeneration),
                    CancellationToken.None);
                _ = RequestCharacterStateAsync(
                    Volatile.Read(ref _clientConnectionGeneration),
                    CancellationToken.None);
            }
            if (message.Channel.Equals("ROOMSTATE", StringComparison.OrdinalIgnoreCase))
                ObserveRoomState(message.Payload);
            if (message.Channel.StartsWith("NAVGRAPH", StringComparison.OrdinalIgnoreCase))
                HandleNavigationGraphMessage(message);
            if (message.Channel.Equals("INVENTORYSTATE", StringComparison.OrdinalIgnoreCase))
                _ = HandleInventoryStateAsync(message.Payload);
            if (message.Channel.Equals("AUTOMATIONSTATE", StringComparison.OrdinalIgnoreCase))
                _ = HandleGameplayAutomationStateAsync(message.Payload,
                    Volatile.Read(ref _clientConnectionGeneration));
            if (message.Channel.Equals("CHARACTERSTATE", StringComparison.OrdinalIgnoreCase))
                _ = HandleCharacterStateAsync(message.Payload,
                    Volatile.Read(ref _clientConnectionGeneration));
            if (message.Channel.Equals("CHARACTERSTATEERROR", StringComparison.OrdinalIgnoreCase))
                Dispatcher.UIThread.Post(() =>
                    ClearCharacterState($"Character details unavailable: {message.Payload}"));
            if (message.Channel.Equals("ITEMSEARCH", StringComparison.OrdinalIgnoreCase))
                _ = HandleWorkshopItemSearchAsync(message.Payload,
                    Volatile.Read(ref _clientConnectionGeneration));
            if (message.Channel.Equals("ITEMSEARCHERROR", StringComparison.OrdinalIgnoreCase))
                Dispatcher.UIThread.Post(() => ClearWorkshopSearch($"Item search unavailable: {message.Payload}"));
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
        SaveFavoriteDestinationCommand = new AsyncCommand(() => SaveCurrentDestinationAsync(ClientSavedDestinationKind.Favorite));
        SaveStashDestinationCommand = new AsyncCommand(() => SaveCurrentDestinationAsync(ClientSavedDestinationKind.Stash));
        NavigateSavedDestinationCommand = new AsyncCommand(NavigateSavedDestinationAsync);
        DeleteSavedDestinationCommand = new AsyncCommand(DeleteSavedDestinationAsync);
        RefreshInventoryCommand = new AsyncCommand(RefreshInventoryAsync);
        SaveInventoryProfileCommand = new AsyncCommand(SaveInventoryProfileAsync);
        AddInventoryRuleCommand = new RelayCommand(AddInventoryRule);
        DeleteInventoryRuleCommand = new RelayCommand(DeleteInventoryRule);
        CaptureEquipmentSetCommand = new RelayCommand(CaptureEquipmentSet);
        DeleteEquipmentSetCommand = new RelayCommand(DeleteEquipmentSet);
        PreviewInventoryRulesCommand = new RelayCommand(PreviewInventoryRules);
        RunInventoryRulesCommand = new AsyncCommand(RunInventoryRulesAsync);
        PreviewEquipmentSetCommand = new RelayCommand(PreviewEquipmentSet);
        ApplyEquipmentSetCommand = new AsyncCommand(ApplyEquipmentSetAsync);
        RefreshGameplayStateCommand = new AsyncCommand(RefreshGameplayStateAsync);
        RefreshCharacterCommand = new AsyncCommand(RefreshCharacterAsync);
        SearchWorkshopItemsCommand = new AsyncCommand(SearchWorkshopItemsAsync);
        AddPlannedWorkshopItemCommand = new RelayCommand(AddPlannedWorkshopItem);
        RemovePlannedWorkshopItemCommand = new RelayCommand(RemovePlannedWorkshopItem);
        ResetPlannedWorkshopItemsCommand = new RelayCommand(ResetPlannedWorkshopItems);
        SaveGameplayProfileCommand = new AsyncCommand(SaveGameplayProfileAsync);
        AddBuffRuleCommand = new RelayCommand(AddBuffRule);
        DeleteBuffRuleCommand = new RelayCommand(DeleteBuffRule);
        PreviewRecoveryCommand = new RelayCommand(() => PreviewGameplayPlan(GameplayPlanArea.Recovery));
        RunRecoveryCommand = new AsyncCommand(() => RunGameplayPlanAsync(GameplayPlanArea.Recovery));
        PreviewCombatCommand = new RelayCommand(() => PreviewGameplayPlan(GameplayPlanArea.Combat));
        RunCombatCommand = new AsyncCommand(() => RunGameplayPlanAsync(GameplayPlanArea.Combat));
        PreviewPartyCommand = new RelayCommand(() => PreviewGameplayPlan(GameplayPlanArea.Party));
        RunPartyCommand = new AsyncCommand(() => RunGameplayPlanAsync(GameplayPlanArea.Party));
        StartGameplayAssistantCommand = new RelayCommand(StartGameplayAssistant);
        StopGameplayAssistantCommand = new RelayCommand(() => StopGameplayAssistant("Gameplay assistant stopped."));
        SendSayCommand = new AsyncCommand(() => SendCommunicationAsync("say", includeTarget: false));
        SendTellCommand = new AsyncCommand(() => SendCommunicationAsync("/", includeTarget: true));
        SendGossipCommand = new AsyncCommand(() => SendCommunicationAsync("gossip", includeTarget: false));
        SendYellCommand = new AsyncCommand(() => SendCommunicationAsync("\"", includeTarget: false));
        RunDiagnosticsCommand = new RelayCommand(RunGameplayDiagnostics);
        RefreshLanEndpoints();
        _ = LoadConnectionProfilesAsync();
        _ = LoadAutomationAsync();
        _ = LoadNavigationAsync("Classic");
        _ = LoadRecordedRoutesAsync();
        _ = LoadSavedDestinationsAsync();
        _inventoryLoadTask = LoadInventoryAutomationAsync();
        _gameplayLoadTask = LoadGameplayAutomationAsync();
        _automationClockTask = RunAutomationClockAsync(_automationClockCancellation.Token);
        _gameplayAssistantClockTask = RunGameplayAssistantClockAsync(_automationClockCancellation.Token);
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
    public ICommand SaveFavoriteDestinationCommand { get; }
    public ICommand SaveStashDestinationCommand { get; }
    public ICommand NavigateSavedDestinationCommand { get; }
    public ICommand DeleteSavedDestinationCommand { get; }
    public ICommand RefreshInventoryCommand { get; }
    public ICommand SaveInventoryProfileCommand { get; }
    public ICommand AddInventoryRuleCommand { get; }
    public ICommand DeleteInventoryRuleCommand { get; }
    public ICommand CaptureEquipmentSetCommand { get; }
    public ICommand DeleteEquipmentSetCommand { get; }
    public ICommand PreviewInventoryRulesCommand { get; }
    public ICommand RunInventoryRulesCommand { get; }
    public ICommand PreviewEquipmentSetCommand { get; }
    public ICommand ApplyEquipmentSetCommand { get; }
    public ICommand RefreshGameplayStateCommand { get; }
    public ICommand RefreshCharacterCommand { get; }
    public ICommand SearchWorkshopItemsCommand { get; }
    public ICommand AddPlannedWorkshopItemCommand { get; }
    public ICommand RemovePlannedWorkshopItemCommand { get; }
    public ICommand ResetPlannedWorkshopItemsCommand { get; }
    public ICommand SaveGameplayProfileCommand { get; }
    public ICommand AddBuffRuleCommand { get; }
    public ICommand DeleteBuffRuleCommand { get; }
    public ICommand PreviewRecoveryCommand { get; }
    public ICommand RunRecoveryCommand { get; }
    public ICommand PreviewCombatCommand { get; }
    public ICommand RunCombatCommand { get; }
    public ICommand PreviewPartyCommand { get; }
    public ICommand RunPartyCommand { get; }
    public ICommand StartGameplayAssistantCommand { get; }
    public ICommand StopGameplayAssistantCommand { get; }
    public ICommand SendSayCommand { get; }
    public ICommand SendTellCommand { get; }
    public ICommand SendGossipCommand { get; }
    public ICommand SendYellCommand { get; }
    public ICommand RunDiagnosticsCommand { get; }

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
    public ObservableCollection<ClientSavedDestination> SavedDestinations { get; } = [];
    public ObservableCollection<ClientInventoryItemState> InventoryItems { get; } = [];
    public ObservableCollection<ClientInventoryItemState> EquippedItems { get; } = [];
    public ClientInventoryAutomationProfile InventoryProfile => _inventoryProfile;
    public ObservableCollection<ClientInventoryRule> InventoryRules => _inventoryProfile.Rules;
    public ObservableCollection<ClientEquipmentSet> EquipmentSets => _inventoryProfile.EquipmentSets;
    public ClientGameplayAutomationProfile GameplayProfile => _gameplayProfile;
    public ObservableCollection<ClientBuffRule> BuffRules => _gameplayProfile.Buffs;
    public ObservableCollection<ClientGameplayNpcState> GameplayNpcs { get; } = [];
    public ObservableCollection<ClientGameplayPartyMemberState> GameplayPartyMembers { get; } = [];
    public ObservableCollection<ClientDiagnosticCheck> GameplayDiagnosticChecks { get; } = [];
    public ObservableCollection<ClientInventoryItemState> CharacterEquipment { get; } = [];
    public ObservableCollection<string> CharacterSpells { get; } = [];
    public ObservableCollection<ClientGameplayEffectState> CharacterEffects { get; } = [];
    public ObservableCollection<ClientWorkshopItem> WorkshopSearchResults { get; } = [];
    public ObservableCollection<ClientWorkshopItem> PlannedWorkshopItems { get; } = [];
    public IReadOnlyList<ClientCombatTargetMode> CombatTargetModes { get; } =
        Enum.GetValues<ClientCombatTargetMode>();
    public IReadOnlyList<ClientInventoryDisposition> InventoryDispositions { get; } =
        Enum.GetValues<ClientInventoryDisposition>();
    public IReadOnlyList<ClientInventoryRuleMatchMode> InventoryRuleMatchModes { get; } =
        Enum.GetValues<ClientInventoryRuleMatchMode>();
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

    public ClientSavedDestination? SelectedSavedDestination
    {
        get => _selectedSavedDestination;
        set => Set(ref _selectedSavedDestination, value);
    }

    public ClientInventoryItemState? SelectedInventoryItem
    {
        get => _selectedInventoryItem;
        set => Set(ref _selectedInventoryItem, value);
    }

    public ClientInventoryRule? SelectedInventoryRule
    {
        get => _selectedInventoryRule;
        set => Set(ref _selectedInventoryRule, value);
    }

    public ClientEquipmentSet? SelectedEquipmentSet
    {
        get => _selectedEquipmentSet;
        set => Set(ref _selectedEquipmentSet, value);
    }

    public ClientBuffRule? SelectedBuffRule
    {
        get => _selectedBuffRule;
        set => Set(ref _selectedBuffRule, value);
    }

    public string EquipmentSetName
    {
        get => _equipmentSetName;
        set => Set(ref _equipmentSetName, value);
    }

    public string InventoryStatus
    {
        get => _inventoryStatus;
        private set => Set(ref _inventoryStatus, value);
    }

    public string InventoryPlanText
    {
        get => _inventoryPlanText;
        private set => Set(ref _inventoryPlanText, value);
    }

    public string GameplayAssistantStatus
    {
        get => _gameplayAssistantStatus;
        private set => Set(ref _gameplayAssistantStatus, value);
    }

    public string CharacterStatus
    {
        get => _characterStatus;
        private set => Set(ref _characterStatus, value);
    }

    public string CharacterIdentityText
    {
        get => _characterIdentityText;
        private set => Set(ref _characterIdentityText, value);
    }

    public string CharacterVitalsText
    {
        get => _characterVitalsText;
        private set => Set(ref _characterVitalsText, value);
    }

    public string CharacterProgressText
    {
        get => _characterProgressText;
        private set => Set(ref _characterProgressText, value);
    }

    public string CharacterAttributesText
    {
        get => _characterAttributesText;
        private set => Set(ref _characterAttributesText, value);
    }

    public string CharacterSkillsText
    {
        get => _characterSkillsText;
        private set => Set(ref _characterSkillsText, value);
    }

    public string WorkshopSearchText
    {
        get => _workshopSearchText;
        set => Set(ref _workshopSearchText, value);
    }

    public string WorkshopSearchStatus
    {
        get => _workshopSearchStatus;
        private set => Set(ref _workshopSearchStatus, value);
    }

    public string WorkshopItemDetails
    {
        get => _workshopItemDetails;
        private set => Set(ref _workshopItemDetails, value);
    }

    public string WorkshopComparisonText
    {
        get => _workshopComparisonText;
        private set => Set(ref _workshopComparisonText, value);
    }

    public ClientWorkshopItem? SelectedWorkshopItem
    {
        get => _selectedWorkshopItem;
        set
        {
            if (Set(ref _selectedWorkshopItem, value))
                WorkshopItemDetails = value is null ? string.Empty : DescribeWorkshopItem(value);
        }
    }

    public ClientWorkshopItem? SelectedPlannedWorkshopItem
    {
        get => _selectedPlannedWorkshopItem;
        set => Set(ref _selectedPlannedWorkshopItem, value);
    }

    public string GameplayPlanText
    {
        get => _gameplayPlanText;
        private set => Set(ref _gameplayPlanText, value);
    }

    public bool IsGameplayAssistantRunning
    {
        get => _isGameplayAssistantRunning;
        private set => Set(ref _isGameplayAssistantRunning, value);
    }

    public string CommunicationTarget
    {
        get => _communicationTarget;
        set => Set(ref _communicationTarget, value);
    }

    public string CommunicationText
    {
        get => _communicationText;
        set => Set(ref _communicationText, value);
    }

    public string SavedDestinationName
    {
        get => _savedDestinationName;
        set => Set(ref _savedDestinationName, value);
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
            StopGameplayAssistant("Gameplay assistant stopped because the selected profile changed.");
            CurrentRoomKey = string.Empty;
            ClearInventoryState("Inventory state cleared because the selected profile changed.");
            ClearGameplayAutomationState("Gameplay state cleared because the selected profile changed.");
            ClearCharacterState("Character details cleared because the selected profile changed.");
            _ = LoadNavigationAsync(value.WorldName);
            RefreshRecordedRoutes();
            RefreshSavedDestinations();
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
        StopGameplayAssistant("Gameplay assistant stopped because the connection changed.");
        await _triggerGate.WaitAsync();
        try
        {
            Interlocked.Increment(ref _clientConnectionGeneration);
            _gameplayState.Reset();
            _timedEventScheduler.Reset();
            _triggerReceiveBuffer.Clear();
            _lastAutomationCommandAt = null;
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                CurrentRoomKey = string.Empty;
                ClearInventoryState("Inventory state cleared because the connection changed.");
                ClearGameplayAutomationState("Gameplay state cleared because the connection changed.");
                ClearCharacterState("Character details cleared because the connection changed.");
            });
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
            StopGameplayAssistant("Gameplay assistant stopped because the connection was lost.");
            Interlocked.Increment(ref _clientConnectionGeneration);
            lock (_navigationTransferSync)
                _navigationTransfer.Reset();
            Interlocked.Exchange(ref _navigationGraphRequested, 0);
            _gameplayState.Reset();
            _timedEventScheduler.Reset();
            _lastAutomationCommandAt = null;
            Dispatcher.UIThread.Post(() =>
            {
                CurrentRoomKey = string.Empty;
                ClearInventoryState("Inventory state cleared because the connection was lost.");
                ClearGameplayAutomationState("Gameplay state cleared because the connection was lost.");
                ClearCharacterState("Character details cleared because the connection was lost.");
            });
        }
        Dispatcher.UIThread.Post(() => ClientStatus = status);
    }

    private async Task LoadNavigationAsync(string worldName)
    {
        var generation = Interlocked.Increment(ref _navigationLoadGeneration);
        var worldId = NormalizeWorldId(worldName);
        lock (_navigationTransferSync)
            _navigationTransfer.Reset();
        Interlocked.Exchange(ref _navigationGraphRequested, 0);
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
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                    NavigationStatus = _terminalSession.IsConnected && SupportsPrivateRoomState()
                        ? $"Waiting for the {worldId} navigation graph from the connected RealmGate host."
                        : $"No local {worldId} navigation graph is available. Connect to its RealmGate host to download it.");
                if (_terminalSession.IsConnected && _gameplayState.GameplayReady && SupportsPrivateRoomState())
                    await RequestNavigationGraphAsync(
                        Volatile.Read(ref _clientConnectionGeneration),
                        CancellationToken.None);
                return;
            }

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
                Interlocked.Exchange(ref _navigationGraphRequested, 0);
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

    private void HandleNavigationGraphMessage(RealmGate.Client.Protocol.RealmGateClientProtocolMessage message)
    {
        try
        {
            ClientNavigationGraphTransferPayload? payload;
            lock (_navigationTransferSync)
                payload = _navigationTransfer.Process(message);

            if (message.Channel.Equals("NAVGRAPHBEGIN", StringComparison.OrdinalIgnoreCase))
            {
                Dispatcher.UIThread.Post(() =>
                    NavigationStatus = $"Downloading {NormalizeWorldId(SelectedConnectionProfile?.WorldName)} navigation graph...");
            }
            if (payload is not null)
            {
                var loadGeneration = Volatile.Read(ref _navigationLoadGeneration);
                _ = ApplyRemoteNavigationGraphAsync(payload, loadGeneration);
            }
        }
        catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException)
        {
            lock (_navigationTransferSync)
                _navigationTransfer.Reset();
            Interlocked.Exchange(ref _navigationGraphRequested, 0);
            Dispatcher.UIThread.Post(() =>
                NavigationStatus = $"Navigation download failed: {exception.Message}");
        }
    }

    private async Task ApplyRemoteNavigationGraphAsync(
        ClientNavigationGraphTransferPayload payload,
        long loadGeneration)
    {
        try
        {
            var graph = await Task.Run(() =>
            {
                var json = payload.DecompressAndVerify();
                using var stream = new MemoryStream(json, writable: false);
                return ClientRouteGraph.Load(stream);
            });
            if (loadGeneration != Volatile.Read(ref _navigationLoadGeneration)) return;

            var selectedWorldId = NormalizeWorldId(SelectedConnectionProfile?.WorldName);
            if (!NormalizeWorldId(payload.WorldId).Equals(selectedWorldId, StringComparison.OrdinalIgnoreCase) ||
                !NormalizeWorldId(graph.WorldId).Equals(selectedWorldId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Downloaded navigation graph world '{graph.WorldId}' does not match selected world '{selectedWorldId}'.");
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _routeGraph = graph;
                Interlocked.Exchange(ref _navigationGraphRequested, 0);
                RefreshNavigationRooms();
                var evidence = graph.HasHazardMetadata ? "decoded hazard metadata" : "legacy topology only";
                NavigationStatus = $"Downloaded {graph.RoomCount:N0} {selectedWorldId} rooms with {evidence}.";
            });
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException)
        {
            if (loadGeneration != Volatile.Read(ref _navigationLoadGeneration)) return;
            Interlocked.Exchange(ref _navigationGraphRequested, 0);
            await Dispatcher.UIThread.InvokeAsync(() =>
                NavigationStatus = $"Navigation download failed: {exception.Message}");
        }
    }

    private async Task RequestNavigationGraphAsync(long generation, CancellationToken cancellationToken)
    {
        if (_routeGraph is not null || !SupportsPrivateRoomState() ||
            generation != Volatile.Read(ref _clientConnectionGeneration) ||
            !_terminalSession.IsConnected || !_gameplayState.GameplayReady)
            return;

        if (Interlocked.CompareExchange(ref _navigationGraphRequested, 1, 0) != 0)
            return;
        await _triggerGate.WaitAsync(cancellationToken);
        try
        {
            if (generation != Volatile.Read(ref _clientConnectionGeneration) ||
                !_terminalSession.IsConnected || !_gameplayState.GameplayReady)
            {
                Interlocked.Exchange(ref _navigationGraphRequested, 0);
                return;
            }
            _terminalSession.NotePrivateCommand("clientdata navigation");
            await _terminalSession.SendLineAsync("clientdata navigation", false, cancellationToken);
        }
        catch
        {
            Interlocked.Exchange(ref _navigationGraphRequested, 0);
            throw;
        }
        finally
        {
            _triggerGate.Release();
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

    private async Task LoadInventoryAutomationAsync()
    {
        try
        {
            var catalog = await _inventoryStore.LoadAsync();
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _inventoryCatalog = catalog;
                InventoryStatus = "Inventory profiles loaded. Connect and refresh inventory.";
            });
        }
        catch (Exception exception)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
                InventoryStatus = $"Inventory profile load failed: {exception.Message}");
        }
    }

    private async Task HandleInventoryStateAsync(string payload)
    {
        try
        {
            await _inventoryLoadTask;
            var state = ClientInventoryStateDecoder.Decode(payload);
            var expectedWorld = NormalizeWorldId(SelectedConnectionProfile?.WorldName);
            if (!NormalizeWorldId(state.WorldId).Equals(expectedWorld, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(
                    $"Inventory world '{state.WorldId}' does not match selected world '{expectedWorld}'.");

            Dispatcher.UIThread.Post(() => ApplyInventoryState(state));
        }
        catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException)
        {
            Dispatcher.UIThread.Post(() =>
                InventoryStatus = $"Inventory state rejected: {exception.Message}");
        }
    }

    private void ApplyInventoryState(ClientInventoryStateSnapshot state)
    {
        _inventoryState = state;
        InventoryItems.Clear();
        foreach (var item in state.Inventory) InventoryItems.Add(item);
        EquippedItems.Clear();
        foreach (var item in state.Equipment) EquippedItems.Add(item);

        _inventoryProfile = ClientInventoryAutomationProfileStore.GetOrCreate(
            _inventoryCatalog, state.WorldId, state.CharacterId, state.CharacterName);
        Raise(nameof(InventoryProfile));
        Raise(nameof(InventoryRules));
        Raise(nameof(EquipmentSets));
        SelectedInventoryItem = InventoryItems.FirstOrDefault();
        SelectedInventoryRule = InventoryRules.FirstOrDefault();
        SelectedEquipmentSet = EquipmentSets.FirstOrDefault();
        InventoryStatus =
            $"{state.CharacterName} in {state.WorldId}: {state.Inventory.Count} carried, " +
            $"{state.Equipment.Count} equipped, {state.CurrencyCopper} copper.";
        InventoryPlanText = "Inventory state refreshed. Preview a rule pass or equipment set before running it.";
        RefreshCharacterPresentation();
    }

    private void ClearInventoryState(string status)
    {
        _inventoryState = null;
        _inventoryProfile = new ClientInventoryAutomationProfile();
        InventoryItems.Clear();
        EquippedItems.Clear();
        SelectedInventoryItem = null;
        SelectedInventoryRule = null;
        SelectedEquipmentSet = null;
        Raise(nameof(InventoryProfile));
        Raise(nameof(InventoryRules));
        Raise(nameof(EquipmentSets));
        InventoryStatus = status;
        InventoryPlanText = "Refresh inventory to begin.";
        RefreshCharacterPresentation();
    }

    private async Task RefreshInventoryAsync() =>
        await RequestInventoryStateAsync(
            Volatile.Read(ref _clientConnectionGeneration), CancellationToken.None);

    private async Task RequestInventoryStateAsync(long generation, CancellationToken cancellationToken)
    {
        if (!SupportsPrivateRoomState() ||
            generation != Volatile.Read(ref _clientConnectionGeneration) ||
            !_terminalSession.IsConnected || !_gameplayState.GameplayReady)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
                InventoryStatus = "Inventory refresh requires a connected RealmGate character session.");
            return;
        }

        await _triggerGate.WaitAsync(cancellationToken);
        try
        {
            if (generation != Volatile.Read(ref _clientConnectionGeneration) || !_terminalSession.IsConnected)
                return;
            _terminalSession.NotePrivateCommand("clientdata inventory");
            await _terminalSession.SendLineAsync("clientdata inventory", false, cancellationToken);
            await Dispatcher.UIThread.InvokeAsync(() => InventoryStatus = "Refreshing inventory from the host...");
        }
        finally { _triggerGate.Release(); }
    }

    private async Task SaveInventoryProfileAsync()
    {
        try
        {
            ClientInventoryAutomationProfileStore.Normalize(_inventoryCatalog);
            _inventoryProfile.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await _inventoryStore.SaveAsync(_inventoryCatalog);
            InventoryStatus = "Inventory rules and equipment sets saved for this world and character.";
        }
        catch (Exception exception)
        {
            InventoryStatus = $"Inventory profile save failed: {exception.Message}";
        }
    }

    private void AddInventoryRule()
    {
        var rule = new ClientInventoryRule
        {
            MatchValue = SelectedInventoryItem?.Name ?? "item name"
        };
        InventoryRules.Add(rule);
        SelectedInventoryRule = rule;
    }

    private void DeleteInventoryRule()
    {
        if (SelectedInventoryRule is null) return;
        var index = InventoryRules.IndexOf(SelectedInventoryRule);
        InventoryRules.Remove(SelectedInventoryRule);
        SelectedInventoryRule = InventoryRules.ElementAtOrDefault(Math.Max(0, index - 1));
    }

    private void CaptureEquipmentSet()
    {
        if (_inventoryState is null)
        {
            InventoryStatus = "Refresh inventory before capturing equipment.";
            return;
        }
        var set = ClientInventoryAutomationPlanner.CaptureEquipmentSet(_inventoryState, EquipmentSetName);
        EquipmentSets.Add(set);
        SelectedEquipmentSet = set;
        EquipmentSetName = "Equipment Set";
        InventoryPlanText = $"Captured '{set.Name}' with {set.Items.Count} equipped item(s).";
    }

    private void DeleteEquipmentSet()
    {
        if (SelectedEquipmentSet is null) return;
        var index = EquipmentSets.IndexOf(SelectedEquipmentSet);
        EquipmentSets.Remove(SelectedEquipmentSet);
        SelectedEquipmentSet = EquipmentSets.ElementAtOrDefault(Math.Max(0, index - 1));
    }

    private void PreviewInventoryRules()
    {
        var plan = BuildInventoryRulePlan();
        InventoryPlanText = FormatInventoryPlan(plan);
    }

    private async Task RunInventoryRulesAsync()
    {
        var plan = BuildInventoryRulePlan();
        InventoryPlanText = FormatInventoryPlan(plan);
        await ExecuteInventoryPlanAsync(plan);
    }

    private void PreviewEquipmentSet()
    {
        var plan = BuildEquipmentSetPlan();
        InventoryPlanText = FormatInventoryPlan(plan);
    }

    private async Task ApplyEquipmentSetAsync()
    {
        var plan = BuildEquipmentSetPlan();
        InventoryPlanText = FormatInventoryPlan(plan);
        await ExecuteInventoryPlanAsync(plan);
    }

    private ClientInventoryAutomationPlan BuildInventoryRulePlan()
    {
        if (_inventoryState is null)
            return new ClientInventoryAutomationPlan
            {
                Action = "Process inventory rules",
                Warnings = ["Refresh inventory before evaluating rules."]
            };
        var atStash = _savedDestinationCatalog.Destinations.Any(destination =>
            destination.Kind == ClientSavedDestinationKind.Stash &&
            NormalizeWorldId(destination.WorldId).Equals(
                NormalizeWorldId(_inventoryState.WorldId), StringComparison.OrdinalIgnoreCase) &&
            ClientRouteGraph.NormalizeRoomKey(destination.RoomKey).Equals(
                ClientRouteGraph.NormalizeRoomKey(CurrentRoomKey), StringComparison.OrdinalIgnoreCase));
        return ClientInventoryAutomationPlanner.PlanRules(
            _inventoryState, _inventoryProfile, new ClientInventoryAutomationContext(atStash));
    }

    private ClientInventoryAutomationPlan BuildEquipmentSetPlan()
    {
        if (_inventoryState is null || SelectedEquipmentSet is null)
            return new ClientInventoryAutomationPlan
            {
                Action = "Apply equipment set",
                Warnings = ["Refresh inventory and select an equipment set first."]
            };
        return ClientInventoryAutomationPlanner.PlanEquipmentSet(
            _inventoryState, _inventoryProfile, SelectedEquipmentSet);
    }

    private async Task ExecuteInventoryPlanAsync(ClientInventoryAutomationPlan plan)
    {
        if (!plan.HasCommands)
        {
            InventoryStatus = plan.Warnings.Count > 0
                ? "Inventory plan was blocked by its safety checks."
                : "Inventory already matches the selected plan.";
            return;
        }
        if (_automationProfile.EmergencyStop)
        {
            InventoryStatus = "Emergency Stop is on. Preview is available, but commands were not sent.";
            return;
        }
        if (!_terminalSession.IsConnected || !_gameplayState.GameplayReady)
        {
            InventoryStatus = "Inventory commands require a connected, ready character session.";
            return;
        }

        var generation = Volatile.Read(ref _clientConnectionGeneration);
        await _triggerGate.WaitAsync();
        try
        {
            await SendAutomationCommandsAsync(plan.Commands, generation, CancellationToken.None);
            InventoryStatus = $"Sent {plan.Commands.Count} inventory command(s).";
        }
        catch (Exception exception)
        {
            InventoryStatus = $"Inventory command failed: {exception.Message}";
            return;
        }
        finally { _triggerGate.Release(); }

        await Task.Delay(Math.Max(500, _automationProfile.MinimumCommandDelayMs));
        await RequestInventoryStateAsync(generation, CancellationToken.None);
    }

    private static string FormatInventoryPlan(ClientInventoryAutomationPlan plan)
    {
        var lines = new List<string> { plan.Summary };
        if (plan.Commands.Count > 0)
        {
            lines.Add("Commands:");
            lines.AddRange(plan.Commands.Select(command => "  " + command));
        }
        if (plan.MissingItems.Count > 0)
            lines.Add("Missing: " + string.Join(", ", plan.MissingItems));
        if (plan.Warnings.Count > 0)
        {
            lines.Add("Safety notes:");
            lines.AddRange(plan.Warnings.Select(warning => "  " + warning));
        }
        return string.Join(Environment.NewLine, lines);
    }

    private enum GameplayPlanArea { Recovery, Combat, Party }

    private async Task LoadGameplayAutomationAsync()
    {
        try
        {
            var catalog = await _gameplayStore.LoadAsync();
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _gameplayCatalog = catalog;
                GameplayAssistantStatus = "Gameplay assistant profiles loaded. Connect and refresh live state.";
            });
        }
        catch (Exception exception)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
                GameplayAssistantStatus = $"Gameplay assistant profile load failed: {exception.Message}");
        }
    }

    private async Task HandleGameplayAutomationStateAsync(string payload, long generation)
    {
        try
        {
            await _gameplayLoadTask;
            var state = ClientGameplayAutomationStateDecoder.Decode(payload);
            var expectedWorld = NormalizeWorldId(SelectedConnectionProfile?.WorldName);
            if (!NormalizeWorldId(state.WorldId).Equals(expectedWorld, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(
                    $"Gameplay world '{state.WorldId}' does not match selected world '{expectedWorld}'.");
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (generation != Volatile.Read(ref _clientConnectionGeneration) || !_terminalSession.IsConnected) return;
                ApplyGameplayAutomationState(state);
            });
        }
        catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
                GameplayAssistantStatus = $"Gameplay state rejected: {exception.Message}");
        }
    }

    private void ApplyGameplayAutomationState(ClientGameplayAutomationSnapshot state)
    {
        if (_gameplayAutomationState is { } previous &&
            (!previous.CharacterId.Equals(state.CharacterId, StringComparison.OrdinalIgnoreCase) ||
             !previous.WorldId.Equals(state.WorldId, StringComparison.OrdinalIgnoreCase)))
            StopGameplayAssistant("Character changed. Review settings before restarting the assistant.");
        _gameplayAutomationState = state;
        _gameplayProfile = ClientGameplayAutomationProfileStore.GetOrCreate(
            _gameplayCatalog, state.WorldId, state.CharacterId, state.CharacterName);
        Raise(nameof(GameplayProfile));
        Raise(nameof(BuffRules));
        GameplayNpcs.Clear();
        foreach (var npc in state.Npcs) GameplayNpcs.Add(npc);
        GameplayPartyMembers.Clear();
        foreach (var member in state.PartyMembers) GameplayPartyMembers.Add(member);
        if (SelectedBuffRule is null || !BuffRules.Contains(SelectedBuffRule))
            SelectedBuffRule = BuffRules.FirstOrDefault();
        GameplayAssistantStatus =
            $"{state.CharacterName} in {state.WorldId}: HP {state.HealthCurrent}/{state.HealthMaximum}, " +
            $"resource {state.ManaCurrent}/{state.ManaMaximum}, {state.Npcs.Count} NPC(s), " +
            $"{state.PartyMembers.Count} party member(s).";
        RefreshCharacterPresentation();
        RunGameplayDiagnostics();
    }

    private void ClearGameplayAutomationState(string status)
    {
        _gameplayAutomationState = null;
        _gameplayProfile = new ClientGameplayAutomationProfile();
        GameplayNpcs.Clear();
        GameplayPartyMembers.Clear();
        GameplayDiagnosticChecks.Clear();
        SelectedBuffRule = null;
        Raise(nameof(GameplayProfile));
        Raise(nameof(BuffRules));
        GameplayAssistantStatus = status;
        GameplayPlanText = "Refresh live state to begin.";
        _lastGameplayActionAt = null;
        _lastGameplayStateRequestAt = null;
        _lastGameplayCommand = string.Empty;
        _repeatedGameplayCommands = 0;
        RefreshCharacterPresentation();
    }

    private async Task RefreshCharacterAsync()
    {
        var generation = Volatile.Read(ref _clientConnectionGeneration);
        await RequestCharacterStateAsync(generation, CancellationToken.None);
        if (generation != Volatile.Read(ref _clientConnectionGeneration)) return;
        await RequestInventoryStateAsync(generation, CancellationToken.None);
        if (generation != Volatile.Read(ref _clientConnectionGeneration)) return;
        await RequestGameplayAutomationStateAsync(generation, CancellationToken.None);
    }

    private async Task RequestCharacterStateAsync(long generation, CancellationToken cancellationToken)
    {
        if (!SupportsPrivateRoomState() ||
            generation != Volatile.Read(ref _clientConnectionGeneration) ||
            !_terminalSession.IsConnected || !_gameplayState.GameplayReady)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
                CharacterStatus = "Character refresh requires a connected RealmGate character session.");
            return;
        }

        await _triggerGate.WaitAsync(cancellationToken);
        try
        {
            if (generation != Volatile.Read(ref _clientConnectionGeneration) || !_terminalSession.IsConnected)
                return;
            _terminalSession.NotePrivateCommand("clientdata character");
            await _terminalSession.SendLineAsync("clientdata character", false, cancellationToken);
            await Dispatcher.UIThread.InvokeAsync(() => CharacterStatus = "Refreshing character from the host...");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            await Dispatcher.UIThread.InvokeAsync(() => CharacterStatus = $"Character refresh failed: {exception.Message}");
        }
        finally { _triggerGate.Release(); }
    }

    private async Task HandleCharacterStateAsync(string payload, long generation)
    {
        try
        {
            var state = ClientCharacterStateDecoder.Decode(payload);
            var expectedWorld = NormalizeWorldId(SelectedConnectionProfile?.WorldName);
            if (!NormalizeWorldId(state.WorldId).Equals(expectedWorld, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(
                    $"Character world '{state.WorldId}' does not match selected world '{expectedWorld}'.");
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (generation != Volatile.Read(ref _clientConnectionGeneration) || !_terminalSession.IsConnected) return;
                if (_characterState is { } previous && !MatchesCharacter(previous,
                        state.WorldId, state.CharacterId, state.RoomId))
                    ClearWorkshopSearch("Character or room changed. Search items again.");
                _characterState = state;
                RefreshCharacterPresentation();
            });
        }
        catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
                ClearCharacterState($"Character state rejected: {exception.Message}"));
        }
    }

    private void ClearCharacterState(string status)
    {
        _characterState = null;
        ClearWorkshopSearch("Connect a character before searching items.");
        RefreshCharacterPresentation();
        CharacterStatus = status;
    }

    private void RefreshCharacterPresentation()
    {
        CharacterEquipment.Clear();
        CharacterSpells.Clear();
        CharacterEffects.Clear();
        if (_characterState is not { } state)
        {
            CharacterIdentityText = string.Empty;
            CharacterVitalsText = string.Empty;
            CharacterProgressText = string.Empty;
            CharacterAttributesText = string.Empty;
            CharacterSkillsText = string.Empty;
            return;
        }

        var gameplay = _gameplayAutomationState is { } live && MatchesCharacter(state,
            live.WorldId, live.CharacterId, live.RoomId) ? live : null;
        var inventory = _inventoryState is { } items && MatchesCharacter(state,
            items.WorldId, items.CharacterId, items.RoomId) ? items : null;
        CharacterStatus = $"{state.CharacterName} - {state.WorldId} - captured {state.CapturedAtUtc.ToLocalTime():g}";
        CharacterIdentityText =
            $"{state.RaceName} {state.ClassName}  |  {state.Sex}  |  Level {state.Level}\n" +
            $"Alignment: {(state.Alignment.Length == 0 ? "Unreported" : state.Alignment)}\n" +
            $"Room: {state.RoomId}";
        CharacterVitalsText = gameplay is null
            ? "Refresh live state for HP, resource, and condition details."
            : $"HP {gameplay.HealthCurrent:N0}/{gameplay.HealthMaximum:N0}  |  " +
              $"Resource {gameplay.ManaCurrent:N0}/{gameplay.ManaMaximum:N0}\n" +
              $"{(gameplay.InCombat ? "In combat" : gameplay.IsResting ? "Resting" : "Ready")}" +
              (gameplay.Conditions.Count == 0 ? string.Empty : $"  |  {string.Join(", ", gameplay.Conditions)}");
        CharacterProgressText = state.NextLevelExperience is long threshold
            ? $"Experience: {state.Experience:N0}\nNext level: {threshold:N0}\nRemaining: {Math.Max(0, threshold - state.Experience):N0}"
            : $"Experience: {state.Experience:N0}\nNext-level threshold unavailable from this world.";
        CharacterAttributesText = state.BaseStats.Count == 0 ? "No attributes reported."
            : string.Join("\n", state.BaseStats.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => $"{pair.Key}: {pair.Value:N0}"));
        CharacterSkillsText = state.Skills.Count == 0 ? "No skills reported."
            : string.Join("\n", state.Skills.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => $"{pair.Key}: {pair.Value:N0}"));
        if (inventory is not null)
            foreach (var item in inventory.Equipment) CharacterEquipment.Add(item);
        if (gameplay is not null)
        {
            foreach (var spell in gameplay.LearnedSpells.Where(spell =>
                         !spell.StartsWith("spell.", StringComparison.OrdinalIgnoreCase)))
                CharacterSpells.Add(spell);
            foreach (var effect in gameplay.ActiveEffects) CharacterEffects.Add(effect);
        }
    }

    private static bool MatchesCharacter(ClientCharacterState state, string worldId, string characterId, string roomId) =>
        state.WorldId.Equals(NormalizeWorldId(worldId), StringComparison.OrdinalIgnoreCase) &&
        state.CharacterId.Equals(characterId, StringComparison.OrdinalIgnoreCase) &&
        state.RoomId.Equals(roomId, StringComparison.OrdinalIgnoreCase);

    private async Task SearchWorkshopItemsAsync()
    {
        var state = _characterState;
        if (state is null || !_terminalSession.IsConnected || !_gameplayState.GameplayReady ||
            !SupportsPrivateRoomState())
        {
            WorkshopSearchStatus = "Connect and refresh a RealmGate character before searching items.";
            return;
        }
        var query = WorkshopSearchText.Trim();
        if (query.Length is < 2 or > 80)
        {
            WorkshopSearchStatus = "Enter 2 to 80 characters to search items.";
            return;
        }

        var generation = Volatile.Read(ref _clientConnectionGeneration);
        await _triggerGate.WaitAsync();
        try
        {
            if (generation != Volatile.Read(ref _clientConnectionGeneration) || !_terminalSession.IsConnected)
                return;
            var command = "clientdata itemsearch " + query;
            _terminalSession.NotePrivateCommand(command);
            await _terminalSession.SendLineAsync(command, false, CancellationToken.None);
            WorkshopSearchStatus = $"Searching {state.WorldId} items...";
        }
        catch (Exception exception)
        {
            ClearWorkshopSearch($"Item search failed: {exception.Message}");
        }
        finally { _triggerGate.Release(); }
    }

    private async Task HandleWorkshopItemSearchAsync(string payload, long generation)
    {
        try
        {
            var result = ClientWorkshopItemSearchDecoder.Decode(payload);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (generation != Volatile.Read(ref _clientConnectionGeneration) || !_terminalSession.IsConnected)
                    return;
                if (_characterState is not { } character ||
                    !MatchesCharacter(character, result.WorldId, result.CharacterId, result.RoomId) ||
                    !result.Query.Equals(WorkshopSearchText.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    ClearWorkshopSearch("Item search no longer matches this character or query. Search again.");
                    return;
                }
                _workshopItemSearch = result;
                WorkshopSearchResults.Clear();
                foreach (var item in result.Matches) WorkshopSearchResults.Add(item);
                SelectedWorkshopItem = WorkshopSearchResults.FirstOrDefault();
                WorkshopSearchStatus = $"{result.Matches.Count:N0} of {result.TotalMatches:N0} {result.WorldId} item(s) shown.";
                RefreshWorkshopComparison();
            });
        }
        catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException)
        {
            await Dispatcher.UIThread.InvokeAsync(() => ClearWorkshopSearch($"Item search rejected: {exception.Message}"));
        }
    }

    private void ClearWorkshopSearch(string status)
    {
        _workshopItemSearch = null;
        _workshopPlanInitialized = false;
        WorkshopSearchResults.Clear();
        PlannedWorkshopItems.Clear();
        SelectedWorkshopItem = null;
        SelectedPlannedWorkshopItem = null;
        WorkshopSearchStatus = status;
        WorkshopComparisonText = "Search items and add a candidate to compare loadouts.";
    }

    private void ResetPlannedWorkshopItems()
    {
        if (_workshopItemSearch is null)
        {
            WorkshopSearchStatus = "Search items before planning a loadout.";
            return;
        }
        PlannedWorkshopItems.Clear();
        foreach (var entry in _workshopItemSearch.Equipped)
            PlannedWorkshopItems.Add(entry.Item);
        _workshopPlanInitialized = true;
        SelectedPlannedWorkshopItem = PlannedWorkshopItems.FirstOrDefault();
        RefreshWorkshopComparison();
    }

    private void AddPlannedWorkshopItem()
    {
        if (_workshopItemSearch is null || SelectedWorkshopItem is not { } item) return;
        if (string.IsNullOrWhiteSpace(item.Slot) || item.Slot.Equals("NONE", StringComparison.OrdinalIgnoreCase))
        {
            WorkshopSearchStatus = $"{item.Name} has no decoded equipment slot; it cannot join a loadout.";
            return;
        }
        if (!_workshopPlanInitialized) ResetPlannedWorkshopItems();
        var replaced = PlannedWorkshopItems.FirstOrDefault(existing =>
            existing.Slot.Equals(item.Slot, StringComparison.OrdinalIgnoreCase));
        if (replaced is not null) PlannedWorkshopItems.Remove(replaced);
        PlannedWorkshopItems.Add(item);
        SelectedPlannedWorkshopItem = item;
        RefreshWorkshopComparison();
    }

    private void RemovePlannedWorkshopItem()
    {
        if (SelectedPlannedWorkshopItem is not { } item) return;
        PlannedWorkshopItems.Remove(item);
        SelectedPlannedWorkshopItem = PlannedWorkshopItems.FirstOrDefault();
        RefreshWorkshopComparison();
    }

    private void RefreshWorkshopComparison()
    {
        if (_workshopItemSearch is not { } search || !_workshopPlanInitialized)
        {
            WorkshopComparisonText = "Add an item to compare it with current equipment, or reset the plan to current.";
            return;
        }

        var current = ClientWorkshopLoadoutTotals.From(search.Equipped.Select(entry => entry.Item));
        var planned = ClientWorkshopLoadoutTotals.From(PlannedWorkshopItems);
        static string Row(string label, long before, long after) =>
            $"{label,-15} {before,9:N0} {after,9:N0} {after - before,9:+#;-#;0}";
        var warnings = new List<string>();
        if (_characterState is { } character)
        {
            foreach (var item in PlannedWorkshopItems)
            {
                if (item.MinimumLevel > character.Level)
                    warnings.Add($"{item.Name}: requires level {item.MinimumLevel}.");
                if (item.MinimumStrength > 0 &&
                    character.BaseStats.TryGetValue("Strength", out var strength) && strength < item.MinimumStrength)
                    warnings.Add($"{item.Name}: requires strength {item.MinimumStrength}.");
                if (item.ClassRestricted || item.RaceRestricted || item.TwoHanded)
                    warnings.Add($"{item.Name}: class/race or hand restrictions require Host verification.");
            }
        }
        WorkshopComparisonText =
            $"{search.WorldId} - {search.CapturedAtUtc.ToLocalTime():g}\n" +
            $"{"Metric",-15} {"Current",9} {"Planned",9} {"Change",9}\n" +
            Row("Weight", current.Weight, planned.Weight) + "\n" +
            Row("AC bonus", current.ArmorClassBonus, planned.ArmorClassBonus) + "\n" +
            Row("DR bonus", current.DamageResistanceBonus, planned.DamageResistanceBonus) + "\n" +
            Row("Accuracy", current.AccuracyBonus, planned.AccuracyBonus) + "\n" +
            Row("Min damage", current.MinimumDamage, planned.MinimumDamage) + "\n" +
            Row("Max damage", current.MaximumDamage, planned.MaximumDamage) +
            (warnings.Count == 0 ? string.Empty : "\n" + string.Join("\n", warnings.Distinct())) +
            "\nPlanning only. The Host decides equip legality and gameplay effects.";
    }

    private static string DescribeWorkshopItem(ClientWorkshopItem item)
    {
        var restrictions = new List<string>();
        if (item.MinimumLevel > 0) restrictions.Add($"Level {item.MinimumLevel}+");
        if (item.MaximumLevel > 0) restrictions.Add($"Maximum level {item.MaximumLevel}");
        if (item.MinimumStrength > 0) restrictions.Add($"Strength {item.MinimumStrength}+");
        if (item.ClassRestricted) restrictions.Add("Class restricted");
        if (item.RaceRestricted) restrictions.Add("Race restricted");
        if (item.TwoHanded) restrictions.Add("Two-handed");
        return $"{item.Name} ({item.Id})\n" +
               $"{item.ItemType}  |  Slot: {(item.Slot.Length == 0 ? "None" : item.Slot)}\n" +
               $"Damage {item.MinimumDamage}-{item.MaximumDamage}  |  AC {item.ArmorClassBonus:+#;-#;0}  |  DR {item.DamageResistanceBonus:+#;-#;0}\n" +
               $"Accuracy {item.AccuracyBonus:+#;-#;0}  |  Speed {item.SpeedBonus:+#;-#;0}\n" +
               $"Weight {item.Weight:N0}  |  Cost {item.Cost:N0}\n" +
               $"Restrictions: {(restrictions.Count == 0 ? "None listed" : string.Join(", ", restrictions))}" +
               (item.Abilities.Count == 0 ? string.Empty :
                   "\nAbilities: " + string.Join(", ", item.Abilities.Select(ability =>
                       $"{ability.Name} {ability.Value:+#;-#;0}")));
    }

    private async Task RefreshGameplayStateAsync() =>
        await RequestGameplayAutomationStateAsync(
            Volatile.Read(ref _clientConnectionGeneration), CancellationToken.None);

    private async Task RequestGameplayAutomationStateAsync(long generation, CancellationToken cancellationToken)
    {
        if (!SupportsPrivateRoomState() ||
            generation != Volatile.Read(ref _clientConnectionGeneration) ||
            !_terminalSession.IsConnected || !_gameplayState.GameplayReady)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
                GameplayAssistantStatus = "Live-state refresh requires a connected RealmGate character session.");
            return;
        }

        await _triggerGate.WaitAsync(cancellationToken);
        try
        {
            if (generation != Volatile.Read(ref _clientConnectionGeneration) || !_terminalSession.IsConnected)
                return;
            _lastGameplayStateRequestAt = DateTimeOffset.UtcNow;
            _terminalSession.NotePrivateCommand("clientdata automation");
            await _terminalSession.SendLineAsync("clientdata automation", false, cancellationToken);
            await Dispatcher.UIThread.InvokeAsync(() => GameplayAssistantStatus = "Refreshing live state from the host...");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
                StopGameplayAssistant($"Live-state refresh failed: {exception.Message}"));
        }
        finally { _triggerGate.Release(); }
    }

    private async Task SaveGameplayProfileAsync()
    {
        try
        {
            ClientGameplayAutomationProfileStore.Normalize(_gameplayCatalog);
            _gameplayProfile.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await _gameplayStore.SaveAsync(_gameplayCatalog);
            GameplayAssistantStatus = "Recovery, combat, and party settings saved for this world and character.";
        }
        catch (Exception exception)
        {
            GameplayAssistantStatus = $"Gameplay assistant profile save failed: {exception.Message}";
        }
    }

    private void AddBuffRule()
    {
        var rule = new ClientBuffRule { Name = "Buff", Priority = BuffRules.Count * 10 + 100 };
        BuffRules.Add(rule);
        SelectedBuffRule = rule;
    }

    private void DeleteBuffRule()
    {
        if (SelectedBuffRule is null) return;
        var index = BuffRules.IndexOf(SelectedBuffRule);
        BuffRules.Remove(SelectedBuffRule);
        SelectedBuffRule = BuffRules.ElementAtOrDefault(Math.Max(0, index - 1));
    }

    private void PreviewGameplayPlan(GameplayPlanArea area)
    {
        var plan = BuildGameplayPlan(area, DateTimeOffset.UtcNow);
        GameplayPlanText = FormatGameplayPlan(area, plan);
    }

    private async Task RunGameplayPlanAsync(GameplayPlanArea area)
    {
        var plan = BuildGameplayPlan(area, DateTimeOffset.UtcNow);
        GameplayPlanText = FormatGameplayPlan(area, plan);
        await ExecuteGameplayPlanAsync(plan, area.ToString());
    }

    private ClientGameplayActionPlan BuildGameplayPlan(GameplayPlanArea area, DateTimeOffset now)
    {
        if (_gameplayAutomationState is null)
            return ClientGameplayActionPlan.Refresh("Refresh live state before evaluating this plan.");
        return area switch
        {
            GameplayPlanArea.Recovery => ClientGameplayAutomationPlanner.PlanRecovery(
                _gameplayAutomationState, _gameplayProfile, now),
            GameplayPlanArea.Combat => ClientGameplayAutomationPlanner.PlanCombat(
                _gameplayAutomationState, _gameplayProfile, now),
            GameplayPlanArea.Party => ClientGameplayAutomationPlanner.PlanParty(
                _gameplayAutomationState, _gameplayProfile, now),
            _ => ClientGameplayActionPlan.None("No gameplay area was selected.")
        };
    }

    private static string FormatGameplayPlan(GameplayPlanArea area, ClientGameplayActionPlan plan)
    {
        var command = plan.HasAction ? Environment.NewLine + "Command: " + plan.Command : string.Empty;
        return $"{area}: {plan.Reason}{command}";
    }

    private async Task ExecuteGameplayPlanAsync(ClientGameplayActionPlan plan, string source)
    {
        if (plan.Kind == ClientGameplayActionKind.RefreshState)
        {
            await RefreshGameplayStateAsync();
            return;
        }
        if (!plan.HasAction)
        {
            GameplayAssistantStatus = plan.Reason;
            return;
        }
        if (_automationProfile.EmergencyStop)
        {
            StopGameplayAssistant("Emergency Stop is on. The gameplay command was not sent.");
            return;
        }
        if (!_terminalSession.IsConnected || !_gameplayState.GameplayReady)
        {
            GameplayAssistantStatus = "Gameplay commands require a connected, ready character session.";
            return;
        }
        var snapshot = _gameplayAutomationState;
        var generation = Volatile.Read(ref _clientConnectionGeneration);
        var wasAutomatic = IsGameplayAssistantRunning;
        if (snapshot is null || !ClientGameplayAutomationPlanner.IsSingleCommand(plan.Command)) return;
        await _triggerGate.WaitAsync();
        try
        {
            if (generation != Volatile.Read(ref _clientConnectionGeneration) ||
                !_terminalSession.IsConnected || !_gameplayState.GameplayReady ||
                _automationProfile.EmergencyStop || (wasAutomatic && !IsGameplayAssistantRunning) ||
                !ReferenceEquals(snapshot, _gameplayAutomationState) ||
                !NormalizeWorldId(snapshot.WorldId).Equals(NormalizeWorldId(SelectedConnectionProfile?.WorldName), StringComparison.OrdinalIgnoreCase) ||
                !ClientRouteGraph.NormalizeRoomKey(snapshot.RoomId).Equals(ClientRouteGraph.NormalizeRoomKey(CurrentRoomKey), StringComparison.OrdinalIgnoreCase) ||
                DateTimeOffset.UtcNow - snapshot.CapturedAtUtc > TimeSpan.FromSeconds(_gameplayProfile.StateMaximumAgeSeconds))
            {
                GameplayAssistantStatus = "Session or room state changed. Refresh before sending another action.";
                return;
            }
            var delay = Math.Max(_automationProfile.MinimumCommandDelayMs, _gameplayProfile.MinimumActionDelayMilliseconds);
            if ((_lastAutomationCommandAt is { } last && DateTimeOffset.UtcNow - last < TimeSpan.FromMilliseconds(delay)) ||
                (_lastGameplayActionAt is { } sent && snapshot.CapturedAtUtc <= sent))
                return;
            if (wasAutomatic && _lastGameplayCommand.Equals(plan.Command, StringComparison.Ordinal) && _repeatedGameplayCommands >= 3)
            {
                StopGameplayAssistant("The same action was requested three times. Review the terminal before restarting.");
                return;
            }
            // Planned commands bypass user aliases; the server receives exactly the reviewed action.
            await _terminalSession.SendLineAsync(plan.Command, sensitive: false);
            _lastGameplayActionAt = DateTimeOffset.UtcNow;
            _lastAutomationCommandAt = _lastGameplayActionAt;
            _repeatedGameplayCommands = _lastGameplayCommand == plan.Command ? _repeatedGameplayCommands + 1 : 1;
            _lastGameplayCommand = plan.Command;
            GameplayAssistantStatus = $"{source}: sent '{plan.Command}'.";
        }
        catch (Exception exception)
        {
            StopGameplayAssistant($"Gameplay assistant stopped: {exception.Message}");
        }
        finally { _triggerGate.Release(); }
        await RequestGameplayAutomationStateAsync(generation, CancellationToken.None);
    }

    private void StartGameplayAssistant()
    {
        if (_automationProfile.EmergencyStop)
        {
            GameplayAssistantStatus = "Clear Emergency Stop before starting the gameplay assistant.";
            return;
        }
        if (!_terminalSession.IsConnected || !_gameplayState.GameplayReady || _gameplayAutomationState is null)
        {
            GameplayAssistantStatus = "Connect, enter gameplay, and refresh live state before starting.";
            return;
        }
        if (!_gameplayProfile.Recovery.Enabled && !_gameplayProfile.Combat.Enabled && !_gameplayProfile.Party.Enabled)
        {
            GameplayAssistantStatus = "Enable at least one recovery, combat, or party section first.";
            return;
        }
        IsGameplayAssistantRunning = true;
        _repeatedGameplayCommands = 0;
        _lastGameplayCommand = string.Empty;
        GameplayAssistantStatus = "Gameplay assistant running with server-authoritative state.";
    }

    private void StopGameplayAssistant(string status)
    {
        IsGameplayAssistantRunning = false;
        GameplayAssistantStatus = status;
    }

    private async Task RunGameplayAssistantClockAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                Task tick = Task.CompletedTask;
                await Dispatcher.UIThread.InvokeAsync(() => { tick = TickGameplayAssistantAsync(cancellationToken); });
                await tick;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
                StopGameplayAssistant($"Gameplay assistant stopped: {exception.Message}"));
        }
    }

    private async Task TickGameplayAssistantAsync(CancellationToken cancellationToken)
    {
                if (!IsGameplayAssistantRunning) return;
                if (_automationProfile.EmergencyStop)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                        StopGameplayAssistant("Emergency Stop activated. Gameplay assistant stopped."));
                    return;
                }
                if (!_terminalSession.IsConnected || !_gameplayState.GameplayReady)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                        StopGameplayAssistant("Connection or gameplay readiness was lost."));
                    return;
                }

                var now = DateTimeOffset.UtcNow;
                if (_lastGameplayActionAt is { } last &&
                    now - last < TimeSpan.FromMilliseconds(_gameplayProfile.MinimumActionDelayMilliseconds))
                    return;
                if (_gameplayAutomationState is null || now - _gameplayAutomationState.CapturedAtUtc > TimeSpan.FromSeconds(2))
                {
                    if (_lastGameplayStateRequestAt is null || now - _lastGameplayStateRequestAt > TimeSpan.FromSeconds(2))
                        await RequestGameplayAutomationStateAsync(
                            Volatile.Read(ref _clientConnectionGeneration), cancellationToken);
                    return;
                }

                var plan = ClientGameplayAutomationPlanner.PlanRecovery(_gameplayAutomationState, _gameplayProfile, now);
                var source = "Recovery";
                if (!plan.HasAction && plan.Kind != ClientGameplayActionKind.RefreshState)
                {
                    plan = ClientGameplayAutomationPlanner.PlanParty(_gameplayAutomationState, _gameplayProfile, now);
                    source = "Party";
                }
                if (!plan.HasAction && plan.Kind != ClientGameplayActionKind.RefreshState)
                {
                    plan = ClientGameplayAutomationPlanner.PlanCombat(_gameplayAutomationState, _gameplayProfile, now);
                    source = "Combat";
                }
                if (plan.Kind == ClientGameplayActionKind.RefreshState)
                {
                    await RequestGameplayAutomationStateAsync(
                        Volatile.Read(ref _clientConnectionGeneration), cancellationToken);
                    return;
                }
                if (plan.HasAction) await ExecuteGameplayPlanAsync(plan, source);
    }

    private async Task SendCommunicationAsync(string command, bool includeTarget)
    {
        if (!_terminalSession.IsConnected || !_gameplayState.GameplayReady)
        {
            GameplayAssistantStatus = "Enter gameplay before sending a message.";
            return;
        }
        var message = (CommunicationText ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
        var target = (CommunicationTarget ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (message.Length == 0)
        {
            GameplayAssistantStatus = "Enter a message first.";
            return;
        }
        if (message.Length > 400) message = message[..400];
        if (includeTarget && target.Length == 0)
        {
            GameplayAssistantStatus = "Select or enter a message target first.";
            return;
        }
        var line = includeTarget ? $"{command}{target} {message}" :
            command == "\"" ? command + message : $"{command} {message}";
        if (line.Any(char.IsControl)) return;
        await _triggerGate.WaitAsync();
        try
        {
            if (!_terminalSession.IsConnected || !_gameplayState.GameplayReady) return;
            await _terminalSession.SendLineAsync(line, sensitive: false);
            _lastAutomationCommandAt = DateTimeOffset.UtcNow;
            CommunicationText = string.Empty;
            GameplayAssistantStatus = $"Sent {command} message.";
        }
        catch (Exception exception) { GameplayAssistantStatus = $"Message was not sent: {exception.Message}"; }
        finally { _triggerGate.Release(); }
    }

    private void RunGameplayDiagnostics()
    {
        GameplayDiagnosticChecks.Clear();
        foreach (var check in ClientGameplayDiagnostics.Evaluate(
                     _gameplayAutomationState,
                     SelectedConnectionProfile?.WorldName ?? "Classic",
                     _terminalSession.IsConnected,
                     _gameplayState.GameplayReady,
                     _automationProfile.EmergencyStop,
                     IsGameplayAssistantRunning,
                     DateTimeOffset.UtcNow))
            GameplayDiagnosticChecks.Add(check);
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

    private async Task LoadSavedDestinationsAsync()
    {
        try
        {
            var catalog = await _savedDestinationStore.LoadAsync();
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _savedDestinationCatalog = catalog;
                RefreshSavedDestinations();
            });
        }
        catch (Exception exception)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
                NavigationStatus = $"Saved destinations unavailable: {exception.Message}");
        }
    }

    private void RefreshSavedDestinations()
    {
        var worldId = NormalizeWorldId(SelectedConnectionProfile?.WorldName);
        var selectedId = SelectedSavedDestination?.Id;
        SavedDestinations.Clear();
        foreach (var destination in _savedDestinationCatalog.Destinations
                     .Where(destination => NormalizeWorldId(destination.WorldId)
                         .Equals(worldId, StringComparison.OrdinalIgnoreCase))
                     .OrderBy(destination => destination.Kind)
                     .ThenBy(destination => destination.Name, StringComparer.OrdinalIgnoreCase))
        {
            SavedDestinations.Add(destination);
        }
        SelectedSavedDestination = selectedId is null
            ? SavedDestinations.FirstOrDefault()
            : SavedDestinations.FirstOrDefault(destination => destination.Id.Equals(
                  selectedId,
                  StringComparison.OrdinalIgnoreCase))
              ?? SavedDestinations.FirstOrDefault();
    }

    private async Task SaveCurrentDestinationAsync(ClientSavedDestinationKind kind)
    {
        if (_routeGraph is null || CurrentRoomKey.Length == 0)
        {
            NavigationStatus = "Enter gameplay and obtain an exact map-qualified room before saving a destination.";
            return;
        }
        var room = _routeGraph.FindRoom(CurrentRoomKey);
        if (room is null)
        {
            NavigationStatus = "The current room is not present in the selected world's navigation graph.";
            return;
        }

        var worldId = NormalizeWorldId(SelectedConnectionProfile?.WorldName);
        var existing = _savedDestinationCatalog.Destinations.FirstOrDefault(destination =>
            NormalizeWorldId(destination.WorldId).Equals(worldId, StringComparison.OrdinalIgnoreCase) &&
            destination.RoomKey.Equals(room.Key, StringComparison.OrdinalIgnoreCase) &&
            destination.Kind == kind);
        var name = string.IsNullOrWhiteSpace(SavedDestinationName)
            ? room.Name
            : SavedDestinationName.Trim();
        var created = existing is null;
        var destination = existing ?? new ClientSavedDestination
        {
            WorldId = worldId,
            RoomKey = room.Key,
            Kind = kind
        };
        destination.Name = name;
        destination.SavedAtUtc = DateTimeOffset.UtcNow;
        if (created) _savedDestinationCatalog.Destinations.Add(destination);

        try
        {
            await _savedDestinationStore.SaveAsync(_savedDestinationCatalog);
            RefreshSavedDestinations();
            SelectedSavedDestination = destination;
            SavedDestinationName = string.Empty;
            NavigationStatus = $"Saved {room.Name} as a {kind.ToString().ToLowerInvariant()} for {worldId}.";
        }
        catch (Exception exception)
        {
            if (created) _savedDestinationCatalog.Destinations.Remove(destination);
            NavigationStatus = $"Destination was not saved: {exception.Message}";
        }
    }

    private async Task NavigateSavedDestinationAsync()
    {
        var destination = SelectedSavedDestination;
        if (destination is null)
        {
            NavigationStatus = "Select a saved destination first.";
            return;
        }
        var worldId = NormalizeWorldId(SelectedConnectionProfile?.WorldName);
        if (!NormalizeWorldId(destination.WorldId).Equals(worldId, StringComparison.OrdinalIgnoreCase))
        {
            NavigationStatus = $"Saved destination world '{destination.WorldId}' does not match selected world '{worldId}'.";
            return;
        }
        if (_routeGraph?.FindRoom(destination.RoomKey) is not { } room)
        {
            NavigationStatus = "The saved destination is not present in the selected world's navigation graph.";
            return;
        }

        NavigationSearchText = room.Key;
        SelectedNavigationRoom = VisibleNavigationRooms.FirstOrDefault(candidate =>
            candidate.Key.Equals(room.Key, StringComparison.OrdinalIgnoreCase));
        await StartNavigationAsync();
    }

    private async Task DeleteSavedDestinationAsync()
    {
        var destination = SelectedSavedDestination;
        if (destination is null) return;
        _savedDestinationCatalog.Destinations.Remove(destination);
        try
        {
            await _savedDestinationStore.SaveAsync(_savedDestinationCatalog);
            RefreshSavedDestinations();
            NavigationStatus = $"Deleted saved destination '{destination.Name}'.";
        }
        catch (Exception exception)
        {
            _savedDestinationCatalog.Destinations.Add(destination);
            RefreshSavedDestinations();
            SelectedSavedDestination = destination;
            NavigationStatus = $"Destination was not deleted: {exception.Message}";
        }
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
        try { await _gameplayAssistantClockTask; }
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
