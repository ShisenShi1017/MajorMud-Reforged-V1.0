using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Input;
using Avalonia.Threading;
using RealmGate.Client.Automation;
using RealmGate.Client.Diagnostics;
using RealmGate.Client.Gameplay;
using RealmGate.Client.Inventory;
using RealmGate.Client.Navigation;
using RealmGate.Client.Profiles;
using RealmGate.Client.Settings;
using RealmGate.Client.Terminal;
using RealmGate.Client.Transport;
using RealmGate.App.Services;
using RealmGate.Desktop.Services;
using RealmGate.Server;

namespace RealmGate.Desktop.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged, IAsyncDisposable
{
    private readonly EmbeddedServerHostController _host = new();
    private readonly ClassicTerminalSession _terminalSession = new();
    private readonly ClientConnectionProfileCatalogStore _profileStore =
        ClientConnectionProfileCatalogStore.CreateForCurrentUser();
    private readonly ClientApplicationSettingsStore _applicationSettingsStore =
        ClientApplicationSettingsStore.CreateForCurrentUser();
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
    private readonly ClassicItemScreenTracker _externalItemScreenTracker = new();
    private readonly ClassicExternalSessionTracker _externalSessionTracker = new();
    private readonly ClientGameplayAutomationRuntime _gameplayAutomationRuntime = new();
    private readonly CustomWorldEditorService _creatorStudio = new();
    private readonly ClassicAdministrationEditorService _creatorAdministration = new();
    private readonly ClassicLibrariumBrowserService _librariumBrowser;
    private readonly NightmareMdbBridgeService _creatorMdbBridge;
    private readonly ICreatorStudioFilePicker? _creatorFilePicker;
    private readonly SemaphoreSlim _triggerGate = new(1, 1);
    private readonly CancellationTokenSource _automationClockCancellation = new();
    private readonly StringBuilder _triggerReceiveBuffer = new();
    private readonly object _routeSync = new();
    private readonly Action<string> _hostLogHandler;
    private ClientConnectionProfileCatalog _profileCatalog = new();
    private ClientApplicationSettings _applicationSettings = new();
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
    private ClientExternalBbsItemCatalog? _externalWorkshopCatalog;
    private ClassicItemScreenObservation? _externalEquipmentObservation;
    private DateTimeOffset? _externalEquipmentCapturedAtUtc;
    private ClassicExternalSessionSnapshot? _externalSessionSnapshot;
    private bool _workshopUsesExternalBbsCatalog;
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
    private string? _selectedCharacterSpell;
    private ClientCombatActionRule? _selectedCombatActionRule;
    private ClientEquipmentSet? _defaultCombatEquipmentSet;
    private ClientEquipmentSet? _multiTargetCombatEquipmentSet;
    private ClientEquipmentSet? _defensiveCombatEquipmentSet;
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
    private string _characterProgressDetailsText = string.Empty;
    private string _characterClassProgressText = string.Empty;
    private double _characterProgressPercent;
    private string _characterAttributesText = string.Empty;
    private string _characterSkillsText = string.Empty;
    private string _characterDerivedStatsText = string.Empty;
    private string _characterDefensesText = string.Empty;
    private string _workshopSearchText = string.Empty;
    private string _workshopSearchStatus = "Search the selected world's items after connecting a character.";
    private string _workshopItemDetails = string.Empty;
    private string _workshopComparisonText = "Search items and add a candidate to compare loadouts.";
    private string _externalEquipmentStatus = "External equipment has not been captured.";
    private bool _workshopPlanInitialized;
    private string _gameplayPlanText = "Refresh live state to begin.";
    private string _spellTarget = string.Empty;
    private string _spellActionStatus = "Select a learned spell to prepare a cast command.";
    private string _sessionStatisticsText = "Run diagnostics to view session statistics.";
    private string _supportBundleStatus = "Support snapshots exclude passwords and terminal conversation text.";
    private CustomWorldSimpleFamilyOption? _selectedCreatorFamily;
    private CustomWorldObjectSummary? _selectedCreatorObject;
    private string _creatorStudioStatus = "Select Load workspace to inspect Custom1 without changing Classic.";
    private string _creatorWorkspaceText = string.Empty;
    private string _creatorSearchText = string.Empty;
    private string _creatorObjectDetails = "Select an object to view its gameplay fields.";
    private string _creatorMdbPath = string.Empty;
    private string _creatorEvidenceFolder = string.Empty;
    private string _creatorSourceStatus = "Choose an MDB and its matching WCC/WGS source folder to begin read-only verification.";
    private string _creatorSourceSummary = string.Empty;
    private bool _creatorImportAcknowledged;
    private NightmareMdbAnalysis? _creatorMdbAnalysis;
    private NightmareWccEvidenceAnalysis? _creatorEvidenceAnalysis;
    private NightmareMdbWccPairValidation? _creatorPairValidation;
    private string _creatorEditTitle = "Select a record to edit or start a new record.";
    private string _creatorEditObjectId = string.Empty;
    private string _creatorEditStatus = "No Custom1 record is open.";
    private string _creatorValidationStatus = "Validation has not run for the current overrides.";
    private string _creatorPublishStatus = "Build the Custom1 Test Realm, then preview its live-world diff.";
    private string _creatorPackageStatus = "Export Custom1 changes or preview a portable package before applying it.";
    private string _creatorPendingPackagePath = string.Empty;
    private string _creatorRoomLinksStatus = "Select a room in Browse, then load its map-qualified connections.";
    private string _creatorShopInventoryStatus = "Select a shop in Browse, then load its Custom1 inventory.";
    private string _creatorNpcEditorStatus = "Select an NPC in Browse, then load its attacks and abilities.";
    private string _creatorQuestScriptStatus = "Select an executable quest or script in Browse.";
    private string _creatorSpecializedRoomId = string.Empty;
    private string _creatorSpecializedShopId = string.Empty;
    private string _creatorSpecializedNpcId = string.Empty;
    private string _creatorSpecializedQuestId = string.Empty;
    private string _creatorSpecializedScriptId = string.Empty;
    private string _creatorNpcSimulationStatus = "Select an NPC in Browse to run the shared runtime combat simulator.";
    private string _creatorWeaponSimulationStatus = "Select an item in Browse to run the shared runtime weapon calculator.";
    private int _creatorNpcTargetDefense;
    private int _creatorNpcTargetArmorClass = 50;
    private int _creatorNpcTargetDamageResistance;
    private int _creatorNpcTargetDodge;
    private int _creatorNpcSimulationRounds = 1000;
    private int _creatorWeaponBaseAttack = 100;
    private int _creatorWeaponTargetDefense;
    private int _creatorWeaponTargetArmorClass = 50;
    private int _creatorWeaponTargetDamageResistance;
    private int _creatorWeaponTargetDodge;
    private int _creatorWeaponCriticalChance;
    private int _creatorWeaponSimulationSwings = 1000;
    private int _creatorSelectedMapNumber = 1;
    private string _creatorMapLayoutStatus = "Load a map to inspect and edit its saved room coordinates.";
    private string _creatorWorldToolSearchText = string.Empty;
    private string _creatorWorldToolStatus = "Search the effective Custom1 world or inspect references for the selected Browse record.";
    private string _creatorMaintenanceConfirmation = string.Empty;
    private string _creatorMaintenanceStatus = "Maintenance actions target Custom1 only. Type CUSTOM1 immediately before applying a change.";
    private string _creatorBulkFamily = "items";
    private string _creatorBulkTargetIds = string.Empty;
    private string _creatorBulkFieldPath = string.Empty;
    private string _creatorBulkValue = string.Empty;
    private string _creatorBulkValueKind = "Text";
    private string _creatorBulkSourceId = string.Empty;
    private string _creatorMaintenanceSearch = string.Empty;
    private CustomWorldObjectSummary? _selectedCreatorCoinRoom;
    private CustomWorldObjectSummary? _selectedCreatorShopReset;
    private CustomWorldObjectSummary? _selectedCreatorCleanupRoom;
    private string _creatorCoinRunic = "0";
    private string _creatorCoinPlatinum = "0";
    private string _creatorCoinGold = "0";
    private string _creatorCoinSilver = "0";
    private string _creatorCoinCopper = "0";
    private string _creatorTextblockFind = string.Empty;
    private string _creatorTextblockReplacement = string.Empty;
    private bool _creatorTextblockSuffixOnly = true;
    private CustomWorldTextblockStripPreviewRow? _selectedCreatorTextblockPreview;
    private string _creatorWorldName = "Custom World 1";
    private string _creatorWorldDescription = string.Empty;
    private string _creatorWorldLoginBanner = string.Empty;
    private string _creatorWorldMotd = string.Empty;
    private bool _creatorWorldRuntimeEnabled = true;
    private bool _creatorWorldTransfersAllowed;
    private string _creatorWorldSettingsStatus = "Load the Creator workspace to inspect Custom1 world settings.";
    private bool _creatorClassicUnlimitedItems = true;
    private bool _creatorClassicEveryoneCanGoto;
    private string _creatorAdministrationConfirmation = string.Empty;
    private string _creatorAdministrationStatus = "Load realm characters or Classic administration settings. Changes require explicit confirmation and a stopped Host.";
    private CreatorManagedCharacterRow? _selectedCreatorManagedCharacter;
    private ClassicLibrariumCategory? _selectedLibrariumCategory;
    private ClassicLibrariumRecordSummary? _selectedLibrariumRecord;
    private string _librariumSearchText = string.Empty;
    private string _librariumStatus = "Select a category and load the generated Classic Librarium projection.";
    private string _librariumSelectedTitle = "Select a record";
    private string _librariumSelectedSubheading = "Gameplay details appear here. Evidence metadata and raw JSON are intentionally hidden.";
    private long _librariumLoadGeneration;
    private string _creatorEditFamily = string.Empty;
    private bool _creatorEditIsNew;
    private bool _creatorEditorActive;
    private bool _creatorPackageApplyAcknowledged;
    private bool _creatorPublishAcknowledged;
    private bool _creatorTestRealmRunning;
    private CustomWorldImportPreview? _creatorPendingPackagePreview;
    private CustomWorldPublishPreview? _creatorPublishPreview;
    private CreatorShopInventoryRow? _selectedCreatorShopInventoryRow;
    private CreatorNpcAttackRow? _selectedCreatorNpcAttackRow;
    private CreatorNpcAbilityRow? _selectedCreatorNpcAbilityRow;
    private CreatorQuestStageRow? _selectedCreatorQuestStageRow;
    private CreatorScriptOperationRow? _selectedCreatorScriptOperationRow;
    private CustomWorldGlobalSearchHit? _selectedCreatorGlobalSearchHit;
    private CustomWorldReferenceHit? _selectedCreatorReferenceHit;
    private CustomWorldKeyDoorRow? _selectedCreatorKeyDoor;
    private CustomWorldMonsterIndexRow? _selectedCreatorMonsterIndex;
    private CustomWorldControlRoomRow? _selectedCreatorControlRoom;
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
    private string _defaultHostWorld = "Classic";
    private string _applicationSettingsStatus = "Application defaults have not loaded yet.";
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

    public MainWindowViewModel(ICreatorStudioFilePicker? creatorFilePicker = null)
    {
        _creatorFilePicker = creatorFilePicker;
        _creatorMdbBridge = new NightmareMdbBridgeService(_creatorStudio);
        _librariumBrowser = new ClassicLibrariumBrowserService(_creatorStudio.ProjectRoot);
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
            ObserveExternalItemText(text, generation);
            ObserveExternalSessionText(text, generation);
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
        SaveApplicationSettingsCommand = new AsyncCommand(SaveApplicationSettingsAsync);
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
        CaptureExternalEquipmentCommand = new AsyncCommand(CaptureExternalEquipmentAsync);
        SearchWorkshopItemsCommand = new AsyncCommand(SearchWorkshopItemsAsync);
        AddPlannedWorkshopItemCommand = new RelayCommand(AddPlannedWorkshopItem);
        RemovePlannedWorkshopItemCommand = new RelayCommand(RemovePlannedWorkshopItem);
        ResetPlannedWorkshopItemsCommand = new RelayCommand(ResetPlannedWorkshopItems);
        SaveGameplayProfileCommand = new AsyncCommand(SaveGameplayProfileAsync);
        AddBuffRuleCommand = new RelayCommand(AddBuffRule);
        DeleteBuffRuleCommand = new RelayCommand(DeleteBuffRule);
        AddCombatActionRuleCommand = new RelayCommand(AddCombatActionRule);
        DeleteCombatActionRuleCommand = new RelayCommand(DeleteCombatActionRule);
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
        CastSelectedSpellCommand = new AsyncCommand(CastSelectedSpellAsync);
        ExportSupportSnapshotCommand = new AsyncCommand(ExportSupportSnapshotAsync);
        LoadCreatorStudioCommand = new AsyncCommand(LoadCreatorStudioAsync);
        SearchCreatorObjectsCommand = new AsyncCommand(SearchCreatorObjectsAsync);
        SelectCreatorMdbCommand = new AsyncCommand(SelectCreatorMdbAsync);
        SelectCreatorEvidenceFolderCommand = new AsyncCommand(SelectCreatorEvidenceFolderAsync);
        ValidateCreatorSourcesCommand = new AsyncCommand(ValidateCreatorSourcesAsync);
        ImportCreatorMdbCommand = new AsyncCommand(ImportCreatorMdbAsync);
        EditCreatorObjectCommand = new AsyncCommand(EditCreatorObjectAsync);
        NewCreatorObjectCommand = new AsyncCommand(NewCreatorObjectAsync);
        SaveCreatorObjectCommand = new AsyncCommand(SaveCreatorObjectAsync);
        ValidateCreatorWorldCommand = new AsyncCommand(ValidateCreatorWorldAsync);
        BuildCreatorWorldCommand = new AsyncCommand(BuildCreatorWorldAsync);
        StartCreatorTestRealmCommand = new AsyncCommand(StartCreatorTestRealmAsync);
        StopCreatorTestRealmCommand = new AsyncCommand(StopCreatorTestRealmAsync);
        PreviewCreatorPublishCommand = new AsyncCommand(PreviewCreatorPublishAsync);
        PublishCreatorTestRealmCommand = new AsyncCommand(PublishCreatorTestRealmAsync);
        RollbackCreatorPublishCommand = new AsyncCommand(RollbackCreatorPublishAsync);
        ExportAllCreatorChangesCommand = new AsyncCommand(() => ExportCreatorChangesAsync("all"));
        ExportCreatorFamilyCommand = new AsyncCommand(() => ExportCreatorChangesAsync("family"));
        ExportCreatorRecordCommand = new AsyncCommand(() => ExportCreatorChangesAsync("record"));
        PreviewCreatorPackageCommand = new AsyncCommand(PreviewCreatorPackageAsync);
        ApplyCreatorPackageCommand = new AsyncCommand(ApplyCreatorPackageAsync);
        LoadCreatorRoomLinksCommand = new AsyncCommand(LoadCreatorRoomLinksAsync);
        SaveCreatorRoomLinksCommand = new AsyncCommand(SaveCreatorRoomLinksAsync);
        LoadCreatorShopInventoryCommand = new AsyncCommand(LoadCreatorShopInventoryAsync);
        SaveCreatorShopInventoryCommand = new AsyncCommand(SaveCreatorShopInventoryAsync);
        AddCreatorShopInventoryRowCommand = new RelayCommand(() => CreatorShopInventory.Add(new CreatorShopInventoryRow()));
        RemoveCreatorShopInventoryRowCommand = new RelayCommand(RemoveCreatorShopInventoryRow);
        LoadCreatorNpcEditorCommand = new AsyncCommand(LoadCreatorNpcEditorAsync);
        SaveCreatorNpcAttacksCommand = new AsyncCommand(SaveCreatorNpcAttacksAsync);
        AddCreatorNpcAttackCommand = new RelayCommand(AddCreatorNpcAttack);
        RemoveCreatorNpcAttackCommand = new RelayCommand(RemoveCreatorNpcAttack);
        SaveCreatorNpcAbilitiesCommand = new AsyncCommand(SaveCreatorNpcAbilitiesAsync);
        AddCreatorNpcAbilityCommand = new RelayCommand(() => CreatorNpcAbilities.Add(new CreatorNpcAbilityRow()));
        RemoveCreatorNpcAbilityCommand = new RelayCommand(RemoveCreatorNpcAbility);
        LoadCreatorQuestStagesCommand = new AsyncCommand(LoadCreatorQuestStagesAsync);
        LoadCreatorSelectedScriptCommand = new AsyncCommand(LoadCreatorSelectedScriptAsync);
        LoadCreatorQuestStageScriptCommand = new AsyncCommand(LoadCreatorQuestStageScriptAsync);
        AddCreatorScriptOperationCommand = new RelayCommand(AddCreatorScriptOperation);
        RemoveCreatorScriptOperationCommand = new RelayCommand(RemoveCreatorScriptOperation);
        SaveCreatorScriptOperationsCommand = new AsyncCommand(SaveCreatorScriptOperationsAsync);
        RunCreatorNpcSimulationCommand = new AsyncCommand(RunCreatorNpcSimulationAsync);
        RunCreatorWeaponSimulationCommand = new AsyncCommand(RunCreatorWeaponSimulationAsync);
        LoadCreatorMapLayoutCommand = new AsyncCommand(LoadCreatorMapLayoutAsync);
        PreviewCreatorMapAutoArrangeCommand = new AsyncCommand(PreviewCreatorMapAutoArrangeAsync);
        SaveCreatorMapLayoutCommand = new AsyncCommand(SaveCreatorMapLayoutAsync);
        SearchCreatorWorldCommand = new AsyncCommand(SearchCreatorWorldAsync);
        FindCreatorReferencesCommand = new AsyncCommand(FindCreatorReferencesAsync);
        LoadCreatorKeyDoorsCommand = new AsyncCommand(LoadCreatorKeyDoorsAsync);
        LoadCreatorMonsterIndexCommand = new AsyncCommand(LoadCreatorMonsterIndexAsync);
        LoadCreatorControlRoomsCommand = new AsyncCommand(LoadCreatorControlRoomsAsync);
        OpenCreatorSearchResultCommand = new AsyncCommand(() => OpenCreatorToolRecordAsync(
            SelectedCreatorGlobalSearchHit?.Family,
            SelectedCreatorGlobalSearchHit?.Id));
        OpenCreatorReferenceResultCommand = new AsyncCommand(() => OpenCreatorToolRecordAsync(
            SelectedCreatorReferenceHit?.Family,
            SelectedCreatorReferenceHit?.Id));
        OpenCreatorKeyDoorExitCommand = new AsyncCommand(() => OpenCreatorToolRecordAsync(
            "exits",
            SelectedCreatorKeyDoor?.ExitId));
        OpenCreatorKeyDoorItemCommand = new AsyncCommand(() => OpenCreatorToolRecordAsync(
            "items",
            SelectedCreatorKeyDoor?.KeyItemId));
        OpenCreatorMonsterCommand = new AsyncCommand(() => OpenCreatorToolRecordAsync(
            "npcs",
            SelectedCreatorMonsterIndex?.NpcId));
        OpenCreatorControlRoomCommand = new AsyncCommand(() => OpenCreatorToolRecordAsync(
            "rooms",
            SelectedCreatorControlRoom?.RoomId));
        LoadCreatorParityAuditCommand = new RelayCommand(LoadCreatorParityAudit);
        ApplyCreatorBulkSetCommand = new AsyncCommand(ApplyCreatorBulkSetAsync);
        ApplyCreatorBulkCopyCommand = new AsyncCommand(ApplyCreatorBulkCopyAsync);
        SearchCreatorCoinRoomsCommand = new AsyncCommand(SearchCreatorCoinRoomsAsync);
        LoadCreatorRoomCurrencyCommand = new AsyncCommand(LoadCreatorRoomCurrencyAsync);
        SaveCreatorRoomCurrencyCommand = new AsyncCommand(SaveCreatorRoomCurrencyAsync);
        SearchCreatorShopResetsCommand = new AsyncCommand(SearchCreatorShopResetsAsync);
        ResetCreatorShopStockCommand = new AsyncCommand(ResetCreatorShopStockAsync);
        SearchCreatorCleanupRoomsCommand = new AsyncCommand(SearchCreatorCleanupRoomsAsync);
        CombineCreatorRoomItemsCommand = new AsyncCommand(CombineCreatorRoomItemsAsync);
        PreviewCreatorTextblockStripCommand = new AsyncCommand(PreviewCreatorTextblockStripAsync);
        ApplyCreatorTextblockStripCommand = new AsyncCommand(ApplyCreatorTextblockStripAsync);
        SaveCreatorWorldSettingsCommand = new AsyncCommand(SaveCreatorWorldSettingsAsync);
        LoadCreatorCharactersCommand = new AsyncCommand(LoadCreatorCharactersAsync);
        DeleteCreatorCharacterCommand = new AsyncCommand(DeleteCreatorCharacterAsync);
        LoadCreatorClassicAdministrationCommand = new AsyncCommand(LoadCreatorClassicAdministrationAsync);
        SaveCreatorClassicAdministrationCommand = new AsyncCommand(SaveCreatorClassicAdministrationAsync);
        LoadLibrariumCommand = new AsyncCommand(LoadLibrariumAsync);
        foreach (var category in _librariumBrowser.GetCategories()) LibrariumCategories.Add(category);
        _selectedLibrariumCategory = LibrariumCategories.FirstOrDefault();
        foreach (var opcode in _creatorStudio.GetEditableNarrativeOpcodes()) CreatorScriptOpcodes.Add(opcode);
        RefreshLanEndpoints();
        _ = LoadApplicationSettingsAndProfilesAsync();
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
    public ICommand SaveApplicationSettingsCommand { get; }
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
    public ICommand CaptureExternalEquipmentCommand { get; }
    public ICommand SearchWorkshopItemsCommand { get; }
    public ICommand AddPlannedWorkshopItemCommand { get; }
    public ICommand RemovePlannedWorkshopItemCommand { get; }
    public ICommand ResetPlannedWorkshopItemsCommand { get; }
    public ICommand SaveGameplayProfileCommand { get; }
    public ICommand AddBuffRuleCommand { get; }
    public ICommand DeleteBuffRuleCommand { get; }
    public ICommand AddCombatActionRuleCommand { get; }
    public ICommand DeleteCombatActionRuleCommand { get; }
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
    public ICommand CastSelectedSpellCommand { get; }
    public ICommand ExportSupportSnapshotCommand { get; }
    public ICommand LoadCreatorStudioCommand { get; }
    public ICommand SearchCreatorObjectsCommand { get; }
    public ICommand SelectCreatorMdbCommand { get; }
    public ICommand SelectCreatorEvidenceFolderCommand { get; }
    public ICommand ValidateCreatorSourcesCommand { get; }
    public ICommand ImportCreatorMdbCommand { get; }
    public ICommand EditCreatorObjectCommand { get; }
    public ICommand NewCreatorObjectCommand { get; }
    public ICommand SaveCreatorObjectCommand { get; }
    public ICommand ValidateCreatorWorldCommand { get; }
    public ICommand BuildCreatorWorldCommand { get; }
    public ICommand StartCreatorTestRealmCommand { get; }
    public ICommand StopCreatorTestRealmCommand { get; }
    public ICommand PreviewCreatorPublishCommand { get; }
    public ICommand PublishCreatorTestRealmCommand { get; }
    public ICommand RollbackCreatorPublishCommand { get; }
    public ICommand ExportAllCreatorChangesCommand { get; }
    public ICommand ExportCreatorFamilyCommand { get; }
    public ICommand ExportCreatorRecordCommand { get; }
    public ICommand PreviewCreatorPackageCommand { get; }
    public ICommand ApplyCreatorPackageCommand { get; }
    public ICommand LoadCreatorRoomLinksCommand { get; }
    public ICommand SaveCreatorRoomLinksCommand { get; }
    public ICommand LoadCreatorShopInventoryCommand { get; }
    public ICommand SaveCreatorShopInventoryCommand { get; }
    public ICommand AddCreatorShopInventoryRowCommand { get; }
    public ICommand RemoveCreatorShopInventoryRowCommand { get; }
    public ICommand LoadCreatorNpcEditorCommand { get; }
    public ICommand SaveCreatorNpcAttacksCommand { get; }
    public ICommand AddCreatorNpcAttackCommand { get; }
    public ICommand RemoveCreatorNpcAttackCommand { get; }
    public ICommand SaveCreatorNpcAbilitiesCommand { get; }
    public ICommand AddCreatorNpcAbilityCommand { get; }
    public ICommand RemoveCreatorNpcAbilityCommand { get; }
    public ICommand LoadCreatorQuestStagesCommand { get; }
    public ICommand LoadCreatorSelectedScriptCommand { get; }
    public ICommand LoadCreatorQuestStageScriptCommand { get; }
    public ICommand AddCreatorScriptOperationCommand { get; }
    public ICommand RemoveCreatorScriptOperationCommand { get; }
    public ICommand SaveCreatorScriptOperationsCommand { get; }
    public ICommand RunCreatorNpcSimulationCommand { get; }
    public ICommand RunCreatorWeaponSimulationCommand { get; }
    public ICommand LoadCreatorMapLayoutCommand { get; }
    public ICommand PreviewCreatorMapAutoArrangeCommand { get; }
    public ICommand SaveCreatorMapLayoutCommand { get; }
    public ICommand SearchCreatorWorldCommand { get; }
    public ICommand FindCreatorReferencesCommand { get; }
    public ICommand LoadCreatorKeyDoorsCommand { get; }
    public ICommand LoadCreatorMonsterIndexCommand { get; }
    public ICommand LoadCreatorControlRoomsCommand { get; }
    public ICommand OpenCreatorSearchResultCommand { get; }
    public ICommand OpenCreatorReferenceResultCommand { get; }
    public ICommand OpenCreatorKeyDoorExitCommand { get; }
    public ICommand OpenCreatorKeyDoorItemCommand { get; }
    public ICommand OpenCreatorMonsterCommand { get; }
    public ICommand OpenCreatorControlRoomCommand { get; }
    public ICommand LoadCreatorParityAuditCommand { get; }
    public ICommand ApplyCreatorBulkSetCommand { get; }
    public ICommand ApplyCreatorBulkCopyCommand { get; }
    public ICommand SearchCreatorCoinRoomsCommand { get; }
    public ICommand LoadCreatorRoomCurrencyCommand { get; }
    public ICommand SaveCreatorRoomCurrencyCommand { get; }
    public ICommand SearchCreatorShopResetsCommand { get; }
    public ICommand ResetCreatorShopStockCommand { get; }
    public ICommand SearchCreatorCleanupRoomsCommand { get; }
    public ICommand CombineCreatorRoomItemsCommand { get; }
    public ICommand PreviewCreatorTextblockStripCommand { get; }
    public ICommand ApplyCreatorTextblockStripCommand { get; }
    public ICommand SaveCreatorWorldSettingsCommand { get; }
    public ICommand LoadCreatorCharactersCommand { get; }
    public ICommand DeleteCreatorCharacterCommand { get; }
    public ICommand LoadCreatorClassicAdministrationCommand { get; }
    public ICommand SaveCreatorClassicAdministrationCommand { get; }
    public ICommand LoadLibrariumCommand { get; }

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
    public ObservableCollection<ClientCombatActionRule> CombatActionRules => _gameplayProfile.CombatActions;
    public ObservableCollection<ClientGameplayNpcState> GameplayNpcs { get; } = [];
    public ObservableCollection<ClientGameplayPartyMemberState> GameplayPartyMembers { get; } = [];
    public ObservableCollection<ClientDiagnosticCheck> GameplayDiagnosticChecks { get; } = [];
    public ObservableCollection<ClientInventoryItemState> CharacterEquipment { get; } = [];
    public ObservableCollection<string> CharacterSpells { get; } = [];
    public ObservableCollection<ClientGameplayEffectState> CharacterEffects { get; } = [];
    public ObservableCollection<ClientWorkshopItem> WorkshopSearchResults { get; } = [];
    public ObservableCollection<ClientWorkshopItem> PlannedWorkshopItems { get; } = [];
    public ObservableCollection<CustomWorldSimpleFamilyOption> CreatorFamilies { get; } = [];
    public ObservableCollection<CustomWorldOverrideSummary> CreatorOverrides { get; } = [];
    public ObservableCollection<CustomWorldObjectSummary> CreatorObjects { get; } = [];
    public ObservableCollection<NightmareMdbTableSummary> CreatorMdbTables { get; } = [];
    public ObservableCollection<NightmareWccEvidenceFile> CreatorEvidenceFiles { get; } = [];
    public ObservableCollection<CreatorFieldEditorRow> CreatorEditFields { get; } = [];
    public ObservableCollection<CustomWorldValidationIssue> CreatorValidationIssues { get; } = [];
    public ObservableCollection<CreatorRoomLinkRow> CreatorRoomLinks { get; } = [];
    public ObservableCollection<CreatorShopInventoryRow> CreatorShopInventory { get; } = [];
    public ObservableCollection<CreatorNpcAttackRow> CreatorNpcAttacks { get; } = [];
    public ObservableCollection<CreatorNpcAbilityRow> CreatorNpcAbilities { get; } = [];
    public ObservableCollection<CreatorQuestStageRow> CreatorQuestStages { get; } = [];
    public ObservableCollection<CreatorScriptOperationRow> CreatorScriptOperations { get; } = [];
    public ObservableCollection<string> CreatorScriptOpcodes { get; } = [];
    public ObservableCollection<CustomWorldNpcAttackSimulationRow> CreatorNpcSimulationRows { get; } = [];
    public ObservableCollection<int> CreatorMapNumbers { get; } = [];
    public ObservableCollection<CreatorMapRoomRow> CreatorMapRooms { get; } = [];
    public ObservableCollection<CreatorMapEdgeRow> CreatorMapEdges { get; } = [];
    public ObservableCollection<CustomWorldGlobalSearchHit> CreatorGlobalSearchResults { get; } = [];
    public ObservableCollection<CustomWorldReferenceHit> CreatorReferenceResults { get; } = [];
    public ObservableCollection<CustomWorldKeyDoorRow> CreatorKeyDoors { get; } = [];
    public ObservableCollection<CustomWorldMonsterIndexRow> CreatorMonsterIndex { get; } = [];
    public ObservableCollection<CustomWorldControlRoomRow> CreatorControlRooms { get; } = [];
    public ObservableCollection<CustomWorldNightmareParityRow> CreatorParityAuditRows { get; } = [];
    public ObservableCollection<CustomWorldObjectSummary> CreatorCoinRooms { get; } = [];
    public ObservableCollection<CustomWorldObjectSummary> CreatorShopResetRecords { get; } = [];
    public ObservableCollection<CustomWorldObjectSummary> CreatorCleanupRooms { get; } = [];
    public ObservableCollection<CustomWorldTextblockStripPreviewRow> CreatorTextblockPreviews { get; } = [];
    public ObservableCollection<CreatorManagedCharacterRow> CreatorManagedCharacters { get; } = [];
    public ObservableCollection<CreatorClassicAccessRow> CreatorClassicAccessRows { get; } = [];
    public ObservableCollection<ClassicLibrariumCategory> LibrariumCategories { get; } = [];
    public ObservableCollection<ClassicLibrariumRecordSummary> LibrariumRecords { get; } = [];
    public ObservableCollection<ClassicLibrariumField> LibrariumFields { get; } = [];
    public IReadOnlyList<string> CreatorBulkValueKinds { get; } = ["Text", "Integer", "Decimal", "Boolean", "Null"];
    public IReadOnlyList<ClientCombatTargetMode> CombatTargetModes { get; } =
        Enum.GetValues<ClientCombatTargetMode>();
    public IReadOnlyList<string> PartyRanks { get; } = ["Frontrank", "Backrank"];
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

    public string? SelectedCharacterSpell
    {
        get => _selectedCharacterSpell;
        set => Set(ref _selectedCharacterSpell, value);
    }

    public string SpellTarget
    {
        get => _spellTarget;
        set => Set(ref _spellTarget, value);
    }

    public string SpellActionStatus
    {
        get => _spellActionStatus;
        private set => Set(ref _spellActionStatus, value);
    }

    public string SessionStatisticsText
    {
        get => _sessionStatisticsText;
        private set => Set(ref _sessionStatisticsText, value);
    }

    public string SupportBundleStatus
    {
        get => _supportBundleStatus;
        private set => Set(ref _supportBundleStatus, value);
    }

    public ClientCombatActionRule? SelectedCombatActionRule
    {
        get => _selectedCombatActionRule;
        set => Set(ref _selectedCombatActionRule, value);
    }

    public CustomWorldSimpleFamilyOption? SelectedCreatorFamily
    {
        get => _selectedCreatorFamily;
        set
        {
            if (!Set(ref _selectedCreatorFamily, value)) return;
            CreatorObjects.Clear();
            SelectedCreatorObject = null;
            CreatorObjectDetails = value is null
                ? "Select a content category."
                : $"Search {value.Label.ToLowerInvariant()} to inspect generated base content and Custom1 overrides.";
        }
    }

    public CustomWorldObjectSummary? SelectedCreatorObject
    {
        get => _selectedCreatorObject;
        set
        {
            if (!Set(ref _selectedCreatorObject, value)) return;
            CreatorObjectDetails = value is null ? "Select an object to view its gameplay fields." :
                FormatCreatorObject(value);
        }
    }

    public string CreatorStudioStatus
    {
        get => _creatorStudioStatus;
        private set => Set(ref _creatorStudioStatus, value);
    }

    public string CreatorWorkspaceText
    {
        get => _creatorWorkspaceText;
        private set => Set(ref _creatorWorkspaceText, value);
    }

    public string CreatorSearchText
    {
        get => _creatorSearchText;
        set => Set(ref _creatorSearchText, value);
    }

    public string CreatorObjectDetails
    {
        get => _creatorObjectDetails;
        private set => Set(ref _creatorObjectDetails, value);
    }

    public string CreatorMdbPath
    {
        get => _creatorMdbPath;
        private set => Set(ref _creatorMdbPath, value);
    }

    public string CreatorEvidenceFolder
    {
        get => _creatorEvidenceFolder;
        private set => Set(ref _creatorEvidenceFolder, value);
    }

    public string CreatorSourceStatus
    {
        get => _creatorSourceStatus;
        private set => Set(ref _creatorSourceStatus, value);
    }

    public string CreatorSourceSummary
    {
        get => _creatorSourceSummary;
        private set => Set(ref _creatorSourceSummary, value);
    }

    public bool CreatorImportAcknowledged
    {
        get => _creatorImportAcknowledged;
        set
        {
            if (!Set(ref _creatorImportAcknowledged, value)) return;
            Raise(nameof(CreatorImportReady));
        }
    }

    public bool CreatorImportReady =>
        _creatorMdbAnalysis is not null &&
        _creatorEvidenceAnalysis is not null &&
        _creatorPairValidation?.CanImport == true &&
        CreatorImportAcknowledged;

    public string CreatorEditTitle
    {
        get => _creatorEditTitle;
        private set => Set(ref _creatorEditTitle, value);
    }

    public string CreatorEditObjectId
    {
        get => _creatorEditObjectId;
        set => Set(ref _creatorEditObjectId, value);
    }

    public string CreatorEditStatus
    {
        get => _creatorEditStatus;
        private set => Set(ref _creatorEditStatus, value);
    }

    public string CreatorValidationStatus
    {
        get => _creatorValidationStatus;
        private set => Set(ref _creatorValidationStatus, value);
    }

    public bool CreatorEditIsNew
    {
        get => _creatorEditIsNew;
        private set => Set(ref _creatorEditIsNew, value);
    }

    public bool CreatorEditorActive
    {
        get => _creatorEditorActive;
        private set => Set(ref _creatorEditorActive, value);
    }

    public string CreatorPackageStatus
    {
        get => _creatorPackageStatus;
        private set => Set(ref _creatorPackageStatus, value);
    }

    public string CreatorPublishStatus
    {
        get => _creatorPublishStatus;
        private set => Set(ref _creatorPublishStatus, value);
    }

    public bool CreatorPublishAcknowledged
    {
        get => _creatorPublishAcknowledged;
        set
        {
            if (!Set(ref _creatorPublishAcknowledged, value)) return;
            Raise(nameof(CreatorPublishReady));
        }
    }

    public bool CreatorPublishReady => _creatorPublishPreview is not null && CreatorPublishAcknowledged;

    public string CreatorPendingPackagePath
    {
        get => _creatorPendingPackagePath;
        private set => Set(ref _creatorPendingPackagePath, value);
    }

    public bool CreatorPackageApplyAcknowledged
    {
        get => _creatorPackageApplyAcknowledged;
        set
        {
            if (!Set(ref _creatorPackageApplyAcknowledged, value)) return;
            Raise(nameof(CreatorPackageApplyReady));
        }
    }

    public bool CreatorPackageApplyReady =>
        _creatorPendingPackagePreview is { InvalidRecords: 0 } &&
        CreatorPackageApplyAcknowledged;

    public string CreatorRoomLinksStatus
    {
        get => _creatorRoomLinksStatus;
        private set => Set(ref _creatorRoomLinksStatus, value);
    }

    public string CreatorShopInventoryStatus
    {
        get => _creatorShopInventoryStatus;
        private set => Set(ref _creatorShopInventoryStatus, value);
    }

    public string CreatorNpcEditorStatus
    {
        get => _creatorNpcEditorStatus;
        private set => Set(ref _creatorNpcEditorStatus, value);
    }

    public string CreatorQuestScriptStatus
    {
        get => _creatorQuestScriptStatus;
        private set => Set(ref _creatorQuestScriptStatus, value);
    }

    public string CreatorSpecializedRoomId
    {
        get => _creatorSpecializedRoomId;
        private set => Set(ref _creatorSpecializedRoomId, value);
    }

    public string CreatorSpecializedShopId
    {
        get => _creatorSpecializedShopId;
        private set => Set(ref _creatorSpecializedShopId, value);
    }

    public string CreatorSpecializedNpcId
    {
        get => _creatorSpecializedNpcId;
        private set => Set(ref _creatorSpecializedNpcId, value);
    }

    public string CreatorSpecializedQuestId
    {
        get => _creatorSpecializedQuestId;
        private set => Set(ref _creatorSpecializedQuestId, value);
    }

    public string CreatorSpecializedScriptId
    {
        get => _creatorSpecializedScriptId;
        private set => Set(ref _creatorSpecializedScriptId, value);
    }

    public CreatorShopInventoryRow? SelectedCreatorShopInventoryRow
    {
        get => _selectedCreatorShopInventoryRow;
        set => Set(ref _selectedCreatorShopInventoryRow, value);
    }

    public CreatorNpcAttackRow? SelectedCreatorNpcAttackRow
    {
        get => _selectedCreatorNpcAttackRow;
        set => Set(ref _selectedCreatorNpcAttackRow, value);
    }

    public CreatorNpcAbilityRow? SelectedCreatorNpcAbilityRow
    {
        get => _selectedCreatorNpcAbilityRow;
        set => Set(ref _selectedCreatorNpcAbilityRow, value);
    }

    public CreatorQuestStageRow? SelectedCreatorQuestStageRow
    {
        get => _selectedCreatorQuestStageRow;
        set => Set(ref _selectedCreatorQuestStageRow, value);
    }

    public CreatorScriptOperationRow? SelectedCreatorScriptOperationRow
    {
        get => _selectedCreatorScriptOperationRow;
        set => Set(ref _selectedCreatorScriptOperationRow, value);
    }

    public string CreatorNpcSimulationStatus
    {
        get => _creatorNpcSimulationStatus;
        private set => Set(ref _creatorNpcSimulationStatus, value);
    }

    public string CreatorWeaponSimulationStatus
    {
        get => _creatorWeaponSimulationStatus;
        private set => Set(ref _creatorWeaponSimulationStatus, value);
    }

    public int CreatorNpcTargetDefense { get => _creatorNpcTargetDefense; set => Set(ref _creatorNpcTargetDefense, value); }
    public int CreatorNpcTargetArmorClass { get => _creatorNpcTargetArmorClass; set => Set(ref _creatorNpcTargetArmorClass, value); }
    public int CreatorNpcTargetDamageResistance { get => _creatorNpcTargetDamageResistance; set => Set(ref _creatorNpcTargetDamageResistance, value); }
    public int CreatorNpcTargetDodge { get => _creatorNpcTargetDodge; set => Set(ref _creatorNpcTargetDodge, value); }
    public int CreatorNpcSimulationRounds { get => _creatorNpcSimulationRounds; set => Set(ref _creatorNpcSimulationRounds, value); }
    public int CreatorWeaponBaseAttack { get => _creatorWeaponBaseAttack; set => Set(ref _creatorWeaponBaseAttack, value); }
    public int CreatorWeaponTargetDefense { get => _creatorWeaponTargetDefense; set => Set(ref _creatorWeaponTargetDefense, value); }
    public int CreatorWeaponTargetArmorClass { get => _creatorWeaponTargetArmorClass; set => Set(ref _creatorWeaponTargetArmorClass, value); }
    public int CreatorWeaponTargetDamageResistance { get => _creatorWeaponTargetDamageResistance; set => Set(ref _creatorWeaponTargetDamageResistance, value); }
    public int CreatorWeaponTargetDodge { get => _creatorWeaponTargetDodge; set => Set(ref _creatorWeaponTargetDodge, value); }
    public int CreatorWeaponCriticalChance { get => _creatorWeaponCriticalChance; set => Set(ref _creatorWeaponCriticalChance, value); }
    public int CreatorWeaponSimulationSwings { get => _creatorWeaponSimulationSwings; set => Set(ref _creatorWeaponSimulationSwings, value); }

    public int CreatorSelectedMapNumber
    {
        get => _creatorSelectedMapNumber;
        set => Set(ref _creatorSelectedMapNumber, value);
    }

    public string CreatorMapLayoutStatus
    {
        get => _creatorMapLayoutStatus;
        private set => Set(ref _creatorMapLayoutStatus, value);
    }

    public string CreatorWorldToolSearchText
    {
        get => _creatorWorldToolSearchText;
        set => Set(ref _creatorWorldToolSearchText, value);
    }

    public string CreatorWorldToolStatus
    {
        get => _creatorWorldToolStatus;
        private set => Set(ref _creatorWorldToolStatus, value);
    }

    public CustomWorldGlobalSearchHit? SelectedCreatorGlobalSearchHit
    {
        get => _selectedCreatorGlobalSearchHit;
        set => Set(ref _selectedCreatorGlobalSearchHit, value);
    }

    public CustomWorldReferenceHit? SelectedCreatorReferenceHit
    {
        get => _selectedCreatorReferenceHit;
        set => Set(ref _selectedCreatorReferenceHit, value);
    }

    public CustomWorldKeyDoorRow? SelectedCreatorKeyDoor
    {
        get => _selectedCreatorKeyDoor;
        set => Set(ref _selectedCreatorKeyDoor, value);
    }

    public CustomWorldMonsterIndexRow? SelectedCreatorMonsterIndex
    {
        get => _selectedCreatorMonsterIndex;
        set => Set(ref _selectedCreatorMonsterIndex, value);
    }

    public CustomWorldControlRoomRow? SelectedCreatorControlRoom
    {
        get => _selectedCreatorControlRoom;
        set => Set(ref _selectedCreatorControlRoom, value);
    }

    public string CreatorMaintenanceConfirmation
    {
        get => _creatorMaintenanceConfirmation;
        set => Set(ref _creatorMaintenanceConfirmation, value);
    }

    public string CreatorMaintenanceStatus
    {
        get => _creatorMaintenanceStatus;
        private set => Set(ref _creatorMaintenanceStatus, value);
    }

    public string CreatorBulkFamily { get => _creatorBulkFamily; set => Set(ref _creatorBulkFamily, value); }
    public string CreatorBulkTargetIds { get => _creatorBulkTargetIds; set => Set(ref _creatorBulkTargetIds, value); }
    public string CreatorBulkFieldPath { get => _creatorBulkFieldPath; set => Set(ref _creatorBulkFieldPath, value); }
    public string CreatorBulkValue { get => _creatorBulkValue; set => Set(ref _creatorBulkValue, value); }
    public string CreatorBulkValueKind { get => _creatorBulkValueKind; set => Set(ref _creatorBulkValueKind, value); }
    public string CreatorBulkSourceId { get => _creatorBulkSourceId; set => Set(ref _creatorBulkSourceId, value); }
    public string CreatorMaintenanceSearch { get => _creatorMaintenanceSearch; set => Set(ref _creatorMaintenanceSearch, value); }
    public string CreatorCoinRunic { get => _creatorCoinRunic; set => Set(ref _creatorCoinRunic, value); }
    public string CreatorCoinPlatinum { get => _creatorCoinPlatinum; set => Set(ref _creatorCoinPlatinum, value); }
    public string CreatorCoinGold { get => _creatorCoinGold; set => Set(ref _creatorCoinGold, value); }
    public string CreatorCoinSilver { get => _creatorCoinSilver; set => Set(ref _creatorCoinSilver, value); }
    public string CreatorCoinCopper { get => _creatorCoinCopper; set => Set(ref _creatorCoinCopper, value); }
    public string CreatorTextblockFind { get => _creatorTextblockFind; set => Set(ref _creatorTextblockFind, value); }
    public string CreatorTextblockReplacement { get => _creatorTextblockReplacement; set => Set(ref _creatorTextblockReplacement, value); }
    public bool CreatorTextblockSuffixOnly { get => _creatorTextblockSuffixOnly; set => Set(ref _creatorTextblockSuffixOnly, value); }

    public CustomWorldObjectSummary? SelectedCreatorCoinRoom
    {
        get => _selectedCreatorCoinRoom;
        set => Set(ref _selectedCreatorCoinRoom, value);
    }

    public CustomWorldObjectSummary? SelectedCreatorShopReset
    {
        get => _selectedCreatorShopReset;
        set => Set(ref _selectedCreatorShopReset, value);
    }

    public CustomWorldObjectSummary? SelectedCreatorCleanupRoom
    {
        get => _selectedCreatorCleanupRoom;
        set => Set(ref _selectedCreatorCleanupRoom, value);
    }

    public CustomWorldTextblockStripPreviewRow? SelectedCreatorTextblockPreview
    {
        get => _selectedCreatorTextblockPreview;
        set => Set(ref _selectedCreatorTextblockPreview, value);
    }

    public string CreatorWorldName { get => _creatorWorldName; set => Set(ref _creatorWorldName, value); }
    public string CreatorWorldDescription { get => _creatorWorldDescription; set => Set(ref _creatorWorldDescription, value); }
    public string CreatorWorldLoginBanner { get => _creatorWorldLoginBanner; set => Set(ref _creatorWorldLoginBanner, value); }
    public string CreatorWorldMotd { get => _creatorWorldMotd; set => Set(ref _creatorWorldMotd, value); }
    public bool CreatorWorldRuntimeEnabled { get => _creatorWorldRuntimeEnabled; set => Set(ref _creatorWorldRuntimeEnabled, value); }
    public bool CreatorWorldTransfersAllowed { get => _creatorWorldTransfersAllowed; set => Set(ref _creatorWorldTransfersAllowed, value); }
    public bool CreatorClassicUnlimitedItems { get => _creatorClassicUnlimitedItems; set => Set(ref _creatorClassicUnlimitedItems, value); }
    public bool CreatorClassicEveryoneCanGoto { get => _creatorClassicEveryoneCanGoto; set => Set(ref _creatorClassicEveryoneCanGoto, value); }
    public string CreatorAdministrationConfirmation { get => _creatorAdministrationConfirmation; set => Set(ref _creatorAdministrationConfirmation, value); }

    public string CreatorWorldSettingsStatus
    {
        get => _creatorWorldSettingsStatus;
        private set => Set(ref _creatorWorldSettingsStatus, value);
    }

    public string CreatorAdministrationStatus
    {
        get => _creatorAdministrationStatus;
        private set => Set(ref _creatorAdministrationStatus, value);
    }

    public CreatorManagedCharacterRow? SelectedCreatorManagedCharacter
    {
        get => _selectedCreatorManagedCharacter;
        set => Set(ref _selectedCreatorManagedCharacter, value);
    }

    public ClassicLibrariumCategory? SelectedLibrariumCategory
    {
        get => _selectedLibrariumCategory;
        set
        {
            if (!Set(ref _selectedLibrariumCategory, value) || value is null) return;
            _ = LoadLibrariumAsync();
        }
    }

    public ClassicLibrariumRecordSummary? SelectedLibrariumRecord
    {
        get => _selectedLibrariumRecord;
        set
        {
            if (!Set(ref _selectedLibrariumRecord, value)) return;
            _ = LoadLibrariumDetailsAsync(value);
        }
    }

    public string LibrariumSearchText { get => _librariumSearchText; set => Set(ref _librariumSearchText, value); }
    public string LibrariumStatus { get => _librariumStatus; private set => Set(ref _librariumStatus, value); }
    public string LibrariumSelectedTitle { get => _librariumSelectedTitle; private set => Set(ref _librariumSelectedTitle, value); }
    public string LibrariumSelectedSubheading { get => _librariumSelectedSubheading; private set => Set(ref _librariumSelectedSubheading, value); }

    public ClientEquipmentSet? DefaultCombatEquipmentSet
    {
        get => _defaultCombatEquipmentSet;
        set
        {
            if (!Set(ref _defaultCombatEquipmentSet, value)) return;
            _gameplayProfile.Combat.DefaultEquipmentSetId = value?.Id ?? string.Empty;
        }
    }

    public ClientEquipmentSet? MultiTargetCombatEquipmentSet
    {
        get => _multiTargetCombatEquipmentSet;
        set
        {
            if (!Set(ref _multiTargetCombatEquipmentSet, value)) return;
            _gameplayProfile.Combat.MultiTargetEquipmentSetId = value?.Id ?? string.Empty;
        }
    }

    public ClientEquipmentSet? DefensiveCombatEquipmentSet
    {
        get => _defensiveCombatEquipmentSet;
        set
        {
            if (!Set(ref _defensiveCombatEquipmentSet, value)) return;
            _gameplayProfile.Combat.DefensiveEquipmentSetId = value?.Id ?? string.Empty;
        }
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

    public string CharacterProgressDetailsText
    {
        get => _characterProgressDetailsText;
        private set => Set(ref _characterProgressDetailsText, value);
    }

    public string CharacterClassProgressText
    {
        get => _characterClassProgressText;
        private set => Set(ref _characterClassProgressText, value);
    }

    public double CharacterProgressPercent
    {
        get => _characterProgressPercent;
        private set => Set(ref _characterProgressPercent, value);
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

    public string CharacterDerivedStatsText
    {
        get => _characterDerivedStatsText;
        private set => Set(ref _characterDerivedStatsText, value);
    }

    public string CharacterDefensesText
    {
        get => _characterDefensesText;
        private set => Set(ref _characterDefensesText, value);
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

    public string ExternalEquipmentStatus
    {
        get => _externalEquipmentStatus;
        private set => Set(ref _externalEquipmentStatus, value);
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
            _externalWorkshopCatalog = null;
            ClearExternalEquipmentCapture(SupportsPrivateRoomState()
                ? "RealmGate equipment is supplied by the connected Host."
                : "External equipment cleared because the selected profile changed.");
            ClearExternalSessionState();
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

    public string DefaultHostWorld
    {
        get => _defaultHostWorld;
        set => Set(ref _defaultHostWorld, value);
    }

    public string ApplicationSettingsStatus
    {
        get => _applicationSettingsStatus;
        private set => Set(ref _applicationSettingsStatus, value);
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
            await _host.StartAsync(new DesktopHostOptions(
                null,
                BindAddress,
                port,
                maximumSessions,
                WorldId: DefaultHostWorld));
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
            if (_creatorTestRealmRunning)
            {
                _creatorTestRealmRunning = false;
                CreatorPublishStatus = "Custom1 Test Realm stopped. Preview the live-world diff before publishing.";
            }
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

    private async Task LoadApplicationSettingsAndProfilesAsync()
    {
        try
        {
            var settingsTask = _applicationSettingsStore.LoadAsync();
            var catalogTask = _profileStore.LoadAsync();
            await Task.WhenAll(settingsTask, catalogTask);
            var settings = await settingsTask;
            var catalog = await catalogTask;
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _applicationSettings = settings;
                _profileCatalog = catalog;
                Raise(nameof(ConnectionProfiles));
                BindAddress = settings.HostBindAddress;
                PortText = settings.HostPort.ToString();
                MaximumSessionsText = settings.HostMaximumSessions.ToString();
                DefaultHostWorld = settings.DefaultWorld;
                SelectedConnectionProfile = ConnectionProfiles.FirstOrDefault(profile =>
                                                profile.ProfileName.Equals(
                                                    settings.DefaultConnectionProfile,
                                                    StringComparison.OrdinalIgnoreCase))
                                            ?? ConnectionProfiles.FirstOrDefault(profile =>
                                                profile.ServerProfileName.Equals(
                                                    settings.DefaultConnectionProfile,
                                                    StringComparison.OrdinalIgnoreCase))
                                            ?? ConnectionProfiles.FirstOrDefault();
                ApplicationSettingsStatus = "Application defaults loaded.";
                RefreshLanEndpoints();
            });
        }
        catch (Exception exception)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ClientStatus = $"Profile load failed: {exception.Message}";
                ApplicationSettingsStatus = $"Application defaults could not load: {exception.Message}";
            });
        }
    }

    private async Task SaveApplicationSettingsAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(DefaultHostWorld))
                throw new InvalidOperationException("Default world is required.");
            if (string.IsNullOrWhiteSpace(BindAddress))
                throw new InvalidOperationException("Bind address is required.");
            if (!int.TryParse(PortText, out var port) || port is < 1 or > 65535)
                throw new InvalidOperationException("Port must be a number from 1 through 65535.");
            if (!int.TryParse(MaximumSessionsText, out var maximumSessions) ||
                maximumSessions is < 1 or > 4096)
            {
                throw new InvalidOperationException("Maximum sessions must be a number from 1 through 4096.");
            }

            _applicationSettings.DefaultWorld = DefaultHostWorld;
            _applicationSettings.DefaultConnectionProfile =
                SelectedConnectionProfile?.ProfileName ?? "Local Host Classic";
            _applicationSettings.HostBindAddress = BindAddress;
            _applicationSettings.HostPort = port;
            _applicationSettings.HostMaximumSessions = maximumSessions;
            await _applicationSettingsStore.SaveAsync(_applicationSettings);
            ApplicationSettingsStatus = "Application defaults saved.";
        }
        catch (Exception exception)
        {
            ApplicationSettingsStatus = $"Application defaults were not saved: {exception.Message}";
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

            if (!sensitive) NoteExternalItemCommand(command);
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

    private void NoteExternalItemCommand(string command)
    {
        if (!SupportsPrivateRoomState())
        {
            _externalItemScreenTracker.NoteOutboundCommand(command);
            _externalSessionTracker.NoteOutboundCommand(command);
        }
    }

    private void ObserveExternalItemText(string text, long generation)
    {
        if (SupportsPrivateRoomState()) return;
        foreach (var observation in _externalItemScreenTracker.Append(text))
        {
            if (observation.Kind != ClassicItemScreenKind.Equipment) continue;
            Dispatcher.UIThread.Post(() =>
            {
                if (generation != Volatile.Read(ref _clientConnectionGeneration) || SupportsPrivateRoomState())
                    return;
                ApplyExternalEquipmentObservation(observation);
            });
        }
    }

    private void ObserveExternalSessionText(string text, long generation)
    {
        if (SupportsPrivateRoomState()) return;
        if (_externalSessionTracker.Append(text) is not { } snapshot) return;
        Dispatcher.UIThread.Post(() =>
        {
            if (generation != Volatile.Read(ref _clientConnectionGeneration) || SupportsPrivateRoomState())
                return;
            _externalSessionSnapshot = snapshot;
            RefreshCharacterPresentation();
        });
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

            NoteExternalItemCommand(command);
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
            _externalItemScreenTracker.Reset();
            _externalSessionTracker.Reset();
            _timedEventScheduler.Reset();
            _triggerReceiveBuffer.Clear();
            _lastAutomationCommandAt = null;
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                CurrentRoomKey = string.Empty;
                ClearInventoryState("Inventory state cleared because the connection changed.");
                ClearGameplayAutomationState("Gameplay state cleared because the connection changed.");
                ClearExternalEquipmentCapture(SupportsPrivateRoomState()
                    ? "RealmGate equipment is supplied by the connected Host."
                    : "External equipment cleared because the connection changed.");
                ClearExternalSessionState();
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
            _externalItemScreenTracker.Reset();
            _externalSessionTracker.Reset();
            _timedEventScheduler.Reset();
            _lastAutomationCommandAt = null;
            Dispatcher.UIThread.Post(() =>
            {
                CurrentRoomKey = string.Empty;
                ClearInventoryState("Inventory state cleared because the connection was lost.");
                ClearGameplayAutomationState("Gameplay state cleared because the connection was lost.");
                ClearExternalEquipmentCapture(SupportsPrivateRoomState()
                    ? "RealmGate equipment is supplied by the connected Host."
                    : "External equipment cleared because the connection was lost.");
                ClearExternalSessionState();
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
        RefreshCombatEquipmentSetSelections();
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
        RefreshCombatEquipmentSetSelections();
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

    private async Task<bool> ExecuteInventoryPlanAsync(ClientInventoryAutomationPlan plan)
    {
        if (!plan.HasCommands)
        {
            InventoryStatus = plan.Warnings.Count > 0
                ? "Inventory plan was blocked by its safety checks."
                : "Inventory already matches the selected plan.";
            return false;
        }
        if (_automationProfile.EmergencyStop)
        {
            InventoryStatus = "Emergency Stop is on. Preview is available, but commands were not sent.";
            return false;
        }
        if (!_terminalSession.IsConnected || !_gameplayState.GameplayReady)
        {
            InventoryStatus = "Inventory commands require a connected, ready character session.";
            return false;
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
            return false;
        }
        finally { _triggerGate.Release(); }

        await Task.Delay(Math.Max(500, _automationProfile.MinimumCommandDelayMs));
        await RequestInventoryStateAsync(generation, CancellationToken.None);
        return true;
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
        Raise(nameof(CombatActionRules));
        RefreshCombatEquipmentSetSelections();
        GameplayNpcs.Clear();
        foreach (var npc in state.Npcs) GameplayNpcs.Add(npc);
        GameplayPartyMembers.Clear();
        foreach (var member in state.PartyMembers) GameplayPartyMembers.Add(member);
        if (SelectedBuffRule is null || !BuffRules.Contains(SelectedBuffRule))
            SelectedBuffRule = BuffRules.FirstOrDefault();
        if (SelectedCombatActionRule is null || !CombatActionRules.Contains(SelectedCombatActionRule))
            SelectedCombatActionRule = CombatActionRules.FirstOrDefault();
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
        SelectedCombatActionRule = null;
        _gameplayAutomationRuntime.Reset();
        Raise(nameof(GameplayProfile));
        Raise(nameof(BuffRules));
        Raise(nameof(CombatActionRules));
        RefreshCombatEquipmentSetSelections();
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
        if (!SupportsPrivateRoomState())
        {
            await RefreshExternalCharacterAsync();
            return;
        }
        var generation = Volatile.Read(ref _clientConnectionGeneration);
        await RequestCharacterStateAsync(generation, CancellationToken.None);
        if (generation != Volatile.Read(ref _clientConnectionGeneration)) return;
        await RequestInventoryStateAsync(generation, CancellationToken.None);
        if (generation != Volatile.Read(ref _clientConnectionGeneration)) return;
        await RequestGameplayAutomationStateAsync(generation, CancellationToken.None);
    }

    private async Task RefreshExternalCharacterAsync()
    {
        if (!_terminalSession.IsConnected)
        {
            CharacterStatus = "Connect the selected external BBS before refreshing character state.";
            return;
        }

        var generation = Volatile.Read(ref _clientConnectionGeneration);
        var commands = new[] { "stat", "experience", "spells", "party", "equipment" };
        await _triggerGate.WaitAsync();
        try
        {
            CharacterStatus = "Requesting bounded character screens from the external BBS...";
            foreach (var command in commands)
            {
                if (generation != Volatile.Read(ref _clientConnectionGeneration) || !_terminalSession.IsConnected)
                    return;
                NoteExternalItemCommand(command);
                await _terminalSession.SendLineAsync(command, sensitive: false, CancellationToken.None);
                await Task.Delay(Math.Max(500, _automationProfile.MinimumCommandDelayMs));
            }
        }
        catch (Exception exception)
        {
            CharacterStatus = $"External character refresh failed: {exception.Message}";
        }
        finally
        {
            _triggerGate.Release();
        }
    }

    private async Task CaptureExternalEquipmentAsync()
    {
        if (SupportsPrivateRoomState())
        {
            await RefreshCharacterAsync();
            return;
        }
        if (!_terminalSession.IsConnected)
        {
            ExternalEquipmentStatus = "Connect the selected external BBS before capturing equipment.";
            return;
        }

        var generation = Volatile.Read(ref _clientConnectionGeneration);
        await _triggerGate.WaitAsync();
        try
        {
            if (generation != Volatile.Read(ref _clientConnectionGeneration) || !_terminalSession.IsConnected)
                return;
            NoteExternalItemCommand("equipment");
            await _terminalSession.SendLineAsync("equipment", sensitive: false, CancellationToken.None);
            ExternalEquipmentStatus = "Waiting for the external BBS equipment screen...";
        }
        catch (Exception exception)
        {
            ExternalEquipmentStatus = $"Equipment capture failed: {exception.Message}";
        }
        finally
        {
            _triggerGate.Release();
        }
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
        ClearWorkshopSearch(SupportsPrivateRoomState()
            ? "Connect a character before searching items."
            : "Search the effective external-BBS item catalog by name, slot, or ID.");
        RefreshCharacterPresentation();
        CharacterStatus = status;
    }

    private void RefreshCharacterPresentation()
    {
        var selectedSpell = SelectedCharacterSpell;
        CharacterEquipment.Clear();
        CharacterSpells.Clear();
        CharacterEffects.Clear();
        if (_characterState is not { } state)
        {
            if (!SupportsPrivateRoomState() && _externalSessionSnapshot is { } external)
            {
                RefreshExternalCharacterPresentation(external);
                return;
            }
            CharacterIdentityText = string.Empty;
            CharacterVitalsText = string.Empty;
            CharacterProgressText = string.Empty;
            CharacterProgressDetailsText = string.Empty;
            CharacterClassProgressText = string.Empty;
            CharacterProgressPercent = 0;
            CharacterAttributesText = string.Empty;
            CharacterSkillsText = string.Empty;
            CharacterDerivedStatsText = string.Empty;
            CharacterDefensesText = string.Empty;
            SelectedCharacterSpell = null;
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
        var progression = ClientCharacterProgressionSummary.From(state);
        CharacterProgressPercent = progression.PercentComplete;
        CharacterProgressText = progression.HasLevelThresholds
            ? $"Experience: {state.Experience:N0}\nLevel progress: {progression.EarnedThisLevel:N0}/{progression.RequiredThisLevel:N0} " +
              $"({progression.PercentComplete:0.0}%)\nRemaining: {progression.RemainingExperience:N0}"
            : $"Experience: {state.Experience:N0}\nLevel thresholds unavailable from this world.";
        CharacterProgressDetailsText = progression.HasLevelThresholds
            ? $"Current level began: {progression.LevelStartExperience:N0}\n" +
              $"Next level: {progression.NextLevelExperience:N0}\n" +
              $"Next promotion CP: {(progression.NextLevelCharacterPointAward is int award ? award.ToString("N0") : "Unreported")}\n" +
              $"Available CP: {progression.CharacterPointsAvailable:N0}\n" +
              $"Lives: {progression.Lives:N0}/{progression.MaximumLives:N0}"
            : $"Available CP: {progression.CharacterPointsAvailable:N0}\n" +
              $"Lives: {progression.Lives:N0}/{progression.MaximumLives:N0}";
        CharacterClassProgressText = FormatClassProgress(state);
        CharacterAttributesText = FormatValues(state.BaseStats, "No attributes reported.");
        CharacterSkillsText = FormatValues(state.Skills, "No skills reported.");
        CharacterDerivedStatsText = FormatValues(state.DerivedStats, "No derived statistics reported.");
        CharacterDefensesText =
            "Resistances\n" + FormatValues(state.Resistances, "None reported.") +
            "\n\nSaving throws\n" + FormatValues(state.SavingThrows, "None reported.");
        if (inventory is not null)
            foreach (var item in inventory.Equipment) CharacterEquipment.Add(item);
        if (gameplay is not null)
        {
            foreach (var spell in ClientSpellActions.DisplayNames(gameplay.LearnedSpells))
                CharacterSpells.Add(spell);
            foreach (var effect in gameplay.ActiveEffects) CharacterEffects.Add(effect);
        }
        SelectedCharacterSpell = selectedSpell is not null && CharacterSpells.Contains(selectedSpell)
            ? selectedSpell
            : CharacterSpells.FirstOrDefault();
    }

    private void RefreshExternalCharacterPresentation(ClassicExternalSessionSnapshot state)
    {
        var name = state.CharacterName.Length == 0 ? "External character" : state.CharacterName;
        CharacterStatus = $"{name} - observed external-BBS state - captured {state.CapturedAtUtc.ToLocalTime():g}";
        var identity = new List<string>();
        var raceClass = string.Join(" ", new[] { state.RaceName, state.ClassName }
            .Where(value => value.Length > 0));
        if (raceClass.Length > 0) identity.Add(raceClass);
        if (state.Level is { } level) identity.Add($"Level {level:N0}");
        CharacterIdentityText = (identity.Count == 0 ? "Identity details not observed." : string.Join("  |  ", identity)) +
            (state.Room is { } room ? $"\nRoom: {room.RoomName}" : string.Empty);
        static string Pair(int? current, int? maximum) => current is null
            ? "Unreported"
            : maximum is null ? current.Value.ToString("N0") : $"{current.Value:N0}/{maximum.Value:N0}";
        CharacterVitalsText =
            $"HP {Pair(state.HealthCurrent, state.HealthMaximum)}  |  Resource {Pair(state.ResourceCurrent, state.ResourceMaximum)}\n" +
            (state.InCombat switch { true => "In combat", false => "Combat off", _ => "Combat state unreported" });
        CharacterProgressText = state.Experience is { } experience
            ? $"Experience: {experience:N0}" +
              (state.ExperienceNeeded is { } needed ? $"\nNext level: {needed:N0}" : string.Empty) +
              (state.ExperienceRemaining is { } remaining ? $"\nRemaining: {remaining:N0}" : string.Empty)
            : "Experience not observed.";
        CharacterProgressDetailsText =
            $"Character points: {(state.CharacterPoints is { } points ? points.ToString("N0") : "Unreported")}\n" +
            $"Lives: {(state.Lives is { } lives ? lives.ToString("N0") : "Unreported")}\n" +
            $"Party members observed: {state.Party.Count:N0}";
        CharacterProgressPercent = state.Experience is { } current && state.ExperienceNeeded is { } target && target > 0
            ? Math.Clamp(current * 100d / target, 0, 100)
            : 0;
        CharacterClassProgressText = state.ClassName.Length == 0
            ? "Class progression is not available from the observed screen."
            : $"{state.ClassName}: level {(state.Level is { } observedLevel ? observedLevel.ToString("N0") : "Unreported")}";
        CharacterAttributesText = FormatValues(state.Attributes, "No attributes observed.");
        CharacterSkillsText = FormatValues(state.Skills, "No skills observed.");
        CharacterDerivedStatsText = FormatValues(state.DerivedStats, "No derived statistics observed.");
        CharacterDefensesText = "Resistances\n" + FormatValues(state.Resistances, "None observed.");
        foreach (var spell in state.Spells)
            CharacterSpells.Add(spell.ManaCost > 0 || spell.EnergyCost > 0
                ? $"{spell.Name} (mana {spell.ManaCost:N0}, energy {spell.EnergyCost:N0})"
                : spell.Name);
        SelectedCharacterSpell = CharacterSpells.FirstOrDefault();
        var effectIndex = 0;
        foreach (var effect in state.ActiveEffects)
            CharacterEffects.Add(new ClientGameplayEffectState(
                $"external-observed:{effectIndex++}", string.Empty, effect,
                state.CapturedAtUtc, null));
    }

    private static bool MatchesCharacter(ClientCharacterState state, string worldId, string characterId, string roomId) =>
        state.WorldId.Equals(NormalizeWorldId(worldId), StringComparison.OrdinalIgnoreCase) &&
        state.CharacterId.Equals(characterId, StringComparison.OrdinalIgnoreCase) &&
        state.RoomId.Equals(roomId, StringComparison.OrdinalIgnoreCase);

    private static string FormatValues(IReadOnlyDictionary<string, int>? values, string empty) =>
        values is null || values.Count == 0
            ? empty
            : string.Join("\n", values.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => $"{pair.Key}: {pair.Value:N0}"));

    private static string FormatClassProgress(ClientCharacterState state)
    {
        var levels = state.ClassLevels ?? new Dictionary<string, int>();
        var experience = state.ClassExperience ?? new Dictionary<string, long>();
        var keys = levels.Keys.Concat(experience.Keys).Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase).ToArray();
        if (keys.Length == 0) return "No class progression reported.";
        return string.Join("\n", keys.Select(key =>
        {
            var name = keys.Length == 1 && state.ClassName.Length > 0 ? state.ClassName : key;
            var level = levels.TryGetValue(key, out var value) ? value.ToString("N0") : "Unreported";
            var xp = experience.TryGetValue(key, out var total) ? total.ToString("N0") : "Unreported";
            return $"{name}: level {level}, {xp} XP";
        }));
    }

    private void ApplyExternalEquipmentObservation(ClassicItemScreenObservation observation)
    {
        _externalEquipmentObservation = observation;
        _externalEquipmentCapturedAtUtc = DateTimeOffset.UtcNow;
        if (_externalWorkshopCatalog is null)
        {
            ExternalEquipmentStatus =
                $"Captured {observation.Entries.Count:N0} external equipment entr{(observation.Entries.Count == 1 ? "y" : "ies")}. Search items to resolve their Classic/MDB data.";
            return;
        }

        var (equipped, unresolved) = ResolveExternalEquipment(_externalWorkshopCatalog);
        SetExternalEquipmentStatus(observation.Entries.Count, equipped.Count, unresolved);
        if (_workshopUsesExternalBbsCatalog && _workshopItemSearch is { } search)
        {
            _workshopItemSearch = search with
            {
                Equipped = equipped,
                CapturedAtUtc = _externalEquipmentCapturedAtUtc.Value
            };
            _workshopPlanInitialized = false;
            PlannedWorkshopItems.Clear();
            SelectedPlannedWorkshopItem = null;
            RefreshWorkshopComparison();
        }
    }

    private (IReadOnlyList<ClientWorkshopEquippedItem> Equipped, IReadOnlyList<string> Unresolved)
        ResolveExternalEquipment(ClientExternalBbsItemCatalog catalog)
    {
        if (_externalEquipmentObservation is not { } observation) return ([], []);
        var equipped = new List<ClientWorkshopEquippedItem>();
        var unresolved = new List<string>();
        var instance = 0;
        foreach (var entry in observation.Entries)
        {
            var item = catalog.FindByDisplayName(entry.Name);
            if (item is null)
            {
                unresolved.Add(entry.DisplayText);
                continue;
            }
            for (var copy = 0; copy < Math.Clamp(entry.Quantity, 1, 100); copy++)
                equipped.Add(new ClientWorkshopEquippedItem($"external:{instance++}", item));
        }
        return (equipped, unresolved);
    }

    private void SetExternalEquipmentStatus(int captured, int matched, IReadOnlyList<string> unresolved)
    {
        var unresolvedText = unresolved.Count == 0
            ? string.Empty
            : $" Unresolved: {string.Join(", ", unresolved.Take(5))}" +
              (unresolved.Count > 5 ? $" and {unresolved.Count - 5:N0} more." : ".");
        ExternalEquipmentStatus =
            $"Captured {captured:N0}; matched {matched:N0}; unresolved {unresolved.Count:N0}." + unresolvedText;
    }

    private void ClearExternalEquipmentCapture(string status)
    {
        _externalItemScreenTracker.Reset();
        _externalEquipmentObservation = null;
        _externalEquipmentCapturedAtUtc = null;
        ExternalEquipmentStatus = status;
    }

    private void ClearExternalSessionState()
    {
        _externalSessionTracker.Reset();
        _externalSessionSnapshot = null;
    }

    private async Task SearchWorkshopItemsAsync()
    {
        var query = WorkshopSearchText.Trim();
        if (query.Length is < 2 or > 80)
        {
            WorkshopSearchStatus = "Enter 2 to 80 characters to search items.";
            return;
        }

        if (!SupportsPrivateRoomState())
        {
            await SearchExternalBbsWorkshopItemsAsync(query);
            return;
        }

        var state = _characterState;
        if (state is null || !_terminalSession.IsConnected || !_gameplayState.GameplayReady)
        {
            WorkshopSearchStatus = "Connect and refresh a RealmGate character before searching items.";
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
                _workshopUsesExternalBbsCatalog = false;
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

    private async Task SearchExternalBbsWorkshopItemsAsync(string query)
    {
        var profile = SelectedConnectionProfile;
        if (profile is null)
        {
            ClearWorkshopSearch("Select an external BBS profile before searching items.");
            return;
        }

        var generation = Volatile.Read(ref _clientConnectionGeneration);
        WorkshopSearchStatus = "Loading the effective external-BBS item catalog...";
        try
        {
            var catalog = _externalWorkshopCatalog;
            if (catalog is null)
            {
                catalog = await Task.Run(() =>
                {
                    var source = ExternalBbsWorkshopDataSource.Resolve(profile);
                    return ClientExternalBbsItemCatalog.Load(source.ClassicDataRoot, source.OverlayPath);
                });
            }
            if (generation != Volatile.Read(ref _clientConnectionGeneration) ||
                !ReferenceEquals(profile, SelectedConnectionProfile))
                return;

            var matches = catalog.Search(query, 75, out var totalMatches);
            var (equipped, unresolved) = ResolveExternalEquipment(catalog);
            _externalWorkshopCatalog = catalog;
            _workshopItemSearch = new ClientWorkshopItemSearch(
                "Classic",
                profile.ProfileId,
                "room.map1.0",
                query,
                totalMatches,
                matches,
                equipped,
                _externalEquipmentCapturedAtUtc ?? DateTimeOffset.UtcNow);
            _workshopUsesExternalBbsCatalog = true;
            WorkshopSearchResults.Clear();
            foreach (var item in matches) WorkshopSearchResults.Add(item);
            SelectedWorkshopItem = WorkshopSearchResults.FirstOrDefault();
            WorkshopSearchStatus =
                $"{matches.Count:N0} of {totalMatches:N0} effective external-BBS item(s) shown. {catalog.AuthorityDescription}.";
            if (_externalEquipmentObservation is { } observation)
                SetExternalEquipmentStatus(observation.Entries.Count, equipped.Count, unresolved);
            RefreshWorkshopComparison();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            ClearWorkshopSearch($"External-BBS item catalog unavailable: {exception.Message}");
        }
    }

    private void ClearWorkshopSearch(string status)
    {
        _workshopItemSearch = null;
        _workshopUsesExternalBbsCatalog = false;
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
        if (_workshopUsesExternalBbsCatalog)
        {
            WorkshopComparisonText =
                $"Effective external-BBS catalog - {search.CapturedAtUtc.ToLocalTime():g}\n" +
                $"{"Metric",-15} {"Current",9} {"Planned",9} {"Change",9}\n" +
                Row("Weight", current.Weight, planned.Weight) + "\n" +
                Row("AC bonus", current.ArmorClassBonus, planned.ArmorClassBonus) + "\n" +
                Row("DR bonus", current.DamageResistanceBonus, planned.DamageResistanceBonus) + "\n" +
                Row("Accuracy", current.AccuracyBonus, planned.AccuracyBonus) + "\n" +
                Row("Min damage", current.MinimumDamage, planned.MinimumDamage) + "\n" +
                Row("Max damage", current.MaximumDamage, planned.MaximumDamage) + "\n" +
                ExternalEquipmentStatus + "\n" +
                "Planning only; the external game server decides equip legality and gameplay effects.";
            return;
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

    private void AddCombatActionRule()
    {
        var rule = new ClientCombatActionRule
        {
            Name = "Combat action",
            Command = "bash {target}",
            Priority = (CombatActionRules.Count + 1) * 10
        };
        CombatActionRules.Add(rule);
        SelectedCombatActionRule = rule;
    }

    private void DeleteCombatActionRule()
    {
        if (SelectedCombatActionRule is null) return;
        var index = CombatActionRules.IndexOf(SelectedCombatActionRule);
        CombatActionRules.Remove(SelectedCombatActionRule);
        SelectedCombatActionRule = CombatActionRules.ElementAtOrDefault(Math.Max(0, index - 1));
    }

    private void RefreshCombatEquipmentSetSelections()
    {
        var combat = _gameplayProfile.Combat;
        Set(ref _defaultCombatEquipmentSet,
            EquipmentSets.FirstOrDefault(set => set.Id.Equals(combat.DefaultEquipmentSetId, StringComparison.OrdinalIgnoreCase)),
            nameof(DefaultCombatEquipmentSet));
        Set(ref _multiTargetCombatEquipmentSet,
            EquipmentSets.FirstOrDefault(set => set.Id.Equals(combat.MultiTargetEquipmentSetId, StringComparison.OrdinalIgnoreCase)),
            nameof(MultiTargetCombatEquipmentSet));
        Set(ref _defensiveCombatEquipmentSet,
            EquipmentSets.FirstOrDefault(set => set.Id.Equals(combat.DefensiveEquipmentSetId, StringComparison.OrdinalIgnoreCase)),
            nameof(DefensiveCombatEquipmentSet));
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
                _gameplayAutomationState, _gameplayProfile, now, _gameplayAutomationRuntime),
            GameplayPlanArea.Party => ClientGameplayAutomationPlanner.PlanParty(
                _gameplayAutomationState, _gameplayProfile, now),
            _ => ClientGameplayActionPlan.None("No gameplay area was selected.")
        };
    }

    private static string FormatGameplayPlan(GameplayPlanArea area, ClientGameplayActionPlan plan)
    {
        var action = plan.Kind == ClientGameplayActionKind.EquipSet
            ? Environment.NewLine + "Equipment set: " + plan.EquipmentSetId
            : plan.HasAction
                ? Environment.NewLine + "Command: " + plan.Command
                : string.Empty;
        return $"{area}: {plan.Reason}{action}";
    }

    private async Task ExecuteCombatEquipmentSetAsync(ClientGameplayActionPlan gameplayPlan)
    {
        var gameplay = _gameplayAutomationState;
        var inventory = _inventoryState;
        var set = EquipmentSets.FirstOrDefault(candidate =>
            candidate.Enabled && candidate.Id.Equals(gameplayPlan.EquipmentSetId, StringComparison.OrdinalIgnoreCase));
        if (gameplay is null || inventory is null || set is null)
        {
            GameplayAssistantStatus = set is null
                ? $"Combat equipment set '{gameplayPlan.EquipmentSetId}' is unavailable. No command was sent."
                : "Refresh inventory and gameplay state before rotating combat equipment.";
            await RequestInventoryStateAsync(
                Volatile.Read(ref _clientConnectionGeneration), CancellationToken.None);
            return;
        }
        var inventoryAge = DateTimeOffset.UtcNow - inventory.CapturedAtUtc;
        if (!NormalizeWorldId(inventory.WorldId).Equals(NormalizeWorldId(gameplay.WorldId), StringComparison.OrdinalIgnoreCase) ||
            !inventory.CharacterId.Equals(gameplay.CharacterId, StringComparison.OrdinalIgnoreCase) ||
            !ClientRouteGraph.NormalizeRoomKey(inventory.RoomId).Equals(
                ClientRouteGraph.NormalizeRoomKey(gameplay.RoomId), StringComparison.OrdinalIgnoreCase) ||
            inventoryAge < TimeSpan.Zero ||
            inventoryAge > TimeSpan.FromSeconds(_gameplayProfile.StateMaximumAgeSeconds))
        {
            GameplayAssistantStatus = "Combat equipment state is stale or belongs to another session. Refreshing inventory.";
            await RequestInventoryStateAsync(
                Volatile.Read(ref _clientConnectionGeneration), CancellationToken.None);
            return;
        }

        var inventoryPlan = ClientInventoryAutomationPlanner.PlanEquipmentSet(inventory, _inventoryProfile, set);
        InventoryPlanText = FormatInventoryPlan(inventoryPlan);
        if (inventoryPlan.MissingItems.Count > 0 || inventoryPlan.Warnings.Count > 0)
        {
            GameplayAssistantStatus = $"Combat equipment set '{set.Name}' is incomplete or blocked. No command was sent.";
            return;
        }
        if (!inventoryPlan.HasCommands)
        {
            _gameplayAutomationRuntime.MarkExecuted(gameplayPlan, DateTimeOffset.UtcNow, gameplay.RoomId);
            GameplayAssistantStatus = $"Combat equipment already matches '{set.Name}'.";
            return;
        }
        if (!await ExecuteInventoryPlanAsync(inventoryPlan))
        {
            GameplayAssistantStatus = $"Combat equipment set '{set.Name}' was not applied.";
            return;
        }
        var appliedAt = DateTimeOffset.UtcNow;
        _lastGameplayActionAt = appliedAt;
        _gameplayAutomationRuntime.MarkExecuted(gameplayPlan, appliedAt, gameplay.RoomId);
        GameplayAssistantStatus = $"Combat equipment set '{set.Name}' applied; refreshing combat state.";
        await RequestGameplayAutomationStateAsync(
            Volatile.Read(ref _clientConnectionGeneration), CancellationToken.None);
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
        if (plan.Kind == ClientGameplayActionKind.EquipSet)
        {
            await ExecuteCombatEquipmentSetAsync(plan);
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
            _gameplayAutomationRuntime.MarkExecuted(plan, _lastGameplayActionAt.Value, snapshot.RoomId);
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
                    plan = ClientGameplayAutomationPlanner.PlanCombat(
                        _gameplayAutomationState, _gameplayProfile, now, _gameplayAutomationRuntime);
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
        RefreshSessionStatistics();
    }

    private async Task CastSelectedSpellAsync()
    {
        if (!SupportsPrivateRoomState() || _gameplayAutomationState is not { } state)
        {
            SpellActionStatus = "Direct spell actions require an authoritative RealmGate character session.";
            return;
        }
        if (!ClientSpellActions.TryCreateCastCommand(
                state.LearnedSpells, SelectedCharacterSpell, SpellTarget, out var command, out var error))
        {
            SpellActionStatus = error;
            return;
        }
        await _triggerGate.WaitAsync();
        try
        {
            if (!_terminalSession.IsConnected || !_gameplayState.GameplayReady)
            {
                SpellActionStatus = "Connect a ready character before casting.";
                return;
            }
            await _terminalSession.SendLineAsync(command, sensitive: false);
            _lastAutomationCommandAt = DateTimeOffset.UtcNow;
            SpellActionStatus = $"Sent: {command}";
        }
        catch (Exception exception)
        {
            SpellActionStatus = $"Spell command was not sent: {exception.Message}";
        }
        finally { _triggerGate.Release(); }
    }

    private void RefreshSessionStatistics()
    {
        var stats = _terminalSession.Statistics;
        static string Duration(TimeSpan value) => value.TotalHours >= 1
            ? $"{(int)value.TotalHours}h {value.Minutes}m {value.Seconds}s"
            : $"{value.Minutes}m {value.Seconds}s";
        SessionStatisticsText =
            $"Connection: {(stats.IsConnected ? "connected" : "disconnected")}\n" +
            $"Connected time: {Duration(stats.ConnectedDuration)}\n" +
            $"Attempts / successes / disconnects: {stats.ConnectionAttempts:N0} / {stats.SuccessfulConnections:N0} / {stats.Disconnects:N0}\n" +
            $"Commands sent: {stats.LinesSent:N0} ({stats.SensitiveLinesSent:N0} sensitive, content not retained)\n" +
            $"Characters sent / received: {stats.SentCharacters:N0} / {stats.ReceivedCharacters:N0}\n" +
            $"Visible chunks / private protocol messages: {stats.VisibleTextChunks:N0} / {stats.ProtocolMessages:N0}\n" +
            $"Transport errors: {stats.TransportErrors:N0}";
    }

    private async Task ExportSupportSnapshotAsync()
    {
        try
        {
            RunGameplayDiagnostics();
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RealmGate", "Diagnostics");
            var path = Path.Combine(folder, $"realmgate-support-{DateTime.Now:yyyyMMdd-HHmmss}.json");
            var version = typeof(MainWindowViewModel).Assembly.GetName().Version?.ToString() ?? "unknown";
            var snapshot = ClientSupportSnapshot.Create(version, _terminalSession.Statistics,
                GameplayDiagnosticChecks.ToArray());
            await snapshot.WriteAsync(path);
            SupportBundleStatus = $"Support snapshot saved to {path}";
        }
        catch (Exception exception)
        {
            SupportBundleStatus = $"Support snapshot failed: {exception.Message}";
        }
    }

    private async Task LoadCreatorStudioAsync()
    {
        try
        {
            CreatorStudioStatus = "Loading the Custom1 workspace...";
            var result = await Task.Run(() =>
            {
                var snapshot = _creatorStudio.Load();
                var families = _creatorStudio.GetEditableFamilies();
                return (snapshot, families);
            });
            CreatorFamilies.Clear();
            foreach (var family in result.families) CreatorFamilies.Add(family);
            CreatorOverrides.Clear();
            foreach (var item in result.snapshot.Overrides) CreatorOverrides.Add(item);
            SelectedCreatorFamily = CreatorFamilies.FirstOrDefault();
            CreatorWorldName = result.snapshot.Settings.Name;
            CreatorWorldDescription = result.snapshot.Settings.Description;
            CreatorWorldLoginBanner = result.snapshot.Settings.LoginBanner;
            CreatorWorldMotd = result.snapshot.Settings.Motd;
            CreatorWorldRuntimeEnabled = result.snapshot.Settings.RuntimeEnabled;
            CreatorWorldTransfersAllowed = result.snapshot.Settings.TransfersAllowed;
            CreatorWorldSettingsStatus = "Loaded Custom1 world settings. Saving requires CUSTOM1 confirmation and a stopped Host.";
            CreatorWorkspaceText =
                $"World: {result.snapshot.Settings.Name}\n" +
                $"Runtime: {(result.snapshot.Settings.RuntimeEnabled ? "enabled" : "disabled")}\n" +
                $"Test Realm data: {(result.snapshot.GeneratedDataReady ? "ready" : "build required")}\n" +
                $"Overrides: {result.snapshot.Overrides.Count:N0}\n" +
                $"Editable categories: {result.families.Count:N0}";
            CreatorStudioStatus = "Custom1 loaded read-only. Classic remains protected.";
        }
        catch (Exception exception)
        {
            CreatorStudioStatus = $"Creator Studio workspace could not be loaded: {exception.Message}";
        }
    }

    private async Task SearchCreatorObjectsAsync()
    {
        if (SelectedCreatorFamily is not { } family)
        {
            CreatorStudioStatus = "Load the workspace and select a category first.";
            return;
        }
        try
        {
            CreatorStudioStatus = $"Searching {family.Label.ToLowerInvariant()}...";
            var objects = await Task.Run(() =>
                _creatorStudio.BrowseObjects(family.Value, CreatorSearchText, 500));
            CreatorObjects.Clear();
            foreach (var item in objects) CreatorObjects.Add(item);
            SelectedCreatorObject = CreatorObjects.FirstOrDefault();
            CreatorStudioStatus = objects.Count == 500
                ? "Showing the first 500 matches. Refine the search to narrow the list."
                : $"Found {objects.Count:N0} {family.Label.ToLowerInvariant()} record(s).";
        }
        catch (Exception exception)
        {
            CreatorStudioStatus = $"Creator Studio search failed: {exception.Message}";
        }
    }

    private async Task SelectCreatorMdbAsync()
    {
        if (_creatorFilePicker is null)
        {
            CreatorSourceStatus = "File selection is unavailable in this host.";
            return;
        }

        var path = await _creatorFilePicker.PickMdbAsync();
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            CreatorSourceStatus = "Reading the MDB without modifying it...";
            var analysis = await Task.Run(() => _creatorMdbBridge.AnalyzeMdb(path));
            CreatorMdbPath = analysis.FilePath;
            _creatorMdbAnalysis = analysis;
            CreatorMdbTables.Clear();
            foreach (var table in analysis.Tables) CreatorMdbTables.Add(table);
            ClearCreatorPairValidation();
            CreatorSourceSummary = analysis.Summary;
            CreatorSourceStatus = "MDB analysis complete. Choose and verify its matching native source folder.";
        }
        catch (Exception exception)
        {
            CreatorSourceStatus = $"MDB analysis failed: {exception.Message}";
        }
    }

    private async Task SelectCreatorEvidenceFolderAsync()
    {
        if (_creatorFilePicker is null)
        {
            CreatorSourceStatus = "Folder selection is unavailable in this host.";
            return;
        }

        var folder = await _creatorFilePicker.PickNativeEvidenceFolderAsync();
        if (string.IsNullOrWhiteSpace(folder)) return;

        try
        {
            CreatorSourceStatus = "Inventorying native DAT/BIN evidence without modifying it...";
            var analysis = await Task.Run(() => _creatorMdbBridge.AnalyzeWccEvidenceFolder(folder));
            CreatorEvidenceFolder = analysis.Folder;
            _creatorEvidenceAnalysis = analysis;
            CreatorEvidenceFiles.Clear();
            foreach (var file in analysis.Files) CreatorEvidenceFiles.Add(file);
            ClearCreatorPairValidation();
            CreatorSourceSummary = analysis.Summary;
            CreatorSourceStatus = "Native evidence inventory complete. Validate the MDB/source pair before import.";
        }
        catch (Exception exception)
        {
            CreatorSourceStatus = $"Native evidence inventory failed: {exception.Message}";
        }
    }

    private async Task ValidateCreatorSourcesAsync()
    {
        if (string.IsNullOrWhiteSpace(CreatorMdbPath) ||
            string.IsNullOrWhiteSpace(CreatorEvidenceFolder))
        {
            CreatorSourceStatus = "Choose both the MDB and its matching WCC/WGS source folder first.";
            return;
        }

        try
        {
            CreatorSourceStatus = "Verifying MDB tables, native family coverage, and exact source anchors...";
            var result = await Task.Run(() =>
            {
                var mdb = _creatorMdbBridge.AnalyzeMdb(CreatorMdbPath);
                var evidence = _creatorMdbBridge.AnalyzeWccEvidenceFolder(CreatorEvidenceFolder);
                var pair = _creatorMdbBridge.ValidateMdbWccPair(CreatorMdbPath, CreatorEvidenceFolder);
                return (mdb, evidence, pair);
            });

            _creatorMdbAnalysis = result.mdb;
            _creatorEvidenceAnalysis = result.evidence;
            _creatorPairValidation = result.pair;
            CreatorImportAcknowledged = false;

            CreatorMdbTables.Clear();
            foreach (var table in result.mdb.Tables) CreatorMdbTables.Add(table);
            CreatorEvidenceFiles.Clear();
            foreach (var file in result.evidence.Files) CreatorEvidenceFiles.Add(file);

            CreatorSourceSummary =
                $"MDB: {result.mdb.Summary}\n" +
                $"Native source: {result.evidence.Summary}\n" +
                $"Pair verification: {result.pair.Summary}";
            CreatorSourceStatus = result.pair.CanImport
                ? "Source pair verified. Review the summary and confirm the Custom1-only import boundary to enable import."
                : "Source pair rejected. Import remains blocked.";
            Raise(nameof(CreatorImportReady));
        }
        catch (Exception exception)
        {
            _creatorPairValidation = null;
            CreatorImportAcknowledged = false;
            CreatorSourceStatus = $"Source verification failed: {exception.Message}";
            Raise(nameof(CreatorImportReady));
        }
    }

    private async Task ImportCreatorMdbAsync()
    {
        if (!CreatorImportReady)
        {
            CreatorSourceStatus = "Import is blocked until source verification passes and the Custom1-only acknowledgement is selected.";
            return;
        }

        try
        {
            CreatorSourceStatus = "Importing verified records into Custom1. Classic remains unchanged...";
            var result = await Task.Run(() =>
                _creatorMdbBridge.ImportMdb(CreatorMdbPath, CreatorEvidenceFolder));
            CreatorImportAcknowledged = false;
            CreatorSourceStatus = "Custom1 import completed. The source MDB and WCC/WGS files remained read-only.";
            CreatorSourceSummary = result.Summary;
            await LoadCreatorStudioAsync();
        }
        catch (Exception exception)
        {
            CreatorImportAcknowledged = false;
            CreatorSourceStatus = $"Custom1 import failed: {exception.Message}";
        }
    }

    private void ClearCreatorPairValidation()
    {
        _creatorPairValidation = null;
        CreatorImportAcknowledged = false;
        Raise(nameof(CreatorImportReady));
    }

    private async Task EditCreatorObjectAsync()
    {
        if (SelectedCreatorObject is not { } selected)
        {
            CreatorEditStatus = "Select a Custom1 record in Browse first.";
            return;
        }

        await LoadCreatorEditorAsync(selected.Family, selected.Id);
    }

    private async Task NewCreatorObjectAsync()
    {
        if (SelectedCreatorFamily is not { } family)
        {
            CreatorEditStatus = "Load the Custom1 workspace and choose a content category first.";
            return;
        }

        try
        {
            CreatorEditStatus = $"Preparing a new {family.Label.ToLowerInvariant()} record...";
            var form = await Task.Run(() => _creatorStudio.GetSimpleForm(family.Value));
            _creatorEditFamily = family.Value;
            CreatorEditObjectId = string.Empty;
            CreatorEditIsNew = true;
            CreatorEditorActive = true;
            CreatorEditTitle = $"New {form.SingularName}";
            CreatorEditFields.Clear();
            foreach (var field in form.Fields)
                CreatorEditFields.Add(new CreatorFieldEditorRow(field));
            CreatorEditStatus = "Enter the required fields. Saving writes a Custom1 override only; generation is a separate guarded action.";
        }
        catch (Exception exception)
        {
            CreatorEditStatus = $"New record could not be prepared: {exception.Message}";
        }
    }

    private async Task LoadCreatorEditorAsync(string family, string id)
    {
        try
        {
            CreatorEditStatus = $"Loading {id} from the effective Custom1 projection...";
            var result = await Task.Run(() =>
            {
                var form = _creatorStudio.GetSimpleForm(family, id);
                var loaded = _creatorStudio.LoadSimpleRecord(family, id);
                return (form, loaded);
            });

            _creatorEditFamily = family;
            CreatorEditObjectId = id;
            CreatorEditIsNew = false;
            CreatorEditorActive = true;
            CreatorEditTitle = $"Edit {result.form.SingularName}: {id}";
            CreatorEditFields.Clear();
            foreach (var field in result.form.Fields)
            {
                result.loaded.TextValues.TryGetValue(field.Path, out var textValue);
                result.loaded.BoolValues.TryGetValue(field.Path, out var boolValue);
                CreatorEditFields.Add(new CreatorFieldEditorRow(
                    field,
                    textValue,
                    field.Kind == CustomWorldSimpleFieldKind.Boolean ? boolValue : null));
            }
            CreatorEditStatus = result.loaded.ExistsInBase
                ? "Editing creates or updates a Custom1 override. The Classic base record remains unchanged."
                : "Editing this Custom1-only record. Save affects Custom1 only.";
        }
        catch (Exception exception)
        {
            CreatorEditStatus = $"Record could not be loaded: {exception.Message}";
        }
    }

    private async Task SaveCreatorObjectAsync()
    {
        if (!CreatorEditorActive || string.IsNullOrWhiteSpace(_creatorEditFamily))
        {
            CreatorEditStatus = "Open or create a record first.";
            return;
        }

        var changedPaths = CreatorEditFields
            .Where(field => field.IsDirty)
            .Select(field => field.Path)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!CreatorEditIsNew && changedPaths.Count == 0)
        {
            CreatorEditStatus = "No fields were changed.";
            return;
        }

        try
        {
            CreatorEditStatus = "Saving the Custom1 override...";
            var textValues = CreatorEditFields
                .Where(field => !field.IsBoolean)
                .ToDictionary(field => field.Path, field => field.ValueForSave(), StringComparer.OrdinalIgnoreCase);
            var boolValues = CreatorEditFields
                .Where(field => field.IsBoolean)
                .ToDictionary(field => field.Path, field => field.BoolValue, StringComparer.OrdinalIgnoreCase);
            var createNew = CreatorEditIsNew;
            var family = _creatorEditFamily;
            var result = await Task.Run(() => _creatorStudio.SaveSimpleRecord(
                family,
                CreatorEditObjectId,
                createNew,
                textValues,
                boolValues,
                changedPaths));

            CreatorValidationStatus = "Overrides changed. Validation must run again before generation.";
            await LoadCreatorEditorAsync(family, result.TargetId);
            await SearchCreatorObjectsAsync();
            SelectedCreatorObject = CreatorObjects.FirstOrDefault(item =>
                item.Id.Equals(result.TargetId, StringComparison.OrdinalIgnoreCase));
            CreatorEditStatus = $"Saved {result.TargetId} to Custom1 overrides. Validate and build when the change set is ready.";
        }
        catch (Exception exception)
        {
            CreatorEditStatus = $"Custom1 override was not saved: {exception.Message}";
        }
    }

    private async Task ValidateCreatorWorldAsync()
    {
        try
        {
            CreatorValidationStatus = "Validating the effective Custom1 world...";
            var issues = await Task.Run(() => _creatorStudio.ValidateCustomWorld(3000));
            SetCreatorValidationResults(issues);
        }
        catch (Exception exception)
        {
            CreatorValidationStatus = $"Custom1 validation failed: {exception.Message}";
        }
    }

    private async Task BuildCreatorWorldAsync()
    {
        try
        {
            CreatorValidationStatus = "Validating Custom1 before generation...";
            var issues = await Task.Run(() => _creatorStudio.ValidateCustomWorld(3000));
            SetCreatorValidationResults(issues);
            var errors = issues.Count(issue => issue.Severity.Equals("ERROR", StringComparison.OrdinalIgnoreCase));
            if (errors > 0)
            {
                CreatorValidationStatus += " Generation remains blocked until the errors are corrected.";
                return;
            }

            CreatorValidationStatus = "Validation passed without errors. Generating the standalone Custom1 Test Realm...";
            var result = await Task.Run(_creatorStudio.Build);
            var warnings = result.Diagnostics.Count(line =>
                line.StartsWith("Warning", StringComparison.OrdinalIgnoreCase));
            CreatorValidationStatus =
                $"Generated Custom1 Test Realm: {result.Families:N0} families, {result.Objects:N0} objects, " +
                $"{result.Files:N0} files, {warnings:N0} compiler warning(s). Live Custom1 was not changed.";
            _creatorPublishPreview = null;
            CreatorPublishAcknowledged = false;
            CreatorPublishStatus = "Test Realm rebuilt. Preview the live-world diff before publishing.";
            Raise(nameof(CreatorPublishReady));
            await LoadCreatorStudioAsync();
        }
        catch (Exception exception)
        {
            CreatorValidationStatus = $"Custom1 generation failed: {exception.Message}";
        }
    }

    private async Task PreviewCreatorPublishAsync()
    {
        try
        {
            CreatorPublishStatus = "Comparing the Custom1 Test Realm with the live world...";
            var preview = await Task.Run(_creatorStudio.PreviewTestRealmPublish);
            _creatorPublishPreview = preview;
            CreatorPublishAcknowledged = false;
            var examples = preview.Added.Concat(preview.Changed).Concat(preview.Removed).Take(8).ToArray();
            CreatorPublishStatus =
                $"Publish preview {preview.TestRealmFingerprint[..12]}: {preview.AddedFiles:N0} added, " +
                $"{preview.ChangedFiles:N0} changed, {preview.RemovedFiles:N0} removed file(s)." +
                (examples.Length == 0 ? " No file differences." : $"\nExamples: {string.Join(", ", examples)}");
            Raise(nameof(CreatorPublishReady));
        }
        catch (Exception exception)
        {
            _creatorPublishPreview = null;
            CreatorPublishAcknowledged = false;
            CreatorPublishStatus = $"Publish preview failed: {exception.Message}";
            Raise(nameof(CreatorPublishReady));
        }
    }

    private async Task StartCreatorTestRealmAsync()
    {
        if (_host.IsRunning)
        {
            CreatorPublishStatus = "Stop the currently running embedded Host before starting the Custom1 Test Realm.";
            return;
        }

        try
        {
            if (!int.TryParse(PortText, out var port))
                throw new InvalidOperationException("Port must be a number from 1 through 65535.");
            if (!int.TryParse(MaximumSessionsText, out var maximumSessions))
                throw new InvalidOperationException("Maximum sessions must be a number.");

            CreatorPublishStatus = "Validating and activating the isolated Custom1 Test Realm...";
            var preview = await Task.Run(_creatorStudio.PreviewTestRealmPublish);
            _creatorPublishPreview = preview;
            CreatorPublishAcknowledged = false;
            Raise(nameof(CreatorPublishReady));

            StatusMessage = "Starting Custom1 Test Realm";
            ListenerText = $"Requested Test Realm: {BindAddress}:{port}";
            await _host.StartAsync(new DesktopHostOptions(
                null,
                BindAddress,
                port,
                maximumSessions,
                TestRealmSourceWorldId: "Custom1"));
            _creatorTestRealmRunning = true;
            StatusMessage = _host.StatusMessage;
            ListenerText = $"Custom1 Test Realm on {BindAddress}:{port}";
            CreatorPublishStatus =
                $"Custom1 Test Realm is running from the staged {preview.TestRealmFingerprint[..12]} build. " +
                "Its characters and world state are isolated from live Custom1. Stop it before publishing.";
            RefreshLanEndpoints();
        }
        catch (Exception exception)
        {
            _creatorTestRealmRunning = false;
            StatusMessage = exception.Message;
            ListenerText = "Not listening";
            CreatorPublishStatus = $"Custom1 Test Realm did not start: {exception.Message}";
            AppendLog(exception.Message);
        }
    }

    private async Task StopCreatorTestRealmAsync()
    {
        if (!_host.IsRunning && !_creatorTestRealmRunning)
        {
            CreatorPublishStatus = "The Custom1 Test Realm is not running.";
            return;
        }

        try
        {
            StatusMessage = "Stopping Custom1 Test Realm";
            await _host.StopAsync();
            _creatorTestRealmRunning = false;
            StatusMessage = _host.StatusMessage;
            ListenerText = "Not listening";
            CreatorPublishStatus = "Custom1 Test Realm stopped. Preview and review the live-world diff before publishing.";
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
            CreatorPublishStatus = $"Custom1 Test Realm did not stop cleanly: {exception.Message}";
            AppendLog(exception.Message);
        }
    }

    private async Task PublishCreatorTestRealmAsync()
    {
        if (_host.IsRunning)
        {
            CreatorPublishStatus = "Stop the embedded Host before publishing Custom1.";
            return;
        }
        if (_creatorPublishPreview is null || !CreatorPublishAcknowledged)
        {
            CreatorPublishStatus = "Preview and acknowledge the current Test Realm diff before publishing.";
            return;
        }

        try
        {
            var expected = _creatorPublishPreview.TestRealmFingerprint;
            CreatorPublishStatus = "Publishing the reviewed Test Realm to live Custom1...";
            var result = await Task.Run(() => _creatorStudio.PublishTestRealm(expected, acknowledged: true));
            _creatorPublishPreview = null;
            CreatorPublishAcknowledged = false;
            CreatorPublishStatus =
                $"Published {result.PublishedFiles:N0} file(s) to live Custom1. " +
                (result.RollbackPath is null ? "This was the first live publication." : "A rollback snapshot was retained.");
            Raise(nameof(CreatorPublishReady));
        }
        catch (Exception exception)
        {
            CreatorPublishStatus = $"Custom1 was not published: {exception.Message}";
        }
    }

    private async Task RollbackCreatorPublishAsync()
    {
        if (_host.IsRunning)
        {
            CreatorPublishStatus = "Stop the embedded Host before rolling back Custom1.";
            return;
        }
        if (!CreatorPublishAcknowledged)
        {
            CreatorPublishStatus = "Acknowledge the rollback warning before restoring the latest snapshot.";
            return;
        }

        try
        {
            CreatorPublishStatus = "Restoring the latest Custom1 publish snapshot...";
            var result = await Task.Run(() => _creatorStudio.RollbackLatestPublish(acknowledged: true));
            _creatorPublishPreview = null;
            CreatorPublishAcknowledged = false;
            CreatorPublishStatus = "Latest Custom1 snapshot restored. The displaced live dataset was retained for recovery.";
            Raise(nameof(CreatorPublishReady));
        }
        catch (Exception exception)
        {
            CreatorPublishStatus = $"Custom1 rollback failed: {exception.Message}";
        }
    }

    private void SetCreatorValidationResults(IReadOnlyList<CustomWorldValidationIssue> issues)
    {
        CreatorValidationIssues.Clear();
        foreach (var issue in issues) CreatorValidationIssues.Add(issue);
        var errors = issues.Count(issue => issue.Severity.Equals("ERROR", StringComparison.OrdinalIgnoreCase));
        var warnings = issues.Count - errors;
        CreatorValidationStatus = errors == 0 && warnings == 0
            ? "PASS: no creator-facing reference or structural issues were found."
            : $"Validation finished with {errors:N0} error(s) and {warnings:N0} warning(s).";
    }

    private async Task ExportCreatorChangesAsync(string scope)
    {
        if (_creatorFilePicker is null)
        {
            CreatorPackageStatus = "Package file selection is unavailable in this host.";
            return;
        }

        var family = SelectedCreatorFamily?.Value ?? string.Empty;
        var record = SelectedCreatorObject;
        if (scope == "family" && family.Length == 0)
        {
            CreatorPackageStatus = "Choose a content category before exporting a family.";
            return;
        }
        if (scope == "record" && record is null)
        {
            CreatorPackageStatus = "Select a record before exporting one record.";
            return;
        }

        var suggested = scope switch
        {
            "record" => $"Custom1-{SafeFileName(record!.Family)}-{SafeFileName(record.Id)}.json",
            "family" => $"Custom1-{SafeFileName(family)}.json",
            _ => "Custom1-All-Changes.json"
        };
        var path = await _creatorFilePicker.PickCustomPackageExportAsync(suggested);
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            CreatorPackageStatus = "Exporting selected Custom1 overrides...";
            var count = await Task.Run(() => scope switch
            {
                "record" => _creatorStudio.ExportCustomChanges(path, record!.Family, [record.Id]),
                "family" => _creatorStudio.ExportCustomChanges(path, family),
                _ => _creatorStudio.ExportCustomChanges(path)
            });
            CreatorPackageStatus = $"Exported {count:N0} Custom1 change(s) to {path}. No generated data or Classic records were included.";
        }
        catch (Exception exception)
        {
            CreatorPackageStatus = $"Custom1 package export failed: {exception.Message}";
        }
    }

    private async Task PreviewCreatorPackageAsync()
    {
        if (_creatorFilePicker is null)
        {
            CreatorPackageStatus = "Package file selection is unavailable in this host.";
            return;
        }

        var path = await _creatorFilePicker.PickCustomPackageAsync();
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            CreatorPackageStatus = "Previewing the package without applying it...";
            var preview = await Task.Run(() => _creatorStudio.PreviewImportPackage(path));
            _creatorPendingPackagePreview = preview;
            CreatorPendingPackagePath = preview.FilePath;
            CreatorPackageApplyAcknowledged = false;
            CreatorPackageStatus = preview.InvalidRecords == 0
                ? preview.Summary + " Review conflicts and acknowledge the Custom1 replacement behavior to enable apply."
                : preview.Summary + " Apply remains blocked because the package contains invalid records.";
            Raise(nameof(CreatorPackageApplyReady));
        }
        catch (Exception exception)
        {
            ClearCreatorPackagePreview();
            CreatorPackageStatus = $"Custom1 package preview failed: {exception.Message}";
        }
    }

    private async Task ApplyCreatorPackageAsync()
    {
        if (!CreatorPackageApplyReady ||
            _creatorPendingPackagePreview is null ||
            string.IsNullOrWhiteSpace(CreatorPendingPackagePath))
        {
            CreatorPackageStatus = "Apply is blocked until a valid package is previewed and its Custom1 replacement behavior is acknowledged.";
            return;
        }

        try
        {
            var path = CreatorPendingPackagePath;
            CreatorPackageStatus = "Applying the previewed package to Custom1 overrides...";
            var applied = await Task.Run(() => _creatorStudio.ApplyImportPackage(path));
            ClearCreatorPackagePreview();
            CreatorPackageStatus = applied.Summary + " A pre-import backup was retained by the shared Creator service. Validate and generate explicitly when ready.";
            CreatorValidationStatus = "Overrides changed. Validation must run again before generation.";
            await LoadCreatorStudioAsync();
        }
        catch (Exception exception)
        {
            CreatorPackageApplyAcknowledged = false;
            CreatorPackageStatus = $"Custom1 package apply failed: {exception.Message}";
        }
    }

    private void ClearCreatorPackagePreview()
    {
        _creatorPendingPackagePreview = null;
        CreatorPendingPackagePath = string.Empty;
        CreatorPackageApplyAcknowledged = false;
        Raise(nameof(CreatorPackageApplyReady));
    }

    private static string SafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string((value ?? string.Empty)
            .Select(character => invalid.Contains(character) ? '-' : character)
            .ToArray()).Trim();
        return sanitized.Length == 0 ? "changes" : sanitized;
    }

    private async Task LoadCreatorRoomLinksAsync()
    {
        if (SelectedCreatorObject is not { } selected ||
            !selected.Family.Equals("rooms", StringComparison.OrdinalIgnoreCase))
        {
            CreatorRoomLinksStatus = "Select a room record in Browse first.";
            return;
        }

        try
        {
            CreatorRoomLinksStatus = $"Loading map-qualified connections for {selected.Id}...";
            var links = await Task.Run(() => _creatorStudio.LoadRoomMapLinks(selected.Id));
            CreatorRoomLinks.Clear();
            foreach (var link in links) CreatorRoomLinks.Add(new CreatorRoomLinkRow(link));
            CreatorSpecializedRoomId = selected.Id;
            CreatorRoomLinksStatus = $"Loaded {links.Count:N0} direction slots for {selected.Name}. Blank destinations remove that Custom1 connection when saved.";
        }
        catch (Exception exception)
        {
            CreatorRoomLinksStatus = $"Room connections could not be loaded: {exception.Message}";
        }
    }

    private async Task SaveCreatorRoomLinksAsync()
    {
        if (string.IsNullOrWhiteSpace(CreatorSpecializedRoomId) || CreatorRoomLinks.Count == 0)
        {
            CreatorRoomLinksStatus = "Load a room's connections before saving.";
            return;
        }

        try
        {
            var links = CreatorRoomLinks.Select(row => row.ToModel()).ToArray();
            CreatorRoomLinksStatus = "Saving map-qualified Custom1 room connections...";
            await Task.Run(() => _creatorStudio.SaveRoomMapLinks(CreatorSpecializedRoomId, links));
            CreatorRoomLinksStatus = $"Saved Custom1 connections for {CreatorSpecializedRoomId}. Validate before generation.";
            CreatorValidationStatus = "Overrides changed. Validation must run again before generation.";
        }
        catch (Exception exception)
        {
            CreatorRoomLinksStatus = $"Room connections were not saved: {exception.Message}";
        }
    }

    private async Task LoadCreatorShopInventoryAsync()
    {
        if (SelectedCreatorObject is not { } selected ||
            !selected.Family.Equals("shops", StringComparison.OrdinalIgnoreCase))
        {
            CreatorShopInventoryStatus = "Select a shop record in Browse first.";
            return;
        }

        try
        {
            CreatorShopInventoryStatus = $"Loading Custom1 inventory for {selected.Id}...";
            var entries = await Task.Run(() => _creatorStudio.LoadShopInventory(selected.Id));
            CreatorShopInventory.Clear();
            foreach (var entry in entries) CreatorShopInventory.Add(new CreatorShopInventoryRow(entry));
            SelectedCreatorShopInventoryRow = CreatorShopInventory.FirstOrDefault();
            CreatorSpecializedShopId = selected.Id;
            CreatorShopInventoryStatus = $"Loaded {entries.Count:N0} stock row(s) for {selected.Name}.";
        }
        catch (Exception exception)
        {
            CreatorShopInventoryStatus = $"Shop inventory could not be loaded: {exception.Message}";
        }
    }

    private async Task SaveCreatorShopInventoryAsync()
    {
        if (string.IsNullOrWhiteSpace(CreatorSpecializedShopId))
        {
            CreatorShopInventoryStatus = "Load a shop inventory before saving.";
            return;
        }

        try
        {
            var entries = CreatorShopInventory.Select(row => row.ToModel()).ToArray();
            CreatorShopInventoryStatus = "Saving Custom1 shop inventory...";
            await Task.Run(() => _creatorStudio.SaveShopInventory(CreatorSpecializedShopId, entries));
            CreatorShopInventoryStatus = $"Saved {entries.Count(entry => !string.IsNullOrWhiteSpace(entry.ItemRef)):N0} stock row(s) for {CreatorSpecializedShopId}. Validate before generation.";
            CreatorValidationStatus = "Overrides changed. Validation must run again before generation.";
        }
        catch (Exception exception)
        {
            CreatorShopInventoryStatus = $"Shop inventory was not saved: {exception.Message}";
        }
    }

    private void RemoveCreatorShopInventoryRow()
    {
        if (SelectedCreatorShopInventoryRow is null) return;
        CreatorShopInventory.Remove(SelectedCreatorShopInventoryRow);
        SelectedCreatorShopInventoryRow = CreatorShopInventory.FirstOrDefault();
        CreatorShopInventoryStatus = "Stock row removed from the editor. Save to write the Custom1 override.";
    }

    private async Task LoadCreatorQuestStagesAsync()
    {
        if (SelectedCreatorObject is not { } selected ||
            !selected.Family.Equals("quests", StringComparison.OrdinalIgnoreCase))
        {
            CreatorQuestScriptStatus = "Select a quest record in Browse first.";
            return;
        }

        try
        {
            CreatorQuestScriptStatus = $"Loading executable stages for {selected.Name}...";
            var stages = await Task.Run(() => _creatorStudio.LoadQuestStages(selected.Id));
            CreatorQuestStages.Clear();
            foreach (var stage in stages) CreatorQuestStages.Add(new CreatorQuestStageRow(stage));
            SelectedCreatorQuestStageRow = CreatorQuestStages.FirstOrDefault();
            CreatorSpecializedQuestId = selected.Id;
            CreatorQuestScriptStatus = stages.Count == 0
                ? $"{selected.Name} has no executable stage flow in the effective Custom1 data. It may be evidence-only."
                : $"Loaded {stages.Count:N0} executable stage(s) for {selected.Name}. Select a stage to inspect its runtime script.";
        }
        catch (Exception exception)
        {
            CreatorQuestStages.Clear();
            SelectedCreatorQuestStageRow = null;
            CreatorQuestScriptStatus = $"Quest stages could not be loaded: {exception.Message}";
        }
    }

    private async Task LoadCreatorSelectedScriptAsync()
    {
        if (SelectedCreatorObject is not { } selected ||
            !selected.Family.Equals("scripts", StringComparison.OrdinalIgnoreCase))
        {
            CreatorQuestScriptStatus = "Select a script record in Browse first.";
            return;
        }

        await LoadCreatorScriptEditorAsync(selected.Id);
    }

    private async Task LoadCreatorQuestStageScriptAsync()
    {
        if (SelectedCreatorQuestStageRow is not { ScriptId.Length: > 0 } stage)
        {
            CreatorQuestScriptStatus = "The selected quest stage has no linked executable script.";
            return;
        }

        await LoadCreatorScriptEditorAsync(stage.ScriptId);
    }

    private async Task LoadCreatorScriptEditorAsync(string scriptId)
    {
        try
        {
            CreatorQuestScriptStatus = $"Loading runtime operations for {scriptId}...";
            var operations = await Task.Run(() => _creatorStudio.LoadScriptOperations(scriptId));
            CreatorScriptOperations.Clear();
            foreach (var operation in operations) CreatorScriptOperations.Add(new CreatorScriptOperationRow(operation, CreatorScriptOpcodes));
            SelectedCreatorScriptOperationRow = CreatorScriptOperations.FirstOrDefault();
            CreatorSpecializedScriptId = scriptId;
            CreatorQuestScriptStatus =
                $"Loaded {operations.Count:N0} runtime-bound operation(s) for {scriptId}. Unsupported opcodes remain protected.";
        }
        catch (Exception exception)
        {
            CreatorScriptOperations.Clear();
            SelectedCreatorScriptOperationRow = null;
            CreatorQuestScriptStatus = $"Script operations could not be loaded: {exception.Message}";
        }
    }

    private void AddCreatorScriptOperation()
    {
        var line = SelectedCreatorScriptOperationRow?.Line ??
                   CreatorScriptOperations.Select(row => row.Line).DefaultIfEmpty(1).Max();
        var sequence = CreatorScriptOperations
            .Where(row => row.Line == line)
            .Select(row => row.Sequence)
            .DefaultIfEmpty(0)
            .Max() + 1;
        var row = new CreatorScriptOperationRow(Math.Max(1, line), Math.Max(1, sequence), CreatorScriptOpcodes);
        CreatorScriptOperations.Add(row);
        SelectedCreatorScriptOperationRow = row;
        CreatorQuestScriptStatus = "Operation added in the editor. Choose a runtime-supported opcode and save to write the Custom1 override.";
    }

    private void RemoveCreatorScriptOperation()
    {
        if (SelectedCreatorScriptOperationRow is null) return;
        CreatorScriptOperations.Remove(SelectedCreatorScriptOperationRow);
        SelectedCreatorScriptOperationRow = CreatorScriptOperations.FirstOrDefault();
        CreatorQuestScriptStatus = "Operation removed in the editor. Save operations to write the Custom1 override.";
    }

    private async Task SaveCreatorScriptOperationsAsync()
    {
        if (string.IsNullOrWhiteSpace(CreatorSpecializedScriptId))
        {
            CreatorQuestScriptStatus = "Load a script before saving operations.";
            return;
        }

        try
        {
            var rows = CreatorScriptOperations.Select(row => row.ToModel()).ToArray();
            CreatorQuestScriptStatus = "Saving guarded Custom1 script operations...";
            await Task.Run(() => _creatorStudio.SaveScriptOperations(CreatorSpecializedScriptId, rows));
            CreatorValidationStatus = "Overrides changed. Validation must run again before generation.";
            await LoadCreatorScriptEditorAsync(CreatorSpecializedScriptId);
            CreatorQuestScriptStatus =
                $"Saved {rows.Length:N0} operation(s) for {CreatorSpecializedScriptId}. Validate before generation.";
        }
        catch (Exception exception)
        {
            CreatorQuestScriptStatus = $"Script operations were not saved: {exception.Message}";
        }
    }

    private async Task LoadCreatorNpcEditorAsync()
    {
        if (SelectedCreatorObject is not { } selected ||
            !selected.Family.Equals("npcs", StringComparison.OrdinalIgnoreCase))
        {
            CreatorNpcEditorStatus = "Select an NPC record in Browse first.";
            return;
        }

        try
        {
            CreatorNpcEditorStatus = $"Loading attacks and abilities for {selected.Id}...";
            var result = await Task.Run(() => (
                Attacks: _creatorStudio.LoadNpcAttacks(selected.Id),
                Abilities: _creatorStudio.LoadNpcAbilities(selected.Id)));
            CreatorNpcAttacks.Clear();
            foreach (var attack in result.Attacks) CreatorNpcAttacks.Add(new CreatorNpcAttackRow(attack));
            CreatorNpcAbilities.Clear();
            foreach (var ability in result.Abilities) CreatorNpcAbilities.Add(new CreatorNpcAbilityRow(ability));
            SelectedCreatorNpcAttackRow = CreatorNpcAttacks.FirstOrDefault();
            SelectedCreatorNpcAbilityRow = CreatorNpcAbilities.FirstOrDefault();
            CreatorSpecializedNpcId = selected.Id;
            CreatorNpcEditorStatus =
                $"Loaded {result.Attacks.Count:N0} attack(s) and {result.Abilities.Count:N0} ability row(s) for {selected.Name}.";
        }
        catch (Exception exception)
        {
            CreatorNpcEditorStatus = $"NPC collections could not be loaded: {exception.Message}";
        }
    }

    private async Task SaveCreatorNpcAttacksAsync()
    {
        if (string.IsNullOrWhiteSpace(CreatorSpecializedNpcId))
        {
            CreatorNpcEditorStatus = "Load an NPC before saving attacks.";
            return;
        }

        try
        {
            var rows = CreatorNpcAttacks.Select(row => row.ToModel()).ToArray();
            CreatorNpcEditorStatus = "Saving Custom1 NPC attacks...";
            await Task.Run(() => _creatorStudio.SaveNpcAttacks(CreatorSpecializedNpcId, rows));
            CreatorNpcEditorStatus = $"Saved {rows.Length:N0} attack(s) for {CreatorSpecializedNpcId}. Validate before generation.";
            CreatorValidationStatus = "Overrides changed. Validation must run again before generation.";
        }
        catch (Exception exception)
        {
            CreatorNpcEditorStatus = $"NPC attacks were not saved: {exception.Message}";
        }
    }

    private async Task SaveCreatorNpcAbilitiesAsync()
    {
        if (string.IsNullOrWhiteSpace(CreatorSpecializedNpcId))
        {
            CreatorNpcEditorStatus = "Load an NPC before saving abilities.";
            return;
        }

        try
        {
            var rows = CreatorNpcAbilities.Select(row => row.ToModel()).ToArray();
            CreatorNpcEditorStatus = "Saving Custom1 NPC abilities...";
            await Task.Run(() => _creatorStudio.SaveNpcAbilities(CreatorSpecializedNpcId, rows));
            CreatorNpcEditorStatus = $"Saved {rows.Length:N0} ability row(s) for {CreatorSpecializedNpcId}. Validate before generation.";
            CreatorValidationStatus = "Overrides changed. Validation must run again before generation.";
        }
        catch (Exception exception)
        {
            CreatorNpcEditorStatus = $"NPC abilities were not saved: {exception.Message}";
        }
    }

    private void AddCreatorNpcAttack()
    {
        var nextSlot = CreatorNpcAttacks.Select(row => row.Slot).DefaultIfEmpty(0).Max() + 1;
        var row = new CreatorNpcAttackRow(nextSlot);
        CreatorNpcAttacks.Add(row);
        SelectedCreatorNpcAttackRow = row;
        CreatorNpcEditorStatus = "Attack row added in the editor. Save attacks to write the Custom1 override.";
    }

    private void RemoveCreatorNpcAttack()
    {
        if (SelectedCreatorNpcAttackRow is null) return;
        CreatorNpcAttacks.Remove(SelectedCreatorNpcAttackRow);
        SelectedCreatorNpcAttackRow = CreatorNpcAttacks.FirstOrDefault();
        CreatorNpcEditorStatus = "Attack row removed in the editor. Save attacks to write the Custom1 override.";
    }

    private void RemoveCreatorNpcAbility()
    {
        if (SelectedCreatorNpcAbilityRow is null) return;
        CreatorNpcAbilities.Remove(SelectedCreatorNpcAbilityRow);
        SelectedCreatorNpcAbilityRow = CreatorNpcAbilities.FirstOrDefault();
        CreatorNpcEditorStatus = "Ability row removed in the editor. Save abilities to write the Custom1 override.";
    }

    private async Task RunCreatorNpcSimulationAsync()
    {
        if (SelectedCreatorObject is not { } selected ||
            !selected.Family.Equals("npcs", StringComparison.OrdinalIgnoreCase))
        {
            CreatorNpcSimulationStatus = "Select an NPC record in Browse first.";
            return;
        }

        try
        {
            CreatorNpcSimulationStatus = $"Running deterministic runtime combat simulation for {selected.Name}...";
            var result = await Task.Run(() => _creatorStudio.SimulateNpcCombat(
                selected.Id,
                CreatorNpcTargetDefense,
                CreatorNpcTargetArmorClass,
                CreatorNpcTargetDamageResistance,
                CreatorNpcTargetDodge,
                CreatorNpcSimulationRounds));
            CreatorNpcSimulationRows.Clear();
            foreach (var row in result.Attacks) CreatorNpcSimulationRows.Add(row);
            CreatorNpcSimulationStatus =
                $"{result.NpcName}: {result.Rounds:N0} rounds, {result.TotalSelections:N0} selected attacks; " +
                $"physical hit/miss/dodge {result.PhysicalHits:N0}/{result.PhysicalMisses:N0}/{result.PhysicalDodges:N0}; " +
                $"spell casts/fails {result.SpellCasts:N0}/{result.SpellFailures:N0}; " +
                $"physical damage {result.TotalPhysicalDamage:N0} ({result.AveragePhysicalDamagePerRound:0.00}/round). {result.Notes}";
        }
        catch (Exception exception)
        {
            CreatorNpcSimulationRows.Clear();
            CreatorNpcSimulationStatus = $"NPC simulation failed: {exception.Message}";
        }
    }

    private async Task RunCreatorWeaponSimulationAsync()
    {
        if (SelectedCreatorObject is not { } selected ||
            !selected.Family.Equals("items", StringComparison.OrdinalIgnoreCase))
        {
            CreatorWeaponSimulationStatus = "Select an item record in Browse first.";
            return;
        }

        try
        {
            CreatorWeaponSimulationStatus = $"Running deterministic runtime weapon simulation for {selected.Name}...";
            var result = await Task.Run(() => _creatorStudio.SimulateWeapon(
                selected.Id,
                CreatorWeaponBaseAttack,
                CreatorWeaponTargetDefense,
                CreatorWeaponTargetArmorClass,
                CreatorWeaponTargetDamageResistance,
                CreatorWeaponTargetDodge,
                CreatorWeaponCriticalChance,
                CreatorWeaponSimulationSwings));
            CreatorWeaponSimulationStatus =
                $"{result.ItemName}: damage {result.MinimumDamage}-{result.MaximumDamage}, accuracy {result.Accuracy:+#;-#;0}, speed {result.Speed}; " +
                $"effective attack {result.EffectiveAttackValue}, hit chance {result.HitChance}%. " +
                $"Across {result.Swings:N0} swings: {result.Hits:N0} hits, {result.Misses:N0} misses, " +
                $"{result.Dodges:N0} dodges, {result.Criticals:N0} criticals; " +
                $"{result.AverageDamagePerSwing:0.00} damage/swing and {result.AverageDamageOnHit:0.00} damage/hit. {result.Notes}";
        }
        catch (Exception exception)
        {
            CreatorWeaponSimulationStatus = $"Weapon simulation failed: {exception.Message}";
        }
    }

    private async Task LoadCreatorMapLayoutAsync()
    {
        try
        {
            var focusRoom = SelectedCreatorObject is { } selected &&
                            selected.Family.Equals("rooms", StringComparison.OrdinalIgnoreCase)
                ? selected.Id
                : null;
            CreatorMapLayoutStatus = $"Loading map {CreatorSelectedMapNumber} from the effective Custom1 world...";
            var snapshot = await Task.Run(() => _creatorStudio.LoadVisualMap(
                focusRoom,
                CreatorSelectedMapNumber > 0 ? CreatorSelectedMapNumber : null));
            CreatorMapNumbers.Clear();
            foreach (var map in snapshot.AvailableMaps) CreatorMapNumbers.Add(map);
            if (!CreatorMapNumbers.Contains(snapshot.MapNumber)) CreatorMapNumbers.Add(snapshot.MapNumber);
            CreatorSelectedMapNumber = snapshot.MapNumber;
            CreatorMapRooms.Clear();
            foreach (var room in snapshot.Rooms) CreatorMapRooms.Add(new CreatorMapRoomRow(room));
            CreatorMapEdges.Clear();
            foreach (var edge in snapshot.Edges) CreatorMapEdges.Add(new CreatorMapEdgeRow(edge));
            CreatorMapLayoutStatus =
                $"Map {snapshot.MapNumber}: {snapshot.Rooms.Count:N0} room(s), {snapshot.Edges.Count:N0} resolved directional connection(s). " +
                "Coordinate edits affect Creator layout only, not room gameplay data.";
        }
        catch (Exception exception)
        {
            CreatorMapRooms.Clear();
            CreatorMapEdges.Clear();
            CreatorMapLayoutStatus = $"Map layout could not be loaded: {exception.Message}";
        }
    }

    private async Task SaveCreatorMapLayoutAsync()
    {
        if (CreatorSelectedMapNumber <= 0 || CreatorMapRooms.Count == 0)
        {
            CreatorMapLayoutStatus = "Load a map before saving its layout.";
            return;
        }

        try
        {
            var mapNumber = CreatorSelectedMapNumber;
            var positions = CreatorMapRooms.Select(room => room.ToPosition()).ToArray();
            CreatorMapLayoutStatus = $"Saving Creator layout coordinates for map {mapNumber}...";
            await Task.Run(() => _creatorStudio.SaveVisualMapLayout(mapNumber, positions));
            CreatorMapLayoutStatus = $"Saved {positions.Length:N0} room coordinate(s) for map {mapNumber}. Gameplay room and exit data were not changed.";
        }
        catch (Exception exception)
        {
            CreatorMapLayoutStatus = $"Map layout was not saved: {exception.Message}";
        }
    }

    private async Task PreviewCreatorMapAutoArrangeAsync()
    {
        try
        {
            var focusRoom = SelectedCreatorObject is { } selected &&
                            selected.Family.Equals("rooms", StringComparison.OrdinalIgnoreCase)
                ? selected.Id
                : null;
            var mapNumber = CreatorSelectedMapNumber > 0 ? CreatorSelectedMapNumber : (int?)null;
            CreatorMapLayoutStatus = "Preparing a non-destructive auto-arrange preview...";
            var snapshot = await Task.Run(() => _creatorStudio.LoadVisualMap(
                focusRoom,
                mapNumber,
                includeSavedLayout: false));
            CreatorMapNumbers.Clear();
            foreach (var map in snapshot.AvailableMaps) CreatorMapNumbers.Add(map);
            if (!CreatorMapNumbers.Contains(snapshot.MapNumber)) CreatorMapNumbers.Add(snapshot.MapNumber);
            CreatorSelectedMapNumber = snapshot.MapNumber;
            CreatorMapRooms.Clear();
            foreach (var room in snapshot.Rooms) CreatorMapRooms.Add(new CreatorMapRoomRow(room));
            CreatorMapEdges.Clear();
            foreach (var edge in snapshot.Edges) CreatorMapEdges.Add(new CreatorMapEdgeRow(edge));
            CreatorMapLayoutStatus =
                $"Auto-arrange preview prepared for map {snapshot.MapNumber} ({snapshot.Rooms.Count:N0} rooms). " +
                "No saved coordinates were changed. Review the values and select Save coordinates to accept them.";
        }
        catch (Exception exception)
        {
            CreatorMapEdges.Clear();
            CreatorMapLayoutStatus = $"Auto-arrange preview failed: {exception.Message}";
        }
    }

    private async Task SearchCreatorWorldAsync()
    {
        if (string.IsNullOrWhiteSpace(CreatorWorldToolSearchText))
        {
            CreatorWorldToolStatus = "Enter a name, ID, or gameplay value to search the effective Custom1 world.";
            return;
        }

        try
        {
            CreatorWorldToolStatus = "Searching all editable Custom1 content...";
            var results = await Task.Run(() =>
                _creatorStudio.GlobalSearch(CreatorWorldToolSearchText, 1000));
            CreatorGlobalSearchResults.Clear();
            foreach (var result in results) CreatorGlobalSearchResults.Add(result);
            SelectedCreatorGlobalSearchHit = CreatorGlobalSearchResults.FirstOrDefault();
            CreatorWorldToolStatus = results.Count == 1000
                ? "Showing the first 1,000 matches. Refine the search to narrow the result."
                : $"Found {results.Count:N0} record(s) across the effective Custom1 world.";
        }
        catch (Exception exception)
        {
            CreatorGlobalSearchResults.Clear();
            SelectedCreatorGlobalSearchHit = null;
            CreatorWorldToolStatus = $"World search failed: {exception.Message}";
        }
    }

    private async Task FindCreatorReferencesAsync()
    {
        if (SelectedCreatorObject is not { } selected)
        {
            CreatorWorldToolStatus = "Select a record in Browse Custom1 before checking where it is used.";
            return;
        }

        try
        {
            CreatorWorldToolStatus = $"Finding effective Custom1 references to {selected.Id}...";
            var results = await Task.Run(() =>
                _creatorStudio.FindReferences(selected.Family, selected.Id, 1000));
            CreatorReferenceResults.Clear();
            foreach (var result in results) CreatorReferenceResults.Add(result);
            SelectedCreatorReferenceHit = CreatorReferenceResults.FirstOrDefault();
            CreatorWorldToolStatus = results.Count == 1000
                ? $"Showing the first 1,000 references to {selected.Id}."
                : $"Found {results.Count:N0} reference(s) to {selected.Name} ({selected.Id}).";
        }
        catch (Exception exception)
        {
            CreatorReferenceResults.Clear();
            SelectedCreatorReferenceHit = null;
            CreatorWorldToolStatus = $"Reference search failed: {exception.Message}";
        }
    }

    private async Task LoadCreatorKeyDoorsAsync()
    {
        try
        {
            CreatorWorldToolStatus = "Indexing keyed exits and their effective Custom1 item references...";
            var results = await Task.Run(() =>
                _creatorStudio.BrowseKeyDoors(CreatorWorldToolSearchText, 2000));
            CreatorKeyDoors.Clear();
            foreach (var result in results) CreatorKeyDoors.Add(result);
            SelectedCreatorKeyDoor = CreatorKeyDoors.FirstOrDefault();
            CreatorWorldToolStatus = $"Loaded {results.Count:N0} keyed exit(s).";
        }
        catch (Exception exception)
        {
            CreatorKeyDoors.Clear();
            SelectedCreatorKeyDoor = null;
            CreatorWorldToolStatus = $"Keyed-door index failed: {exception.Message}";
        }
    }

    private async Task LoadCreatorMonsterIndexAsync()
    {
        try
        {
            CreatorWorldToolStatus = "Building the effective Custom1 monster and spawn index...";
            var results = await Task.Run(() =>
                _creatorStudio.BrowseMonsterIndex(CreatorWorldToolSearchText, limit: 5000));
            CreatorMonsterIndex.Clear();
            foreach (var result in results) CreatorMonsterIndex.Add(result);
            SelectedCreatorMonsterIndex = CreatorMonsterIndex.FirstOrDefault();
            CreatorWorldToolStatus = $"Loaded {results.Count:N0} monster/NPC index row(s).";
        }
        catch (Exception exception)
        {
            CreatorMonsterIndex.Clear();
            SelectedCreatorMonsterIndex = null;
            CreatorWorldToolStatus = $"Monster index failed: {exception.Message}";
        }
    }

    private async Task LoadCreatorControlRoomsAsync()
    {
        try
        {
            CreatorWorldToolStatus = "Indexing rooms controlled by interactions, triggers, and scripts...";
            var results = await Task.Run(() =>
                _creatorStudio.BrowseControlRooms(CreatorWorldToolSearchText, 5000));
            CreatorControlRooms.Clear();
            foreach (var result in results) CreatorControlRooms.Add(result);
            SelectedCreatorControlRoom = CreatorControlRooms.FirstOrDefault();
            CreatorWorldToolStatus = $"Loaded {results.Count:N0} control-room row(s).";
        }
        catch (Exception exception)
        {
            CreatorControlRooms.Clear();
            SelectedCreatorControlRoom = null;
            CreatorWorldToolStatus = $"Control-room index failed: {exception.Message}";
        }
    }

    private async Task OpenCreatorToolRecordAsync(string? family, string? id)
    {
        if (string.IsNullOrWhiteSpace(family) || string.IsNullOrWhiteSpace(id))
        {
            CreatorWorldToolStatus = "Select a result before opening its Custom1 record.";
            return;
        }

        try
        {
            if (CreatorFamilies.Count == 0) await LoadCreatorStudioAsync();
            var familyOption = CreatorFamilies.FirstOrDefault(option =>
                option.Value.Equals(family, StringComparison.OrdinalIgnoreCase));
            if (familyOption is null)
                throw new InvalidOperationException($"The '{family}' category is not editable in Creator Studio.");

            SelectedCreatorFamily = familyOption;
            CreatorSearchText = id;
            await SearchCreatorObjectsAsync();
            SelectedCreatorObject = CreatorObjects.FirstOrDefault(item =>
                item.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (SelectedCreatorObject is null)
                throw new InvalidOperationException($"The effective Custom1 record '{family}/{id}' was not found.");

            await LoadCreatorEditorAsync(family, id);
            CreatorWorldToolStatus = $"Opened {family}/{id} in Edit and Build. Classic remains unchanged.";
        }
        catch (Exception exception)
        {
            CreatorWorldToolStatus = $"Record could not be opened: {exception.Message}";
        }
    }

    private void LoadCreatorParityAudit()
    {
        CreatorParityAuditRows.Clear();
        foreach (var row in _creatorStudio.BuildNightmareParityAudit()) CreatorParityAuditRows.Add(row);
        CreatorMaintenanceStatus = $"Loaded {CreatorParityAuditRows.Count:N0} Creator capability comparison row(s). This report is read-only.";
    }

    private async Task ApplyCreatorBulkSetAsync()
    {
        try
        {
            EnsureCreatorMaintenanceAuthorized(requireHostStopped: false);
            var targets = SplitCreatorMaintenanceLines(CreatorBulkTargetIds).ToArray();
            CreatorMaintenanceStatus = "Applying the confirmed Custom1 bulk field change...";
            var result = await Task.Run(() => _creatorStudio.BulkSetField(
                CreatorBulkFamily,
                targets,
                CreatorBulkFieldPath,
                CreatorBulkValue,
                CreatorBulkValueKind));
            CompleteCreatorOverrideMaintenance(result.Summary);
        }
        catch (Exception exception)
        {
            CreatorMaintenanceStatus = $"Bulk set was not applied: {exception.Message}";
        }
    }

    private async Task ApplyCreatorBulkCopyAsync()
    {
        try
        {
            EnsureCreatorMaintenanceAuthorized(requireHostStopped: false);
            var targets = SplitCreatorMaintenanceLines(CreatorBulkTargetIds).ToArray();
            CreatorMaintenanceStatus = "Copying the confirmed field into Custom1 overrides...";
            var result = await Task.Run(() => _creatorStudio.BulkCopyField(
                CreatorBulkFamily,
                CreatorBulkSourceId,
                targets,
                CreatorBulkFieldPath));
            CompleteCreatorOverrideMaintenance(result.Summary);
        }
        catch (Exception exception)
        {
            CreatorMaintenanceStatus = $"Bulk copy was not applied: {exception.Message}";
        }
    }

    private async Task SearchCreatorCoinRoomsAsync()
    {
        try
        {
            var rows = await Task.Run(() => _creatorStudio.BrowseObjects("rooms", CreatorMaintenanceSearch, 2000));
            CreatorCoinRooms.Clear();
            foreach (var row in rows) CreatorCoinRooms.Add(row);
            SelectedCreatorCoinRoom = CreatorCoinRooms.FirstOrDefault();
            CreatorMaintenanceStatus = $"Loaded {rows.Count:N0} room(s). Currency changes affect only stopped Custom1 living-world state.";
        }
        catch (Exception exception)
        {
            CreatorMaintenanceStatus = $"Room search failed: {exception.Message}";
        }
    }

    private async Task LoadCreatorRoomCurrencyAsync()
    {
        if (SelectedCreatorCoinRoom is null)
        {
            CreatorMaintenanceStatus = "Select a Custom1 room first.";
            return;
        }

        try
        {
            var value = await Task.Run(() => _creatorStudio.GetCustom1RoomCurrency(SelectedCreatorCoinRoom.Id));
            CreatorCoinRunic = value.RunicCoins.ToString();
            CreatorCoinPlatinum = value.PlatinumPieces.ToString();
            CreatorCoinGold = value.GoldCrowns.ToString();
            CreatorCoinSilver = value.SilverNobles.ToString();
            CreatorCoinCopper = value.CopperFarthings.ToString();
            CreatorMaintenanceStatus = $"Loaded current Custom1 currency for {SelectedCreatorCoinRoom.Name}.";
        }
        catch (Exception exception)
        {
            CreatorMaintenanceStatus = $"Room currency could not be loaded: {exception.Message}";
        }
    }

    private async Task SaveCreatorRoomCurrencyAsync()
    {
        if (SelectedCreatorCoinRoom is null)
        {
            CreatorMaintenanceStatus = "Select a Custom1 room first.";
            return;
        }

        try
        {
            EnsureCreatorMaintenanceAuthorized(requireHostStopped: true);
            var roomId = SelectedCreatorCoinRoom.Id;
            var runic = ParseCreatorCoin(CreatorCoinRunic);
            var platinum = ParseCreatorCoin(CreatorCoinPlatinum);
            var gold = ParseCreatorCoin(CreatorCoinGold);
            var silver = ParseCreatorCoin(CreatorCoinSilver);
            var copper = ParseCreatorCoin(CreatorCoinCopper);
            var value = await Task.Run(() => _creatorStudio.SetCustom1RoomCurrency(roomId, runic, platinum, gold, silver, copper));
            CreatorMaintenanceConfirmation = string.Empty;
            CreatorMaintenanceStatus = $"Saved stopped Custom1 room currency: {RuntimeClassicRoomCurrencyStore.Format(value)}";
        }
        catch (Exception exception)
        {
            CreatorMaintenanceStatus = $"Room currency was not saved: {exception.Message}";
        }
    }

    private async Task SearchCreatorShopResetsAsync()
    {
        try
        {
            var rows = await Task.Run(() => _creatorStudio.BrowseObjects("shops", CreatorMaintenanceSearch, 2000));
            CreatorShopResetRecords.Clear();
            foreach (var row in rows) CreatorShopResetRecords.Add(row);
            SelectedCreatorShopReset = CreatorShopResetRecords.FirstOrDefault();
            CreatorMaintenanceStatus = $"Loaded {rows.Count:N0} shop/service record(s). Reset uses generated normal stock and requires a stopped Host.";
        }
        catch (Exception exception)
        {
            CreatorMaintenanceStatus = $"Shop search failed: {exception.Message}";
        }
    }

    private async Task ResetCreatorShopStockAsync()
    {
        if (SelectedCreatorShopReset is null)
        {
            CreatorMaintenanceStatus = "Select a Custom1 shop first.";
            return;
        }

        try
        {
            EnsureCreatorMaintenanceAuthorized(requireHostStopped: true);
            var selected = SelectedCreatorShopReset;
            var result = await Task.Run(() => _creatorStudio.ResetCustom1ShopStock(selected.Id));
            CreatorMaintenanceConfirmation = string.Empty;
            CreatorMaintenanceStatus =
                $"Reset {selected.Name}: {result.StockEntries:N0} stock line(s), {result.TotalQuantity:N0} total item(s). Classic was not touched.";
        }
        catch (Exception exception)
        {
            CreatorMaintenanceStatus = $"Shop stock was not reset: {exception.Message}";
        }
    }

    private async Task SearchCreatorCleanupRoomsAsync()
    {
        try
        {
            var rows = await Task.Run(() => _creatorStudio.BrowseObjects("rooms", CreatorMaintenanceSearch, 2000));
            CreatorCleanupRooms.Clear();
            foreach (var row in rows) CreatorCleanupRooms.Add(row);
            SelectedCreatorCleanupRoom = CreatorCleanupRooms.FirstOrDefault();
            CreatorMaintenanceStatus = $"Loaded {rows.Count:N0} room(s). Cleanup combines only duplicate dynamic Custom1 save stacks.";
        }
        catch (Exception exception)
        {
            CreatorMaintenanceStatus = $"Cleanup room search failed: {exception.Message}";
        }
    }

    private async Task CombineCreatorRoomItemsAsync()
    {
        if (SelectedCreatorCleanupRoom is null)
        {
            CreatorMaintenanceStatus = "Select a Custom1 room first.";
            return;
        }

        try
        {
            EnsureCreatorMaintenanceAuthorized(requireHostStopped: true);
            var result = await Task.Run(() => _creatorStudio.CombineCustom1RoomItems(SelectedCreatorCleanupRoom.Id));
            CreatorMaintenanceConfirmation = string.Empty;
            CreatorMaintenanceStatus = !result.StateFileFound
                ? "No Custom1 living-world save exists; there were no dynamic stacks to combine."
                : $"Combined {result.EligibleStacks:N0} eligible dynamic stack(s) into {result.FinalStacks:N0}; removed {result.RemovedStacks:N0} duplicates while preserving {result.TotalQuantity:N0} total quantity.";
        }
        catch (Exception exception)
        {
            CreatorMaintenanceStatus = $"Room stack cleanup was not applied: {exception.Message}";
        }
    }

    private async Task PreviewCreatorTextblockStripAsync()
    {
        try
        {
            CreatorMaintenanceStatus = "Preparing a read-only textblock replacement preview...";
            var rows = await Task.Run(() => _creatorStudio.PreviewTextblockStrip(
                CreatorTextblockFind,
                CreatorTextblockReplacement,
                CreatorTextblockSuffixOnly,
                5000));
            CreatorTextblockPreviews.Clear();
            foreach (var row in rows) CreatorTextblockPreviews.Add(row);
            SelectedCreatorTextblockPreview = CreatorTextblockPreviews.FirstOrDefault();
            CreatorMaintenanceStatus = rows.Count == 0
                ? "No textblocks would change. Nothing was written."
                : $"Preview only: {rows.Count:N0} textblock(s) would change. Review before applying.";
        }
        catch (Exception exception)
        {
            CreatorTextblockPreviews.Clear();
            SelectedCreatorTextblockPreview = null;
            CreatorMaintenanceStatus = $"Textblock preview failed: {exception.Message}";
        }
    }

    private async Task ApplyCreatorTextblockStripAsync()
    {
        if (CreatorTextblockPreviews.Count == 0)
        {
            CreatorMaintenanceStatus = "Preview the textblock changes before applying them.";
            return;
        }

        try
        {
            EnsureCreatorMaintenanceAuthorized(requireHostStopped: false);
            var ids = CreatorTextblockPreviews.Select(row => row.TextblockId).ToArray();
            var result = await Task.Run(() => _creatorStudio.ApplyTextblockStrip(
                CreatorTextblockFind,
                CreatorTextblockReplacement,
                CreatorTextblockSuffixOnly,
                ids));
            CreatorTextblockPreviews.Clear();
            SelectedCreatorTextblockPreview = null;
            CompleteCreatorOverrideMaintenance(result.Summary);
        }
        catch (Exception exception)
        {
            CreatorMaintenanceStatus = $"Textblock changes were not applied: {exception.Message}";
        }
    }

    private void EnsureCreatorMaintenanceAuthorized(bool requireHostStopped)
    {
        if (!CreatorMaintenanceConfirmation.Trim().Equals("CUSTOM1", StringComparison.Ordinal))
            throw new InvalidOperationException("Type CUSTOM1 in the confirmation box immediately before applying this action.");
        if (requireHostStopped && (_host.IsRunning || _creatorTestRealmRunning))
            throw new InvalidOperationException("Stop the embedded Host and Test Realm before changing live Custom1 state.");
    }

    private void CompleteCreatorOverrideMaintenance(string summary)
    {
        CreatorMaintenanceConfirmation = string.Empty;
        CreatorValidationStatus = "Overrides changed. Validation must run again before generation.";
        CreatorMaintenanceStatus = summary + " Validate and regenerate the Custom1 Test Realm before runtime testing.";
    }

    private static IEnumerable<string> SplitCreatorMaintenanceLines(string? value) =>
        (value ?? string.Empty).Replace("\r", string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static long ParseCreatorCoin(string? value)
    {
        if (!long.TryParse(value?.Trim(), out var parsed) || parsed < 0)
            throw new InvalidOperationException("Coin amounts must be whole numbers zero or greater.");
        return parsed;
    }

    private async Task LoadLibrariumAsync()
    {
        var category = SelectedLibrariumCategory;
        if (category is null)
        {
            LibrariumStatus = "Select a Librarium category.";
            return;
        }

        var generation = Interlocked.Increment(ref _librariumLoadGeneration);
        try
        {
            LibrariumStatus = $"Loading {category.DisplayName}...";
            var records = await Task.Run(() =>
                _librariumBrowser.Browse(category.Key, LibrariumSearchText, 2000));
            if (generation != Volatile.Read(ref _librariumLoadGeneration)) return;

            LibrariumRecords.Clear();
            foreach (var record in records) LibrariumRecords.Add(record);
            LibrariumStatus = records.Count == 2000
                ? $"Showing the first 2,000 matching {category.DisplayName.ToLowerInvariant()} records. Narrow the search to find more."
                : $"Loaded {records.Count:N0} {category.DisplayName.ToLowerInvariant()} record(s) from generated Classic data.";
            SelectedLibrariumRecord = LibrariumRecords.FirstOrDefault();
            if (SelectedLibrariumRecord is null)
            {
                LibrariumFields.Clear();
                LibrariumSelectedTitle = "No matching records";
                LibrariumSelectedSubheading = "Change the search text or select another category.";
            }
        }
        catch (Exception exception)
        {
            if (generation != Volatile.Read(ref _librariumLoadGeneration)) return;
            LibrariumRecords.Clear();
            LibrariumFields.Clear();
            LibrariumSelectedTitle = "Librarium unavailable";
            LibrariumSelectedSubheading = exception.Message;
            LibrariumStatus = "Classic Librarium data could not be loaded: " + exception.Message;
        }
    }

    private async Task LoadLibrariumDetailsAsync(ClassicLibrariumRecordSummary? selected)
    {
        var generation = Volatile.Read(ref _librariumLoadGeneration);
        LibrariumFields.Clear();
        if (selected is null) return;

        LibrariumSelectedTitle = selected.Name;
        LibrariumSelectedSubheading = selected.Subheading;
        try
        {
            var details = await Task.Run(() => _librariumBrowser.Read(selected.Category, selected.Id));
            if (generation != Volatile.Read(ref _librariumLoadGeneration) ||
                !ReferenceEquals(selected, SelectedLibrariumRecord)) return;
            LibrariumSelectedTitle = details.Name;
            LibrariumSelectedSubheading = details.Subheading;
            foreach (var field in details.Fields) LibrariumFields.Add(field);
        }
        catch (Exception exception)
        {
            if (!ReferenceEquals(selected, SelectedLibrariumRecord)) return;
            LibrariumSelectedSubheading = "Record details could not be loaded: " + exception.Message;
        }
    }

    private async Task SaveCreatorWorldSettingsAsync()
    {
        try
        {
            EnsureCreatorAdministrationHostStopped();
            if (!CreatorAdministrationConfirmation.Trim().Equals("CUSTOM1", StringComparison.Ordinal))
                throw new InvalidOperationException("Type CUSTOM1 in the administration confirmation box before saving world settings.");

            CreatorWorldSettingsStatus = "Saving and verifying Custom1 world settings...";
            var requested = new CustomWorldEditorSettings(
                CustomWorldEditorService.CustomWorldId,
                CreatorWorldName,
                CreatorWorldDescription,
                CreatorWorldLoginBanner,
                CreatorWorldMotd,
                CreatorWorldRuntimeEnabled,
                CreatorWorldTransfersAllowed,
                $"Data/{CustomWorldEditorService.CustomWorldId}");
            var saved = await Task.Run(() => _creatorStudio.SaveSettings(requested));
            CreatorWorldName = saved.Name;
            CreatorWorldDescription = saved.Description;
            CreatorWorldLoginBanner = saved.LoginBanner;
            CreatorWorldMotd = saved.Motd;
            CreatorWorldRuntimeEnabled = saved.RuntimeEnabled;
            CreatorWorldTransfersAllowed = saved.TransfersAllowed;
            CreatorAdministrationConfirmation = string.Empty;
            CreatorWorldSettingsStatus = "Custom1 world settings were saved and read back. Restart the Host to use them.";
        }
        catch (Exception exception)
        {
            CreatorWorldSettingsStatus = $"Custom1 world settings were not saved: {exception.Message}";
        }
    }

    private async Task LoadCreatorCharactersAsync()
    {
        try
        {
            CreatorAdministrationStatus = "Loading realm-scoped character saves...";
            var characters = await Task.Run(_creatorAdministration.LoadAllCharacters);
            CreatorManagedCharacters.Clear();
            foreach (var character in characters) CreatorManagedCharacters.Add(new CreatorManagedCharacterRow(character));
            SelectedCreatorManagedCharacter = CreatorManagedCharacters.FirstOrDefault();
            CreatorAdministrationStatus = $"Loaded {characters.Count:N0} saved character(s) across all realms. Deletion requires the exact character name and a stopped Host.";
        }
        catch (Exception exception)
        {
            CreatorManagedCharacters.Clear();
            SelectedCreatorManagedCharacter = null;
            CreatorAdministrationStatus = $"Characters could not be loaded: {exception.Message}";
        }
    }

    private async Task DeleteCreatorCharacterAsync()
    {
        var selected = SelectedCreatorManagedCharacter;
        if (selected is null)
        {
            CreatorAdministrationStatus = "Select a character before deleting.";
            return;
        }

        try
        {
            EnsureCreatorAdministrationHostStopped();
            if (!CreatorAdministrationConfirmation.Trim().Equals(selected.CharacterName, StringComparison.Ordinal))
                throw new InvalidOperationException($"Type the exact character name '{selected.CharacterName}' to confirm permanent deletion.");

            var deleted = await Task.Run(() => _creatorAdministration.DeleteCharacter(selected.Character));
            CreatorAdministrationConfirmation = string.Empty;
            await LoadCreatorCharactersAsync();
            if (deleted.WorldId.Equals("classic", StringComparison.OrdinalIgnoreCase))
                await LoadCreatorClassicAdministrationAsync();
            CreatorAdministrationStatus = $"Deleted {deleted.DisplayName} from {deleted.WorldId}. Other realms and characters were unchanged.";
        }
        catch (Exception exception)
        {
            CreatorAdministrationStatus = $"Character was not deleted: {exception.Message}";
        }
    }

    private async Task LoadCreatorClassicAdministrationAsync()
    {
        try
        {
            CreatorAdministrationStatus = "Loading and verifying Classic administration policy...";
            var snapshot = await Task.Run(_creatorAdministration.Load);
            CreatorClassicUnlimitedItems = snapshot.Policy.UnlimitedItemsEnabled;
            CreatorClassicEveryoneCanGoto = snapshot.Policy.EveryoneCanUseSysGoto;
            CreatorClassicAccessRows.Clear();
            foreach (var character in snapshot.Characters)
                CreatorClassicAccessRows.Add(new CreatorClassicAccessRow(
                    character,
                    snapshot.Policy.HasCharacterSysopAccess(character.CharacterId)));
            var warning = snapshot.Warnings.Count == 0
                ? string.Empty
                : $" Warnings: {string.Join(" | ", snapshot.Warnings.Take(3))}";
            CreatorAdministrationStatus = $"Loaded Classic policy and {snapshot.Characters.Count:N0} Classic character(s).{warning}";
        }
        catch (Exception exception)
        {
            CreatorClassicAccessRows.Clear();
            CreatorAdministrationStatus = $"Classic administration could not be loaded: {exception.Message}";
        }
    }

    private async Task SaveCreatorClassicAdministrationAsync()
    {
        try
        {
            EnsureCreatorAdministrationHostStopped();
            if (!CreatorAdministrationConfirmation.Trim().Equals("CLASSIC", StringComparison.Ordinal))
                throw new InvalidOperationException("Type CLASSIC in the administration confirmation box before saving Classic policy.");

            var requested = new RuntimeClassicAdministrationPolicy
            {
                WorldId = "classic",
                UnlimitedItemsEnabled = CreatorClassicUnlimitedItems,
                EveryoneCanUseSysGoto = CreatorClassicEveryoneCanGoto,
                SysopCharacterIds = CreatorClassicAccessRows
                    .Where(row => row.HasSysopAccess)
                    .Select(row => row.CharacterId)
                    .ToList()
            };
            var result = await Task.Run(() => _creatorAdministration.Save(requested));
            CreatorClassicUnlimitedItems = result.Policy.UnlimitedItemsEnabled;
            CreatorClassicEveryoneCanGoto = result.Policy.EveryoneCanUseSysGoto;
            foreach (var row in CreatorClassicAccessRows)
                row.HasSysopAccess = result.Policy.HasCharacterSysopAccess(row.CharacterId);
            CreatorAdministrationConfirmation = string.Empty;
            CreatorAdministrationStatus = $"Classic policy saved and read-back verified with {result.Policy.SysopCharacterIds.Count:N0} character SYSOP grant(s).";
        }
        catch (Exception exception)
        {
            CreatorAdministrationStatus = $"Classic policy was not saved: {exception.Message}";
        }
    }

    private void EnsureCreatorAdministrationHostStopped()
    {
        if (_host.IsRunning || _creatorTestRealmRunning)
            throw new InvalidOperationException("Stop the embedded Host and Test Realm before changing world settings, character saves, or Classic policy.");
    }

    private string FormatCreatorObject(CustomWorldObjectSummary summary)
    {
        try
        {
            var json = _creatorStudio.ReadBaseRecordJson(summary.Family, summary.Id);
            var root = JsonNode.Parse(json) as JsonObject;
            var fields = root?["fields"] as JsonObject ?? root;
            var lines = new List<string> { summary.Name, $"ID: {summary.Id}", $"Category: {summary.Family}" };
            if (fields is not null)
            {
                foreach (var pair in fields.Where(pair => IsCreatorDisplayField(pair.Key, pair.Value)).Take(40))
                    lines.Add($"{HumanizeCreatorField(pair.Key)}: {CreatorDisplayValue(pair.Value)}");
            }
            return string.Join(Environment.NewLine, lines);
        }
        catch (Exception exception)
        {
            return $"{summary.Name}\nID: {summary.Id}\nDetails unavailable: {exception.Message}";
        }
    }

    private static bool IsCreatorDisplayField(string key, JsonNode? value) =>
        value is JsonValue && !key.Contains("evidence", StringComparison.OrdinalIgnoreCase) &&
        !key.Contains("provenance", StringComparison.OrdinalIgnoreCase) &&
        !key.Contains("confidence", StringComparison.OrdinalIgnoreCase) &&
        !key.Contains("source", StringComparison.OrdinalIgnoreCase);

    private static string CreatorDisplayValue(JsonNode? value)
    {
        var text = value?.ToJsonString() ?? "Not set";
        if (text.Length >= 2 && text[0] == '"' && text[^1] == '"')
            text = JsonSerializer.Deserialize<string>(text) ?? string.Empty;
        return text.Length > 180 ? text[..177] + "..." : text;
    }

    private static string HumanizeCreatorField(string value)
    {
        var words = value.Replace('_', ' ').Replace('.', ' ').Trim();
        return words.Length == 0 ? "Value" : char.ToUpperInvariant(words[0]) + words[1..];
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
