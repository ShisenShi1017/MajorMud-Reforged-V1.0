using System.Collections.ObjectModel;
using Reforged.App.Models;
using Reforged.App.Services;

namespace Reforged.App.ViewModels;

public sealed class PlayViewModel : ViewModelBase
{
    private readonly IClientDataService _data;
    private readonly IClientAutomationService _automation;
    private readonly IClientCoordinationService _coordination;
    private readonly IMegaMudPathService _paths;
    private readonly IClientSessionService _sessions;
    private readonly IClientParsingProfileService _parsing;
    private readonly IClientLibrariumBridgeService _librariumBridge;
    private readonly IClientSysGotoService _sysGoto;
    private readonly IClientMapProfileService _mapProfiles;
    private readonly IClientWorkspaceProfileService _workspaceProfiles;
    private readonly IClientConnectionProfileService _connectionProfiles = new ClientConnectionProfileService();
    private readonly Dictionary<string, string> _lastRecordedMapRoomBySession = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, MegaMudPathRecord> _autoRoamRoutes = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _autoRoamRestarting = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CancellationTokenSource> _manualBatchCancellation = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTimeOffset> _lastAwayReplyBySessionAndSender = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTimeOffset> _nextCombatAutomationAt = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _combatAutomationInFlight = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTimeOffset> _combatSpellCooldowns = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (string Command, string Target, DateTimeOffset ExpiresAt)> _pendingCombatSpellFallback = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _combatObservedSessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _manualRetargetBlockedRoomBySession = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _survivalAutomationInFlight = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTimeOffset> _nextSurvivalAutomationAt = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _pendingRecoveryPostCommand = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _roamingProcessedRoomBySession = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _roamingAutomationInFlight = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTimeOffset> _nextObstacleRetryAt = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _lootAutomationInFlight = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTimeOffset> _nextLootAutomationAt = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _lastLootSnapshotBySession = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _partyAutomationInFlight = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTimeOffset> _nextPartyAutomationAt = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _partyWaitingForLeader = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _partyFollowingLeader = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _spellMaintenanceInFlight = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTimeOffset> _nextSpellMaintenanceAt = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _lightedRoomBySession = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (string Command, DateTimeOffset ExpiresAt)> _pendingMaintainedCast = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTimeOffset> _nextPvpActionAt = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (string Attacker, DateTimeOffset ObservedAt)> _pvpAttackers = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _pvpActionInFlight = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _authoritativeStateBootstrapped = new(StringComparer.OrdinalIgnoreCase);
    private ClientDataCategory? _selectedCategory;
    private ClientDataRecord? _selectedRecord;
    private ClientAutomationRule? _selectedRule;
    private ClientScheduledEvent? _selectedScheduledEvent;
    private ClientPriorityAction? _selectedSpellPriority;
    private ClientPriorityAction? _selectedCombatPriority;
    private ClientCurrencyRule? _selectedCurrencyRule;
    private AutomationArea? _selectedAutomationArea;
    private string _searchText = string.Empty;
    private CoordinatedSession? _selectedSession;
    private TrustedController? _selectedController;
    private string _coordinationTarget = "followers";
    private string _coordinationCommand = string.Empty;
    private string _chatSender = string.Empty;
    private string _chatMessage = "@do go hole";
    private string _pathSearchText = string.Empty;
    private MegaMudPathRecord? _selectedPath;
    private ImportedPathEntry? _selectedLocalPath;
    private string _recordingRouteName = "Recorded Route";
    private string? _selectedRouteCommand;
    private string _routeCommandEditor = string.Empty;
    private ClientSysGotoDestination? _selectedSysGotoDestination;
    private string _newSysGotoCityName = "Newhaven";
    private string _newSysGotoCommandArgument = "Newhaven";
    private ClientSessionConnection? _selectedLiveSession;
    private string _terminalCommand = string.Empty;
    private string _status = "Loading Host data…";
    private ClientParsingRule? _selectedParsingRule;
    private string _parsingSampleLine = "HP: 125/200 MP: 80/100";
    private ClientParsingTestMatch? _parsingTestResult;
    private ClientParsingProfileSummary? _selectedParsingProfileSummary;
    private string _duplicateParsingName = "Profile Copy";
    private string _duplicateParsingServer = "Server Copy";
    private string _duplicateParsingWorld = "Classic";
    private ClientLibrariumSnapshot _librariumSnapshot = new();
    private ClientSavedBuildSnapshot? _selectedSavedBuildA;
    private ClientSavedBuildSnapshot? _selectedSavedBuildB;
    private string _savedBuildName = "Saved Build";
    private ClientRoomMetadata? _selectedMapRoom;
    private int _spawnTimerMinutes = 60;
    private string _spawnTimerLabel = "Spawn";
    private string _mapRoomSearch = string.Empty;
    private bool _mapFavoritesOnly;
    private ClientConnectionProfile? _selectedConnectionProfile;

    public ObservableCollection<ClientDataCategory> Categories { get; } = [];
    public ObservableCollection<ClientDataRecord> VisibleRecords { get; } = [];
    public ObservableCollection<ClientAutomationRule> VisibleRules { get; } = [];
    public ObservableCollection<MegaMudPathRecord> VisiblePaths { get; } = [];
    public ObservableCollection<ImportedPathEntry> LocalPaths => _paths.ImportedProfile.Paths;
    public ObservableCollection<ClientSessionConnection> LiveSessions => _sessions.Sessions;
    public ObservableCollection<ClientConnectionProfile> ConnectionProfiles => _connectionProfiles.Profiles;
    public ObservableCollection<ClientSavedBuildSnapshot> SavedBuilds { get; } = [];
    public ObservableCollection<ClientRoomMetadata> VisibleMapRooms { get; } = [];
    public ClientMapProfile MapProfile => _mapProfiles.Profile;
    public IReadOnlyList<ClientMapPolicy> MapPolicies { get; } = Enum.GetValues<ClientMapPolicy>();
    public IReadOnlyList<ClientMapStyle> MapStyles { get; } = Enum.GetValues<ClientMapStyle>();
    public ClientWorkspaceProfile WorkspaceProfile => _workspaceProfiles.Profile;
    public IReadOnlyList<ClientWorkspaceTheme> WorkspaceThemes { get; } = Enum.GetValues<ClientWorkspaceTheme>();
    public IReadOnlyList<ClientLayoutPreset> WorkspaceLayoutPresets { get; } = Enum.GetValues<ClientLayoutPreset>();
    public IReadOnlyList<ClientTerminalViewMode> TerminalViewModes { get; } = [ClientTerminalViewMode.Reforged];
    public ClientRoomMetadata? SelectedMapRoom { get => _selectedMapRoom; set => Set(ref _selectedMapRoom, value); }
    public int SpawnTimerMinutes { get => _spawnTimerMinutes; set => Set(ref _spawnTimerMinutes, Math.Clamp(value, 1, 10080)); }
    public string SpawnTimerLabel { get => _spawnTimerLabel; set => Set(ref _spawnTimerLabel, value); }
    public string MapRoomSearch { get => _mapRoomSearch; set { if (Set(ref _mapRoomSearch, value)) RefreshMapRooms(); } }
    public bool MapFavoritesOnly { get => _mapFavoritesOnly; set { if (Set(ref _mapFavoritesOnly, value)) RefreshMapRooms(); } }
    public ObservableCollection<ClientBuildComparisonRow> SavedBuildComparison { get; } = [];
    public IReadOnlyList<string> Classifications { get; } = ["Default", "Friend", "Neutral", "Enemy", "Ignore"];
    public IReadOnlyList<string> Highlights { get; } = ["Default", "Blue", "Green", "Yellow", "Orange", "Red", "Purple"];
    public IReadOnlyList<AutomationArea> AutomationAreas { get; } = Enum.GetValues<AutomationArea>();
    public IReadOnlyList<CoordinationRole> CoordinationRoles { get; } = Enum.GetValues<CoordinationRole>();
    public IReadOnlyList<string> CoordinationTargets { get; } = ["followers", "all", "tank", "healer", "support"];
    public IReadOnlyList<ClientParsingRuleKind> ParsingRuleKinds { get; } = Enum.GetValues<ClientParsingRuleKind>();
    public ClientParsingProfile ParsingProfile => _parsing.Profile;
    public ClientParsingRule? SelectedParsingRule { get => _selectedParsingRule; set => Set(ref _selectedParsingRule, value); }
    public IReadOnlyList<ClientParsingProfileSummary> AvailableParsingProfiles => _parsing.AvailableProfiles;
    public string ParsingSampleLine { get => _parsingSampleLine; set => Set(ref _parsingSampleLine, value); }
    public ClientParsingTestMatch? ParsingTestResult { get => _parsingTestResult; private set => Set(ref _parsingTestResult, value); }
    public ClientParsingProfileSummary? SelectedParsingProfileSummary { get => _selectedParsingProfileSummary; set => Set(ref _selectedParsingProfileSummary, value); }
    public string DuplicateParsingName { get => _duplicateParsingName; set => Set(ref _duplicateParsingName, value); }
    public string DuplicateParsingServer { get => _duplicateParsingServer; set => Set(ref _duplicateParsingServer, value); }
    public string DuplicateParsingWorld { get => _duplicateParsingWorld; set => Set(ref _duplicateParsingWorld, value); }
    public ClientLibrariumSnapshot LibrariumSnapshot { get => _librariumSnapshot; private set => Set(ref _librariumSnapshot, value); }
    public ClientSavedBuildSnapshot? SelectedSavedBuildA { get => _selectedSavedBuildA; set { if (Set(ref _selectedSavedBuildA, value)) RefreshSavedBuildComparison(); } }
    public ClientSavedBuildSnapshot? SelectedSavedBuildB { get => _selectedSavedBuildB; set { if (Set(ref _selectedSavedBuildB, value)) RefreshSavedBuildComparison(); } }
    public string SavedBuildName { get => _savedBuildName; set => Set(ref _savedBuildName, value); }
    public IReadOnlyList<string> TriggerKinds { get; } = ["health-below", "health-above", "mana-below", "mana-above", "in-combat", "out-of-combat", "combat-started", "combat-ended", "resting", "moving", "room-entered", "text-contains", "always"];

    public ClientDataCategory? SelectedCategory { get => _selectedCategory; set { if (Set(ref _selectedCategory, value)) RefreshRecords(); } }
    public ClientDataRecord? SelectedRecord { get => _selectedRecord; set => Set(ref _selectedRecord, value); }
    public ClientAutomationRule? SelectedRule { get => _selectedRule; set => Set(ref _selectedRule, value); }
    public AutomationArea? SelectedAutomationArea { get => _selectedAutomationArea; set { if (Set(ref _selectedAutomationArea, value)) RefreshRules(); } }
    public ClientAutomationProfile AutomationProfile => _automation.Profile;
    public ClientAutomationOptions AutomationOptions => _automation.Profile.Options;
    public ClientScheduledEvent? SelectedScheduledEvent { get => _selectedScheduledEvent; set => Set(ref _selectedScheduledEvent, value); }
    public ClientPriorityAction? SelectedSpellPriority { get => _selectedSpellPriority; set => Set(ref _selectedSpellPriority, value); }
    public ClientPriorityAction? SelectedCombatPriority { get => _selectedCombatPriority; set => Set(ref _selectedCombatPriority, value); }
    public ClientCurrencyRule? SelectedCurrencyRule { get => _selectedCurrencyRule; set => Set(ref _selectedCurrencyRule, value); }
    public IReadOnlyList<string> ClientThemes { get; } = ["Classic", "Modern", "Dark", "Light", "High Contrast", "Custom"];
    public ClientCoordinationProfile CoordinationProfile => _coordination.Profile;
    public CoordinatedSession? SelectedSession { get => _selectedSession; set => Set(ref _selectedSession, value); }
    public TrustedController? SelectedController { get => _selectedController; set => Set(ref _selectedController, value); }
    public string CoordinationTarget { get => _coordinationTarget; set => Set(ref _coordinationTarget, value); }
    public string CoordinationCommand { get => _coordinationCommand; set => Set(ref _coordinationCommand, value); }
    public string ChatSender { get => _chatSender; set => Set(ref _chatSender, value); }
    public string ChatMessage { get => _chatMessage; set => Set(ref _chatMessage, value); }
    public string SearchText { get => _searchText; set { if (Set(ref _searchText, value)) RefreshRecords(); } }
    public string PathSearchText { get => _pathSearchText; set { if (Set(ref _pathSearchText, value)) RefreshPaths(); } }
    public MegaMudPathRecord? SelectedPath { get => _selectedPath; set => Set(ref _selectedPath, value); }
    public ImportedPathEntry? SelectedLocalPath { get => _selectedLocalPath; set => Set(ref _selectedLocalPath, value); }
    public string RecordingRouteName { get => _recordingRouteName; set => Set(ref _recordingRouteName, value); }
    public RouteRecordingState RouteRecording => _paths.Recording;
    public string? SelectedRouteCommand { get => _selectedRouteCommand; set { if (Set(ref _selectedRouteCommand, value)) RouteCommandEditor = value ?? string.Empty; } }
    public string RouteCommandEditor { get => _routeCommandEditor; set => Set(ref _routeCommandEditor, value); }
    public ClientSessionConnection? SelectedLiveSession
    {
        get => _selectedLiveSession;
        set
        {
            if (!Set(ref _selectedLiveSession, value))
                return;

            SelectConnectionProfileForSession(value);
            if (value is not null)
                _ = SelectSessionProfilesAsync(value);
        }
    }
    public ClientConnectionProfile? SelectedConnectionProfile
    {
        get => _selectedConnectionProfile;
        set
        {
            if (Set(ref _selectedConnectionProfile, value))
                Raise(nameof(ConnectionProfileSummary));
        }
    }
    public string ConnectionProfileSummary => SelectedConnectionProfile is null
        ? "No saved character profile is assigned to this session."
        : $"{SelectedConnectionProfile.DisplaySummary} • {SelectedConnectionProfile.EndpointSummary}";
    public string TerminalCommand { get => _terminalCommand; set => Set(ref _terminalCommand, value); }
    public ClientSysGotoProfile SysGotoProfile => _sysGoto.Profile;
    public ClientSysGotoDestination? SelectedSysGotoDestination { get => _selectedSysGotoDestination; set => Set(ref _selectedSysGotoDestination, value); }
    public string NewSysGotoCityName { get => _newSysGotoCityName; set => Set(ref _newSysGotoCityName, value); }
    public string NewSysGotoCommandArgument { get => _newSysGotoCommandArgument; set => Set(ref _newSysGotoCommandArgument, value); }

    public string PathSourceDescription => _paths.SourceDescription;
    public string Status { get => _status; private set => Set(ref _status, value); }
    public string ScopeLabel => "Server: Local Host   •   World: Classic   •   Changes: Client only";
    public string AutomationPolicyLabel => AutomationProfile.Capabilities.EmergencyStop
        ? "Emergency Stop is active. Every automatic command is blocked."
        : "Independent client automation switches are active. Every generated action is sent as a normal Host command.";

    public PlayViewModel(IClientDataService data, IClientAutomationService automation, IClientCoordinationService coordination, IMegaMudPathService paths, IClientSessionService sessions, IClientParsingProfileService parsing, IClientLibrariumBridgeService librariumBridge, IClientSysGotoService sysGoto, IClientMapProfileService mapProfiles, IClientWorkspaceProfileService workspaceProfiles)
    {
        _data = data;
        _automation = automation;
        _coordination = coordination;
        _paths = paths;
        _sessions = sessions;
        _parsing = parsing;
        _librariumBridge = librariumBridge;
        _sysGoto = sysGoto;
        _mapProfiles = mapProfiles;
        _workspaceProfiles = workspaceProfiles;
        _sessions.LineReceived += SessionLineReceived;
        _sessions.CommandSent += SessionCommandSent;
        _sessions.StateChanged += SessionStateChanged;
        _sessions.RouteChanged += SessionRouteChanged;
        _coordination.CommandRequested += CoordinationCommandRequested;
        _coordination.ResponseRequested += CoordinationResponseRequested;
        _coordination.QueryRequested += CoordinationQueryRequested;
        _automation.CommandRequested += AutomationCommandRequested;
    }

    private void SessionStateChanged(object? sender, ClientSessionStateEvent e)
    {
        Status = e.Message;
        var session = _sessions.Sessions.FirstOrDefault(candidate =>
            candidate.SessionId.Equals(e.SessionId, StringComparison.OrdinalIgnoreCase));
        if (session is null) return;

        if (e.State is ClientConnectionState.Disconnected or ClientConnectionState.Faulted or ClientConnectionState.Reconnecting)
        {
            ClearTransientAutomationState(session.SessionId);
            session.CombatAutomationStatus = "Waiting for a verified live connection.";
            session.PartyAutomationStatus = "Waiting for a fresh party roster after reconnect.";
            session.PvpAutomationStatus = "Waiting for fresh room and PvP state after reconnect.";
            session.SpellAutomationStatus = "Waiting for fresh spell and effect state after reconnect.";
            session.LootAutomationStatus = "Waiting for fresh room inventory after reconnect.";
            session.RoamingAutomationStatus = "Waiting for a verified room after reconnect.";
            return;
        }

        if (e.State != ClientConnectionState.Connected) return;
        var coordinated = CoordinationProfile.Sessions.FirstOrDefault(candidate =>
            candidate.SessionId.Equals(session.SessionId, StringComparison.OrdinalIgnoreCase));
        if (coordinated is not null && CoordinationProfile.AutoResyncOnReconnect)
        {
            coordinated.PendingCommands = 0;
            coordinated.LastAcknowledgedSequence = coordinated.LastSequence;
            coordinated.LastAcknowledgedAt = DateTimeOffset.UtcNow;
            coordinated.NeedsResync = false;
            coordinated.LastResult = "Resynchronized after reconnect";
        }
        // TCP Connected is intentionally not treated as MajorMUD logged in.
        // The first authoritative refresh is triggered only after inbound room
        // or HP-prompt evidence proves the character is actually in-game.
    }

    private void ClearTransientAutomationState(string sessionId)
    {
        _combatAutomationInFlight.Remove(sessionId);
        _combatObservedSessions.Remove(sessionId);
        _combatSpellCooldowns.Remove(sessionId);
        _pendingCombatSpellFallback.Remove(sessionId);
        _manualRetargetBlockedRoomBySession.Remove(sessionId);
        _survivalAutomationInFlight.Remove(sessionId);
        _nextSurvivalAutomationAt.Remove(sessionId);
        _pendingRecoveryPostCommand.Remove(sessionId);
        _roamingProcessedRoomBySession.Remove(sessionId);
        _roamingAutomationInFlight.Remove(sessionId);
        _nextObstacleRetryAt.Remove(sessionId);
        _lootAutomationInFlight.Remove(sessionId);
        _nextLootAutomationAt.Remove(sessionId);
        _lastLootSnapshotBySession.Remove(sessionId);
        _partyAutomationInFlight.Remove(sessionId);
        _nextPartyAutomationAt.Remove(sessionId);
        _partyWaitingForLeader.Remove(sessionId);
        _partyFollowingLeader.Remove(sessionId);
        _spellMaintenanceInFlight.Remove(sessionId);
        _nextSpellMaintenanceAt.Remove(sessionId);
        _lightedRoomBySession.Remove(sessionId);
        _pendingMaintainedCast.Remove(sessionId);
        _nextPvpActionAt.Remove(sessionId);
        _pvpAttackers.Remove(sessionId);
        _pvpActionInFlight.Remove(sessionId);
        _authoritativeStateBootstrapped.Remove(sessionId);
    }

    private void EnsureAuthoritativeStateBootstrap(ClientSessionConnection session)
    {
        if (!ClientCommandIsolationPolicy.HasVerifiedGameplay(session)) return;
        if (!_authoritativeStateBootstrapped.Add(session.SessionId)) return;
        _ = RefreshAuthoritativeClientStateAsync(session);
    }

    private async Task RefreshAuthoritativeClientStateAsync(ClientSessionConnection session)
    {
        try
        {
            if (!ClientCommandIsolationPolicy.HasVerifiedGameplay(session)) return;

            if (!ClientCommandIsolationPolicy.SupportsReforgedPrivateProtocol(session))
            {
                // Legacy MajorMUD/BBS servers do not implement ROOMDATA or
                // CLIENTDATA. Ask for the native room display instead; the
                // Classic room parser extracts the visible exits from LOOK.
                await _sessions.SendCommandAsAsync(session.SessionId, "look", ClientCommandOrigin.ClientAutomation);
                return;
            }

            foreach (var command in new[] { "clientdata stats", "clientdata spells", "clientdata party", "clientdata pvp", "roomdata" })
            {
                if (!ClientCommandIsolationPolicy.HasVerifiedGameplay(session)) return;
                await _sessions.SendClientProtocolCommandAsync(session.SessionId, command);
            }
        }
        catch (Exception exception)
        {
            Status = $"Logged in, but the authoritative client-state refresh stopped safely: {exception.Message}";
        }
    }

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_data.LoadAsync(), _automation.LoadAsync(), _coordination.LoadAsync(), _paths.LoadAsync(), _parsing.LoadAsync(), _workspaceProfiles.LoadAsync(), _connectionProfiles.LoadAsync(), LoadSavedBuildsAsync());
        Categories.Clear();
        foreach (var category in _data.Categories) Categories.Add(category);
        SelectedCategory = Categories.FirstOrDefault(c => c.Records.Count > 0) ?? Categories.FirstOrDefault();
        SelectedAutomationArea = AutomationAreas.FirstOrDefault();
        Raise(nameof(AutomationProfile));
        Raise(nameof(AutomationOptions));
        Raise(nameof(CoordinationProfile));
        Raise(nameof(WorkspaceProfile));
        Raise(nameof(ParsingProfile));
        Raise(nameof(AvailableParsingProfiles));
        SelectedParsingRule = ParsingProfile.Rules.FirstOrDefault();
        SelectedParsingProfileSummary = AvailableParsingProfiles.FirstOrDefault();
        SelectedSession = CoordinationProfile.Sessions.FirstOrDefault();
        SelectedController = CoordinationProfile.TrustedControllers.FirstOrDefault();
        foreach (var coordinated in CoordinationProfile.Sessions)
        {
            var live = _sessions.EnsureSession(coordinated.SessionId, coordinated.DisplayName, coordinated.CharacterName);
            live.TerminalViewMode = coordinated.TerminalViewMode;
            var assignedProfile = _connectionProfiles.GetAssignedProfile(live.SessionId);
            if (assignedProfile is not null)
            {
                _connectionProfiles.ApplyToSession(assignedProfile, live);
                _sessions.ResizeClassicTerminal(live.SessionId, assignedProfile.TerminalColumns, assignedProfile.TerminalRows);
            }
        }
        if (_sessions.Sessions.Count == 0)
        {
            var coordinated = new CoordinatedSession { DisplayName = "Session 1", Role = CoordinationRole.Leader };
            CoordinationProfile.Sessions.Add(coordinated);
            _sessions.EnsureSession(coordinated.SessionId, coordinated.DisplayName, coordinated.CharacterName);
        }
        SelectedLiveSession = _sessions.Sessions.FirstOrDefault();
        RefreshPaths();
        SelectedLocalPath = LocalPaths.FirstOrDefault();
        Raise(nameof(PathSourceDescription));
        Status = $"{_data.SourceDescription} • Reforged client ready • Emergency Stop is active by default.";
    }


    public void CreateNewConnectionProfile() =>
        CreateNewConnectionProfile(SelectedLiveSession);

    public void CreateNewConnectionProfile(ClientSessionConnection? session)
    {
        SelectedConnectionProfile = _connectionProfiles.CreateNew(session);
        Status = "Created a new Client-only character profile. Edit it, save it, then apply it to a disconnected session.";
        Raise(nameof(ConnectionProfiles));
        Raise(nameof(ConnectionProfileSummary));
    }

    public async Task SaveSelectedConnectionProfileAsync()
    {
        if (SelectedConnectionProfile is null)
        {
            Status = "Select or create a character profile first.";
            return;
        }

        SelectedConnectionProfile.UpdatedAt = DateTimeOffset.Now;
        await _connectionProfiles.SaveAsync();
        Status = $"Saved character profile '{SelectedConnectionProfile.ProfileName}'. Passwords are never stored.";
        Raise(nameof(ConnectionProfiles));
        Raise(nameof(ConnectionProfileSummary));
    }

    public Task SaveCurrentConnectionProfileAsync() =>
        SaveConnectionProfileAsync(SelectedLiveSession);

    public async Task SaveConnectionProfileAsync(ClientSessionConnection? session)
    {
        if (session is null)
        {
            Status = "Select a session before saving a character profile.";
            return;
        }

        var profile = _connectionProfiles.GetAssignedProfile(session.SessionId);
        if (profile is null)
        {
            var baseName = string.IsNullOrWhiteSpace(session.CharacterName)
                ? $"{session.ServerProfileName} Character"
                : session.CharacterName;
            profile = _connectionProfiles.CreateFromSession(session, baseName);
        }
        else
        {
            _connectionProfiles.UpdateFromSession(profile, session);
        }

        _connectionProfiles.AssignToSession(session.SessionId, profile.ProfileId);
        SyncCoordinationFromLiveSession(session);
        await Task.WhenAll(_connectionProfiles.SaveAsync(), _coordination.SaveAsync());
        SelectedConnectionProfile = profile;
        Status = $"Saved '{profile.ProfileName}' for {profile.ServerProfileName} / {profile.WorldName}. Passwords were not saved.";
        Raise(nameof(ConnectionProfiles));
        Raise(nameof(ConnectionProfileSummary));
    }

    public Task SaveCurrentConnectionProfileAsAsync() =>
        SaveConnectionProfileAsAsync(SelectedLiveSession);

    public async Task SaveConnectionProfileAsAsync(ClientSessionConnection? session)
    {
        if (session is null)
        {
            Status = "Select a session before using Save As.";
            return;
        }

        var baseName = string.IsNullOrWhiteSpace(session.CharacterName)
            ? $"{session.ServerProfileName} Character"
            : $"{session.CharacterName} - {session.ServerProfileName}";
        var profile = _connectionProfiles.CreateFromSession(session, baseName);
        _connectionProfiles.AssignToSession(session.SessionId, profile.ProfileId);
        SyncCoordinationFromLiveSession(session);
        await Task.WhenAll(_connectionProfiles.SaveAsync(), _coordination.SaveAsync());
        SelectedConnectionProfile = profile;
        Status = $"Saved a new character profile as '{profile.ProfileName}'. Passwords were not saved.";
        Raise(nameof(ConnectionProfiles));
        Raise(nameof(ConnectionProfileSummary));
    }

    public async Task DuplicateSelectedConnectionProfileAsync()
    {
        if (SelectedConnectionProfile is null)
        {
            Status = "Select a character profile to duplicate.";
            return;
        }

        var duplicate = _connectionProfiles.Duplicate(
            SelectedConnectionProfile,
            $"{SelectedConnectionProfile.ProfileName} Copy");
        await _connectionProfiles.SaveAsync();
        SelectedConnectionProfile = duplicate;
        Status = $"Created '{duplicate.ProfileName}'. It is not applied to a session yet.";
        Raise(nameof(ConnectionProfiles));
        Raise(nameof(ConnectionProfileSummary));
    }

    public async Task DeleteSelectedConnectionProfileAsync()
    {
        if (SelectedConnectionProfile is null)
        {
            Status = "Select a character profile to delete.";
            return;
        }

        var deletedName = SelectedConnectionProfile.ProfileName;
        _connectionProfiles.Delete(SelectedConnectionProfile);
        await _connectionProfiles.SaveAsync();
        SelectedConnectionProfile = SelectedLiveSession is null
            ? ConnectionProfiles.FirstOrDefault()
            : _connectionProfiles.FindBestMatch(SelectedLiveSession);
        Status = $"Deleted Client-only character profile '{deletedName}'. No Host or MajorMUD data was changed.";
        Raise(nameof(ConnectionProfiles));
        Raise(nameof(ConnectionProfileSummary));
    }

    public async Task ApplySelectedConnectionProfileAsync()
    {
        if (SelectedConnectionProfile is null || SelectedLiveSession is null)
        {
            Status = "Select both a saved character profile and a session.";
            return;
        }

        if (SelectedLiveSession.ConnectionState is ClientConnectionState.Connected or ClientConnectionState.Connecting or ClientConnectionState.Reconnecting)
        {
            Status = "Disconnect the session before applying a different character profile.";
            return;
        }

        _connectionProfiles.ApplyToSession(SelectedConnectionProfile, SelectedLiveSession);
        _sessions.ResizeClassicTerminal(
            SelectedLiveSession.SessionId,
            SelectedConnectionProfile.TerminalColumns,
            SelectedConnectionProfile.TerminalRows);
        _connectionProfiles.AssignToSession(SelectedLiveSession.SessionId, SelectedConnectionProfile.ProfileId);
        SyncCoordinationFromLiveSession(SelectedLiveSession);
        await Task.WhenAll(_connectionProfiles.SaveAsync(), _coordination.SaveAsync());
        await SelectSessionProfilesAsync(SelectedLiveSession);
        Status = $"Applied '{SelectedConnectionProfile.ProfileName}' to {SelectedLiveSession.DisplayName}. The profile will not auto-connect or send login text.";
        Raise(nameof(SelectedLiveSession));
        Raise(nameof(ConnectionProfileSummary));
    }

    private void SelectConnectionProfileForSession(ClientSessionConnection? session)
    {
        SelectedConnectionProfile = session is null
            ? ConnectionProfiles.FirstOrDefault()
            : _connectionProfiles.FindBestMatch(session);
    }

    private void SyncCoordinationFromLiveSession(ClientSessionConnection session)
    {
        var coordinated = CoordinationProfile.Sessions.FirstOrDefault(item =>
            string.Equals(item.SessionId, session.SessionId, StringComparison.OrdinalIgnoreCase));
        if (coordinated is null)
            return;

        coordinated.DisplayName = session.DisplayName;
        coordinated.CharacterName = session.CharacterName;
        coordinated.TerminalViewMode = session.TerminalViewMode;
    }


    public async Task SelectParsingProfileForSessionAsync(ClientSessionConnection session)
    {
        await _parsing.SelectProfileAsync(session.ServerProfileName, session.WorldName);
        RefreshParsingProfileView();
        Status = $"Using parsing profile '{ParsingProfile.Name}' for {session.ServerProfileName} / {session.WorldName}.";
    }

    public async Task ApplySelectedSessionParsingProfileAsync()
    {
        if (SelectedLiveSession is null) return;
        await SelectParsingProfileForSessionAsync(SelectedLiveSession);
    }

    private async Task SelectSessionProfilesAsync(ClientSessionConnection session)
    {
        await SelectParsingProfileForSessionAsync(session);
        await _sysGoto.SelectProfileAsync(session.ServerProfileName, session.WorldName);
        await _mapProfiles.SelectProfileAsync(session.ServerProfileName, session.WorldName, session.CharacterName);
        Raise(nameof(SysGotoProfile));
        Raise(nameof(MapProfile));
        RefreshMapRooms();
        SelectedMapRoom = MapProfile.Rooms.FirstOrDefault(r => string.Equals(r.RoomKey, session.CurrentRoomKey, StringComparison.OrdinalIgnoreCase)) ?? MapProfile.Rooms.FirstOrDefault();
        SelectedSysGotoDestination = SysGotoProfile.Destinations.FirstOrDefault();
    }

    public async Task SelectParsingProfileFromListAsync()
    {
        if (SelectedParsingProfileSummary is null) return;
        await _parsing.SelectProfileByKeyAsync(SelectedParsingProfileSummary.Key);
        RefreshParsingProfileView();
        Status = $"Selected Client profile '{ParsingProfile.Name}'.";
    }

    public async Task DuplicateParsingProfileAsync()
    {
        try
        {
            await _parsing.DuplicateActiveAsync(DuplicateParsingName, DuplicateParsingServer, DuplicateParsingWorld);
            RefreshParsingProfileView();
            Status = $"Created Client-only profile '{ParsingProfile.Name}' for {ParsingProfile.ServerName} / {ParsingProfile.WorldName}.";
        }
        catch (Exception exception) { Status = $"Profile was not duplicated: {exception.Message}"; }
    }

    public async Task DeleteParsingProfileAsync()
    {
        try
        {
            var name = ParsingProfile.Name;
            await _parsing.DeleteActiveAsync();
            RefreshParsingProfileView();
            Status = $"Deleted Client-only profile '{name}'. Host settings were not changed.";
        }
        catch (Exception exception) { Status = $"Profile was not deleted: {exception.Message}"; }
    }

    public async Task ExportParsingProfileAsync(string path)
    {
        try { await _parsing.ExportActiveAsync(path); Status = $"Exported '{ParsingProfile.Name}' as a portable Client profile."; }
        catch (Exception exception) { Status = $"Profile was not exported: {exception.Message}"; }
    }

    public async Task ImportParsingProfileAsync(string path)
    {
        try
        {
            await _parsing.ImportAsync(path);
            RefreshParsingProfileView();
            Status = $"Imported Client-only profile '{ParsingProfile.Name}'.";
        }
        catch (Exception exception) { Status = $"Profile was not imported: {exception.Message}"; }
    }

    private void RefreshParsingProfileView()
    {
        Raise(nameof(ParsingProfile));
        Raise(nameof(AvailableParsingProfiles));
        SelectedParsingProfileSummary = AvailableParsingProfiles.FirstOrDefault(p => p.ServerName == ParsingProfile.ServerName && p.WorldName == ParsingProfile.WorldName);
        SelectedParsingRule = ParsingProfile.Rules.FirstOrDefault();
        DuplicateParsingName = $"{ParsingProfile.Name} Copy";
        DuplicateParsingServer = $"{ParsingProfile.ServerName} Copy";
        DuplicateParsingWorld = ParsingProfile.WorldName;
    }

    public void TestParsingSample()
    {
        ParsingTestResult = _parsing.TestLine(ParsingSampleLine);
        Status = ParsingTestResult.Message;
    }

    public async Task SaveParsingProfileAsync()
    {
        try
        {
            await _parsing.SaveAsync();
            Status = $"Saved Client-only parsing profile for {ParsingProfile.ServerName} / {ParsingProfile.WorldName}.";
        }
        catch (Exception exception)
        {
            Status = $"Parsing profile was not saved: {exception.Message}";
        }
    }

    public void AddParsingRule()
    {
        _parsing.AddRule(ClientParsingRuleKind.RoomTitle);
        SelectedParsingRule = ParsingProfile.Rules.LastOrDefault();
    }

    public void RemoveParsingRule()
    {
        var selected = SelectedParsingRule;
        _parsing.RemoveRule(selected);
        SelectedParsingRule = ParsingProfile.Rules.FirstOrDefault();
    }

    public void RestoreParsingDefaults()
    {
        _parsing.RestoreDefaults();
        Raise(nameof(ParsingProfile));
        SelectedParsingRule = ParsingProfile.Rules.FirstOrDefault();
        Status = "Restored the built-in Classic recognition defaults. Save to keep them for this Client profile.";
    }

    public async Task SaveSelectedAsync()
    {
        if (SelectedRecord is null) return;
        await _data.SaveOverrideAsync(SelectedRecord);
        Raise(nameof(SelectedRecord));
        Status = $"Saved Client-only settings for {SelectedRecord.Name}. Host data was not changed.";
    }

    public async Task ResetSelectedAsync()
    {
        if (SelectedRecord is null) return;
        await _data.ResetOverrideAsync(SelectedRecord);
        Raise(nameof(SelectedRecord));
        Status = $"Reset local settings for {SelectedRecord.Name}.";
    }


    public async Task ToggleAutomationAreaAsync(string areaName)
    {
        if (!Enum.TryParse<AutomationArea>(areaName, true, out var area)) return;
        var rules = AutomationProfile.Rules.Where(r => r.Area == area).ToArray();
        var enable = rules.Any(r => !r.Enabled);
        foreach (var rule in rules) rule.Enabled = enable;
        RefreshRules();
        await _automation.SaveAsync();
        Status = $"{area} auto features {(enable ? "enabled" : "disabled")}.";
    }
    public async Task SaveAutomationAsync()
    {
        await _automation.SaveAsync();
        Status = $"Saved automation profile '{AutomationProfile.Name}'. Host gameplay data was not changed.";
    }

    public async Task SetMasterAutomationAsync(bool enabled)
    {
        _automation.SetMasterEnabled(enabled);
        Raise(nameof(AutomationProfile));
        await _automation.SaveAsync();
        Status = enabled ? "Emergency Stop cleared; individually selected automation switches may run." : "Emergency Stop applied; every automatic command is blocked.";
    }

    public async Task StopAllAutomationAsync()
    {
        _autoRoamRoutes.Clear();
        foreach (var cancellation in _manualBatchCancellation.Values) cancellation.Cancel();
        _manualBatchCancellation.Clear();
        foreach (var session in _sessions.Sessions)
            _sessions.StopRoute(session.SessionId, "Emergency Stop");
        _automation.StopAll();
        Raise(nameof(AutomationProfile));
        await _automation.SaveAsync();
        Status = "STOP ALL applied. Automation is disabled and all future rule evaluation is paused.";
    }

    public async Task RunManualMassActionAsync(ClientSessionConnection? session, string action)
    {
        if (session is null || session.ConnectionState != ClientConnectionState.Connected)
        { Status = "Connect this character before using a mass action."; return; }
        var normalized = action.Trim().ToLowerInvariant();
        IReadOnlyList<string> commands = normalized switch
        {
            "get" => BuildItemCommands("get", session.CurrentRoomItems, includeCoins: !string.IsNullOrWhiteSpace(session.CurrentRoomCurrency)),
            "getitems" => BuildItemCommands("get", session.CurrentRoomItems),
            "getcash" => string.IsNullOrWhiteSpace(session.CurrentRoomCurrency) ? [] : ["get coins"],
            "drop" => BuildItemCommands("drop", session.InventoryItems),
            "equip" => BuildItemCommands("equip", session.InventoryItems),
            "deposit" => ["deposit all"],
            _ => []
        };
        if (commands.Count == 0)
        {
            Status = normalized == "get"
                ? "No visible room items are present in the latest live room snapshot."
                : normalized == "getitems"
                    ? "No visible room items are present in the latest live room snapshot."
                    : normalized == "getcash"
                        ? "No room currency is present in the latest live room snapshot."
                : "No inventory items are present in the latest captured inventory snapshot.";
            return;
        }

        if (_manualBatchCancellation.Remove(session.SessionId, out var existing)) { existing.Cancel(); existing.Dispose(); }
        using var cancellation = new CancellationTokenSource();
        _manualBatchCancellation[session.SessionId] = cancellation;
        var origin = normalized is "getitems" or "getcash" ? ClientCommandOrigin.ClientAutomation : ClientCommandOrigin.Human;
        var sent = 0;
        try
        {
            foreach (var command in commands.Take(250))
            {
                cancellation.Token.ThrowIfCancellationRequested();
                if (session.ConnectionState != ClientConnectionState.Connected) break;
                await _sessions.SendCommandAsAsync(session.SessionId, command, origin, cancellation.Token);
                sent++;
                if (commands.Count > 1) await Task.Delay(Math.Max(500, AutomationProfile.MinimumCommandDelayMs), cancellation.Token);
            }
            Status = $"{normalized.ToUpperInvariant()} ALL sent {sent} validated command(s).";
        }
        catch (OperationCanceledException) { Status = $"{normalized.ToUpperInvariant()} ALL stopped after {sent} command(s)."; }
        catch (Exception exception) { Status = $"{normalized.ToUpperInvariant()} ALL stopped after {sent} command(s): {exception.Message}"; }
        finally
        {
            if (_manualBatchCancellation.TryGetValue(session.SessionId, out var active) && ReferenceEquals(active, cancellation))
                _manualBatchCancellation.Remove(session.SessionId);
        }
    }

    private static IReadOnlyList<string> BuildItemCommands(string verb, IEnumerable<string> displayItems, bool includeCoins = false)
    {
        var commands = new List<string>();
        if (includeCoins) commands.Add("get coins");
        foreach (var display in displayItems)
        {
            var name = display.Trim();
            var quantity = 1;
            var separator = name.IndexOf("x ", StringComparison.OrdinalIgnoreCase);
            if (separator > 0 && int.TryParse(name[..separator].Trim(), out var parsed))
            { quantity = Math.Clamp(parsed, 1, 250); name = name[(separator + 2)..].Trim(); }
            if (name.Length == 0 || name.Equals("coins", StringComparison.OrdinalIgnoreCase)) continue;
            for (var index = 0; index < quantity && commands.Count < 250; index++) commands.Add($"{verb} {name}");
        }
        return commands;
    }

    public void AddAutomationRule()
    {
        var area = SelectedAutomationArea ?? AutomationArea.Utility;
        var rule = new ClientAutomationRule { Area = area, Name = "New Rule", Description = "Client-only automation rule", Priority = 100 };
        AutomationProfile.Rules.Add(rule);
        RefreshRules();
        SelectedRule = rule;
    }

    public void RemoveAutomationRule()
    {
        if (SelectedRule is null) return;
        AutomationProfile.Rules.Remove(SelectedRule);
        RefreshRules();
    }



    public void AddSpellPriority()
    {
        var item = new ClientPriorityAction { Name = "New Spell", Command = "cast ", Priority = AutomationOptions.Spells.PriorityActions.Count * 10 + 10 };
        AutomationOptions.Spells.PriorityActions.Add(item);
        SelectedSpellPriority = item;
        Status = "Added a Client-only spell priority action.";
    }

    public void RemoveSpellPriority()
    {
        if (SelectedSpellPriority is null) return;
        AutomationOptions.Spells.PriorityActions.Remove(SelectedSpellPriority);
        SelectedSpellPriority = AutomationOptions.Spells.PriorityActions.FirstOrDefault();
    }

    public void AddCombatPriority()
    {
        var item = new ClientPriorityAction { Name = "New Attack", Command = "attack", Priority = AutomationOptions.Combat.PriorityActions.Count * 10 + 10, OnlyInCombat = true };
        AutomationOptions.Combat.PriorityActions.Add(item);
        SelectedCombatPriority = item;
        Status = "Added a Client-only combat priority action.";
    }

    public void RemoveCombatPriority()
    {
        if (SelectedCombatPriority is null) return;
        AutomationOptions.Combat.PriorityActions.Remove(SelectedCombatPriority);
        SelectedCombatPriority = AutomationOptions.Combat.PriorityActions.FirstOrDefault();
    }

    public void AddCurrencyRule()
    {
        var item = new ClientCurrencyRule { Name = "New Currency", RecognitionWords = string.Empty, SortOrder = AutomationOptions.LootAndCash.CurrencyRules.Count * 10 + 10 };
        AutomationOptions.LootAndCash.CurrencyRules.Add(item);
        SelectedCurrencyRule = item;
        Status = "Added a Client-only currency recognition rule.";
    }

    public void RemoveCurrencyRule()
    {
        if (SelectedCurrencyRule is null) return;
        AutomationOptions.LootAndCash.CurrencyRules.Remove(SelectedCurrencyRule);
        SelectedCurrencyRule = AutomationOptions.LootAndCash.CurrencyRules.FirstOrDefault();
    }

    public void AddScheduledEvent()
    {
        var item = new ClientScheduledEvent();
        AutomationOptions.Events.ScheduledEvents.Add(item);
        SelectedScheduledEvent = item;
        Status = "Added a Client-only scheduled event.";
    }

    public void RemoveScheduledEvent()
    {
        if (SelectedScheduledEvent is null) return;
        AutomationOptions.Events.ScheduledEvents.Remove(SelectedScheduledEvent);
        SelectedScheduledEvent = AutomationOptions.Events.ScheduledEvents.FirstOrDefault();
        Status = "Removed the selected Client-only scheduled event.";
    }

    public async Task SaveAutomationOptionsAsync()
    {
        await _automation.SaveAsync();
        Status = "Saved Reforged client options. Host and world data were not changed.";
    }

    public async Task SetQuickAutomationOptionAsync(string option, bool enabled)
    {
        switch (option.Trim().ToLowerInvariant())
        {
            case "combat": AutomationOptions.Combat.AutoAttack = enabled; AutomationProfile.Capabilities.CombatEnabled = enabled; break;
            case "preattack": AutomationOptions.Combat.AutoPreAttack = enabled; break;
            case "nuke": AutomationOptions.Spells.AutoNuke = enabled; break;
            case "restheal": AutomationOptions.Health.AutoHeal = enabled; AutomationOptions.Health.AutoRest = enabled; AutomationProfile.Capabilities.HealthRecoveryEnabled = enabled; break;
            case "bless":
                AutomationOptions.Spells.MaintainBless = enabled;
                AutomationProfile.Capabilities.SpellMaintenanceEnabled = enabled || AutomationOptions.Spells.MaintainArmor || AutomationOptions.Spells.MaintainRoomLight;
                break;
            case "light":
                AutomationOptions.Spells.MaintainRoomLight = enabled;
                AutomationProfile.Capabilities.SpellMaintenanceEnabled = enabled || AutomationOptions.Spells.MaintainBless || AutomationOptions.Spells.MaintainArmor;
                break;
            case "getitems": AutomationOptions.LootAndCash.GetItems = enabled; AutomationProfile.Capabilities.LootAutomationEnabled = enabled || AutomationOptions.LootAndCash.GetCash; break;
            case "getcash": AutomationOptions.LootAndCash.GetCash = enabled; AutomationProfile.Capabilities.LootAutomationEnabled = enabled || AutomationOptions.LootAndCash.GetItems; break;
            case "sneak": AutomationOptions.Roaming.AutoSneak = enabled; SynchronizeRoamingCapability(enabled); break;
            case "hide": AutomationOptions.Utility.AutoHide = enabled; SynchronizeRoamingCapability(enabled); break;
            case "search": AutomationOptions.Roaming.AutoSearch = enabled; SynchronizeRoamingCapability(enabled); break;
            case "track": AutomationOptions.Roaming.AutoTrack = enabled; SynchronizeRoamingCapability(enabled); break;
            case "events": AutomationProfile.Capabilities.TimedEventsEnabled = enabled; break;
            default: Status = $"Unknown automation option '{option}'."; return;
        }
        await _automation.SaveAsync();
        Raise(nameof(AutomationOptions));
        Raise(nameof(AutomationProfile));
        Status = option.Equals("combat", StringComparison.OrdinalIgnoreCase)
            ? $"Verified combat automation {(enabled ? "enabled" : "disabled")}. Emergency Stop remains authoritative."
            : $"{option} automation {(enabled ? "enabled" : "disabled")} in the saved client profile. Emergency Stop remains authoritative.";
    }

    private void SynchronizeRoamingCapability(bool enabled) =>
        AutomationProfile.Capabilities.RoamingAutomationEnabled = enabled || AutomationOptions.Roaming.AutoSneak ||
            AutomationOptions.Roaming.AutoSearch || AutomationOptions.Roaming.AutoTrack || AutomationOptions.Utility.AutoHide;

    public async Task SaveCoordinationAsync()
    {
        await _coordination.SaveAsync();
        Status = $"Saved coordination profile '{CoordinationProfile.Name}'. Commands remain Client-side requests sent through each session.";
    }

    public Task SaveSelectedTerminalViewModeAsync() =>
        SaveTerminalViewModeAsync(SelectedLiveSession);

    public async Task SaveTerminalViewModeAsync(ClientSessionConnection? session)
    {
        if (session is null) return;
        session.TerminalViewMode = ClientTerminalViewMode.Reforged;
        var coordinated = CoordinationProfile.Sessions.FirstOrDefault(s => s.SessionId == session.SessionId);
        if (coordinated is not null) coordinated.TerminalViewMode = session.TerminalViewMode;

        var assignedProfile = _connectionProfiles.GetAssignedProfile(session.SessionId);
        if (assignedProfile is not null)
        {
            assignedProfile.TerminalViewMode = session.TerminalViewMode;
            assignedProfile.UpdatedAt = DateTimeOffset.Now;
        }

        await Task.WhenAll(_coordination.SaveAsync(), _connectionProfiles.SaveAsync());
        Status = $"Reforged client active for {session.DisplayName}.";
    }

    public void AddSession()
    {
        if (CoordinationProfile.Sessions.Count >= 6)
        {
            Status = "A coordination group supports up to six sessions.";
            return;
        }
        var session = new CoordinatedSession { DisplayName = $"Session {CoordinationProfile.Sessions.Count + 1}", Role = CoordinationRole.Follower };
        CoordinationProfile.Sessions.Add(session);
        _sessions.EnsureSession(session.SessionId, session.DisplayName, session.CharacterName);
        SelectedSession = session;
        SelectedLiveSession = _sessions.Sessions.FirstOrDefault(s => s.SessionId == session.SessionId);
        SelectedConnectionProfile = null;
        Raise(nameof(CoordinationProfile));
        Raise(nameof(WorkspaceProfile));
        Raise(nameof(ParsingProfile));
        SelectedParsingRule = ParsingProfile.Rules.FirstOrDefault();
    }

    public void RemoveSession()
    {
        if (SelectedSession is null) return;
        _ = _sessions.DisconnectAsync(SelectedSession.SessionId);
        var live = _sessions.Sessions.FirstOrDefault(s => s.SessionId == SelectedSession.SessionId);
        if (live is not null) _sessions.Sessions.Remove(live);
        _connectionProfiles.RemoveAssignment(SelectedSession.SessionId);
        _ = _connectionProfiles.SaveAsync();
        CoordinationProfile.Sessions.Remove(SelectedSession);
        SelectedSession = CoordinationProfile.Sessions.FirstOrDefault();
        SelectedLiveSession = _sessions.Sessions.FirstOrDefault();
        Raise(nameof(CoordinationProfile));
        Raise(nameof(WorkspaceProfile));
        Raise(nameof(ParsingProfile));
        SelectedParsingRule = ParsingProfile.Rules.FirstOrDefault();
    }

    public void AddTrustedController()
    {
        var controller = new TrustedController { PlayerName = "Trusted Player" };
        CoordinationProfile.TrustedControllers.Add(controller);
        SelectedController = controller;
        Raise(nameof(CoordinationProfile));
        Raise(nameof(WorkspaceProfile));
        Raise(nameof(ParsingProfile));
        SelectedParsingRule = ParsingProfile.Rules.FirstOrDefault();
    }

    public void RemoveTrustedController()
    {
        if (SelectedController is null) return;
        CoordinationProfile.TrustedControllers.Remove(SelectedController);
        SelectedController = CoordinationProfile.TrustedControllers.FirstOrDefault();
        foreach (var coordinated in CoordinationProfile.Sessions)
        {
            var live = _sessions.EnsureSession(coordinated.SessionId, coordinated.DisplayName, coordinated.CharacterName);
            live.TerminalViewMode = coordinated.TerminalViewMode;
        }
        if (_sessions.Sessions.Count == 0)
        {
            var coordinated = new CoordinatedSession { DisplayName = "Session 1", Role = CoordinationRole.Leader };
            CoordinationProfile.Sessions.Add(coordinated);
            _sessions.EnsureSession(coordinated.SessionId, coordinated.DisplayName, coordinated.CharacterName);
        }
        SelectedLiveSession = _sessions.Sessions.FirstOrDefault();
        Raise(nameof(CoordinationProfile));
        Raise(nameof(WorkspaceProfile));
        Raise(nameof(ParsingProfile));
        SelectedParsingRule = ParsingProfile.Rules.FirstOrDefault();
    }

    public void SendCoordinationCommand()
    {
        if (!CanContinueCommunications()) { Status = "Enable trusted communication and clear Emergency Stop first."; return; }

        var sent = _coordination.DispatchLocal(CoordinationTarget, CoordinationCommand);
        Status = sent.Count == 0 ? "No eligible follower session accepted the command." : $"Queued '{CoordinationCommand}' for {sent.Count} session(s).";
        Raise(nameof(CoordinationProfile));
        Raise(nameof(WorkspaceProfile));
        Raise(nameof(ParsingProfile));
        SelectedParsingRule = ParsingProfile.Rules.FirstOrDefault();
    }

    public void TestChatCommand()
    {
        if (!CanContinueCommunications()) { Status = "Enable trusted communication and clear Emergency Stop first."; return; }

        var parsed = _coordination.ParseChatCommand(ChatSender, ChatMessage);
        if (!parsed.Recognized)
        {
            Status = parsed.Error;
            return;
        }
        if (parsed.Error.Length > 0)
        {
            Status = $"@do rejected: {parsed.Error}";
            return;
        }
        var sent = _coordination.DispatchChat(ChatSender, ChatMessage);
        Status = $"@do command '{parsed.Command}' queued for {sent.Count} session(s).";
        Raise(nameof(CoordinationProfile));
        Raise(nameof(WorkspaceProfile));
        Raise(nameof(ParsingProfile));
        SelectedParsingRule = ParsingProfile.Rules.FirstOrDefault();
    }

    public void PauseCoordination()
    {
        _coordination.PauseAll();
        Status = "All coordinated sessions paused.";
        Raise(nameof(CoordinationProfile));
        Raise(nameof(WorkspaceProfile));
        Raise(nameof(ParsingProfile));
        SelectedParsingRule = ParsingProfile.Rules.FirstOrDefault();
    }

    public void ResumeCoordination()
    {
        if (!CanContinueCommunications()) { Status = "Enable trusted communication and clear Emergency Stop first."; return; }

        _coordination.ResumeAll();
        Status = "All enabled coordinated sessions resumed.";
        Raise(nameof(CoordinationProfile));
        Raise(nameof(WorkspaceProfile));
        Raise(nameof(ParsingProfile));
        SelectedParsingRule = ParsingProfile.Rules.FirstOrDefault();
    }

    public void StopCoordination()
    {
        _coordination.StopAll();
        Status = "STOP ALL cleared coordinated queues and paused every follower.";
        Raise(nameof(CoordinationProfile));
        Raise(nameof(WorkspaceProfile));
        Raise(nameof(ParsingProfile));
        SelectedParsingRule = ParsingProfile.Rules.FirstOrDefault();
    }

    public void ResyncCoordination()
    {
        _coordination.ResyncAll();
        Status = "Coordination queues cleared and local session links resynchronized.";
        Raise(nameof(CoordinationProfile));
    }


    public async Task ImportSelectedPathAsync()
    {
        if (SelectedPath is null) return;
        await _paths.ImportAsync(SelectedPath);
        Raise(nameof(SelectedPath));
        Status = $"Imported {SelectedPath.FileName} into the Reforged client path profile. Source and Librarium data were not changed.";
    }

    public async Task RemoveSelectedPathAsync()
    {
        if (SelectedPath is null) return;
        await _paths.RemoveImportAsync(SelectedPath);
        Raise(nameof(SelectedPath));
        Status = $"Removed the local Client import for {SelectedPath.FileName}. The original .mp file was not changed.";
    }


    public Task ConnectSelectedSessionAsync() =>
        ConnectSessionAsync(SelectedLiveSession);

    public async Task ConnectSessionAsync(ClientSessionConnection? session)
    {
        if (session is null) return;
        try
        {
            await SelectSessionProfilesAsync(session);
            await _sessions.ConnectAsync(session.SessionId);
            Status = session.ConnectionMessage;
        }
        catch (Exception exception) { Status = $"Connection failed: {exception.Message}"; }
        Raise(nameof(SelectedLiveSession));
    }

    public Task DisconnectSelectedSessionAsync() =>
        DisconnectSessionAsync(SelectedLiveSession);

    public async Task DisconnectSessionAsync(ClientSessionConnection? session)
    {
        if (session is null) return;
        await _sessions.DisconnectAsync(session.SessionId);
        Status = $"{session.DisplayName} disconnected.";
        Raise(nameof(SelectedLiveSession));
    }

    public async Task SendTerminalCommandAsync()
    {
        var command = TerminalCommand?.Trim() ?? string.Empty;
        TerminalCommand = string.Empty;
        await SendTerminalCommandAsync(SelectedLiveSession, command);
    }

    public async Task SendTerminalCommandAsync(
        ClientSessionConnection? session,
        string? command)
    {
        if (session is null) return;

        var normalized = command?.Trim() ?? string.Empty;
        if (normalized.Equals("attack", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("attack ", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("backstab", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("backstab ", StringComparison.OrdinalIgnoreCase))
            _manualRetargetBlockedRoomBySession.Remove(session.SessionId);
        try
        {
            if (_automation.TryResolveHumanInput(normalized, out var automatedCommands))
            {
                foreach (var automatedCommand in automatedCommands)
                {
                    if (!_automation.CanContinueInputAutomation || session.ConnectionState != ClientConnectionState.Connected) break;
                    await _sessions.SendCommandAsAsync(session.SessionId, automatedCommand, ClientCommandOrigin.ClientAutomation);
                    if (automatedCommands.Count > 1)
                        await Task.Delay(Math.Max(500, AutomationProfile.MinimumCommandDelayMs));
                }
                Status = automatedCommands.Count == 1
                    ? $"Expanded client alias to '{automatedCommands[0]}'."
                    : $"Completed approved macro ({automatedCommands.Count} commands).";
                Raise(nameof(SelectedLiveSession));
                return;
            }
            await _sessions.SendCommandAsync(session.SessionId, normalized);
            Status = normalized.Length == 0
                ? $"Requested a fresh prompt from {session.DisplayName}."
                : $"Sent '{normalized}' to {session.DisplayName}.";
        }
        catch (Exception exception) { Status = exception.Message; }

        Raise(nameof(SelectedLiveSession));
    }

    public Task SendSensitiveTerminalCommandAsync(string secret) =>
        SendSensitiveTerminalCommandAsync(SelectedLiveSession, secret);

    public async Task SendClientProtocolCommandAsync(ClientSessionConnection? session, string command)
    {
        if (session is null || string.IsNullOrWhiteSpace(command)) return;
        try { await _sessions.SendClientProtocolCommandAsync(session.SessionId, command.Trim()); }
        catch (Exception exception) { Status = exception.Message; }
        Raise(nameof(SelectedLiveSession));
    }

    public async Task SendSensitiveTerminalCommandAsync(
        ClientSessionConnection? session,
        string secret)
    {
        if (session is null || string.IsNullOrEmpty(secret)) return;
        try
        {
            await _sessions.SendSensitiveCommandAsync(session.SessionId, secret);
            Status = $"Sent hidden password entry to {session.DisplayName}.";
        }
        catch (Exception exception) { Status = exception.Message; }
        Raise(nameof(SelectedLiveSession));
    }

    public async Task SendTerminalKeyAsync(
        ClientSessionConnection? session,
        string sequence)
    {
        if (session is null || string.IsNullOrEmpty(sequence)) return;
        try
        {
            await _sessions.SendKeyAsync(session.SessionId, sequence);
        }
        catch (Exception exception) { Status = exception.Message; }
        Raise(nameof(SelectedLiveSession));
    }

    public void ResizeSelectedClassicTerminal(int columns, int rows) =>
        ResizeClassicTerminal(SelectedLiveSession, columns, rows);

    public void ResizeClassicTerminal(
        ClientSessionConnection? session,
        int columns,
        int rows)
    {
        if (session is null) return;
        _sessions.ResizeClassicTerminal(session.SessionId, columns, rows);
    }

    public Task SaveClassicPartyPreferencesAsync() =>
        SaveClassicPartyPreferencesAsync(SelectedLiveSession);

    public async Task SaveClassicPartyPreferencesAsync(ClientSessionConnection? session)
    {
        if (session is null) return;
        await SaveTerminalViewModeAsync(session);
        Status = $"Reforged party configuration saved for {session.DisplayName}. Enabled party capabilities now apply independently.";
        Raise(nameof(SelectedLiveSession));
    }

    public Task SaveClassicCombatPreferencesAsync() =>
        SaveClassicCombatPreferencesAsync(SelectedLiveSession);

    public async Task SaveClassicCombatPreferencesAsync(ClientSessionConnection? session)
    {
        if (session is null) return;
        AutomationOptions.Combat.AutoAttack = session.ClassicCombatPreferences.AutoCombat;
        AutomationProfile.Capabilities.CombatEnabled = session.ClassicCombatPreferences.AutoCombat;
        await Task.WhenAll(SaveTerminalViewModeAsync(session), _automation.SaveAsync());
        Status = session.ClassicCombatPreferences.AutoCombat
            ? $"Verified combat automation enabled for {session.DisplayName}. Emergency Stop remains authoritative."
            : $"Combat automation disabled for {session.DisplayName}.";
        Raise(nameof(AutomationProfile));
        Raise(nameof(SelectedLiveSession));
    }

    public Task SaveClassicHealthPreferencesAsync() =>
        SaveClassicHealthPreferencesAsync(SelectedLiveSession);

    public async Task SaveClassicHealthPreferencesAsync(ClientSessionConnection? session)
    {
        if (session is null) return;
        var preferences = session.ClassicHealthPreferences;
        var health = AutomationOptions.Health;
        health.AutoHeal = preferences.AutoRestHeal;
        health.AutoRest = preferences.AutoRestHeal;
        health.AutoMeditate = preferences.AutoRestHeal && (preferences.UseMeditateAbility || preferences.MeditateBeforeResting);
        health.MinorHealAtPercent = preferences.HealMinorHealthPercent;
        health.MajorHealAtPercent = preferences.HealMajorHealthPercent;
        health.RestManaAtPercent = preferences.RestManaBelowPercent;
        health.StopRestingManaPercent = preferences.RestManaMaximumPercent;
        AutomationOptions.Combat.AutoFlee = preferences.AutoRestHeal && preferences.RunHealthBelowPercent > 0;
        AutomationOptions.Combat.FleeAtPercent = preferences.RunHealthBelowPercent;
        AutomationProfile.Capabilities.HealthRecoveryEnabled = preferences.AutoRestHeal;
        await Task.WhenAll(SaveTerminalViewModeAsync(session), _automation.SaveAsync());
        Status = preferences.AutoRestHeal
            ? $"Health, healing and recovery automation enabled for {session.DisplayName}."
            : $"Health, healing and recovery automation disabled for {session.DisplayName}.";
        Raise(nameof(AutomationProfile));
        Raise(nameof(AutomationOptions));
        Raise(nameof(SelectedLiveSession));
    }

    private void ApplyClassicPartyDirective(ClientSessionConnection session, string line)
    {
        if (!AutomationProfile.Capabilities.PartyAutomationEnabled || AutomationProfile.Capabilities.EmergencyStop) return;

        var prefs = session.ClassicPartyPreferences;
        if (line.Contains(prefs.PartyWaitCommand, StringComparison.OrdinalIgnoreCase))
        {
            if (!prefs.IgnoreWaitWhenLeading)
            {
                _sessions.PauseRoute(session.SessionId);
                Status = $"Party wait received: {prefs.PartyWaitCommand}";
            }
            return;
        }
        if (line.Contains(prefs.PartyResumeCommand, StringComparison.OrdinalIgnoreCase))
        {
            _sessions.ResumeRoute(session.SessionId);
            Status = $"Party resume received: {prefs.PartyResumeCommand}";
        }
    }

    public void ClearSelectedTerminal() =>
        ClearTerminal(SelectedLiveSession);

    public void ClearTerminal(ClientSessionConnection? session)
    {
        if (session is null) return;
        _sessions.ClearTerminal(session.SessionId);
        Status = $"Cleared {session.DisplayName} terminal scrollback.";
    }

    public void ResetAnsi(ClientSessionConnection? session)
    {
        if (session is null) return;
        _sessions.ResetAnsi(session.SessionId);
        Status = $"Reset {session.DisplayName} ANSI renderer locally.";
    }

    public void ResetClientInternals(ClientSessionConnection? session)
    {
        if (session is null) return;
        _sessions.ResetClientInternals(session.SessionId);
        Status = $"Reset {session.DisplayName} typed client caches locally. No command was sent.";
    }

    public IReadOnlyList<ClientItemWeightState> ResolveItemWeights(ClientSessionConnection session)
    {
        var itemRecords = _data.Categories.FirstOrDefault(category =>
            category.Name.Equals("Items", StringComparison.OrdinalIgnoreCase))?.Records ?? [];
        var rows = new List<ClientItemWeightState>();
        AddWeightRows(rows, session.InventoryItems, "Inventory", itemRecords);
        AddWeightRows(rows, session.EquippedItems, "Equipped", itemRecords);
        return rows;
    }

    private static void AddWeightRows(
        ICollection<ClientItemWeightState> rows,
        IEnumerable<string> entries,
        string location,
        IEnumerable<ClientDataRecord> itemRecords)
    {
        foreach (var entry in entries)
        {
            var text = entry.Trim();
            var quantity = 1;
            var separator = text.IndexOf("x ", StringComparison.OrdinalIgnoreCase);
            if (separator > 0 && int.TryParse(text[..separator], out var parsedQuantity))
            {
                quantity = Math.Max(1, parsedQuantity);
                text = text[(separator + 2)..].Trim();
            }
            var record = itemRecords.FirstOrDefault(item =>
                item.Id.Equals(text, StringComparison.OrdinalIgnoreCase) ||
                item.Name.Equals(text, StringComparison.OrdinalIgnoreCase) ||
                item.EffectiveName.Equals(text, StringComparison.OrdinalIgnoreCase));
            if (record is null || !TryReadCatalogWeight(record.Summary, out var unitWeight)) continue;
            rows.Add(new ClientItemWeightState(record.EffectiveName, quantity, unitWeight,
                unitWeight > long.MaxValue / quantity ? long.MaxValue : unitWeight * quantity, location));
        }
    }

    private static bool TryReadCatalogWeight(string summary, out long weight)
    {
        weight = 0;
        foreach (var field in summary.Split('•', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (!field.StartsWith("Weight:", StringComparison.OrdinalIgnoreCase)) continue;
            return long.TryParse(field["Weight:".Length..].Trim(), out weight) && weight >= 0;
        }
        return false;
    }

    public Task ExportSelectedTerminalLogAsync() =>
        ExportTerminalLogAsync(SelectedLiveSession);

    public async Task ExportTerminalLogAsync(ClientSessionConnection? session)
    {
        if (session is null) return;
        try
        {
            var path = await _sessions.ExportTerminalLogAsync(session.SessionId);
            Status = $"{session.DisplayName} terminal log exported to {path}";
        }
        catch (Exception exception) { Status = exception.Message; }
    }


    public void RefreshMapRooms()
    {
        VisibleMapRooms.Clear();
        var search = MapRoomSearch.Trim();
        foreach (var room in MapProfile.Rooms
                     .Where(room => string.Equals(room.Layer, MapProfile.ActiveLayer, StringComparison.OrdinalIgnoreCase))
                     .Where(room => !MapFavoritesOnly || room.Favorite)
                     .Where(room => search.Length == 0 || room.DisplayLabel.Contains(search, StringComparison.OrdinalIgnoreCase) || room.Notes.Contains(search, StringComparison.OrdinalIgnoreCase))
                     .OrderByDescending(room => room.Favorite)
                     .ThenBy(room => room.RoomName, StringComparer.OrdinalIgnoreCase))
            VisibleMapRooms.Add(room);
        if (SelectedMapRoom is not null && !VisibleMapRooms.Contains(SelectedMapRoom)) SelectedMapRoom = VisibleMapRooms.FirstOrDefault();
    }

    public void ToggleSelectedMapFavorite()
    {
        if (SelectedMapRoom is null) return;
        SelectedMapRoom.Favorite = !SelectedMapRoom.Favorite;
        RefreshMapRooms();
        Status = SelectedMapRoom.Favorite ? $"Added {SelectedMapRoom.RoomName} to map favorites." : $"Removed {SelectedMapRoom.RoomName} from map favorites.";
    }

    public async Task SaveMapProfileAsync()
    {
        if (SelectedLiveSession is not null)
        {
            MapProfile.ServerName = SelectedLiveSession.ServerProfileName;
            MapProfile.WorldName = SelectedLiveSession.WorldName;
            MapProfile.CharacterName = string.IsNullOrWhiteSpace(SelectedLiveSession.CharacterName) ? SelectedLiveSession.DisplayName : SelectedLiveSession.CharacterName;
        }
        await _mapProfiles.SaveAsync();
        RefreshMapRooms();
        Raise(nameof(MapProfile));
        Status = $"Saved Client-only map settings for {MapProfile.CharacterName} on {MapProfile.ServerName} / {MapProfile.WorldName}.";
    }

    public void SelectCurrentMapRoom()
    {
        if (SelectedLiveSession is null) return;
        SelectedMapRoom = _mapProfiles.RecordVisit(SelectedLiveSession.CurrentRoomName, SelectedLiveSession.CurrentRoomKey);
        RefreshMapRooms();
        Raise(nameof(MapProfile));
        Status = $"Selected map room '{SelectedMapRoom.DisplayLabel}'.";
    }

    public async Task SetSpawnTimerAsync()
    {
        if (SelectedMapRoom is null) { Status = "Select a Client map room first."; return; }
        SelectedMapRoom.SpawnLabel = string.IsNullOrWhiteSpace(SpawnTimerLabel) ? "Spawn" : SpawnTimerLabel.Trim();
        SelectedMapRoom.SpawnReadyAt = DateTimeOffset.Now.AddMinutes(SpawnTimerMinutes);
        await _mapProfiles.SaveAsync();
        Status = $"Set Client timer for {SelectedMapRoom.RoomName}. The Host does not receive or own this timer.";
    }

    public async Task ClearSpawnTimerAsync()
    {
        if (SelectedMapRoom is null) return;
        SelectedMapRoom.SpawnReadyAt = null;
        SelectedMapRoom.SpawnLabel = string.Empty;
        await _mapProfiles.SaveAsync();
        Status = $"Cleared the Client timer for {SelectedMapRoom.RoomName}.";
    }

    public async Task SaveSysGotoProfileAsync()
    {
        if (SelectedLiveSession is not null)
        {
            SysGotoProfile.ServerName = SelectedLiveSession.ServerProfileName;
            SysGotoProfile.WorldName = SelectedLiveSession.WorldName;
        }
        await _sysGoto.SaveAsync();
        Status = $"Saved Client-only sys goto settings for {SysGotoProfile.ServerName} / {SysGotoProfile.WorldName}.";
    }

    public void AddSysGotoDestination()
    {
        var city = new ClientSysGotoDestination
        {
            DisplayName = string.IsNullOrWhiteSpace(NewSysGotoCityName) ? "New City" : NewSysGotoCityName.Trim(),
            CommandArgument = string.IsNullOrWhiteSpace(NewSysGotoCommandArgument) ? NewSysGotoCityName.Trim() : NewSysGotoCommandArgument.Trim(),
            Enabled = true
        };
        SysGotoProfile.Destinations.Add(city);
        SelectedSysGotoDestination = city;
        NewSysGotoCityName = "New City";
        NewSysGotoCommandArgument = string.Empty;
        Raise(nameof(SysGotoProfile));
    }

    public void RemoveSelectedSysGotoDestination()
    {
        if (SelectedSysGotoDestination is null) return;
        SysGotoProfile.Destinations.Remove(SelectedSysGotoDestination);
        SelectedSysGotoDestination = SysGotoProfile.Destinations.FirstOrDefault();
        Raise(nameof(SysGotoProfile));
    }

    public async Task TravelWithSysGotoAsync()
    {
        if (SelectedLiveSession is null || SelectedSysGotoDestination is null)
        {
            Status = "Select a live session and approved city.";
            return;
        }
        if (!SysGotoProfile.Enabled)
        {
            Status = "Enable sys goto city travel for this server/world profile first.";
            return;
        }
        try
        {
            var command = _sysGoto.BuildCommand(SelectedSysGotoDestination);
            await _sessions.SendCommandAsync(SelectedLiveSession.SessionId, command);
            Status = $"Sent approved city travel command '{command}'. The Host decides whether it is allowed.";
        }
        catch (Exception exception) { Status = exception.Message; }
    }

    public void TravelGroupWithSysGoto()
    {
        if (!CanContinueCommunications() || !AutomationProfile.Capabilities.NavigationEnabled)
        { Status = "Enable trusted communication and verified navigation, and clear Emergency Stop first."; return; }

        if (SelectedSysGotoDestination is null || !SysGotoProfile.Enabled)
        {
            Status = "Enable sys goto and select an approved city before sending group travel.";
            return;
        }
        try
        {
            var command = _sysGoto.BuildCommand(SelectedSysGotoDestination);
            var sent = _coordination.DispatchLocal(CoordinationTarget, command, "Local leader");
            Status = sent.Count == 0 ? "No eligible coordinated session accepted the city-travel command." : $"Queued approved city travel for {sent.Count} coordinated session(s).";
        }
        catch (Exception exception) { Status = exception.Message; }
    }

    public async Task StartSelectedRouteAsync()
    {
        if (!CanRunVerifiedNavigation()) return;

        if (SelectedLiveSession is null || SelectedPath is null) return;
        try { await _sessions.StartRouteAsync(SelectedLiveSession.SessionId, SelectedPath); Status = $"Started {SelectedPath.DisplayName}."; }
        catch (Exception exception) { Status = exception.Message; }
        Raise(nameof(SelectedLiveSession));
    }

    public async Task StartRouteToDestinationAsync(ClientSessionConnection? session, MegaMudPathRecord destination, bool requireExactStart)
    {
        if (!CanRunVerifiedNavigation()) return;
        if (session is null || session.ConnectionState != ClientConnectionState.Connected)
        {
            Status = "Connect this character before starting a route.";
            return;
        }

        var currentKey = session.CurrentRoomKey.Trim();
        var exactStart = currentKey.Length > 0 &&
            (destination.NativeStartKey.Equals(currentKey, StringComparison.OrdinalIgnoreCase)
             || destination.Start?.LibrariumRoomId?.Equals(currentKey, StringComparison.OrdinalIgnoreCase) == true);
        MegaMudPathRecord? route = exactStart ? destination : null;
        if (route is null && !requireExactStart && currentKey.Length > 0 && !string.IsNullOrWhiteSpace(destination.NativeEndKey))
            route = _paths.CalculateReplacementRoute(currentKey, destination.NativeEndKey);

        if (!requireExactStart && route is not null && route.Steps.Count == 0)
        {
            Status = $"{session.DisplayName} is already at {destination.EndLabel}. Nothing was sent.";
            return;
        }
        if (route is null || route.Steps.Count == 0)
        {
            Status = requireExactStart
                ? $"Loop '{destination.DisplayName}' starts at {destination.NativeStartKey}; the character is at {session.CurrentRoomKey}."
                : $"No verified route connects {session.CurrentRoomKey} to {destination.NativeEndKey}. Nothing was sent.";
            return;
        }

        try
        {
            SelectedLiveSession = session;
            SelectedPath = destination;
            await _sessions.StartRouteAsync(session.SessionId, route);
            Status = exactStart
                ? $"Started {destination.DisplayName}."
                : $"Calculated and started a verified {route.Steps.Count}-step route to {destination.EndLabel}.";
        }
        catch (Exception exception) { Status = exception.Message; }
        Raise(nameof(SelectedLiveSession));
    }

    public async Task StartRouteToRoomAsync(ClientSessionConnection? session, string destinationRoomKey)
    {
        if (!CanRunVerifiedNavigation()) return;
        if (session is null || session.ConnectionState != ClientConnectionState.Connected)
        { Status = "Connect this character before using click-to-walk."; return; }
        var start = session.CurrentRoomKey.Trim();
        var destination = destinationRoomKey.Trim();
        if (start.Length == 0 || destination.Length == 0)
        { Status = "Click-to-walk requires confirmed current and destination room keys."; return; }
        if (start.Equals(destination, StringComparison.OrdinalIgnoreCase))
        { Status = "The character is already in that room."; return; }
        var route = _paths.CalculateReplacementRoute(start, destination);
        if (route is null || route.Steps.Count == 0)
        { Status = $"No verified route connects {start} to {destination}. Nothing was sent."; return; }
        try
        {
            await _sessions.StartRouteAsync(session.SessionId, route);
            Status = $"Click-to-walk started a verified {route.Steps.Count}-step route to {destination}.";
        }
        catch (Exception exception) { Status = $"Click-to-walk stopped safely: {exception.Message}"; }
        Raise(nameof(SelectedLiveSession));
    }

    public void PauseSelectedRoute()
    {
        if (SelectedLiveSession is null) return;
        _sessions.PauseRoute(SelectedLiveSession.SessionId);
        Raise(nameof(SelectedLiveSession));
    }

    public async Task StartBackTrackAsync(ClientSessionConnection? session)
    {
        if (!CanRunVerifiedNavigation()) return;
        if (session is null || session.ConnectionState != ClientConnectionState.Connected) { Status = "Connect this character before backtracking."; return; }
        if (session.RouteState is RouteExecutionState.Running or RouteExecutionState.Paused or RouteExecutionState.WaitingForRoom)
        { Status = "Stop the active route before backtracking."; return; }

        var nodes = session.RouteMapNodes.OrderBy(node => node.Step).ToArray();
        var segments = session.RouteMapSegments.OrderBy(segment => segment.Step).ToArray();
        if (nodes.Length < 2 || segments.Length == 0) { Status = "There is no completed room-confirmed route to backtrack."; return; }
        var lastKey = nodes[^1].ActualRoomKey.Trim();
        if (lastKey.Length == 0 || !lastKey.Equals(session.CurrentRoomKey.Trim(), StringComparison.OrdinalIgnoreCase))
        { Status = "The character is no longer at the confirmed end of the last route. Back Track sent nothing."; return; }

        static string Opposite(string command) => command.Trim().ToLowerInvariant() switch
        {
            "n" or "north" => "south", "ne" or "northeast" => "southwest", "e" or "east" => "west",
            "se" or "southeast" => "northwest", "s" or "south" => "north", "sw" or "southwest" => "northeast",
            "w" or "west" => "east", "nw" or "northwest" => "southeast", "u" or "up" => "down", "d" or "down" => "up",
            _ => string.Empty
        };

        var reverseSteps = new ObservableCollection<MegaMudPathStep>();
        for (var index = segments.Length - 1; index >= 0; index--)
        {
            var segment = segments[index];
            var fromNode = nodes.FirstOrDefault(node => node.Step == segment.Step);
            var targetNode = index == 0 ? nodes.FirstOrDefault(node => node.Step == 0) : nodes.FirstOrDefault(node => node.Step == segments[index - 1].Step);
            var reverse = Opposite(segment.Command);
            if (fromNode is null || targetNode is null || reverse.Length == 0
                || string.IsNullOrWhiteSpace(fromNode.ActualRoomKey) || string.IsNullOrWhiteSpace(targetNode.ActualRoomKey)
                || segment.State is RoutePlaybackNodeState.Mismatch or RoutePlaybackNodeState.Failed or RoutePlaybackNodeState.Pending)
            { Status = $"Back Track cannot safely reverse step {segment.Step} ('{segment.Command}'). Nothing was sent."; return; }
            reverseSteps.Add(new MegaMudPathStep
            {
                Number = reverseSteps.Count + 1, NativeRoomKey = fromNode.ActualRoomKey, Command = reverse,
                FriendlyCommand = reverse, IsMovement = true, ExpectedNativeRoomKey = targetNode.ActualRoomKey
            });
        }

        var route = new MegaMudPathRecord
        {
            FileName = "client-backtrack.mp", Author = "Reforged Client", StartLabel = nodes[^1].ActualRoomName,
            EndLabel = nodes[0].ActualRoomName, NativeStartKey = nodes[^1].ActualRoomKey, NativeEndKey = nodes[0].ActualRoomKey,
            DeclaredStepCount = reverseSteps.Count, Steps = reverseSteps
        };
        try { SelectedLiveSession = session; await _sessions.StartRouteAsync(session.SessionId, route); Status = $"Backtracking {reverseSteps.Count} confirmed step(s)."; }
        catch (Exception exception) { Status = exception.Message; }
        Raise(nameof(SelectedLiveSession));
    }

    public void ResumeSelectedRoute()
    {
        if (!CanRunVerifiedNavigation()) return;

        if (SelectedLiveSession is null) return;
        _sessions.ResumeRoute(SelectedLiveSession.SessionId);
        Raise(nameof(SelectedLiveSession));
    }

    public void StopSelectedRoute()
    {
        if (SelectedLiveSession is null) return;
        _autoRoamRoutes.Remove(SelectedLiveSession.SessionId);
        _sessions.StopRoute(SelectedLiveSession.SessionId);
        Raise(nameof(SelectedLiveSession));
    }

    public void StopRoute(ClientSessionConnection session)
    {
        _autoRoamRoutes.Remove(session.SessionId);
        _sessions.StopRoute(session.SessionId, "Verified navigation disabled");
    }

    public async Task StartAutoRoamAsync(ClientSessionConnection? session, MegaMudPathRecord loop)
    {
        if (!CanRunVerifiedNavigation()) return;
        if (session is null || session.ConnectionState != ClientConnectionState.Connected) { Status = "Connect this character before auto-roaming."; return; }
        var startKey = loop.NativeStartKey.Trim();
        var endKey = loop.NativeEndKey.Trim();
        if (startKey.Length == 0 || !startKey.Equals(session.CurrentRoomKey.Trim(), StringComparison.OrdinalIgnoreCase))
        { Status = $"Auto-Roam loop '{loop.DisplayName}' must start at {loop.NativeStartKey}. Nothing was sent."; return; }
        var finalKey = loop.Steps.LastOrDefault(step => step.IsMovement)?.ExpectedNativeRoomKey?.Trim() ?? string.Empty;
        if (!endKey.Equals(startKey, StringComparison.OrdinalIgnoreCase) && !finalKey.Equals(startKey, StringComparison.OrdinalIgnoreCase))
        { Status = $"'{loop.DisplayName}' does not return to its exact decoded start room, so it cannot repeat safely."; return; }
        if (!AutomationProfile.Capabilities.NavigationEnabled)
        {
            AutomationProfile.Capabilities.NavigationEnabled = true;
            await _automation.SaveAsync();
            Raise(nameof(AutomationProfile));
        }
        _autoRoamRoutes[session.SessionId] = loop;
        SelectedLiveSession = session;
        try { await _sessions.StartRouteAsync(session.SessionId, loop); Status = $"Auto-Roam started: {loop.DisplayName}."; }
        catch (Exception exception) { _autoRoamRoutes.Remove(session.SessionId); Status = exception.Message; }
    }

    private async void SessionRouteChanged(object? sender, RouteExecutionEvent e)
    {
        Status = e.Message;
        if (e.State != RouteExecutionState.Completed || !_autoRoamRoutes.TryGetValue(e.SessionId, out var loop)
            || !AutomationProfile.Capabilities.NavigationEnabled || AutomationProfile.Capabilities.EmergencyStop || !_autoRoamRestarting.Add(e.SessionId)) return;
        try
        {
            await Task.Delay(Math.Max(500, AutomationProfile.MinimumCommandDelayMs));
            var session = _sessions.Sessions.FirstOrDefault(candidate => candidate.SessionId.Equals(e.SessionId, StringComparison.OrdinalIgnoreCase));
            if (session is null || session.ConnectionState != ClientConnectionState.Connected || !_autoRoamRoutes.ContainsKey(e.SessionId)) return;
            if (!session.CurrentRoomKey.Trim().Equals(loop.NativeStartKey.Trim(), StringComparison.OrdinalIgnoreCase))
            { _autoRoamRoutes.Remove(e.SessionId); Status = "Auto-Roam stopped because the loop did not return to its exact start room."; return; }
            await _sessions.StartRouteAsync(e.SessionId, loop);
            Status = $"Auto-Roam repeating: {loop.DisplayName}.";
        }
        catch (Exception exception) { _autoRoamRoutes.Remove(e.SessionId); Status = $"Auto-Roam stopped: {exception.Message}"; }
        finally { _autoRoamRestarting.Remove(e.SessionId); }
    }

    public void PrepareSelectedRouteRecovery()
    {
        if (SelectedLiveSession is null) return;
        _sessions.PrepareRouteRecovery(SelectedLiveSession.SessionId);
        Status = SelectedLiveSession.RouteRecoveryStatus;
        Raise(nameof(SelectedLiveSession));
    }

    public async Task CalculateAndRunRouteRecoveryAsync()
    {
        if (!CanRunVerifiedNavigation()) return;

        if (SelectedLiveSession is null) return;
        var session = SelectedLiveSession;
        var replacement = _paths.CalculateReplacementRoute(session.CurrentRoomKey, session.RouteDestinationKey);
        if (replacement is null || replacement.Steps.Count == 0)
        {
            session.RouteRecoveryPlan = $"No replacement route was found from {session.CurrentRoomKey} to {session.RouteDestinationKey}.";
            Status = session.RouteRecoveryPlan;
            Raise(nameof(SelectedLiveSession));
            return;
        }
        session.RouteRecoveryPlan = $"Replacement route calculated: {replacement.Steps.Count} step(s) from {replacement.NativeStartKey} to {replacement.NativeEndKey}.";
        try
        {
            await _sessions.StartRouteAsync(session.SessionId, replacement);
            Status = session.RouteRecoveryPlan;
        }
        catch (Exception exception) { Status = exception.Message; }
        Raise(nameof(SelectedLiveSession));
    }

    private bool CanRunVerifiedNavigation()
    {
        if (AutomationProfile.Capabilities.EmergencyStop)
        {
            Status = "Emergency Stop is active. No navigation command was sent.";
            return false;
        }
        // An explicit Goto, Loop, or Back Track click authorizes that route.
        // The automation toggle governs unsolicited navigation decisions.
        return true;
    }

    public void StartRouteRecording() => StartRouteRecording(SelectedLiveSession);

    public void StartRouteRecording(ClientSessionConnection? session)
    {
        if (session is null) { Status = "Select a live session before recording."; return; }
        _paths.BeginRecording(session.SessionId, RecordingRouteName, session.CurrentRoomName);
        Raise(nameof(RouteRecording));
        Status = $"Recording route '{RecordingRouteName}' from {session.DisplayName}.";
    }

    public Task FinishRouteRecordingAsync() => FinishRouteRecordingAsync(SelectedLiveSession);

    public async Task FinishRouteRecordingAsync(ClientSessionConnection? session)
    {
        var endRoom = session?.CurrentRoomName ?? string.Empty;
        var entry = await _paths.FinishRecordingAsync(endRoom);
        Raise(nameof(RouteRecording)); Raise(nameof(LocalPaths));
        SelectedLocalPath = entry ?? LocalPaths.LastOrDefault();
        Status = entry is null ? "Route recording contained no commands." : $"Saved recorded route '{entry.DisplayName}'.";
    }

    public void CancelRouteRecording()
    {
        _paths.CancelRecording();
        Raise(nameof(RouteRecording));
        Status = "Route recording cancelled.";
    }


    public async Task RefreshLibrariumSnapshotAsync()
    {
        LibrariumSnapshot = await _librariumBridge.BuildSnapshotAsync(SelectedLiveSession);
        Status = LibrariumSnapshot.Summary;
    }


    public async Task SaveCurrentBuildAsync()
    {
        if (LibrariumSnapshot.CapturedAt is null)
        {
            Status = "Refresh the current character snapshot before saving a build.";
            return;
        }
        var saved = await _librariumBridge.SaveBuildAsync(SavedBuildName, LibrariumSnapshot);
        SavedBuilds.Add(saved);
        SelectedSavedBuildB = saved;
        if (SelectedSavedBuildA is null) SelectedSavedBuildA = saved;
        SavedBuildName = $"{LibrariumSnapshot.CharacterName} Build";
        Status = $"Saved Client-only build snapshot '{saved.Name}'.";
    }

    public async Task ExportSelectedBuildAsync(string filePath)
    {
        var selected = SelectedSavedBuildB ?? SelectedSavedBuildA;
        if (selected is null)
        {
            Status = "Select a saved build before exporting.";
            return;
        }
        await _librariumBridge.ExportBuildAsync(selected, filePath);
        Status = $"Exported Client-only build '{selected.Name}'.";
    }

    public async Task ImportSavedBuildAsync(string filePath)
    {
        var imported = await _librariumBridge.ImportBuildAsync(filePath);
        SavedBuilds.Add(imported);
        SelectedSavedBuildB = imported;
        if (SelectedSavedBuildA is null) SelectedSavedBuildA = imported;
        Status = $"Imported Client-only build '{imported.Name}' for {imported.CharacterName}.";
    }

    public async Task DeleteSelectedBuildAsync()
    {
        var selected = SelectedSavedBuildB ?? SelectedSavedBuildA;
        if (selected is null) return;
        await _librariumBridge.DeleteBuildAsync(selected.Id);
        SavedBuilds.Remove(selected);
        if (ReferenceEquals(SelectedSavedBuildA, selected)) SelectedSavedBuildA = SavedBuilds.FirstOrDefault();
        if (ReferenceEquals(SelectedSavedBuildB, selected)) SelectedSavedBuildB = SavedBuilds.Skip(1).FirstOrDefault() ?? SavedBuilds.FirstOrDefault();
        RefreshSavedBuildComparison();
        Status = $"Deleted Client-only saved build '{selected.Name}'.";
    }

    private async Task LoadSavedBuildsAsync()
    {
        SavedBuilds.Clear();
        foreach (var build in await _librariumBridge.LoadSavedBuildsAsync()) SavedBuilds.Add(build);
        SelectedSavedBuildA = SavedBuilds.FirstOrDefault();
        SelectedSavedBuildB = SavedBuilds.Skip(1).FirstOrDefault() ?? SavedBuilds.FirstOrDefault();
    }

    private void RefreshSavedBuildComparison()
    {
        SavedBuildComparison.Clear();
        foreach (var row in _librariumBridge.CompareBuilds(SelectedSavedBuildA, SelectedSavedBuildB)) SavedBuildComparison.Add(row);
    }

    public async Task SaveLocalPathAsync()
    {
        if (SelectedLocalPath is null) return;
        await _paths.SaveImportedProfileAsync();
        Status = $"Saved Client route '{SelectedLocalPath.DisplayName}'.";
    }

    public async Task DuplicateLocalPathAsync()
    {
        if (SelectedLocalPath is null) return;
        await _paths.DuplicateImportedAsync(SelectedLocalPath);
        Raise(nameof(LocalPaths));
        SelectedLocalPath = LocalPaths.LastOrDefault();
        Status = "Duplicated local Client route.";
    }

    public async Task DeleteLocalPathAsync()
    {
        if (SelectedLocalPath is null) return;
        var name = SelectedLocalPath.DisplayName;
        await _paths.DeleteImportedAsync(SelectedLocalPath);
        Raise(nameof(LocalPaths));
        SelectedLocalPath = LocalPaths.FirstOrDefault();
        Status = $"Deleted local Client route '{name}'. Original .mp sources were not changed.";
    }

    public void AddLocalPathCommand()
    {
        if (SelectedLocalPath is null) return;
        var command = string.IsNullOrWhiteSpace(RouteCommandEditor) ? "north" : RouteCommandEditor.Trim();
        SelectedLocalPath.Commands.Add(command);
        SelectedRouteCommand = command;
        Raise(nameof(SelectedLocalPath));
    }

    public void UpdateLocalPathCommand()
    {
        if (SelectedLocalPath is null || SelectedRouteCommand is null || string.IsNullOrWhiteSpace(RouteCommandEditor)) return;
        var index = SelectedLocalPath.Commands.IndexOf(SelectedRouteCommand);
        if (index < 0) return;
        var command = RouteCommandEditor.Trim();
        SelectedLocalPath.Commands[index] = command;
        SelectedRouteCommand = command;
        Raise(nameof(SelectedLocalPath));
    }

    public void RemoveLocalPathCommand()
    {
        if (SelectedLocalPath is null || SelectedRouteCommand is null) return;
        var index = SelectedLocalPath.Commands.IndexOf(SelectedRouteCommand);
        SelectedLocalPath.Commands.Remove(SelectedRouteCommand);
        SelectedRouteCommand = SelectedLocalPath.Commands.ElementAtOrDefault(Math.Min(index, SelectedLocalPath.Commands.Count - 1));
        Raise(nameof(SelectedLocalPath));
    }

    public void MoveLocalPathCommand(int offset)
    {
        if (SelectedLocalPath is null || SelectedRouteCommand is null) return;
        var current = SelectedLocalPath.Commands.IndexOf(SelectedRouteCommand);
        var target = current + offset;
        if (current < 0 || target < 0 || target >= SelectedLocalPath.Commands.Count) return;
        SelectedLocalPath.Commands.Move(current, target);
        Raise(nameof(SelectedLocalPath));
    }

    public async Task RunLocalPathAsync()
    {
        if (!CanRunVerifiedNavigation()) return;

        if (SelectedLiveSession is null || SelectedLocalPath is null) return;
        var route = new MegaMudPathRecord
        {
            FileName = SelectedLocalPath.SourceFileName,
            StartLabel = SelectedLocalPath.StartLabel,
            EndLabel = SelectedLocalPath.EndLabel,
            Steps = new ObservableCollection<MegaMudPathStep>(SelectedLocalPath.Commands.Select((command, index) => new MegaMudPathStep
            {
                Number = index + 1, Command = command, FriendlyCommand = command, IsMovement = IsRouteMovement(command),
            })),
        };
        await _sessions.StartRouteAsync(SelectedLiveSession.SessionId, route);
        Status = $"Started local route '{SelectedLocalPath.DisplayName}'.";
    }

    private void SessionCommandSent(object? sender, ClientCommandSentEvent e)
    {
        if (e.Origin != ClientCommandOrigin.Human || e.Command.Equals("[hidden]", StringComparison.OrdinalIgnoreCase)) return;
        _paths.RecordCommand(e.SessionId, e.Command);
        Raise(nameof(RouteRecording));
    }

    private static bool IsRouteMovement(string command) => new[] { "n", "north", "ne", "northeast", "e", "east", "se", "southeast", "s", "south", "sw", "southwest", "w", "west", "nw", "northwest", "u", "up", "d", "down" }.Contains(command.Trim(), StringComparer.OrdinalIgnoreCase);

    private async void CoordinationCommandRequested(object? sender, CoordinatedCommandRequest request)
    {
        if (!CanContinueCommunications()) return;

        try
        {
            await _sessions.SendCommandAsync(request.SessionId, request.Command);
            _coordination.MarkCompleted(request.SessionId, "Command sent");
            if (CoordinationProfile.SendAcknowledgements && !request.Source.Equals("Local leader", StringComparison.OrdinalIgnoreCase))
                _coordination.SendResponse(request.SessionId, request.Source, CoordinationResponseKind.Acknowledgement, $"Accepted #{request.Sequence}: {request.Command}");
        }
        catch (Exception exception)
        {
            _coordination.MarkCompleted(request.SessionId, exception.Message);
            if (!request.Source.Equals("Local leader", StringComparison.OrdinalIgnoreCase))
                _coordination.SendResponse(request.SessionId, request.Source, CoordinationResponseKind.Failure, $"Failed: {request.Command} — {exception.Message}");
        }
        Raise(nameof(CoordinationProfile));
        Raise(nameof(WorkspaceProfile));
        Raise(nameof(ParsingProfile));
        SelectedParsingRule = ParsingProfile.Rules.FirstOrDefault();
    }

    private async void CoordinationResponseRequested(object? sender, CoordinationResponse response)
    {
        if (!CanContinueCommunications()) return;

        try
        {
            var prefix = response.Kind switch
            {
                CoordinationResponseKind.Acknowledgement => "ACK",
                CoordinationResponseKind.Refusal => "REFUSED",
                CoordinationResponseKind.QueryResponse => "STATUS",
                _ => "FAILED"
            };
            await _sessions.SendCommandAsync(response.SourceSessionId, $"tell {response.Recipient} [{prefix}] {response.Message}");
        }
        catch (Exception exception) { Status = $"Coordination response failed: {exception.Message}"; }
    }

    private void CoordinationQueryRequested(object? sender, CoordinationQueryRequest query)
    {
        if (!CanContinueCommunications()) return;

        var session = _sessions.Sessions.FirstOrDefault(s => s.SessionId.Equals(query.SourceSessionId, StringComparison.OrdinalIgnoreCase));
        if (session is null) return;
        var response = query.Permission switch
        {
            RemoteControlPermission.QueryStatus => session.VitalSummary + $" • Combat {(session.InCombat ? "yes" : "no")} • Resting {(session.IsResting ? "yes" : "no")}",
            RemoteControlPermission.QueryLocation => $"Location: {session.CurrentRoomName}" + (string.IsNullOrWhiteSpace(session.CurrentRoomKey) ? string.Empty : $" ({session.CurrentRoomKey})"),
            RemoteControlPermission.QueryInventory => session.InventorySummary + FormatSnapshotAge(session.InventoryUpdatedAt),
            RemoteControlPermission.QueryExperience => session.ExperienceSummary + FormatSnapshotAge(session.ExperienceUpdatedAt),
            _ => "Unsupported query."
        };
        _coordination.SendResponse(query.SourceSessionId, query.Sender, CoordinationResponseKind.QueryResponse, response);
    }

    private static string FormatSnapshotAge(DateTimeOffset? capturedAt)
    {
        if (capturedAt is null) return " • no live snapshot captured yet; run inventory or score first";
        var age = DateTimeOffset.Now - capturedAt.Value;
        return age.TotalSeconds < 90 ? " • live snapshot" : $" • captured {Math.Max(1, (int)age.TotalMinutes)} minute(s) ago";
    }

    private async void AutomationCommandRequested(object? sender, ClientAutomationCommandRequest request)
    {
        if (AutomationProfile.Capabilities.EmergencyStop) return;
        var session = _sessions.Sessions.FirstOrDefault(candidate => candidate.SessionId.Equals(request.SessionId, StringComparison.OrdinalIgnoreCase))
            ?? SelectedLiveSession;
        if (session is null || session.ConnectionState != ClientConnectionState.Connected) return;
        if (request.RuleName.Equals("Auto Get Items", StringComparison.OrdinalIgnoreCase))
        {
            await RunManualMassActionAsync(session, "getitems");
            return;
        }
        if (request.RuleName.Equals("Auto Get Cash", StringComparison.OrdinalIgnoreCase))
        {
            await RunManualMassActionAsync(session, "getcash");
            return;
        }
        try { await _sessions.SendCommandAsAsync(session.SessionId, request.Command, ClientCommandOrigin.ClientAutomation); Status = $"{request.RuleName}: {request.Command}"; }
        catch (Exception exception) { Status = $"Automation command failed: {exception.Message}"; }
    }

    public void TickTimedEvents(ClientSessionConnection? session, DateTimeOffset timestamp)
    {
        if (session is null) return;
        var gameplayReady = ClientCommandIsolationPolicy.HasVerifiedGameplay(session);
        if (gameplayReady) EnsureAuthoritativeStateBootstrap(session);

        var context = new ClientAutomationContext(
            session.HealthPercent, session.ManaPercent, session.InCombat, session.IsResting,
            session.IsMoving, string.Empty, timestamp, session.SessionId);
        _automation.EvaluateTimedEvents(context, gameplayReady);
        if (gameplayReady)
            _ = TickAutomationClockAsync(session, timestamp);
    }

    private async Task TickAutomationClockAsync(ClientSessionConnection session, DateTimeOffset timestamp)
    {
        if (await TickSafeSurvivalAutomationAsync(session, timestamp)) return;
        if (await TickSafeSpellMaintenanceAsync(session, timestamp)) return;
        if (await TickSafePartyAutomationAsync(session, timestamp)) return;
        if (await TickSafeLootAutomationAsync(session, timestamp)) return;
        if (await TickSafeRoamingAutomationAsync(session, timestamp)) return;
        await TickSafeCombatAutomationAsync(session, timestamp);
    }

    private async Task<bool> TickSafeSpellMaintenanceAsync(ClientSessionConnection session, DateTimeOffset timestamp)
    {
        var options = AutomationOptions.Spells;
        if (!AutomationProfile.Capabilities.SpellMaintenanceEnabled || AutomationProfile.Capabilities.EmergencyStop ||
            session.ConnectionState != ClientConnectionState.Connected || (!options.MaintainBless && !options.MaintainArmor && !options.MaintainRoomLight)) return false;
        if (options.CastMaintainedSpellsOnlyOutOfCombat && session.InCombat)
        { session.SpellAutomationStatus = "Maintained spells are waiting for combat to end."; return false; }
        if (session.IsMoving || session.IsResting) return false;
        if (_nextSpellMaintenanceAt.TryGetValue(session.SessionId, out var next) && timestamp < next) return false;
        if (!_spellMaintenanceInFlight.Add(session.SessionId)) return true;
        try
        {
            if (session.ActiveEffectsUpdatedAt is not { } effectsAt || timestamp - effectsAt > TimeSpan.FromSeconds(30))
            {
                await _sessions.SendClientProtocolCommandAsync(session.SessionId, "clientdata stats");
                _nextSpellMaintenanceAt[session.SessionId] = timestamp.AddSeconds(2);
                session.SpellAutomationStatus = "Refreshing authoritative active effects privately; no spell was guessed.";
                return true;
            }
            if (session.SpellbookUpdatedAt is not { } spellbookAt || timestamp - spellbookAt > TimeSpan.FromMinutes(10))
            {
                await _sessions.SendClientProtocolCommandAsync(session.SessionId, "clientdata spells");
                _nextSpellMaintenanceAt[session.SessionId] = timestamp.AddSeconds(2);
                session.SpellAutomationStatus = "Refreshing the learned spellbook privately; no spell was guessed.";
                return true;
            }
            if (options.MaintainRoomLight)
            {
                if (session.CurrentRoomEnvironmentUpdatedAt is not { } environmentAt || timestamp - environmentAt > TimeSpan.FromSeconds(30))
                {
                    await _sessions.SendClientProtocolCommandAsync(session.SessionId, "roomdata");
                    _nextSpellMaintenanceAt[session.SessionId] = timestamp.AddSeconds(2);
                    session.SpellAutomationStatus = "Refreshing decoded room lighting privately; no light spell was guessed.";
                    return true;
                }
                var roomKey = session.CurrentRoomKey.Trim();
                if (session.CurrentRoomLightLevel < 0 && roomKey.Length > 0 &&
                    (!_lightedRoomBySession.TryGetValue(session.SessionId, out var lighted) || !lighted.Equals(roomKey, StringComparison.OrdinalIgnoreCase)))
                {
                    var lightSpell = ResolveLearnedSpellForCommand(session, options.LightCommand);
                    if (lightSpell is not null && CanAffordMaintainedSpell(session, lightSpell, options.ReserveManaPercent))
                    {
                        await _sessions.SendCommandAsAsync(session.SessionId, options.LightCommand.Trim(), ClientCommandOrigin.ClientAutomation);
                        _lightedRoomBySession[session.SessionId] = roomKey;
                        _pendingMaintainedCast[session.SessionId] = (options.LightCommand.Trim(), timestamp.AddSeconds(8));
                        _nextSpellMaintenanceAt[session.SessionId] = timestamp.AddSeconds(Math.Max(5, options.MaintainedSpellCooldownSeconds));
                        session.SpellAutomationStatus = $"Decoded room light is {session.CurrentRoomLightLevel}; casting learned light spell {lightSpell.Name} once for this room.";
                        return true;
                    }
                }
            }
            var candidates = new[]
            {
                (options.MaintainBless, options.BlessCommand, "blessing"),
                (options.MaintainArmor, options.ArmorCommand, "armor")
            };
            foreach (var candidate in candidates.Where(candidate => candidate.Item1 && !string.IsNullOrWhiteSpace(candidate.Item2)))
            {
                var command = candidate.Item2.Trim();
                var learned = ResolveLearnedSpellForCommand(session, command);
                if (learned is null) continue;
                if (session.ActiveEffectDetails.Any(effect => effect.SpellId.Equals(learned.CanonicalId, StringComparison.OrdinalIgnoreCase) ||
                    effect.Name.Equals(learned.Name, StringComparison.OrdinalIgnoreCase))) continue;
                if (!CanAffordMaintainedSpell(session, learned, options.ReserveManaPercent))
                { session.SpellAutomationStatus = $"Waiting for enough mana to maintain {learned.Name} without breaking reserve."; return false; }
                await _sessions.SendCommandAsAsync(session.SessionId, command, ClientCommandOrigin.ClientAutomation);
                _pendingMaintainedCast[session.SessionId] = (command, timestamp.AddSeconds(8));
                _nextSpellMaintenanceAt[session.SessionId] = timestamp.AddSeconds(Math.Max(5, options.MaintainedSpellCooldownSeconds));
                session.SpellAutomationStatus = $"Maintaining {learned.Name}; its exact effect was absent from the authoritative effect list.";
                return true;
            }
            session.SpellAutomationStatus = "Configured maintained effects are active, or their commands do not resolve to learned spells.";
            _nextSpellMaintenanceAt[session.SessionId] = timestamp.AddSeconds(5);
            return false;
        }
        catch (Exception exception) { session.SpellAutomationStatus = $"Spell maintenance stopped safely: {exception.Message}"; return true; }
        finally { _spellMaintenanceInFlight.Remove(session.SessionId); }
    }

    private static ClientLearnedSpellState? ResolveLearnedSpellForCommand(ClientSessionConnection session, string command) =>
        session.LearnedSpellDetails.Where(spell => command.Contains(spell.Name, StringComparison.OrdinalIgnoreCase) ||
            command.Contains(spell.CanonicalId, StringComparison.OrdinalIgnoreCase)).OrderByDescending(spell => spell.Name.Length).FirstOrDefault();

    private static bool CanAffordMaintainedSpell(ClientSessionConnection session, ClientLearnedSpellState spell, int reservePercent)
    {
        var reserve = session.ManaMaximum <= 0 ? 0 : (int)Math.Ceiling(session.ManaMaximum * reservePercent / 100d);
        return session.ManaCurrent >= spell.ManaCost && session.ManaCurrent - spell.ManaCost >= reserve;
    }

    private async Task<bool> TickSafePartyAutomationAsync(ClientSessionConnection session, DateTimeOffset timestamp)
    {
        if (!AutomationProfile.Capabilities.PartyAutomationEnabled || AutomationProfile.Capabilities.EmergencyStop ||
            session.ConnectionState != ClientConnectionState.Connected) return false;
        var options = AutomationOptions.Party;
        if (!options.AutoAssistLeader && !options.AutoFollowLeader && !options.AutoAcceptTrustedInvites &&
            !options.RegroupWhenSeparated && !options.WaitWhenLeaderMissing && !options.ResumeWhenLeaderReturns) return false;
        if (_nextPartyAutomationAt.TryGetValue(session.SessionId, out var next) && timestamp < next) return false;
        if (!_partyAutomationInFlight.Add(session.SessionId)) return true;
        try
        {
            var leaderName = options.LeaderName.Trim();
            if (leaderName.Length == 0)
            { session.PartyAutomationStatus = "Party coordination paused until an explicit leader name is configured."; return false; }
            if (session.PartyUpdatedAt is not { } captured || timestamp - captured > TimeSpan.FromSeconds(options.PartyStateMaximumAgeSeconds))
            {
                await _sessions.SendClientProtocolCommandAsync(session.SessionId, "clientdata party");
                _nextPartyAutomationAt[session.SessionId] = timestamp.AddSeconds(2);
                session.PartyAutomationStatus = "Refreshing the authoritative party roster; no party action was guessed.";
                return true;
            }
            var leaderPresent = session.PartyMembers.Any(member => member.Name.Equals(leaderName, StringComparison.OrdinalIgnoreCase));
            if (!leaderPresent)
            {
                _partyFollowingLeader.Remove(session.SessionId);
                if (options.WaitWhenLeaderMissing && _partyWaitingForLeader.Add(session.SessionId))
                {
                    _sessions.PauseRoute(session.SessionId);
                    return await SendPartyCommandAsync(session, options.WaitCommand, $"Leader {leaderName} is absent; waiting and pausing this session's route.", timestamp);
                }
                if (options.RegroupWhenSeparated)
                    return await SendPartyCommandAsync(session, options.RegroupCommand, $"Leader {leaderName} is absent; requesting regroup information.", timestamp);
                session.PartyAutomationStatus = $"Leader {leaderName} is not in the fresh party roster; no assist or follow command was sent.";
                return false;
            }
            if (_partyWaitingForLeader.Remove(session.SessionId) && options.ResumeWhenLeaderReturns)
            {
                _sessions.ResumeRoute(session.SessionId);
                return await SendPartyCommandAsync(session, AppendPartyTarget(options.ResumeCommand, leaderName), $"Leader {leaderName} returned; resuming follow and this session's route.", timestamp);
            }
            if (options.AutoFollowLeader && _partyFollowingLeader.Add(session.SessionId))
                return await SendPartyCommandAsync(session, AppendPartyTarget(options.FollowCommand, leaderName), $"Following configured leader {leaderName}.", timestamp);
            if (options.AutoAssistLeader && session.InCombat)
                return await SendPartyCommandAsync(session, AppendPartyTarget(options.AssistCommand, leaderName), $"Assisting configured leader {leaderName} in combat.", timestamp);
            session.PartyAutomationStatus = $"Leader {leaderName} is present; no coordination action is currently required.";
            return false;
        }
        catch (Exception exception) { session.PartyAutomationStatus = $"Party coordination stopped safely: {exception.Message}"; return true; }
        finally { _partyAutomationInFlight.Remove(session.SessionId); }
    }

    private async Task<bool> SendPartyCommandAsync(ClientSessionConnection session, string command, string reason, DateTimeOffset timestamp)
    {
        if (!CanContinuePartyAutomation(session)) return false;
        if (string.IsNullOrWhiteSpace(command)) { session.PartyAutomationStatus = "Party action paused because its command is blank."; return false; }
        await _sessions.SendCommandAsAsync(session.SessionId, command.Trim(), ClientCommandOrigin.ClientAutomation);
        _nextPartyAutomationAt[session.SessionId] = timestamp.AddSeconds(AutomationOptions.Party.CommandCooldownSeconds);
        session.PartyAutomationStatus = reason;
        return true;
    }

    private bool CanContinuePartyAutomation(ClientSessionConnection session) =>
        AutomationProfile.Capabilities.PartyAutomationEnabled && !AutomationProfile.Capabilities.EmergencyStop &&
        session.ConnectionState == ClientConnectionState.Connected;

    private static string AppendPartyTarget(string command, string target)
    {
        var normalized = command.Trim();
        if (normalized.Contains("{leader}", StringComparison.OrdinalIgnoreCase))
            return normalized.Replace("{leader}", target, StringComparison.OrdinalIgnoreCase);
        return $"{normalized} {target}".Trim();
    }

    private async Task<bool> TickSafeLootAutomationAsync(ClientSessionConnection session, DateTimeOffset timestamp)
    {
        if (!AutomationProfile.Capabilities.LootAutomationEnabled || AutomationProfile.Capabilities.EmergencyStop ||
            session.ConnectionState != ClientConnectionState.Connected || session.InCombat || session.IsResting || session.IsMoving)
            return false;
        var options = AutomationOptions.LootAndCash;
        if (_nextLootAutomationAt.TryGetValue(session.SessionId, out var next) && timestamp < next) return true;
        if (session.CurrentRoomNpcDetailsUpdatedAt is not { } roomDataAt ||
            session.RoomUpdatedAt is { } observedAt && roomDataAt < observedAt)
        {
            await _sessions.SendClientProtocolCommandAsync(session.SessionId, "roomdata");
            session.LootAutomationStatus = "Refreshing authoritative room items and services.";
            return true;
        }
        if (!_lootAutomationInFlight.Add(session.SessionId)) return true;
        try
        {
            if (session.EncumbrancePercent >= options.StopLootingAtWeightPercent)
            {
                if (options.DropWhenOverweight)
                    return await SendLootCommandsAsync(session, [options.DropOverweightCommand], "Dropping the configured overweight item.");
                session.LootAutomationStatus = $"Looting stopped at {session.EncumbrancePercent}% encumbrance.";
                return false;
            }

            var atBank = session.CurrentRoomServices.Any(service => service.Kind.Equals("bank", StringComparison.OrdinalIgnoreCase));
            if (atBank && options.DepositAtBank && session.CarriedCurrency >= options.DepositWhenCarrying)
            {
                if (options.KeepOnCharacter > 0)
                {
                    session.LootAutomationStatus = "Deposit paused: exact denomination data cannot preserve the configured keep amount safely.";
                    return false;
                }
                return await SendLootCommandsAsync(session, [options.DepositCommand], "Depositing currency at the decoded bank.");
            }
            if (atBank && options.ConvertCurrencyAtBank)
            {
                session.LootAutomationStatus = "Currency conversion is unavailable because the server has no exchange command.";
                return false;
            }

            var atShop = session.CurrentRoomServices.Any(service => service.Kind.Equals("shop", StringComparison.OrdinalIgnoreCase));
            if (atShop && options.AutoSellAtShop && session.InventoryItems.Count > 0)
            {
                if (string.IsNullOrWhiteSpace(options.SellIncludeWords))
                { session.LootAutomationStatus = "Auto Sell paused until explicit sell include words are configured."; return false; }
                var sellable = FilterNames(session.InventoryItems, options.SellIncludeWords, options.SellExcludeWords).ToArray();
                var sellCommands = BuildItemTemplateCommands(options.SellCommand, "sell", sellable).Take(100).ToArray();
                if (sellCommands.Length > 0)
                    return await SendLootCommandsAsync(session, sellCommands, $"Selling {sellCommands.Length} filtered inventory item(s) at the decoded shop.");
            }

            var fingerprint = $"{session.CurrentRoomKey}|{session.CurrentRoomCurrency}|{string.Join('|', session.CurrentRoomItemDetails.Select(item => item.InstanceId))}|" +
                              $"{options.GetCash}:{options.GetItems}:{options.LootCorpses}:{options.IncludeWords}:{options.ExcludeWords}:{options.IgnoreHeavyItems}:{options.MaximumItemWeight}";
            if (_lastLootSnapshotBySession.TryGetValue(session.SessionId, out var previous) && previous == fingerprint) return false;

            var commands = new List<string>();
            if (options.GetCash && !string.IsNullOrWhiteSpace(session.CurrentRoomCurrency))
            {
                foreach (var rule in options.CurrencyRules.Where(rule => rule.Enabled && rule.PickUp).OrderBy(rule => rule.SortOrder))
                {
                    var words = SplitWords(rule.RecognitionWords);
                    var matched = words.FirstOrDefault(word => session.CurrentRoomCurrency.Contains(word, StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrWhiteSpace(matched)) commands.Add($"get {matched}");
                }
            }
            if (options.LootCorpses && session.CurrentRoomItems.Any(item => item.Contains("corpse", StringComparison.OrdinalIgnoreCase)))
                commands.Add(options.LootCorpseCommand);
            if (options.GetItems)
            {
                var candidates = session.CurrentRoomItemDetails
                    .Where(item => item.Gettable && !item.Hidden)
                    .Where(item => !options.IgnoreHeavyItems || item.Weight <= options.MaximumItemWeight)
                    .Select(item => item.Name);
                commands.AddRange(BuildItemTemplateCommands(options.GetItemsCommand, "get", FilterLootNames(candidates, options)).Take(100));
            }
            _lastLootSnapshotBySession[session.SessionId] = fingerprint;
            if (commands.Count == 0)
            { session.LootAutomationStatus = "No enabled currency or filtered gettable items are present."; return false; }
            return await SendLootCommandsAsync(session, commands.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                $"Collecting {commands.Distinct(StringComparer.OrdinalIgnoreCase).Count()} approved loot action(s).");
        }
        catch (Exception exception) { session.LootAutomationStatus = $"Loot automation stopped safely: {exception.Message}"; return true; }
        finally { _lootAutomationInFlight.Remove(session.SessionId); }
    }

    private async Task<bool> SendLootCommandsAsync(ClientSessionConnection session, IReadOnlyList<string> commands, string reason)
    {
        var sent = 0;
        foreach (var command in commands.Where(command => !string.IsNullOrWhiteSpace(command)))
        {
            if (!CanContinueLootAutomation(session)) break;
            await _sessions.SendCommandAsAsync(session.SessionId, command.Trim(), ClientCommandOrigin.ClientAutomation);
            sent++;
            await Task.Delay(Math.Max(500, AutomationProfile.MinimumCommandDelayMs));
            if (!CanContinueLootAutomation(session)) break;
        }
        if (CanContinueLootAutomation(session))
        {
            await _sessions.SendClientProtocolCommandAsync(session.SessionId, "clientdata inventory");
            await _sessions.SendClientProtocolCommandAsync(session.SessionId, "roomdata");
        }
        _nextLootAutomationAt[session.SessionId] = DateTimeOffset.Now.AddSeconds(4);
        session.LootAutomationStatus = sent == 0 ? "No valid loot command was sent." : reason;
        return sent > 0;
    }

    private bool CanContinueLootAutomation(ClientSessionConnection session) =>
        AutomationProfile.Capabilities.LootAutomationEnabled && !AutomationProfile.Capabilities.EmergencyStop &&
        session.ConnectionState == ClientConnectionState.Connected && !session.InCombat && !session.IsResting && !session.IsMoving;

    private static IEnumerable<string> FilterLootNames(IEnumerable<string> names, LootCashAutomationOptions options)
        => FilterNames(names, options.IncludeWords, options.ExcludeWords);

    private static IEnumerable<string> FilterNames(IEnumerable<string> names, string includeWords, string excludeWords)
    {
        var includes = SplitWords(includeWords);
        var excludes = SplitWords(excludeWords);
        return names.Select(name => name.Trim()).Where(name => name.Length > 0)
            .Where(name => includes.Length == 0 || includes.Any(word => name.Contains(word, StringComparison.OrdinalIgnoreCase)))
            .Where(name => !excludes.Any(word => name.Contains(word, StringComparison.OrdinalIgnoreCase)));
    }

    private static string[] SplitWords(string value) => value.Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static IReadOnlyList<string> BuildItemTemplateCommands(string template, string fallbackVerb, IEnumerable<string> names)
    {
        var normalizedTemplate = string.IsNullOrWhiteSpace(template) ? $"{fallbackVerb} {{item}}" : template.Trim();
        return BuildItemCommands(fallbackVerb, names)
            .Select(command => command[(fallbackVerb.Length + 1)..])
            .Select(item => normalizedTemplate.Contains("{item}", StringComparison.OrdinalIgnoreCase)
                ? normalizedTemplate.Replace("{item}", item, StringComparison.OrdinalIgnoreCase)
                : $"{fallbackVerb} {item}")
            .ToArray();
    }

    private async Task<bool> TickSafeSurvivalAutomationAsync(ClientSessionConnection session, DateTimeOffset timestamp)
    {
        if (!AutomationProfile.Capabilities.HealthRecoveryEnabled || AutomationProfile.Capabilities.EmergencyStop ||
            session.ConnectionState != ClientConnectionState.Connected) return false;
        if (session.PromptUpdatedAt is not { } vitalsAt || timestamp - vitalsAt > TimeSpan.FromSeconds(10))
        {
            await _sessions.SendClientProtocolCommandAsync(session.SessionId, "clientdata stats");
            _nextSurvivalAutomationAt[session.SessionId] = timestamp.AddSeconds(3);
            session.SurvivalAutomationStatus = "Refreshing current health, mana/Kai and effects privately.";
            return true;
        }
        if (_nextSurvivalAutomationAt.TryGetValue(session.SessionId, out var next) && timestamp < next) return true;
        if (!_survivalAutomationInFlight.Add(session.SessionId)) return true;
        try
        {
            var health = AutomationOptions.Health;
            var utility = AutomationOptions.Utility;
            var effects = string.Join(' ', session.ActiveEffects);
            var incapacitated = (session.HealthMaximum > 0 && session.HealthCurrent <= 0) ||
                                effects.Contains("incapacitat", StringComparison.OrdinalIgnoreCase) ||
                                effects.Contains("unconscious", StringComparison.OrdinalIgnoreCase);
            if (incapacitated && utility.AutoRecover)
                return await SendSurvivalCommandAsync(session, utility.RecoverCommand, "Recovering from incapacitation.", 5);
            if ((effects.Contains("asleep", StringComparison.OrdinalIgnoreCase) || effects.Contains("sleeping", StringComparison.OrdinalIgnoreCase)) && health.AutoWake)
                return await SendSurvivalCommandAsync(session, health.WakeCommand, "Waking the character.", 3);
            if (session.IsMoving) { session.SurvivalAutomationStatus = "Waiting for movement to finish."; return false; }

            if (session.InCombat)
            {
                if (health.AutoRecallAtCriticalHealth && session.HealthPercent <= health.RecallAtPercent)
                    return await SendSurvivalCommandAsync(session, health.RecallCommand, "Emergency recall threshold reached.", 5);
                if (AutomationOptions.Combat.AutoFlee && session.HealthPercent <= AutomationOptions.Combat.FleeAtPercent)
                    return await SendSurvivalCommandAsync(session, "flee", "Emergency flee threshold reached.", 3);
            }

            if (health.CurePoison && effects.Contains("poison", StringComparison.OrdinalIgnoreCase) &&
                await TrySendSurvivalSpellAsync(session, health.CurePoisonCommand, "Curing poison.")) return true;
            if (health.CureDisease && effects.Contains("disease", StringComparison.OrdinalIgnoreCase) &&
                await TrySendSurvivalSpellAsync(session, health.CureDiseaseCommand, "Curing disease.")) return true;
            if (health.CureBlindness && effects.Contains("blind", StringComparison.OrdinalIgnoreCase) &&
                await TrySendSurvivalSpellAsync(session, health.CureBlindnessCommand, "Curing blindness.")) return true;
            if (health.CureSilence && effects.Contains("silenc", StringComparison.OrdinalIgnoreCase) &&
                !health.CureSilenceCommand.TrimStart().StartsWith("cast ", StringComparison.OrdinalIgnoreCase))
                return await SendSurvivalCommandAsync(session, health.CureSilenceCommand, "Removing silence with a non-spell action.", 5);

            if (health.AutoUseHealingItem && session.HealthPercent <= health.HealingItemAtPercent)
                return await SendSurvivalCommandAsync(session, health.HealingItemCommand, "Using the configured emergency healing item.", 5);

            if (health.AutoHeal)
            {
                var healCommand = session.HealthPercent <= health.MajorHealAtPercent
                    ? AutomationOptions.Spells.MajorHealCommand
                    : session.HealthPercent <= health.MinorHealAtPercent ? AutomationOptions.Spells.MinorHealCommand : string.Empty;
                if (!string.IsNullOrWhiteSpace(healCommand) && await TrySendSurvivalSpellAsync(session, healCommand, "Casting an affordable learned healing spell.")) return true;
            }

            var woundedParty = session.PartyMembers.Where(member => member.HealthPercent < AutomationOptions.Party.HealPartyBelowPercent)
                .OrderBy(member => member.HealthPercent).FirstOrDefault();
            if (woundedParty is not null && session.InCombat && AutomationOptions.Combat.AutoRescueParty)
                return await SendSurvivalCommandAsync(session, $"{AutomationOptions.Combat.RescueCommand} {woundedParty.Name}", $"Rescuing {woundedParty.Name}.", 5);
            if (woundedParty is not null && AutomationOptions.Party.AutoHealParty &&
                await TrySendSurvivalSpellAsync(session, $"{AutomationOptions.Party.HealPartyCommand} {woundedParty.Name}", $"Healing party member {woundedParty.Name}.")) return true;

            if (session.InCombat) { session.SurvivalAutomationStatus = "Combat active; no survival threshold requires action."; return false; }
            var recovery = session.ClassicHealthPreferences;
            if (session.IsResting && health.StopRestingWhenRecovered && session.ManaPercent >= health.StopRestingManaPercent &&
                session.HealthPercent >= recovery.HealRestHealthPercent)
                return await FinishRecoveryAsync(session, utility.StandCommand);
            var needsRecovery = session.ManaPercent <= health.RestManaAtPercent ||
                                (health.AutoRest && session.HealthPercent <= recovery.RestHealthBelowPercent);
            if (!session.IsResting && needsRecovery && (health.AutoMeditate || health.AutoRest))
            {
                var meditate = health.AutoMeditate || recovery.UseMeditateAbility || recovery.MeditateBeforeResting;
                return await BeginRecoveryAsync(session, meditate ? "meditate" : "rest");
            }
            session.SurvivalAutomationStatus = "Vitals are within configured survival limits.";
            return false;
        }
        catch (Exception exception) { session.SurvivalAutomationStatus = $"Survival action stopped safely: {exception.Message}"; return true; }
        finally { _survivalAutomationInFlight.Remove(session.SessionId); }
    }

    private async Task<bool> TickSafeRoamingAutomationAsync(ClientSessionConnection session, DateTimeOffset timestamp)
    {
        if (!AutomationProfile.Capabilities.RoamingAutomationEnabled || AutomationProfile.Capabilities.EmergencyStop ||
            session.ConnectionState != ClientConnectionState.Connected || session.InCombat || session.IsResting || session.IsMoving)
            return false;
        var options = AutomationOptions.Roaming;
        if (session.CurrentRoomNpcDetailsUpdatedAt is not { } roomDataAt ||
            session.RoomUpdatedAt is { } roomObservedAt && roomDataAt < roomObservedAt)
        {
            await _sessions.SendClientProtocolCommandAsync(session.SessionId, "roomdata");
            session.RoamingAutomationStatus = "Refreshing decoded room commands and obstacle data.";
            return true;
        }
        if (AutomationProfile.Capabilities.NavigationEnabled && options.AutoRecoverDesynchronizedRoute && session.RouteRecoveryAvailable &&
            session.RouteState is RouteExecutionState.Paused or RouteExecutionState.Faulted)
        {
            var replacement = _paths.CalculateReplacementRoute(session.CurrentRoomKey, session.RouteDestinationKey);
            if (replacement is null || replacement.Steps.Count == 0)
            { session.RoamingAutomationStatus = "Route recovery stopped: no verified replacement route exists."; return false; }
            await _sessions.StartRouteAsync(session.SessionId, replacement);
            session.RoamingAutomationStatus = $"Started verified {replacement.Steps.Count}-step route recovery.";
            return true;
        }

        var roomIdentity = string.IsNullOrWhiteSpace(session.CurrentRoomKey) ? session.CurrentRoomName : session.CurrentRoomKey;
        if (string.IsNullOrWhiteSpace(roomIdentity) ||
            (_roamingProcessedRoomBySession.TryGetValue(session.SessionId, out var processed) && processed.Equals(roomIdentity, StringComparison.OrdinalIgnoreCase)))
            return false;
        if (!_roamingAutomationInFlight.Add(session.SessionId)) return true;
        _roamingProcessedRoomBySession[session.SessionId] = roomIdentity;
        var pausedRoute = session.RouteState == RouteExecutionState.Running;
        try
        {
            if (pausedRoute) _sessions.PauseRoute(session.SessionId);
            var knownTrapCommand = session.CurrentRoomCommandDetails.Any(command =>
                command.Command.Contains("trap", StringComparison.OrdinalIgnoreCase) ||
                command.Command.Contains("disarm", StringComparison.OrdinalIgnoreCase) ||
                command.InteractionType.Contains("trap", StringComparison.OrdinalIgnoreCase));
            var commands = new List<(bool Enabled, string Command, string Label)>
            {
                (options.DetectTraps && knownTrapCommand, options.DetectTrapCommand, "detect traps"),
                (options.AutoSearch, options.SearchCommand, "search"),
                (options.DisarmTraps && knownTrapCommand, options.DisarmTrapCommand, "disarm trap"),
                (options.AutoSneak, options.SneakCommand, "sneak"),
                (AutomationOptions.Utility.AutoHide, AutomationOptions.Utility.HideCommand, "hide"),
                (options.AutoTrack, options.TrackCommand, "track"),
                (AutomationOptions.Utility.AutoLook, AutomationOptions.Utility.LookCommand, "look")
            };
            var sent = new List<string>();
            foreach (var action in commands.Where(action => action.Enabled && !string.IsNullOrWhiteSpace(action.Command)))
            {
                if (!CanContinueRoamingAutomation(session)) break;
                await _sessions.SendCommandAsAsync(session.SessionId, action.Command.Trim(), ClientCommandOrigin.ClientAutomation);
                sent.Add(action.Label);
                await Task.Delay(Math.Clamp(options.RoomActionDelayMilliseconds, 500, 10000));
                if (!CanContinueRoamingAutomation(session)) break;
            }
            session.RoamingAutomationStatus = sent.Count == 0
                ? "Room confirmed; no enabled room-entry action applied."
                : $"Room-entry actions: {string.Join(", ", sent)}.";
            return sent.Count > 0;
        }
        catch (Exception exception)
        {
            session.RoamingAutomationStatus = $"Room-entry automation stopped safely: {exception.Message}";
            return true;
        }
        finally
        {
            if (pausedRoute && session.RouteState == RouteExecutionState.Paused && CanContinueRoamingAutomation(session) &&
                AutomationProfile.Capabilities.NavigationEnabled)
                _sessions.ResumeRoute(session.SessionId);
            _roamingAutomationInFlight.Remove(session.SessionId);
        }
    }

    private bool CanContinueRoamingAutomation(ClientSessionConnection session) =>
        AutomationProfile.Capabilities.RoamingAutomationEnabled && !AutomationProfile.Capabilities.EmergencyStop &&
        session.ConnectionState == ClientConnectionState.Connected && !session.InCombat && !session.IsResting && !session.IsMoving;

    private async Task<bool> TrySendSurvivalSpellAsync(ClientSessionConnection session, string command, string reason)
    {
        var normalized = command.Trim();
        if (normalized.Length == 0) return false;
        if (!normalized.StartsWith("cast ", StringComparison.OrdinalIgnoreCase))
            return await SendSurvivalCommandAsync(session, normalized, reason, 4);
        if (session.ActiveEffects.Any(effect => effect.Contains("silenc", StringComparison.OrdinalIgnoreCase)))
        { session.SurvivalAutomationStatus = "A configured survival spell is blocked by silence."; return false; }
        var learned = ResolveLearnedSpell(session, normalized);
        if (learned is null)
        { session.SurvivalAutomationStatus = "A configured survival spell is not in the captured learned-spell list."; return false; }
        if (session.ManaCurrent < learned.ManaCost)
        { session.SurvivalAutomationStatus = $"Not enough mana for {learned.Name}; no spell sent."; return false; }
        return await SendSurvivalCommandAsync(session, normalized, reason, 4);
    }

    private async Task<bool> SendSurvivalCommandAsync(
        ClientSessionConnection session, string command, string reason, int cooldownSeconds)
    {
        if (string.IsNullOrWhiteSpace(command)) return false;
        await _sessions.SendCommandAsAsync(session.SessionId, command.Trim(), ClientCommandOrigin.ClientAutomation);
        _nextSurvivalAutomationAt[session.SessionId] = DateTimeOffset.Now.AddSeconds(Math.Max(1, cooldownSeconds));
        session.SurvivalAutomationStatus = reason;
        return true;
    }

    private async Task<bool> BeginRecoveryAsync(ClientSessionConnection session, string mode)
    {
        var preferences = session.ClassicHealthPreferences;
        var meditate = mode.Equals("meditate", StringComparison.OrdinalIgnoreCase);
        var pre = meditate ? preferences.PreMeditateCommand : preferences.PreRestCommand;
        var post = meditate ? preferences.PostMeditateCommand : preferences.PostRestCommand;
        if (!string.IsNullOrWhiteSpace(pre))
        {
            await _sessions.SendCommandAsAsync(session.SessionId, pre.Trim(), ClientCommandOrigin.ClientAutomation);
            await Task.Delay(Math.Max(500, AutomationProfile.MinimumCommandDelayMs));
            if (!CanContinueHealthRecovery(session)) return false;
        }
        await _sessions.SendCommandAsAsync(session.SessionId, mode, ClientCommandOrigin.ClientAutomation);
        if (!string.IsNullOrWhiteSpace(post)) _pendingRecoveryPostCommand[session.SessionId] = post.Trim();
        _nextSurvivalAutomationAt[session.SessionId] = DateTimeOffset.Now.AddSeconds(4);
        session.SurvivalAutomationStatus = $"Beginning {mode}.";
        return true;
    }

    private async Task<bool> FinishRecoveryAsync(ClientSessionConnection session, string standCommand)
    {
        await _sessions.SendCommandAsAsync(session.SessionId,
            string.IsNullOrWhiteSpace(standCommand) ? "stand" : standCommand.Trim(), ClientCommandOrigin.ClientAutomation);
        if (_pendingRecoveryPostCommand.Remove(session.SessionId, out var post))
        {
            await Task.Delay(Math.Max(500, AutomationProfile.MinimumCommandDelayMs));
            if (!CanContinueHealthRecovery(session)) return false;
            await _sessions.SendCommandAsAsync(session.SessionId, post, ClientCommandOrigin.ClientAutomation);
        }
        _nextSurvivalAutomationAt[session.SessionId] = DateTimeOffset.Now.AddSeconds(3);
        session.SurvivalAutomationStatus = "Recovery complete; standing.";
        return true;
    }

    private bool CanContinueHealthRecovery(ClientSessionConnection session) =>
        AutomationProfile.Capabilities.HealthRecoveryEnabled &&
        !AutomationProfile.Capabilities.EmergencyStop &&
        session.ConnectionState == ClientConnectionState.Connected;

    private static ClientLearnedSpellState? ResolveLearnedSpell(ClientSessionConnection session, string command) =>
        session.LearnedSpellDetails
            .Where(spell => command.Contains(spell.Name, StringComparison.OrdinalIgnoreCase) ||
                            command.Contains(spell.CanonicalId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(spell => spell.Name.Length).FirstOrDefault();

    private async Task TickSafeCombatAutomationAsync(ClientSessionConnection session, DateTimeOffset timestamp)
    {
        var options = AutomationOptions.Combat;
        if (!AutomationProfile.Capabilities.CombatEnabled || AutomationProfile.Capabilities.EmergencyStop || !options.AutoAttack)
        { session.CombatAutomationStatus = "Verified combat automation is off."; return; }
        if (session.ConnectionState != ClientConnectionState.Connected)
        { session.CombatAutomationStatus = "Waiting for a connected session."; return; }
        if (session.CurrentRoomNpcDetailsUpdatedAt is not { } npcCapturedAt ||
            (timestamp - npcCapturedAt).TotalSeconds > 2)
        {
            await _sessions.SendClientProtocolCommandAsync(session.SessionId, "roomdata");
            session.CombatAutomationStatus = "Refreshing authoritative hostile-target data.";
            return;
        }
        if (ResolveCombatAutomationBlocker(session) is { } blocker)
        { session.CombatAutomationStatus = blocker; return; }
        if (session.InCombat && options.AutoFlee && session.HealthPercent <= options.FleeAtPercent)
        {
            if (_nextCombatAutomationAt.TryGetValue(session.SessionId, out var fleeReady) && timestamp < fleeReady) return;
            await _sessions.SendCommandAsAsync(session.SessionId, "flee", ClientCommandOrigin.ClientAutomation);
            _nextCombatAutomationAt[session.SessionId] = timestamp.AddSeconds(3);
            session.CombatAutomationStatus = $"Fleeing at {session.HealthPercent}% health.";
            return;
        }
        if (session.InCombat && options.EscapeAtLowMana && session.ManaPercent <= options.LowManaEscapeAtPercent)
        {
            if (_nextCombatAutomationAt.TryGetValue(session.SessionId, out var manaReady) && timestamp < manaReady) return;
            var escape = string.IsNullOrWhiteSpace(options.LowManaEscapeCommand) ? "flee" : options.LowManaEscapeCommand.Trim();
            await _sessions.SendCommandAsAsync(session.SessionId, escape, ClientCommandOrigin.ClientAutomation);
            _nextCombatAutomationAt[session.SessionId] = timestamp.AddSeconds(3);
            session.CombatAutomationStatus = $"Using the configured low-mana escape at {session.ManaPercent}% mana/Kai.";
            return;
        }
        if (session.InCombat)
        {
            _combatObservedSessions.Add(session.SessionId);
            await TickCombatRoundActionAsync(session, timestamp);
            return;
        }
        if (session.IsResting || session.IsMoving)
        { session.CombatAutomationStatus = session.IsResting ? "Waiting while resting." : "Waiting for movement to finish."; return; }
        var retargeting = _combatObservedSessions.Remove(session.SessionId);
        if (_manualRetargetBlockedRoomBySession.TryGetValue(session.SessionId, out var blockedRoom) &&
            !blockedRoom.Equals(session.CurrentRoomKey, StringComparison.OrdinalIgnoreCase))
            _manualRetargetBlockedRoomBySession.Remove(session.SessionId);
        if (retargeting && !options.AutoRetarget)
        {
            _manualRetargetBlockedRoomBySession[session.SessionId] = session.CurrentRoomKey;
            session.CombatAutomationStatus = "Combat ended; automatic retargeting is disabled.";
            return;
        }
        if (_manualRetargetBlockedRoomBySession.ContainsKey(session.SessionId))
        { session.CombatAutomationStatus = "Waiting for a manual target or room change."; return; }
        if (_nextCombatAutomationAt.TryGetValue(session.SessionId, out var next) && timestamp < next) return;
        if (!_combatAutomationInFlight.Add(session.SessionId)) return;
        try
        {
            var hostiles = session.CurrentRoomNpcDetails
                .Where(npc => npc.Alive && npc.Disposition.Equals("hostile", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var preferences = session.ClassicCombatPreferences;
            if (hostiles.Length < preferences.MinimumMobsPerRoom || hostiles.Length > preferences.MaximumMobsPerRoom)
            { session.CombatAutomationStatus = $"Stopped: {hostiles.Length} hostile NPCs are outside the configured room limit."; return; }
            if (hostiles.Sum(npc => Math.Max(0L, npc.ExperienceReward)) > preferences.MaxTotalExperiencePerRoom)
            { session.CombatAutomationStatus = "Stopped: decoded room experience exceeds the configured limit."; return; }
            var target = hostiles.FirstOrDefault();
            if (target is null || string.IsNullOrWhiteSpace(target.Name))
            { session.CombatAutomationStatus = "Waiting: no living decoded hostile target is present."; return; }

            if (AutomationOptions.Spells.AutoNuke && session.ManaPercent > AutomationOptions.Spells.ReserveManaPercent)
            {
                var spellOptions = AutomationOptions.Spells;
                var debuff = SelectCombatSpellAction(spellOptions.DebuffPriorityActions, session, hostiles.Length, timestamp, spellOptions.AreaNukeMinimumTargets);
                var nuke = SelectCombatSpellAction(spellOptions.NukePriorityActions, session, hostiles.Length, timestamp, spellOptions.AreaNukeMinimumTargets);
                var spellAction = debuff ?? nuke;
                if (spellAction is not null)
                {
                    await SendCombatSpellAsync(session, spellAction, target.Name, spellOptions.NukeCooldownSeconds);
                    await Task.Delay(Math.Max(500, AutomationProfile.MinimumCommandDelayMs));
                    if (session.ConnectionState != ClientConnectionState.Connected || session.InCombat) return;
                }
            }

            if (options.AutoPreAttack && !string.IsNullOrWhiteSpace(options.PreAttackCommand))
            {
                await _sessions.SendCommandAsAsync(session.SessionId,
                    ExpandCombatTarget(options.PreAttackCommand, target.Name), ClientCommandOrigin.ClientAutomation);
                await Task.Delay(Math.Max(500, AutomationProfile.MinimumCommandDelayMs));
                if (session.ConnectionState != ClientConnectionState.Connected || session.InCombat) return;
            }

            if (!retargeting && preferences.DoBackstabAttacks && !(preferences.DontBackstabIfMultiAttack && hostiles.Length > 1))
            {
                if (!string.IsNullOrWhiteSpace(preferences.BackstabWeapon))
                {
                    await _sessions.SendCommandAsAsync(session.SessionId, $"equip {preferences.BackstabWeapon}", ClientCommandOrigin.ClientAutomation);
                    await Task.Delay(Math.Max(500, AutomationProfile.MinimumCommandDelayMs));
                    if (!CanContinueVerifiedCombat(session)) return;
                }
                if (!string.IsNullOrWhiteSpace(preferences.PreBackstabCommand))
                {
                    await _sessions.SendCommandAsAsync(session.SessionId, preferences.PreBackstabCommand.Trim(), ClientCommandOrigin.ClientAutomation);
                    await Task.Delay(Math.Max(500, AutomationProfile.MinimumCommandDelayMs));
                    if (!CanContinueVerifiedCombat(session)) return;
                }
                await _sessions.SendCommandAsAsync(session.SessionId, $"backstab {target.Name}", ClientCommandOrigin.ClientAutomation);
                if (!string.IsNullOrWhiteSpace(preferences.PostBackstabCommand))
                {
                    await Task.Delay(Math.Max(500, AutomationProfile.MinimumCommandDelayMs));
                    if (!CanContinueVerifiedCombat(session)) return;
                    await _sessions.SendCommandAsAsync(session.SessionId, preferences.PostBackstabCommand.Trim(), ClientCommandOrigin.ClientAutomation);
                }
                if (!string.IsNullOrWhiteSpace(preferences.NormalWeapon))
                {
                    await Task.Delay(Math.Max(500, AutomationProfile.MinimumCommandDelayMs));
                    if (!CanContinueVerifiedCombat(session)) return;
                    await _sessions.SendCommandAsAsync(session.SessionId, $"equip {preferences.NormalWeapon}", ClientCommandOrigin.ClientAutomation);
                }
                _nextCombatAutomationAt[session.SessionId] = DateTimeOffset.Now.AddSeconds(3);
                session.CombatAutomationStatus = $"Backstab engagement requested against {target.Name}.";
                return;
            }

            var configuredAttack = retargeting ? options.RetargetCommand : options.PrimaryAttackCommand;
            var attack = string.IsNullOrWhiteSpace(configuredAttack) ? "attack" : configuredAttack.Trim();
            await _sessions.SendCommandAsAsync(session.SessionId,
                ExpandCombatTarget(attack, target.Name), ClientCommandOrigin.ClientAutomation);
            _nextCombatAutomationAt[session.SessionId] = DateTimeOffset.Now.AddSeconds(3);
            session.CombatAutomationStatus = $"Engagement requested against {target.Name}.";
            Status = $"Auto Combat engaged decoded hostile target {target.Name}.";
        }
        catch (Exception exception)
        {
            _nextCombatAutomationAt[session.SessionId] = DateTimeOffset.Now.AddSeconds(5);
            Status = $"Auto Combat stopped safely: {exception.Message}";
            session.CombatAutomationStatus = Status;
        }
        finally { _combatAutomationInFlight.Remove(session.SessionId); }
    }

    private async Task TickCombatRoundActionAsync(ClientSessionConnection session, DateTimeOffset timestamp)
    {
        if (_nextCombatAutomationAt.TryGetValue(session.SessionId, out var next) && timestamp < next) return;
        if (!_combatAutomationInFlight.Add(session.SessionId)) return;
        try
        {
            var target = session.CurrentRoomNpcDetails.FirstOrDefault(npc => npc.Alive &&
                npc.Disposition.Equals("hostile", StringComparison.OrdinalIgnoreCase));
            if (target is null)
            { session.CombatAutomationStatus = "Combat is active, but no authoritative hostile target is visible; no command sent."; return; }
            var action = AutomationOptions.Combat.PriorityActions
                .Where(candidate => candidate.Enabled && candidate.OnlyInCombat && !string.IsNullOrWhiteSpace(candidate.Command))
                .Where(candidate => session.HealthPercent >= candidate.MinimumHealthPercent && session.ManaPercent >= candidate.MinimumManaPercent)
                .Where(candidate => CanUseCombatRoundAction(candidate, session))
                .Where(candidate => !_combatSpellCooldowns.TryGetValue($"{session.SessionId}:{candidate.Id}", out var readyAt) || timestamp >= readyAt)
                .OrderBy(candidate => candidate.Priority).FirstOrDefault();
            if (action is null)
            { session.CombatAutomationStatus = "Combat active; waiting for a configured round action cooldown."; return; }
            var command = action.AppendTarget ? ExpandCombatTarget(action.Command, target.Name) : action.Command.Trim();
            await _sessions.SendCommandAsAsync(session.SessionId, command, ClientCommandOrigin.ClientAutomation);
            _combatSpellCooldowns[$"{session.SessionId}:{action.Id}"] = DateTimeOffset.Now.AddSeconds(3);
            _nextCombatAutomationAt[session.SessionId] = DateTimeOffset.Now.AddSeconds(1);
            session.CombatAutomationStatus = $"Combat priority: {action.Name} against {target.Name}.";
        }
        catch (Exception exception) { session.CombatAutomationStatus = $"Round action stopped safely: {exception.Message}"; }
        finally { _combatAutomationInFlight.Remove(session.SessionId); }
    }

    private static string? ResolveCombatAutomationBlocker(ClientSessionConnection session)
    {
        if (session.HealthMaximum > 0 && session.HealthCurrent <= 0) return "Stopped: character is incapacitated.";
        var effects = string.Join(' ', session.ActiveEffects);
        if (effects.Contains("incapacitat", StringComparison.OrdinalIgnoreCase) || effects.Contains("unconscious", StringComparison.OrdinalIgnoreCase))
            return "Stopped: character is incapacitated.";
        if (effects.Contains("blind", StringComparison.OrdinalIgnoreCase)) return "Stopped: blindness blocks safe targeting.";
        return null;
    }

    private bool CanContinueVerifiedCombat(ClientSessionConnection session) =>
        AutomationProfile.Capabilities.CombatEnabled &&
        !AutomationProfile.Capabilities.EmergencyStop &&
        session.ConnectionState == ClientConnectionState.Connected;

    private bool CanUseCombatRoundAction(ClientPriorityAction action, ClientSessionConnection session)
    {
        var effects = string.Join(' ', session.ActiveEffects);
        var command = action.Command.Trim();
        if ((effects.Contains("kai bound", StringComparison.OrdinalIgnoreCase) || effects.Contains("kai-bind", StringComparison.OrdinalIgnoreCase)) &&
            (command.StartsWith("punch", StringComparison.OrdinalIgnoreCase) || command.StartsWith("kick", StringComparison.OrdinalIgnoreCase) ||
             command.StartsWith("jumpkick", StringComparison.OrdinalIgnoreCase) || command.Contains("kai", StringComparison.OrdinalIgnoreCase)))
            return false;
        return !command.StartsWith("cast ", StringComparison.OrdinalIgnoreCase) || CanUseLearnedCombatSpell(action, session);
    }

    private ClientPriorityAction? SelectCombatSpellAction(
        IEnumerable<ClientPriorityAction> actions, ClientSessionConnection session, int targetCount,
        DateTimeOffset timestamp, int areaMinimumTargets) =>
        actions.Where(action => action.Enabled && !string.IsNullOrWhiteSpace(action.Command))
            .Where(action => !action.OnlyInCombat)
            .Where(action => session.HealthPercent >= action.MinimumHealthPercent && session.ManaPercent >= action.MinimumManaPercent)
            .Where(action => CanUseLearnedCombatSpell(action, session))
            .Where(action => targetCount >= Math.Max(1, action.IsAreaAction ? Math.Max(areaMinimumTargets, action.MinimumTargetCount) : action.MinimumTargetCount) &&
                             targetCount <= Math.Max(1, action.MaximumTargetCount))
            .Where(action => !_combatSpellCooldowns.TryGetValue($"{session.SessionId}:{action.Id}", out var readyAt) || timestamp >= readyAt)
            .OrderBy(action => action.Priority)
            .FirstOrDefault();

    private async Task SendCombatSpellAsync(
        ClientSessionConnection session, ClientPriorityAction action, string targetName, int cooldownSeconds)
    {
        await _sessions.SendCommandAsAsync(session.SessionId,
            action.AppendTarget ? ExpandCombatTarget(action.Command, targetName) : action.Command.Trim(),
            ClientCommandOrigin.ClientAutomation);
        _combatSpellCooldowns[$"{session.SessionId}:{action.Id}"] =
            DateTimeOffset.Now.AddSeconds(Math.Max(1, cooldownSeconds));
        if (!string.IsNullOrWhiteSpace(action.FallbackCommand))
            _pendingCombatSpellFallback[session.SessionId] =
                (action.FallbackCommand.Trim(), targetName, DateTimeOffset.Now.AddSeconds(8));
        Status = $"Auto Nuke used {action.Name} on decoded hostile target {targetName}.";
    }

    private bool CanUseLearnedCombatSpell(ClientPriorityAction action, ClientSessionConnection session)
    {
        var effects = string.Join(' ', session.ActiveEffects);
        if (effects.Contains("silenc", StringComparison.OrdinalIgnoreCase)) return false;
        if ((effects.Contains("kai bound", StringComparison.OrdinalIgnoreCase) || effects.Contains("kai-bind", StringComparison.OrdinalIgnoreCase)) &&
            action.Command.Contains("kai", StringComparison.OrdinalIgnoreCase)) return false;
        var learned = session.LearnedSpellDetails
            .Where(spell => action.Command.Contains(spell.Name, StringComparison.OrdinalIgnoreCase) ||
                            action.Command.Contains(spell.CanonicalId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(spell => spell.Name.Length).FirstOrDefault();
        if (learned is null || session.ManaMaximum <= 0) return false;
        var reserve = (int)Math.Ceiling(session.ManaMaximum * AutomationOptions.Spells.ReserveManaPercent / 100d);
        return session.ManaCurrent >= learned.ManaCost && session.ManaCurrent - learned.ManaCost >= reserve;
    }

    private static string ExpandCombatTarget(string command, string targetName)
    {
        var trimmed = command.Trim();
        return trimmed.Contains("{target}", StringComparison.OrdinalIgnoreCase)
            ? trimmed.Replace("{target}", targetName, StringComparison.OrdinalIgnoreCase)
            : $"{trimmed} {targetName}";
    }


    public async Task SaveWorkspaceProfileAsync()
    {
        await _workspaceProfiles.SaveAsync();
        Status = $"Workspace profile '{WorkspaceProfile.ProfileName}' saved.";
        Raise(nameof(WorkspaceProfile));
    }

    public async Task ApplyWorkspacePresetAsync()
    {
        var preset = WorkspaceProfile.LayoutPreset;
        await _workspaceProfiles.ResetAsync(preset);
        Status = $"{preset} workspace preset applied.";
        Raise(nameof(WorkspaceProfile));
    }

    private void SessionLineReceived(object? sender, ClientTerminalLineEvent e)
    {
        var session = _sessions.Sessions.FirstOrDefault(s => s.SessionId == e.SessionId);
        if (session is null) return;
        EnsureAuthoritativeStateBootstrap(session);
        _ = TryHandleCombatAutomationOutcomeAsync(session, e.Line, e.Timestamp);
        _ = TryHandleRoamingObstacleAsync(session, e.Line, e.Timestamp);
        _ = TryHandleTrustedPartyInviteAsync(session, e.Line, e.Timestamp);
        _ = TryHandleMaintainedSpellOutcomeAsync(session, e.Line, e.Timestamp);
        _ = TryHandlePvpSafetyAsync(session, e.Line, e.Timestamp);
        TrySendAwayReply(session, e.Line, e.Timestamp);
        var context = new ClientAutomationContext(session.HealthPercent, session.ManaPercent, session.InCombat, session.IsResting, session.IsMoving, e.Line, e.Timestamp, session.SessionId);
        _automation.Evaluate(context);
        ApplyClassicPartyDirective(session, e.Line);

        if (CanContinueCommunications() && CoordinationProfile.AcceptChatCommands && e.Line.Contains('@'))
        {
            var separator = e.Line.IndexOf(':');
            var senderName = separator > 0 ? e.Line[..separator].Trim(' ', '[', ']') : string.Empty;
            var message = separator > 0 ? e.Line[(separator + 1)..].Trim() : e.Line.Trim();
            if (message.StartsWith(CoordinationProfile.CommandPrefix, StringComparison.OrdinalIgnoreCase))
            {
                if (!_coordination.TryHandleControlCommand(e.SessionId, senderName, message) &&
                    !_coordination.TryCreateQuery(e.SessionId, senderName, message))
                {
                    var parsed = _coordination.ParseChatCommand(senderName, message);
                    if (parsed.Recognized && parsed.Error.Length > 0)
                        _coordination.SendResponse(e.SessionId, senderName, CoordinationResponseKind.Refusal, parsed.Error);
                    else
                        _coordination.DispatchChat(senderName, message);
                }
            }
        }
        if (SelectedLiveSession?.SessionId.Equals(session.SessionId, StringComparison.OrdinalIgnoreCase) == true &&
            (!string.IsNullOrWhiteSpace(session.CurrentRoomName) || !string.IsNullOrWhiteSpace(session.CurrentRoomKey)))
        {
            var roomSignature = $"{session.CurrentRoomKey}|{session.CurrentRoomName}";
            if (!_lastRecordedMapRoomBySession.TryGetValue(session.SessionId, out var previous) || !string.Equals(previous, roomSignature, StringComparison.OrdinalIgnoreCase))
            {
                _lastRecordedMapRoomBySession[session.SessionId] = roomSignature;
                SelectedMapRoom = _mapProfiles.RecordVisit(session.CurrentRoomName, session.CurrentRoomKey);
                _ = _mapProfiles.SaveAsync();
                RefreshMapRooms();
                Raise(nameof(MapProfile));
            }
        }
        Raise(nameof(SelectedLiveSession));
    }

    private async Task TryHandleCombatAutomationOutcomeAsync(
        ClientSessionConnection session, string line, DateTimeOffset timestamp)
    {
        if (!CanContinueVerifiedCombat(session)) return;
        var text = line.Trim();
        if (_pendingCombatSpellFallback.TryGetValue(session.SessionId, out var pending))
        {
            if (timestamp > pending.ExpiresAt) _pendingCombatSpellFallback.Remove(session.SessionId);
            else if (text.Contains("failed to cast", StringComparison.OrdinalIgnoreCase) ||
                     text.Contains("do not know", StringComparison.OrdinalIgnoreCase) ||
                     text.Contains("not enough mana", StringComparison.OrdinalIgnoreCase) ||
                     text.Contains("cannot cast", StringComparison.OrdinalIgnoreCase))
            {
                _pendingCombatSpellFallback.Remove(session.SessionId);
                var fallback = new ClientPriorityAction { Command = pending.Command };
                if (CanUseLearnedCombatSpell(fallback, session))
                {
                    await _sessions.SendCommandAsAsync(session.SessionId,
                        ExpandCombatTarget(pending.Command, pending.Target), ClientCommandOrigin.ClientAutomation);
                    session.CombatAutomationStatus = $"Primary spell failed; auxiliary spell sent against {pending.Target}.";
                }
                else session.CombatAutomationStatus = "Primary spell failed; auxiliary spell was not learned or would break the mana reserve.";
            }
        }
        var backstabFailed = (text.Contains("backstab", StringComparison.OrdinalIgnoreCase) &&
                              (text.Contains("fail", StringComparison.OrdinalIgnoreCase) || text.Contains("miss", StringComparison.OrdinalIgnoreCase)));
        if (backstabFailed && session.ClassicCombatPreferences.RunIfBackstabFails)
        {
            await _sessions.SendCommandAsAsync(session.SessionId, "flee", ClientCommandOrigin.ClientAutomation);
            session.CombatAutomationStatus = "Backstab failed; configured flee response sent.";
        }
    }

    private async Task TryHandleTrustedPartyInviteAsync(ClientSessionConnection session, string line, DateTimeOffset timestamp)
    {
        if (!AutomationProfile.Capabilities.PartyAutomationEnabled || AutomationProfile.Capabilities.EmergencyStop ||
            !AutomationOptions.Party.AutoAcceptTrustedInvites) return;
        const string marker = " has invited you to follow ";
        var markerAt = line.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerAt <= 0) return;
        var inviter = line[..markerAt].Trim(' ', '[', ']');
        var explicitLeader = inviter.Equals(AutomationOptions.Party.LeaderName.Trim(), StringComparison.OrdinalIgnoreCase);
        var trustedInvite = CoordinationProfile.TrustedControllers.Any(controller => controller.Enabled &&
            controller.PlayerName.Equals(inviter, StringComparison.OrdinalIgnoreCase) && controller.Permissions.HasFlag(RemoteControlPermission.Invite));
        if (!explicitLeader && !trustedInvite)
        { session.PartyAutomationStatus = $"Invitation from {inviter} was not accepted because that player is not trusted for invitations."; return; }
        await SendPartyCommandAsync(session, AppendPartyTarget(AutomationOptions.Party.AcceptInviteCommand, inviter),
            $"Accepted an exact party invitation from trusted player {inviter}.", timestamp);
    }

    private async Task TryHandleMaintainedSpellOutcomeAsync(ClientSessionConnection session, string line, DateTimeOffset timestamp)
    {
        if (!AutomationProfile.Capabilities.SpellMaintenanceEnabled || AutomationProfile.Capabilities.EmergencyStop) return;
        var text = line.Trim();
        if (AutomationOptions.Spells.RecastAfterDispel &&
            (text.Contains("dispel", StringComparison.OrdinalIgnoreCase) || text.Contains("fades", StringComparison.OrdinalIgnoreCase) ||
             text.Contains("wears off", StringComparison.OrdinalIgnoreCase)))
        {
            session.ActiveEffectsUpdatedAt = null;
            _nextSpellMaintenanceAt.Remove(session.SessionId);
            await _sessions.SendClientProtocolCommandAsync(session.SessionId, "clientdata stats");
            session.SpellAutomationStatus = "A spell-loss message was observed; refreshing exact active effects before deciding whether to recast.";
            return;
        }
        if (!_pendingMaintainedCast.TryGetValue(session.SessionId, out var pending)) return;
        if (timestamp > pending.ExpiresAt) { _pendingMaintainedCast.Remove(session.SessionId); return; }
        var failed = text.Equals("The spell failed.", StringComparison.OrdinalIgnoreCase) ||
                     text.Contains("failed to cast", StringComparison.OrdinalIgnoreCase);
        if (!failed) return;
        _pendingMaintainedCast.Remove(session.SessionId);
        _lightedRoomBySession.Remove(session.SessionId);
        if (!AutomationOptions.Spells.RetryFailedCast)
        { session.SpellAutomationStatus = "Maintained cast failed; automatic retry is disabled."; return; }
        await Task.Delay(Math.Max(500, AutomationProfile.MinimumCommandDelayMs));
        if (!AutomationProfile.Capabilities.SpellMaintenanceEnabled || AutomationProfile.Capabilities.EmergencyStop ||
            session.ConnectionState != ClientConnectionState.Connected || session.InCombat || session.IsMoving) return;
        await _sessions.SendCommandAsAsync(session.SessionId, pending.Command, ClientCommandOrigin.ClientAutomation);
        _nextSpellMaintenanceAt[session.SessionId] = DateTimeOffset.Now.AddSeconds(Math.Max(5, AutomationOptions.Spells.MaintainedSpellCooldownSeconds));
        session.SpellAutomationStatus = "Maintained cast failed; the same verified command was retried once and will not loop.";
    }

    private async Task TryHandlePvpSafetyAsync(ClientSessionConnection session, string line, DateTimeOffset timestamp)
    {
        var options = AutomationOptions.PvP;
        if (!AutomationProfile.Capabilities.PvpAutomationEnabled || AutomationProfile.Capabilities.EmergencyStop || !options.Enabled ||
            session.ConnectionState != ClientConnectionState.Connected) return;
        if (_nextPvpActionAt.TryGetValue(session.SessionId, out var readyAt) && timestamp < readyAt) return;
        if (session.PvpStateUpdatedAt is not { } pvpAt || timestamp - pvpAt > TimeSpan.FromSeconds(10))
        {
            await _sessions.SendClientProtocolCommandAsync(session.SessionId, "clientdata pvp");
            _nextPvpActionAt[session.SessionId] = timestamp.AddSeconds(5);
            session.PvpAutomationStatus = "Refreshing SYSOP-controlled PvP state privately.";
            return;
        }
        if (session.PvpState is not { WorldPvpEnabled: true, SafeRoom: false })
        {
            session.PvpAutomationStatus = session.PvpState?.SafeRoom == true
                ? "PvP responses are disabled in this proven safe room."
                : "PvP responses are disabled because the SYSOP has disabled realm PvP.";
            return;
        }
        if (session.RoomUpdatedAt is not { } roomAt || timestamp - roomAt > TimeSpan.FromSeconds(options.AttackerStateMaximumAgeSeconds))
        { session.PvpAutomationStatus = "PvP response withheld because same-room player data is stale."; return; }

        var attacker = session.CurrentRoomPlayers.OrderByDescending(name => name.Length).FirstOrDefault(name =>
            line.StartsWith(name + " ", StringComparison.OrdinalIgnoreCase) &&
            (line.Contains(" attacks you", StringComparison.OrdinalIgnoreCase) || line.Contains(" hits you", StringComparison.OrdinalIgnoreCase) ||
             line.Contains(" strikes you", StringComparison.OrdinalIgnoreCase) || line.Contains(" misses you", StringComparison.OrdinalIgnoreCase)));
        var defendedPartyMember = attacker is null ? null : string.Empty;
        if (attacker is null && options.DefendPartyMembers)
        {
            attacker = session.CurrentRoomPlayers.OrderByDescending(name => name.Length).FirstOrDefault(name =>
                line.StartsWith(name + " ", StringComparison.OrdinalIgnoreCase));
            if (attacker is not null)
                defendedPartyMember = session.PartyMembers.OrderByDescending(member => member.Name.Length)
                    .FirstOrDefault(member => line.Contains($" attacks {member.Name}", StringComparison.OrdinalIgnoreCase) ||
                                              line.Contains($" hits {member.Name}", StringComparison.OrdinalIgnoreCase) ||
                                              line.Contains($" strikes {member.Name}", StringComparison.OrdinalIgnoreCase) ||
                                              line.Contains($" misses {member.Name}", StringComparison.OrdinalIgnoreCase))?.Name;
            if (string.IsNullOrWhiteSpace(defendedPartyMember)) attacker = null;
        }
        var fleeing = false;
        if (attacker is null && _pvpAttackers.TryGetValue(session.SessionId, out var previous) &&
            timestamp - previous.ObservedAt <= TimeSpan.FromSeconds(options.AttackerStateMaximumAgeSeconds) &&
            line.StartsWith(previous.Attacker + " ", StringComparison.OrdinalIgnoreCase) && line.Contains("flees from you", StringComparison.OrdinalIgnoreCase))
        { attacker = previous.Attacker; fleeing = true; }
        if (attacker is null) return;

        var trusted = CoordinationProfile.TrustedControllers.Any(controller => controller.Enabled &&
            controller.PlayerName.Equals(attacker, StringComparison.OrdinalIgnoreCase));
        if (options.IgnoreTrustedPlayers && trusted)
        { session.PvpAutomationStatus = $"Ignored PvP event from explicitly trusted player {attacker}."; return; }
        _pvpAttackers[session.SessionId] = (attacker, timestamp);
        if (options.AlertWhenAttacked)
            session.PvpAutomationStatus = $"PvP alert: authoritative same-room player {attacker} attacked this character.";
        if (!_pvpActionInFlight.Add(session.SessionId)) return;
        try
        {
            string? command = null;
            string reason;
            if (fleeing)
            {
                if (!options.ChaseFleeingAttacker) return;
                // Reforged does not presently advertise a live CHASE command. Preserve
                // the option without emitting a command the connected server lacks.
                session.PvpAutomationStatus = $"{attacker} fled; chase is withheld until the server exposes an authoritative chase route.";
                return;
            }
            if (options.AutoFlee && session.HealthPercent <= options.FleeAtPercent)
            { command = options.ReactionCommand; reason = $"Fleeing from player attacker {attacker} at {session.HealthPercent}% health."; }
            else if (!string.IsNullOrWhiteSpace(defendedPartyMember))
            { command = AppendPvpTarget(options.ReturnAttackCommand, attacker); reason = $"Defending party member {defendedPartyMember} from verified aggressor {attacker}."; }
            else if (options.UseDefensiveAction)
            { session.PvpAutomationStatus = "Legacy defensive-command response is unavailable; MegaMUD Defend is party protection."; return; }
            else if (options.AutoReturnAttack && session.HealthPercent >= options.ReturnAttackAtOrAbovePercent)
            { command = AppendPvpTarget(options.ReturnAttackCommand, attacker); reason = $"Returning the verified same-room attack from {attacker}."; }
            else return;
            if (string.IsNullOrWhiteSpace(command))
            { session.PvpAutomationStatus = "PvP action withheld because its configured command is blank."; return; }
            if (!CanContinuePvpAutomation(session)) return;
            await _sessions.SendCommandAsAsync(session.SessionId, command.Trim(), ClientCommandOrigin.ClientAutomation);
            _nextPvpActionAt[session.SessionId] = timestamp.AddSeconds(options.CommandCooldownSeconds);
            session.PvpAutomationStatus = reason;
        }
        catch (Exception exception) { session.PvpAutomationStatus = $"PvP response stopped safely: {exception.Message}"; }
        finally { _pvpActionInFlight.Remove(session.SessionId); }
    }

    private static string AppendPvpTarget(string command, string attacker)
    {
        var normalized = command.Trim();
        if (normalized.Contains("{target}", StringComparison.OrdinalIgnoreCase))
            return normalized.Replace("{target}", attacker, StringComparison.OrdinalIgnoreCase);
        return $"{normalized} {attacker}".Trim();
    }

    private bool CanContinuePvpAutomation(ClientSessionConnection session) =>
        AutomationProfile.Capabilities.PvpAutomationEnabled && !AutomationProfile.Capabilities.EmergencyStop &&
        AutomationOptions.PvP.Enabled && session.ConnectionState == ClientConnectionState.Connected;

    private async Task TryHandleRoamingObstacleAsync(
        ClientSessionConnection session, string line, DateTimeOffset timestamp)
    {
        if (!AutomationProfile.Capabilities.RoamingAutomationEnabled || !AutomationProfile.Capabilities.NavigationEnabled ||
            AutomationProfile.Capabilities.EmergencyStop || session.LastCommandOrigin != ClientCommandOrigin.RouteAutomation ||
            session.RouteState != RouteExecutionState.WaitingForRoom) return;
        if (_nextObstacleRetryAt.TryGetValue(session.SessionId, out var next) && timestamp < next) return;
        var direction = NormalizeRouteDirection(session.LastCommandText);
        if (direction.Length == 0) return;
        var text = line.Trim();
        var options = AutomationOptions.Roaming;
        string command;
        if ((text.Contains("locked", StringComparison.OrdinalIgnoreCase) || text.Contains("lock blocks", StringComparison.OrdinalIgnoreCase)) && options.PickLockedDoors)
            command = ExpandDirection(options.PickLockCommand, direction);
        else if ((text.Contains("locked", StringComparison.OrdinalIgnoreCase) || text.Contains("blocked", StringComparison.OrdinalIgnoreCase)) && options.BashBlockedDoors)
            command = ExpandDirection(options.BashDoorCommand, direction);
        else if ((text.Contains("closed", StringComparison.OrdinalIgnoreCase) || text.Contains("shut", StringComparison.OrdinalIgnoreCase)) && options.OpenClosedDoors)
            command = ExpandDirection(options.OpenDoorCommand, direction);
        else return;
        _nextObstacleRetryAt[session.SessionId] = timestamp.AddSeconds(4);
        try
        {
            await _sessions.SendCommandAsAsync(session.SessionId, command, ClientCommandOrigin.ClientAutomation);
            await Task.Delay(Math.Max(500, AutomationProfile.MinimumCommandDelayMs));
            if (!CanContinueRoamingAutomation(session) || !AutomationProfile.Capabilities.NavigationEnabled) return;
            await _sessions.SendCommandAsAsync(session.SessionId, direction, ClientCommandOrigin.RouteAutomation);
            session.RoamingAutomationStatus = $"Handled route obstacle with '{command}' and retried {direction}.";
        }
        catch (Exception exception) { session.RoamingAutomationStatus = $"Obstacle handling stopped safely: {exception.Message}"; }
    }

    private static string ExpandDirection(string command, string direction)
    {
        var trimmed = command.Trim();
        return trimmed.Contains("{direction}", StringComparison.OrdinalIgnoreCase)
            ? trimmed.Replace("{direction}", direction, StringComparison.OrdinalIgnoreCase)
            : $"{trimmed} {direction}";
    }

    private static string NormalizeRouteDirection(string command) => command.Trim().ToLowerInvariant() switch
    {
        "n" or "north" => "north", "ne" or "northeast" => "northeast", "e" or "east" => "east",
        "se" or "southeast" => "southeast", "s" or "south" => "south", "sw" or "southwest" => "southwest",
        "w" or "west" => "west", "nw" or "northwest" => "northwest", "u" or "up" => "up", "d" or "down" => "down",
        _ => string.Empty
    };

    private void TrySendAwayReply(ClientSessionConnection session, string line, DateTimeOffset timestamp)
    {
        var capabilities = AutomationProfile.Capabilities;
        if (!capabilities.CommunicationsAutomationEnabled || !capabilities.AwayFromKeyboardEnabled || capabilities.EmergencyStop
            || session.ConnectionState != ClientConnectionState.Connected) return;
        var match = System.Text.RegularExpressions.Regex.Match(
            line.Trim(), @"^(?<sender>[A-Za-z0-9_'\-]{1,40}) tells you,\s*'(?<message>.*)'$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        if (!match.Success || match.Groups["message"].Value.TrimStart().StartsWith("[AFK]", StringComparison.OrdinalIgnoreCase)) return;
        var senderName = match.Groups["sender"].Value;
        if (senderName.Equals(session.CharacterName, StringComparison.OrdinalIgnoreCase)) return;
        var key = $"{session.SessionId}\u001f{senderName}";
        var cooldown = Math.Clamp(capabilities.AwayFromKeyboardReplyCooldownSeconds, 30, 86400);
        if (_lastAwayReplyBySessionAndSender.TryGetValue(key, out var last) && (timestamp - last).TotalSeconds < cooldown) return;
        var message = string.Join(" ", capabilities.AwayFromKeyboardMessage
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        if (message.Length == 0) return;
        _lastAwayReplyBySessionAndSender[key] = timestamp;
        _ = SendAwayReplyAsync(session, senderName, message);
    }

    private async Task SendAwayReplyAsync(ClientSessionConnection session, string senderName, string message)
    {
        try
        {
            if (!CanContinueCommunications() || session.ConnectionState != ClientConnectionState.Connected) return;
            await _sessions.SendCommandAsAsync(session.SessionId, $"tell {senderName} [AFK] {message}", ClientCommandOrigin.ClientAutomation);
            Status = $"AFK reply sent to {senderName}.";
        }
        catch (Exception exception) { Status = $"AFK reply failed: {exception.Message}"; }
    }

    private bool CanContinueCommunications() =>
        AutomationProfile.Capabilities.CommunicationsAutomationEnabled && !AutomationProfile.Capabilities.EmergencyStop;

    private void RefreshPaths()
    {
        VisiblePaths.Clear();
        var query = PathSearchText.Trim();
        foreach (var route in _paths.AvailablePaths.Where(p => query.Length == 0 ||
                     p.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                     p.FileName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                     p.Author.Contains(query, StringComparison.OrdinalIgnoreCase)).Take(2500))
        {
            VisiblePaths.Add(route);
        }
        SelectedPath = VisiblePaths.FirstOrDefault();
    }

    private void RefreshRules()
    {
        VisibleRules.Clear();
        if (SelectedAutomationArea is null) return;
        foreach (var rule in AutomationProfile.Rules.Where(r => r.Area == SelectedAutomationArea).OrderBy(r => r.Priority)) VisibleRules.Add(rule);
        SelectedRule = VisibleRules.FirstOrDefault();
    }

    private void RefreshRecords()
    {
        VisibleRecords.Clear();
        if (SelectedCategory is null) return;
        var query = SearchText.Trim();
        foreach (var record in SelectedCategory.Records.Where(r =>
                     query.Length == 0 || r.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                     r.Local.Aliases.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                     r.Local.Notes.Contains(query, StringComparison.OrdinalIgnoreCase)).Take(2500))
        {
            VisibleRecords.Add(record);
        }
        SelectedRecord = VisibleRecords.FirstOrDefault();
    }
}
