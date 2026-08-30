using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Reforged.App.Models;

public enum ClientConnectionState { Disconnected, Connecting, Connected, Reconnecting, Faulted }
public enum ClientTerminalViewMode
{
    Reforged = 0,
    // Legacy serialized name. Both values intentionally select the one Reforged client.
    ClassicMegaMud = Reforged
}
public enum RouteExecutionState { Idle, Running, Paused, WaitingForRoom, Completed, Stopped, Faulted }
public enum ClientCommandOrigin { Human, AutonomousAgent, RouteAutomation, ClientAutomation, SystemLogin, ClientProtocol }

public sealed record ClientRoomLinkState(string Direction, string RoomKey, string RoomName);
public sealed record ClientRoomCommandState(string Command, string Source, string InteractionType, string Requirements);
public sealed record ClientRoomTopologyNode(
    string RoomKey, string RoomName, int Depth, bool Current, string[] Players, string[] LiveNpcs,
    string[] ExpectedMonsters, string[] BossNames, bool BossAlive, bool IsBossLair,
    bool HasShop, bool HasRoomCommand, int RoomType);
public sealed record ClientRoomTopologyLink(string SourceRoomKey, string TargetRoomKey, string Direction, string GateKind, bool Hidden, bool OneWay, bool Blocked);
public sealed record ClientRoomTopologySnapshot(ClientRoomTopologyNode[] Nodes, ClientRoomTopologyLink[] Links);
public sealed record ClientRoomInspectionNpc(
    string InstanceId, string CanonicalNpcId, int LegacyNpcId, string Name,
    int CurrentHitPoints, int MaximumHitPoints, bool Alive, DateTimeOffset? RespawnAtUtc,
    string Disposition, string InteractionModel, int Level, long ExperienceReward);
public sealed record ClientRoomInspectionExit(string Direction, string RoomKey, string RoomName, string GateKind, bool HasBlockedDestination);
public sealed record ClientRoomInspectionSpawn(string DisplayName, int RespawnMinutes, int MaximumAreaPopulation);
public sealed record ClientRoomServiceState(string Id, string Kind, string Name);
public sealed record ClientRoomItemState(string InstanceId, string CanonicalItemId, string Name, int Weight, bool Gettable, int Quantity, bool Hidden);
public sealed record ClientLearnedSpellState(string CanonicalId, string Name, int ManaCost, int EnergyCost);
public sealed record ClientActiveEffectState(string EffectId, string SpellId, string Name, DateTimeOffset AppliedAtUtc, DateTimeOffset? ExpiresAtUtc);
public sealed record ClientRoomEnvironmentState(string RoomKey, int EnvironmentLightLevel, int EnvironmentRoomType);
public sealed record ClientPvpState(bool Active, bool WorldPvpEnabled, bool SafeRoom, string EncounterId, string OpponentCharacterId, string OpponentName,
    string RoomId, int RoundNumber, DateTimeOffset? StartedAtUtc);
public sealed record ClientRoomInspectionSnapshot(
    string RoomKey, string Name, string Description, ClientRoomInspectionNpc[] Npcs, string[] Players, string[] Items,
    string Currency, ClientRoomInspectionExit[] Exits, string[] Commands, ClientRoomInspectionSpawn[] ExpectedSpawns,
    int? ImportedShopNumber, int EnvironmentLightLevel, int EnvironmentRoomType, DateTimeOffset CapturedAt);
public sealed record ClientMapCatalogEntry(int MapId, int RoomCount)
{
    public string DisplayName => $"Map {MapId} — {RoomCount:N0} rooms";
}


public sealed class ClassicPartyAutomationPreferences : INotifyPropertyChanged
{
    private bool _helpLeaderBashDoors = true;
    private bool _sayPanicWhenLeading = true;
    private bool _ignorePanicWhenFollowing;
    private bool _ignorePartyWhenFollowing;
    private bool _ignoreWaitWhenLeading;
    private int _maxMonstersWhenPartying = 4;
    private int _maxMonsterExperience;
    private int _waitHealthPercent = 50;
    private int _leaderWaitMinutes = 2;
    private int _partyFrequencySeconds = 15;
    private string _partyWaitCommand = "@wait";
    private string _partyResumeCommand = "@ok";

    public bool HelpLeaderBashDoors { get => _helpLeaderBashDoors; set => Set(ref _helpLeaderBashDoors, value); }
    public bool SayPanicWhenLeading { get => _sayPanicWhenLeading; set => Set(ref _sayPanicWhenLeading, value); }
    public bool IgnorePanicWhenFollowing { get => _ignorePanicWhenFollowing; set => Set(ref _ignorePanicWhenFollowing, value); }
    public bool IgnorePartyWhenFollowing { get => _ignorePartyWhenFollowing; set => Set(ref _ignorePartyWhenFollowing, value); }
    public bool IgnoreWaitWhenLeading { get => _ignoreWaitWhenLeading; set => Set(ref _ignoreWaitWhenLeading, value); }
    public int MaxMonstersWhenPartying { get => _maxMonstersWhenPartying; set => Set(ref _maxMonstersWhenPartying, Math.Clamp(value, 0, 99)); }
    public int MaxMonsterExperience { get => _maxMonsterExperience; set => Set(ref _maxMonsterExperience, Math.Max(0, value)); }
    public int WaitHealthPercent { get => _waitHealthPercent; set => Set(ref _waitHealthPercent, Math.Clamp(value, 0, 100)); }
    public int LeaderWaitMinutes { get => _leaderWaitMinutes; set => Set(ref _leaderWaitMinutes, Math.Clamp(value, 0, 999)); }
    public int PartyFrequencySeconds { get => _partyFrequencySeconds; set => Set(ref _partyFrequencySeconds, Math.Clamp(value, 1, 999)); }
    public string PartyWaitCommand { get => _partyWaitCommand; set => Set(ref _partyWaitCommand, string.IsNullOrWhiteSpace(value) ? "@wait" : value.Trim()); }
    public string PartyResumeCommand { get => _partyResumeCommand; set => Set(ref _partyResumeCommand, string.IsNullOrWhiteSpace(value) ? "@ok" : value.Trim()); }

    public event PropertyChangedEventHandler? PropertyChanged;
    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }
}



public sealed class ClassicHealthAutomationPreferences : INotifyPropertyChanged
{
    private bool _autoRestHeal = true;
    private int _restHealthMaximumPercent = 99;
    private int _restHealthBelowPercent = 65;
    private int _healRestHealthPercent = 80;
    private int _healMinorHealthPercent = 80;
    private int _healMajorHealthPercent = 70;
    private int _runHealthBelowPercent = 30;
    private int _hangupHealthBelowPercent = 10;
    private int _restManaMaximumPercent = 100;
    private int _restManaBelowPercent = 20;
    private int _healRestManaPercent = 50;
    private int _healMinorManaPercent = 80;
    private int _healMajorManaPercent = 10;
    private int _runManaBelowPercent;
    private int _blessManaAbovePercent = 80;
    private int _healPeriodSeconds = 4;
    private bool _useMeditateAbility;
    private bool _meditateBeforeResting;
    private string _preRestCommand = string.Empty;
    private string _postRestCommand = string.Empty;
    private string _preMeditateCommand = string.Empty;
    private string _postMeditateCommand = string.Empty;

    public bool AutoRestHeal { get => _autoRestHeal; set => Set(ref _autoRestHeal, value); }
    public int RestHealthMaximumPercent { get => _restHealthMaximumPercent; set => Set(ref _restHealthMaximumPercent, Percent(value)); }
    public int RestHealthBelowPercent { get => _restHealthBelowPercent; set => Set(ref _restHealthBelowPercent, Percent(value)); }
    public int HealRestHealthPercent { get => _healRestHealthPercent; set => Set(ref _healRestHealthPercent, Percent(value)); }
    public int HealMinorHealthPercent { get => _healMinorHealthPercent; set => Set(ref _healMinorHealthPercent, Percent(value)); }
    public int HealMajorHealthPercent { get => _healMajorHealthPercent; set => Set(ref _healMajorHealthPercent, Percent(value)); }
    public int RunHealthBelowPercent { get => _runHealthBelowPercent; set => Set(ref _runHealthBelowPercent, Percent(value)); }
    public int HangupHealthBelowPercent { get => _hangupHealthBelowPercent; set => Set(ref _hangupHealthBelowPercent, Percent(value)); }
    public int RestManaMaximumPercent { get => _restManaMaximumPercent; set => Set(ref _restManaMaximumPercent, Percent(value)); }
    public int RestManaBelowPercent { get => _restManaBelowPercent; set => Set(ref _restManaBelowPercent, Percent(value)); }
    public int HealRestManaPercent { get => _healRestManaPercent; set => Set(ref _healRestManaPercent, Percent(value)); }
    public int HealMinorManaPercent { get => _healMinorManaPercent; set => Set(ref _healMinorManaPercent, Percent(value)); }
    public int HealMajorManaPercent { get => _healMajorManaPercent; set => Set(ref _healMajorManaPercent, Percent(value)); }
    public int RunManaBelowPercent { get => _runManaBelowPercent; set => Set(ref _runManaBelowPercent, Percent(value)); }
    public int BlessManaAbovePercent { get => _blessManaAbovePercent; set => Set(ref _blessManaAbovePercent, Percent(value)); }
    public int HealPeriodSeconds { get => _healPeriodSeconds; set => Set(ref _healPeriodSeconds, Math.Clamp(value, 1, 999)); }
    public bool UseMeditateAbility { get => _useMeditateAbility; set => Set(ref _useMeditateAbility, value); }
    public bool MeditateBeforeResting { get => _meditateBeforeResting; set => Set(ref _meditateBeforeResting, value); }
    public string PreRestCommand { get => _preRestCommand; set => Set(ref _preRestCommand, value ?? string.Empty); }
    public string PostRestCommand { get => _postRestCommand; set => Set(ref _postRestCommand, value ?? string.Empty); }
    public string PreMeditateCommand { get => _preMeditateCommand; set => Set(ref _preMeditateCommand, value ?? string.Empty); }
    public string PostMeditateCommand { get => _postMeditateCommand; set => Set(ref _postMeditateCommand, value ?? string.Empty); }

    private static int Percent(int value) => Math.Clamp(value, 0, 100);
    public event PropertyChangedEventHandler? PropertyChanged;
    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }
}

public sealed class ClassicCombatAutomationPreferences : INotifyPropertyChanged
{
    private bool _autoCombat = true;
    private bool _doBackstabAttacks = true;
    private bool _runIfBackstabFails;
    private bool _dontBackstabIfMultiAttack;
    private bool _mainSpellFallsToAux;
    private bool _resetMultiWithMobEntry = true;
    private bool _clearOnceEngaged;
    private string _normalAttackCommand = "attack";
    private string _backstabWeapon = string.Empty;
    private string _normalWeapon = string.Empty;
    private string _alternateWeapon = string.Empty;
    private string _shield = string.Empty;
    private string _preBackstabCommand = string.Empty;
    private string _postBackstabCommand = string.Empty;
    private string _spellCombatResetCommand = "reset";
    private int _maxTotalExperiencePerRoom = 9999999;
    private int _minimumMobsPerRoom;
    private int _maximumMobsPerRoom = 99;

    public bool AutoCombat { get => _autoCombat; set => Set(ref _autoCombat, value); }
    public bool DoBackstabAttacks { get => _doBackstabAttacks; set => Set(ref _doBackstabAttacks, value); }
    public bool RunIfBackstabFails { get => _runIfBackstabFails; set => Set(ref _runIfBackstabFails, value); }
    public bool DontBackstabIfMultiAttack { get => _dontBackstabIfMultiAttack; set => Set(ref _dontBackstabIfMultiAttack, value); }
    public bool MainSpellFallsToAux { get => _mainSpellFallsToAux; set => Set(ref _mainSpellFallsToAux, value); }
    public bool ResetMultiWithMobEntry { get => _resetMultiWithMobEntry; set => Set(ref _resetMultiWithMobEntry, value); }
    public bool ClearOnceEngaged { get => _clearOnceEngaged; set => Set(ref _clearOnceEngaged, value); }
    public string NormalAttackCommand { get => _normalAttackCommand; set => Set(ref _normalAttackCommand, value?.Trim() ?? string.Empty); }
    public string BackstabWeapon { get => _backstabWeapon; set => Set(ref _backstabWeapon, value?.Trim() ?? string.Empty); }
    public string NormalWeapon { get => _normalWeapon; set => Set(ref _normalWeapon, value?.Trim() ?? string.Empty); }
    public string AlternateWeapon { get => _alternateWeapon; set => Set(ref _alternateWeapon, value?.Trim() ?? string.Empty); }
    public string Shield { get => _shield; set => Set(ref _shield, value?.Trim() ?? string.Empty); }
    public string PreBackstabCommand { get => _preBackstabCommand; set => Set(ref _preBackstabCommand, value ?? string.Empty); }
    public string PostBackstabCommand { get => _postBackstabCommand; set => Set(ref _postBackstabCommand, value ?? string.Empty); }
    public string SpellCombatResetCommand { get => _spellCombatResetCommand; set => Set(ref _spellCombatResetCommand, value?.Trim() ?? string.Empty); }
    public int MaxTotalExperiencePerRoom { get => _maxTotalExperiencePerRoom; set => Set(ref _maxTotalExperiencePerRoom, Math.Max(0, value)); }
    public int MinimumMobsPerRoom { get => _minimumMobsPerRoom; set => Set(ref _minimumMobsPerRoom, Math.Clamp(value, 0, 99)); }
    public int MaximumMobsPerRoom { get => _maximumMobsPerRoom; set => Set(ref _maximumMobsPerRoom, Math.Clamp(value, 0, 99)); }

    public event PropertyChangedEventHandler? PropertyChanged;
    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }
}

public sealed class ClientSessionConnection : INotifyPropertyChanged
{
    private string _displayName = "Session";
    private string _characterName = string.Empty;
    private string _serverProfileName = "Local Host";
    private string _worldName = "Classic";
    private string _hostName = "127.0.0.1";
    private int _port = 4000;
    private bool _autoReconnect = true;
    private int _reconnectDelaySeconds = 5;
    private ClientConnectionState _connectionState;
    private string _connectionMessage = "Disconnected";
    private ClientTerminalViewMode _terminalViewMode = ClientTerminalViewMode.Reforged;
    private DateTimeOffset? _connectedAt;
    private string _lastReceivedLine = string.Empty;
    private string _livePromptText = string.Empty;
    private ClassicTerminalSnapshot _classicScreen = ClassicTerminalSnapshot.Empty;
    private string _telnetStatus = "Not connected";
    private int _terminalColumns = 80;
    private int _terminalRows = 25;
    private int _healthPercent = 100;
    private int _manaPercent = 100;
    private int _healthCurrent;
    private int _healthMaximum;
    private int _manaCurrent;
    private int _manaMaximum;
    private long _carriedCurrency;
    private long _carriedWeight;
    private long _carryingCapacity;
    private int _encumbrancePercent;
    private string _encumbranceBand = string.Empty;
    private DateTimeOffset? _currencyUpdatedAt;
    private DateTimeOffset? _encumbranceUpdatedAt;
    private long _experienceCurrent;
    private long _experienceNeeded;
    private long _experienceRemaining;
    private int _characterLevel;
    private int _lives;
    private int _characterPoints;
    private DateTimeOffset? _inventoryUpdatedAt;
    private DateTimeOffset? _experienceUpdatedAt;
    private DateTimeOffset? _equipmentUpdatedAt;
    private DateTimeOffset? _spellbookUpdatedAt;
    private DateTimeOffset? _activeEffectsUpdatedAt;
    private DateTimeOffset? _partyUpdatedAt;
    private DateTimeOffset? _onlinePlayersUpdatedAt;
    private ClientPvpState? _pvpState;
    private DateTimeOffset? _pvpStateUpdatedAt;
    private bool _inCombat;
    private bool _autoCombatActive;
    private string _lastCombatEvent = "None";
    private string _combatAutomationStatus = "Automation safety lock is active.";
    private string _survivalAutomationStatus = "Automation safety lock is active.";
    private string _roamingAutomationStatus = "Automation safety lock is active.";
    private string _lootAutomationStatus = "Automation safety lock is active.";
    private string _partyAutomationStatus = "Automation safety lock is active.";
    private string _spellAutomationStatus = "Automation safety lock is active.";
    private string _pvpAutomationStatus = "Automation safety lock is active.";
    private int _lastCombatAmount;
    private DateTimeOffset? _lastCombatEventAt;
    private long _sessionExperienceGained;
    private int _sessionCombatEventCount;
    private int _sessionRoomsObserved;
    private bool _isResting;
    private bool _isMoving;
    private string _currentRoomName = string.Empty;
    private string _currentRoomKey = string.Empty;
    private string _currentRoomDescription = string.Empty;
    private string _currentRoomCurrency = string.Empty;
    private string _lastRawExitText = string.Empty;
    private DateTimeOffset? _roomUpdatedAt;
    private DateTimeOffset? _exitsUpdatedAt;
    private DateTimeOffset? _promptUpdatedAt;
    private DateTimeOffset? _lastServerActivityAt;
    private string _lastCommandText = string.Empty;
    private ClientCommandOrigin _lastCommandOrigin = ClientCommandOrigin.Human;
    private DateTimeOffset? _lastCommandAt;
    private RouteExecutionState _routeState;
    private string _activeRouteName = string.Empty;
    private int _routeStep;
    private int _routeStepCount;
    private string _routeMessage = "No active route";
    private string _lastConfirmedRouteRoom = string.Empty;
    private bool _awaitingRouteConfirmation;
    private int _routeDesyncCount;
    private string _expectedRouteRoomKey = string.Empty;
    private string _routeTopologyStatus = "No active route";
    private int _routeVerifiedStepCount;
    private int _routeUnverifiedStepCount;
    private string _routeRecoveryStatus = "No recovery needed";
    private bool _routeRecoveryAvailable;
    private string _routeDestinationKey = string.Empty;
    private string _routeRecoveryPlan = "No replacement route calculated";

    public string SessionId { get; set; } = Guid.NewGuid().ToString("N");
    public string DisplayName { get => _displayName; set => Set(ref _displayName, value); }
    public string CharacterName { get => _characterName; set => Set(ref _characterName, value); }
    public string ServerProfileName { get => _serverProfileName; set => Set(ref _serverProfileName, value); }
    public string WorldName { get => _worldName; set => Set(ref _worldName, value); }
    public string HostName { get => _hostName; set => Set(ref _hostName, value); }
    public int Port { get => _port; set => Set(ref _port, Math.Clamp(value, 1, 65535)); }
    public bool AutoReconnect { get => _autoReconnect; set => Set(ref _autoReconnect, value); }
    public int ReconnectDelaySeconds { get => _reconnectDelaySeconds; set => Set(ref _reconnectDelaySeconds, Math.Clamp(value, 1, 120)); }
    public ClientConnectionState ConnectionState { get => _connectionState; set { if (Set(ref _connectionState, value)) Raise(nameof(StatusSummary)); } }
    public string ConnectionMessage { get => _connectionMessage; set { if (Set(ref _connectionMessage, value)) Raise(nameof(StatusSummary)); } }
    public ClientTerminalViewMode TerminalViewMode { get => _terminalViewMode; set => Set(ref _terminalViewMode, value); }
    public DateTimeOffset? ConnectedAt { get => _connectedAt; set => Set(ref _connectedAt, value); }
    public ObservableCollection<string> TerminalLines { get; set; } = [];
    public ObservableCollection<ClientTerminalEntry> TerminalEntries { get; } = [];
    public ObservableCollection<ClientTerminalEntry> MainEntries { get; } = [];
    public ObservableCollection<ClientTerminalEntry> CombatEntries { get; } = [];
    public ObservableCollection<ClientTerminalEntry> ChatEntries { get; } = [];
    public ObservableCollection<ClientTerminalEntry> TellEntries { get; } = [];
    public ObservableCollection<ClientTerminalEntry> PartyEntries { get; } = [];
    public ObservableCollection<ClientTerminalEntry> SystemEntries { get; } = [];
    public ClientTerminalPreferences TerminalPreferences { get; } = new();
    public ClassicPartyAutomationPreferences ClassicPartyPreferences { get; set; } = new();
    public ClassicCombatAutomationPreferences ClassicCombatPreferences { get; set; } = new();
    public ClassicHealthAutomationPreferences ClassicHealthPreferences { get; set; } = new();
    public ObservableCollection<string> CommandHistory { get; set; } = [];
    public string LastReceivedLine { get => _lastReceivedLine; set => Set(ref _lastReceivedLine, value); }
    public string LivePromptText { get => _livePromptText; set => Set(ref _livePromptText, value); }
    public ClassicTerminalSnapshot ClassicScreen { get => _classicScreen; set => Set(ref _classicScreen, value); }
    public string TelnetStatus { get => _telnetStatus; set => Set(ref _telnetStatus, value); }
    public int TerminalColumns { get => _terminalColumns; set { if (Set(ref _terminalColumns, Math.Clamp(value, 20, ushort.MaxValue))) Raise(nameof(TerminalSizeSummary)); } }
    public int TerminalRows { get => _terminalRows; set { if (Set(ref _terminalRows, Math.Clamp(value, 5, ushort.MaxValue))) Raise(nameof(TerminalSizeSummary)); } }
    public string TerminalSizeSummary => $"{TerminalColumns}x{TerminalRows}";
    public int HealthPercent { get => _healthPercent; set => Set(ref _healthPercent, Math.Clamp(value, 0, 100)); }
    public int ManaPercent { get => _manaPercent; set => Set(ref _manaPercent, Math.Clamp(value, 0, 100)); }
    public int HealthCurrent { get => _healthCurrent; set => Set(ref _healthCurrent, Math.Max(0, value)); }
    public int HealthMaximum { get => _healthMaximum; set => Set(ref _healthMaximum, Math.Max(0, value)); }
    public int ManaCurrent { get => _manaCurrent; set => Set(ref _manaCurrent, Math.Max(0, value)); }
    public int ManaMaximum { get => _manaMaximum; set => Set(ref _manaMaximum, Math.Max(0, value)); }
    public long CarriedCurrency { get => _carriedCurrency; set => Set(ref _carriedCurrency, Math.Max(0, value)); }
    public long CarriedWeight { get => _carriedWeight; set => Set(ref _carriedWeight, Math.Max(0, value)); }
    public long CarryingCapacity { get => _carryingCapacity; set => Set(ref _carryingCapacity, Math.Max(0, value)); }
    public int EncumbrancePercent { get => _encumbrancePercent; set => Set(ref _encumbrancePercent, Math.Clamp(value, 0, 999)); }
    public string EncumbranceBand { get => _encumbranceBand; set => Set(ref _encumbranceBand, value?.Trim() ?? string.Empty); }
    public DateTimeOffset? CurrencyUpdatedAt { get => _currencyUpdatedAt; set => Set(ref _currencyUpdatedAt, value); }
    public DateTimeOffset? EncumbranceUpdatedAt { get => _encumbranceUpdatedAt; set => Set(ref _encumbranceUpdatedAt, value); }
    public long ExperienceCurrent { get => _experienceCurrent; set { if (Set(ref _experienceCurrent, Math.Max(0, value))) Raise(nameof(ExperienceSummary)); } }
    public long ExperienceNeeded { get => _experienceNeeded; set { if (Set(ref _experienceNeeded, Math.Max(0, value))) Raise(nameof(ExperienceSummary)); } }
    public long ExperienceRemaining { get => _experienceRemaining; set => Set(ref _experienceRemaining, Math.Max(0, value)); }
    public int CharacterLevel { get => _characterLevel; set { if (Set(ref _characterLevel, Math.Max(0, value))) Raise(nameof(ExperienceSummary)); } }
    public int Lives { get => _lives; set => Set(ref _lives, Math.Max(0, value)); }
    public int CharacterPoints { get => _characterPoints; set => Set(ref _characterPoints, Math.Max(0, value)); }
    public DateTimeOffset? InventoryUpdatedAt { get => _inventoryUpdatedAt; set => Set(ref _inventoryUpdatedAt, value); }
    public DateTimeOffset? ExperienceUpdatedAt { get => _experienceUpdatedAt; set => Set(ref _experienceUpdatedAt, value); }
    public DateTimeOffset? EquipmentUpdatedAt { get => _equipmentUpdatedAt; set => Set(ref _equipmentUpdatedAt, value); }
    public DateTimeOffset? SpellbookUpdatedAt { get => _spellbookUpdatedAt; set => Set(ref _spellbookUpdatedAt, value); }
    public DateTimeOffset? ActiveEffectsUpdatedAt { get => _activeEffectsUpdatedAt; set => Set(ref _activeEffectsUpdatedAt, value); }
    public DateTimeOffset? PartyUpdatedAt { get => _partyUpdatedAt; set => Set(ref _partyUpdatedAt, value); }
    public DateTimeOffset? OnlinePlayersUpdatedAt { get => _onlinePlayersUpdatedAt; set => Set(ref _onlinePlayersUpdatedAt, value); }
    public ObservableCollection<string> InventoryItems { get; } = [];
    public ObservableCollection<string> EquippedItems { get; } = [];
    public ObservableCollection<string> LearnedSpells { get; } = [];
    public ObservableCollection<ClientLearnedSpellState> LearnedSpellDetails { get; } = [];
    public ObservableCollection<string> ActiveEffects { get; } = [];
    public ObservableCollection<ClientActiveEffectState> ActiveEffectDetails { get; } = [];
    public ClientPvpState? PvpState
    {
        get => _pvpState;
        set
        {
            if (!Set(ref _pvpState, value)) return;
            Raise(nameof(PvpRealmSummary));
            Raise(nameof(PvpCombatSummary));
        }
    }
    public DateTimeOffset? PvpStateUpdatedAt { get => _pvpStateUpdatedAt; set => Set(ref _pvpStateUpdatedAt, value); }
    public string PvpRealmSummary => PvpState is null ? "PvP: awaiting server" :
        !PvpState.WorldPvpEnabled ? "PvP: disabled by SYSOP" :
        PvpState.SafeRoom ? "PvP: safe room" : "PvP: enabled";
    public string PvpCombatSummary => PvpState?.Active == true
        ? $"Opponent: {PvpState.OpponentName} · round {PvpState.RoundNumber}"
        : "No active PvP encounter";
    public ObservableCollection<ClientPartyMemberState> PartyMembers { get; } = [];
    public ObservableCollection<ClientOnlinePlayerState> OnlinePlayers { get; } = [];
    public ObservableCollection<ClientBossHomeState> BossHomes { get; } = [];
    public ObservableCollection<ClientServerCatalogFamily> ServerCatalogFamilies { get; } = [];
    public string ServerCatalogWorld { get; set; } = string.Empty;
    public string ServerCatalogCompileHash { get; set; } = string.Empty;
    public DateTimeOffset? ServerCatalogUpdatedAt { get; set; }
    public ClientLibrariumPage? LibrariumPage { get; set; }
    public ObservableCollection<ClientExperienceSample> SessionExperienceSamples { get; } = [];
    public bool InCombat { get => _inCombat; set => Set(ref _inCombat, value); }
    public bool AutoCombatActive { get => _autoCombatActive; set => Set(ref _autoCombatActive, value); }
    public string LastCombatEvent { get => _lastCombatEvent; set => Set(ref _lastCombatEvent, value); }
    public string CombatAutomationStatus { get => _combatAutomationStatus; set => Set(ref _combatAutomationStatus, value?.Trim() ?? string.Empty); }
    public string SurvivalAutomationStatus { get => _survivalAutomationStatus; set => Set(ref _survivalAutomationStatus, value?.Trim() ?? string.Empty); }
    public string RoamingAutomationStatus { get => _roamingAutomationStatus; set => Set(ref _roamingAutomationStatus, value?.Trim() ?? string.Empty); }
    public string LootAutomationStatus { get => _lootAutomationStatus; set => Set(ref _lootAutomationStatus, value?.Trim() ?? string.Empty); }
    public string PartyAutomationStatus { get => _partyAutomationStatus; set => Set(ref _partyAutomationStatus, value?.Trim() ?? string.Empty); }
    public string SpellAutomationStatus { get => _spellAutomationStatus; set => Set(ref _spellAutomationStatus, value?.Trim() ?? string.Empty); }
    public string PvpAutomationStatus { get => _pvpAutomationStatus; set => Set(ref _pvpAutomationStatus, value?.Trim() ?? string.Empty); }
    public int LastCombatAmount { get => _lastCombatAmount; set => Set(ref _lastCombatAmount, Math.Max(0, value)); }
    public DateTimeOffset? LastCombatEventAt { get => _lastCombatEventAt; set => Set(ref _lastCombatEventAt, value); }
    public long SessionExperienceGained { get => _sessionExperienceGained; set => Set(ref _sessionExperienceGained, Math.Max(0, value)); }
    public int SessionCombatEventCount { get => _sessionCombatEventCount; set => Set(ref _sessionCombatEventCount, Math.Max(0, value)); }
    public int SessionRoomsObserved { get => _sessionRoomsObserved; set => Set(ref _sessionRoomsObserved, Math.Max(0, value)); }
    public bool IsResting { get => _isResting; set => Set(ref _isResting, value); }
    public bool IsMoving { get => _isMoving; set => Set(ref _isMoving, value); }
    public string CurrentRoomName { get => _currentRoomName; set => Set(ref _currentRoomName, value); }
    public string CurrentRoomKey { get => _currentRoomKey; set => Set(ref _currentRoomKey, value); }
    public string CurrentRoomDescription { get => _currentRoomDescription; set => Set(ref _currentRoomDescription, value); }
    public string CurrentRoomCurrency { get => _currentRoomCurrency; set => Set(ref _currentRoomCurrency, value); }
    public string LastRawExitText { get => _lastRawExitText; set => Set(ref _lastRawExitText, value?.Trim() ?? string.Empty); }
    public ObservableCollection<string> CurrentRoomExits { get; } = [];
    public ObservableCollection<string> CurrentRoomNpcs { get; } = [];
    public ObservableCollection<ClientRoomInspectionNpc> CurrentRoomNpcDetails { get; } = [];
    public ObservableCollection<ClientRoomServiceState> CurrentRoomServices { get; } = [];
    public ObservableCollection<ClientRoomItemState> CurrentRoomItemDetails { get; } = [];
    public DateTimeOffset? CurrentRoomNpcDetailsUpdatedAt { get; set; }
    public int? CurrentRoomLightLevel { get; set; }
    public DateTimeOffset? CurrentRoomEnvironmentUpdatedAt { get; set; }
    public ObservableCollection<string> CurrentRoomPlayers { get; } = [];
    public ObservableCollection<string> CurrentRoomItems { get; } = [];
    public ObservableCollection<ClientRoomLinkState> CurrentRoomLinks { get; } = [];
    public ObservableCollection<string> CurrentRoomCommands { get; } = [];
    public ObservableCollection<ClientRoomCommandState> CurrentRoomCommandDetails { get; } = [];
    public ObservableCollection<ClientRoomTopologyNode> CurrentRoomTopologyNodes { get; } = [];
    public ObservableCollection<ClientRoomTopologyLink> CurrentRoomTopologyLinks { get; } = [];
    public ClientRoomInspectionSnapshot? InspectedRoom { get; set; }
    public string RoomInspectionError { get; set; } = string.Empty;
    public ObservableCollection<ClientMapCatalogEntry> ServerMaps { get; } = [];
    public DateTimeOffset? RoomUpdatedAt { get => _roomUpdatedAt; set => Set(ref _roomUpdatedAt, value); }
    public DateTimeOffset? ExitsUpdatedAt { get => _exitsUpdatedAt; set => Set(ref _exitsUpdatedAt, value); }
    public DateTimeOffset? PromptUpdatedAt { get => _promptUpdatedAt; set => Set(ref _promptUpdatedAt, value); }
    public DateTimeOffset? LastServerActivityAt { get => _lastServerActivityAt; set => Set(ref _lastServerActivityAt, value); }
    public string LastCommandText { get => _lastCommandText; set => Set(ref _lastCommandText, value); }
    public ClientCommandOrigin LastCommandOrigin { get => _lastCommandOrigin; set => Set(ref _lastCommandOrigin, value); }
    public DateTimeOffset? LastCommandAt { get => _lastCommandAt; set => Set(ref _lastCommandAt, value); }
    public RouteExecutionState RouteState { get => _routeState; set { if (Set(ref _routeState, value)) Raise(nameof(RouteProgress)); } }
    public string ActiveRouteName { get => _activeRouteName; set => Set(ref _activeRouteName, value); }
    public int RouteStep { get => _routeStep; set { if (Set(ref _routeStep, value)) Raise(nameof(RouteProgress)); } }
    public int RouteStepCount { get => _routeStepCount; set { if (Set(ref _routeStepCount, value)) Raise(nameof(RouteProgress)); } }
    public string RouteMessage { get => _routeMessage; set { if (Set(ref _routeMessage, value)) Raise(nameof(RouteProgress)); } }
    public string LastConfirmedRouteRoom { get => _lastConfirmedRouteRoom; set => Set(ref _lastConfirmedRouteRoom, value); }
    public bool AwaitingRouteConfirmation { get => _awaitingRouteConfirmation; set => Set(ref _awaitingRouteConfirmation, value); }
    public int RouteDesyncCount { get => _routeDesyncCount; set => Set(ref _routeDesyncCount, Math.Max(0, value)); }
    public string ExpectedRouteRoomKey { get => _expectedRouteRoomKey; set => Set(ref _expectedRouteRoomKey, value); }
    public string RouteTopologyStatus { get => _routeTopologyStatus; set => Set(ref _routeTopologyStatus, value); }
    public int RouteVerifiedStepCount { get => _routeVerifiedStepCount; set => Set(ref _routeVerifiedStepCount, Math.Max(0, value)); }
    public int RouteUnverifiedStepCount { get => _routeUnverifiedStepCount; set => Set(ref _routeUnverifiedStepCount, Math.Max(0, value)); }
    public string RouteRecoveryStatus { get => _routeRecoveryStatus; set => Set(ref _routeRecoveryStatus, value); }
    public bool RouteRecoveryAvailable { get => _routeRecoveryAvailable; set => Set(ref _routeRecoveryAvailable, value); }
    public string RouteDestinationKey { get => _routeDestinationKey; set => Set(ref _routeDestinationKey, value); }
    public string RouteRecoveryPlan { get => _routeRecoveryPlan; set => Set(ref _routeRecoveryPlan, value); }
    public ObservableCollection<RouteTraceEntry> RouteTrace { get; } = [];
    public ObservableCollection<RoutePlaybackNode> RoutePlayback { get; } = [];
    public ObservableCollection<RouteMapSegment> RouteMapSegments { get; } = [];
    public ObservableCollection<RouteMapNode> RouteMapNodes { get; } = [];
    public string StatusSummary => $"{DisplayName} • {ConnectionMessage}";
    public string InventorySummary => InventoryItems.Count == 0 ? "Inventory: no captured items" : $"Inventory ({InventoryItems.Count}): {string.Join(", ", InventoryItems.Take(12))}" + (InventoryItems.Count > 12 ? $" and {InventoryItems.Count - 12} more" : string.Empty);
    public string EquipmentSummary => EquippedItems.Count == 0 ? "Equipment: no captured items" : $"Equipment ({EquippedItems.Count}): {string.Join(", ", EquippedItems.Take(12))}" + (EquippedItems.Count > 12 ? $" and {EquippedItems.Count - 12} more" : string.Empty);
    public string ExperienceSummary => CharacterLevel > 0 ? $"Level {CharacterLevel} • XP {ExperienceCurrent:N0}" + (ExperienceNeeded > 0 ? $" / {ExperienceNeeded:N0}" : string.Empty) : $"XP {ExperienceCurrent:N0}" + (ExperienceNeeded > 0 ? $" / {ExperienceNeeded:N0}" : string.Empty);
    public string VitalSummary => $"HP {HealthCurrent}/{HealthMaximum} ({HealthPercent}%) • MP {ManaCurrent}/{ManaMaximum} ({ManaPercent}%) • Cash {CarriedCurrency:N0}";
    public string RouteProgress => RouteStepCount == 0 ? RouteMessage : $"{RouteMessage} • {RouteStep}/{RouteStepCount}";

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        Raise(name);
        return true;
    }
}

public sealed record ClientCommandSentEvent(
    string SessionId,
    string Command,
    DateTimeOffset Timestamp,
    ClientCommandOrigin Origin = ClientCommandOrigin.Human);
public sealed record ClientTerminalLineEvent(string SessionId, string Line, DateTimeOffset Timestamp);
public sealed record ClientSessionStateEvent(string SessionId, ClientConnectionState State, string Message);
public sealed record RouteExecutionEvent(string SessionId, RouteExecutionState State, int Step, int StepCount, string Message);

public sealed record RouteTraceEntry(int Step, string Command, string FromRoom, string ToRoom, string Status, DateTimeOffset Timestamp, string ExpectedRoomKey = "", string ActualRoomKey = "");

public sealed record ClientPartyMemberState(
    string Name, string ClassName, int HealthPercent, int? ResourcePercent,
    string ResourceKind, string Rank, string State, bool Poisoned)
{
    public string Vitals => ResourcePercent is { } resource
        ? $"HP {HealthPercent}% · {(ResourceKind == "Kai" ? "KAI" : "MA")} {resource}%"
        : $"HP {HealthPercent}%";
}

public sealed record ClientExperienceSample(DateTimeOffset Timestamp, long CumulativeExperience);

public sealed record ClientOnlinePlayerState(string Name, string ClassName, int Level);

public sealed record ClientBossHomeState(string Name, string State, string CurrentRoom, string HomeRoom, DateTimeOffset? RespawnAtUtc);

public sealed record ClientServerCatalogFamily(string Name, int RecordCount, string Sha256)
{
    public string DisplayName => LibrariumFriendlyNames.Family(Name);
}
public sealed record ClientServerCatalogSnapshot(string World, string CompileHash, IReadOnlyList<ClientServerCatalogFamily> Families, DateTimeOffset CapturedAt);
public sealed record ClientLibrariumRecord(string Id, string Name, string Summary);
public sealed record ClientLibrariumPage(string Family, string Query, int Offset, int PageSize, int TotalCount, IReadOnlyList<ClientLibrariumRecord> Records, DateTimeOffset CapturedAt);
public sealed record ClientLibrariumLink(string Reference, string DisplayName);

public static class LibrariumFriendlyNames
{
    public static string Family(string value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant().Replace('-', '_');
        var known = normalized switch
        {
            "rooms" or "room" => "Rooms and Areas",
            "npcs" or "npc" or "monsters" => "Creatures and Bosses",
            "items" or "item" or "equipment" => "Items and Equipment",
            "spells" or "spell" or "abilities" => "Spells and Abilities",
            "quests" or "quest" or "quest_scripts" => "Quests",
            "trainers" or "trainer" or "training" => "Trainers",
            "shops" or "shop" or "merchants" => "Shops and Services",
            "room_commands" or "commands" or "room_actions" => "Things You Can Do in Rooms",
            "spawns" or "spawn" => "Creature Locations and Return Times",
            "exits" or "exit" => "Room Connections",
            _ => string.Empty
        };
        if (known.Length > 0) return known;
        var text = (value ?? string.Empty).Replace('_', ' ').Replace('-', ' ').Trim();
        if (text.Length == 0) return "Game information";
        return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(text.ToLowerInvariant())
            .Replace("Npcs", "Creatures", StringComparison.Ordinal)
            .Replace("Npc ", "Creature ", StringComparison.Ordinal);
    }
}

public sealed record ClientItemWeightState(string Item, int Quantity, long UnitWeight, long TotalWeight, string Location);

public sealed record RouteRoomConfirmation(string RoomName, string RoomKey, string RecognitionSource = "Room title", bool IsHighConfidence = false);

public enum RoutePlaybackNodeState { Pending, Active, Verified, Confirmed, Mismatch, Failed }

public sealed class RoutePlaybackNode : INotifyPropertyChanged
{
    private RoutePlaybackNodeState _state;
    private string _actualRoomName = string.Empty;
    private string _actualRoomKey = string.Empty;

    public int Step { get; init; }
    public string Command { get; init; } = string.Empty;
    public string ExpectedRoomKey { get; init; } = string.Empty;
    public RoutePlaybackNodeState State { get => _state; set { if (_state == value) return; _state = value; Raise(); Raise(nameof(StateLabel)); } }
    public string ActualRoomName { get => _actualRoomName; set { if (_actualRoomName == value) return; _actualRoomName = value; Raise(); } }
    public string ActualRoomKey { get => _actualRoomKey; set { if (_actualRoomKey == value) return; _actualRoomKey = value; Raise(); } }
    public string StateLabel => State.ToString();

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}


public sealed class RouteMapSegment : INotifyPropertyChanged
{
    private RoutePlaybackNodeState _state;
    public int Step { get; init; }
    public double X1 { get; init; }
    public double Y1 { get; init; }
    public double X2 { get; init; }
    public double Y2 { get; init; }
    public string Command { get; init; } = string.Empty;
    public RoutePlaybackNodeState State { get => _state; set { if (_state == value) return; _state = value; Raise(); Raise(nameof(Stroke)); Raise(nameof(Thickness)); } }
    public string Stroke => State switch
    {
        RoutePlaybackNodeState.Verified => "#278A46",
        RoutePlaybackNodeState.Confirmed => "#2F6FA3",
        RoutePlaybackNodeState.Active => "#E08A00",
        RoutePlaybackNodeState.Mismatch or RoutePlaybackNodeState.Failed => "#B42318",
        _ => "#9AA8B3",
    };
    public double Thickness => State == RoutePlaybackNodeState.Active ? 5 : 3;
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}


public sealed class RouteMapNode : INotifyPropertyChanged
{
    private RoutePlaybackNodeState _state;
    private string _actualRoomName = string.Empty;
    private string _actualRoomKey = string.Empty;

    public int Step { get; init; }
    public double X { get; init; }
    public double Y { get; init; }
    public string ExpectedRoomKey { get; init; } = string.Empty;
    public string Command { get; init; } = string.Empty;
    public bool IsStart { get; init; }
    public RoutePlaybackNodeState State { get => _state; set { if (_state == value) return; _state = value; Raise(); Raise(nameof(Fill)); Raise(nameof(Border)); Raise(nameof(Size)); Raise(nameof(Label)); } }
    public string ActualRoomName { get => _actualRoomName; set { if (_actualRoomName == value) return; _actualRoomName = value; Raise(); Raise(nameof(Label)); Raise(nameof(ToolTip)); } }
    public string ActualRoomKey { get => _actualRoomKey; set { if (_actualRoomKey == value) return; _actualRoomKey = value; Raise(); Raise(nameof(Label)); Raise(nameof(ToolTip)); } }
    public double Size => State == RoutePlaybackNodeState.Active ? 20 : IsStart ? 18 : 15;
    public string Fill => State switch
    {
        RoutePlaybackNodeState.Verified => "#278A46",
        RoutePlaybackNodeState.Confirmed => "#2F6FA3",
        RoutePlaybackNodeState.Active => "#F6A623",
        RoutePlaybackNodeState.Mismatch or RoutePlaybackNodeState.Failed => "#B42318",
        _ => IsStart ? "#E6EEF4" : "#F7FAFC",
    };
    public string Border => State is RoutePlaybackNodeState.Mismatch or RoutePlaybackNodeState.Failed ? "#7A1510" : "#D7E2EA";
    public string Label
    {
        get
        {
            if (IsStart) return "START";
            if (!string.IsNullOrWhiteSpace(ActualRoomName)) return ActualRoomName;
            if (!string.IsNullOrWhiteSpace(ExpectedRoomKey)) return ExpectedRoomKey;
            return $"Step {Step}";
        }
    }
    public string ToolTip => $"Step {Step}: {Command}\nExpected: {ExpectedRoomKey}\nActual: {ActualRoomName} {ActualRoomKey}".Trim();

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
