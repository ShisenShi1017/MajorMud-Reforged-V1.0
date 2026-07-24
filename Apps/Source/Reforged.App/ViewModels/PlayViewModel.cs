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
    private readonly Dictionary<string, string> _lastRecordedMapRoomBySession = new(StringComparer.OrdinalIgnoreCase);
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

    public ObservableCollection<ClientDataCategory> Categories { get; } = [];
    public ObservableCollection<ClientDataRecord> VisibleRecords { get; } = [];
    public ObservableCollection<ClientAutomationRule> VisibleRules { get; } = [];
    public ObservableCollection<MegaMudPathRecord> VisiblePaths { get; } = [];
    public ObservableCollection<ImportedPathEntry> LocalPaths => _paths.ImportedProfile.Paths;
    public ObservableCollection<ClientSessionConnection> LiveSessions => _sessions.Sessions;
    public ObservableCollection<ClientSavedBuildSnapshot> SavedBuilds { get; } = [];
    public ObservableCollection<ClientRoomMetadata> VisibleMapRooms { get; } = [];
    public ClientMapProfile MapProfile => _mapProfiles.Profile;
    public IReadOnlyList<ClientMapPolicy> MapPolicies { get; } = Enum.GetValues<ClientMapPolicy>();
    public IReadOnlyList<ClientMapStyle> MapStyles { get; } = Enum.GetValues<ClientMapStyle>();
    public ClientWorkspaceProfile WorkspaceProfile => _workspaceProfiles.Profile;
    public IReadOnlyList<ClientWorkspaceTheme> WorkspaceThemes { get; } = Enum.GetValues<ClientWorkspaceTheme>();
    public IReadOnlyList<ClientLayoutPreset> WorkspaceLayoutPresets { get; } = Enum.GetValues<ClientLayoutPreset>();
    public IReadOnlyList<ClientTerminalViewMode> TerminalViewModes { get; } = Enum.GetValues<ClientTerminalViewMode>();
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
    public ClientSessionConnection? SelectedLiveSession { get => _selectedLiveSession; set { if (Set(ref _selectedLiveSession, value) && value is not null) _ = SelectSessionProfilesAsync(value); } }
    public string TerminalCommand { get => _terminalCommand; set => Set(ref _terminalCommand, value); }
    public ClientSysGotoProfile SysGotoProfile => _sysGoto.Profile;
    public ClientSysGotoDestination? SelectedSysGotoDestination { get => _selectedSysGotoDestination; set => Set(ref _selectedSysGotoDestination, value); }
    public string NewSysGotoCityName { get => _newSysGotoCityName; set => Set(ref _newSysGotoCityName, value); }
    public string NewSysGotoCommandArgument { get => _newSysGotoCommandArgument; set => Set(ref _newSysGotoCommandArgument, value); }

    public string PathSourceDescription => _paths.SourceDescription;
    public string Status { get => _status; private set => Set(ref _status, value); }
    public string ScopeLabel => "Server: Local Host   •   World: Classic   •   Changes: Client only";
    public string AutomationPolicyLabel => _automation.Policy.AutomationAllowed
        ? "Client automation is available. Every generated action is sent as a normal Host command."
        : "This Host profile currently disallows Client automation.";

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
        _sessions.StateChanged += (_, e) => Status = e.Message;
        _sessions.RouteChanged += (_, e) => Status = e.Message;
        _coordination.CommandRequested += CoordinationCommandRequested;
        _coordination.ResponseRequested += CoordinationResponseRequested;
        _coordination.QueryRequested += CoordinationQueryRequested;
        _automation.CommandRequested += AutomationCommandRequested;
    }

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_data.LoadAsync(), _automation.LoadAsync(), _coordination.LoadAsync(), _paths.LoadAsync(), _parsing.LoadAsync(), _workspaceProfiles.LoadAsync(), LoadSavedBuildsAsync());
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
        Status = $"{_data.SourceDescription} • {Categories.Sum(c => c.Records.Count):N0} Host records • {_automation.Profile.Rules.Count} automation rules • {_paths.AvailablePaths.Count:N0} MegaMUD paths";
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
        await _mapProfiles.SelectProfileAsync(session.ServerProfileName, session.WorldName);
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
        Status = enabled ? "Character automation enabled." : "Character automation paused.";
    }

    public async Task StopAllAutomationAsync()
    {
        _automation.StopAll();
        Raise(nameof(AutomationProfile));
        await _automation.SaveAsync();
        Status = "STOP ALL applied. Automation is disabled and all future rule evaluation is paused.";
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
        Status = "Saved MegaMUD-compatible Client options. Host and world data were not changed.";
    }

    public async Task SaveCoordinationAsync()
    {
        await _coordination.SaveAsync();
        Status = $"Saved coordination profile '{CoordinationProfile.Name}'. Commands remain Client-side requests sent through each session.";
    }

    public async Task SaveSelectedTerminalViewModeAsync()
    {
        if (SelectedLiveSession is null) return;
        var coordinated = CoordinationProfile.Sessions.FirstOrDefault(s => s.SessionId == SelectedLiveSession.SessionId);
        if (coordinated is not null) coordinated.TerminalViewMode = SelectedLiveSession.TerminalViewMode;
        await _coordination.SaveAsync();
        Status = SelectedLiveSession.TerminalViewMode == ClientTerminalViewMode.ClassicMegaMud
            ? "Classic MegaMUD view selected for this session."
            : "Reforged view selected for this session.";
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
        var sent = _coordination.DispatchLocal(CoordinationTarget, CoordinationCommand);
        Status = sent.Count == 0 ? "No eligible follower session accepted the command." : $"Queued '{CoordinationCommand}' for {sent.Count} session(s).";
        Raise(nameof(CoordinationProfile));
        Raise(nameof(WorkspaceProfile));
        Raise(nameof(ParsingProfile));
        SelectedParsingRule = ParsingProfile.Rules.FirstOrDefault();
    }

    public void TestChatCommand()
    {
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
        Status = $"Imported {SelectedPath.FileName} into the Local Host / Classic Client path profile. Source and Librarium data were not changed.";
    }

    public async Task RemoveSelectedPathAsync()
    {
        if (SelectedPath is null) return;
        await _paths.RemoveImportAsync(SelectedPath);
        Raise(nameof(SelectedPath));
        Status = $"Removed the local Client import for {SelectedPath.FileName}. The original .mp file was not changed.";
    }


    public async Task ConnectSelectedSessionAsync()
    {
        if (SelectedLiveSession is null) return;
        try
        {
            await SelectSessionProfilesAsync(SelectedLiveSession);
            await _sessions.ConnectAsync(SelectedLiveSession.SessionId);
            Status = SelectedLiveSession.ConnectionMessage;
        }
        catch (Exception exception) { Status = $"Connection failed: {exception.Message}"; }
        Raise(nameof(SelectedLiveSession));
    }

    public async Task DisconnectSelectedSessionAsync()
    {
        if (SelectedLiveSession is null) return;
        await _sessions.DisconnectAsync(SelectedLiveSession.SessionId);
        Status = "Session disconnected.";
        Raise(nameof(SelectedLiveSession));
    }

    public async Task SendTerminalCommandAsync()
    {
        if (SelectedLiveSession is null) return;

        var command = TerminalCommand?.Trim() ?? string.Empty;
        TerminalCommand = string.Empty;

        try
        {
            await _sessions.SendCommandAsync(SelectedLiveSession.SessionId, command);
            Status = command.Length == 0 ? "Requested fresh Host prompt." : $"Sent '{command}'.";
        }
        catch (Exception exception) { Status = exception.Message; }

        Raise(nameof(SelectedLiveSession));
    }

    public async Task SendSensitiveTerminalCommandAsync(string secret)
    {
        if (SelectedLiveSession is null || string.IsNullOrEmpty(secret)) return;
        try
        {
            await _sessions.SendSensitiveCommandAsync(SelectedLiveSession.SessionId, secret);
            Status = "Sent hidden password entry.";
        }
        catch (Exception exception) { Status = exception.Message; }
        Raise(nameof(SelectedLiveSession));
    }

    public async Task SaveClassicPartyPreferencesAsync()
    {
        if (SelectedLiveSession is null) return;
        await SaveSelectedTerminalViewModeAsync();
        Status = "Classic party automation settings saved for this session profile.";
        Raise(nameof(SelectedLiveSession));
    }

    public async Task SaveClassicCombatPreferencesAsync()
    {
        if (SelectedLiveSession is null) return;
        AutomationProfile.Enabled = SelectedLiveSession.ClassicCombatPreferences.AutoCombat;
        await SaveSelectedTerminalViewModeAsync();
        Status = "Classic combat automation settings saved for this session profile.";
        Raise(nameof(AutomationProfile));
        Raise(nameof(SelectedLiveSession));
    }

    public async Task SaveClassicHealthPreferencesAsync()
    {
        if (SelectedLiveSession is null) return;
        await SaveSelectedTerminalViewModeAsync();
        Status = "Classic health, rest, and healing automation settings saved for this session profile.";
        Raise(nameof(SelectedLiveSession));
    }

    private void ApplyClassicPartyDirective(ClientSessionConnection session, string line)
    {
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

    public void ClearSelectedTerminal()
    {
        if (SelectedLiveSession is null) return;
        _sessions.ClearTerminal(SelectedLiveSession.SessionId);
        Status = $"Cleared {SelectedLiveSession.DisplayName} terminal scrollback.";
    }

    public async Task ExportSelectedTerminalLogAsync()
    {
        if (SelectedLiveSession is null) return;
        try
        {
            var path = await _sessions.ExportTerminalLogAsync(SelectedLiveSession.SessionId);
            Status = $"Terminal log exported to {path}";
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
        }
        await _mapProfiles.SaveAsync();
        RefreshMapRooms();
        Raise(nameof(MapProfile));
        Status = $"Saved Client-only map settings for {MapProfile.ServerName} / {MapProfile.WorldName}.";
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
        if (SelectedLiveSession is null || SelectedPath is null) return;
        try { await _sessions.StartRouteAsync(SelectedLiveSession.SessionId, SelectedPath); Status = $"Started {SelectedPath.DisplayName}."; }
        catch (Exception exception) { Status = exception.Message; }
        Raise(nameof(SelectedLiveSession));
    }

    public void PauseSelectedRoute()
    {
        if (SelectedLiveSession is null) return;
        _sessions.PauseRoute(SelectedLiveSession.SessionId);
        Raise(nameof(SelectedLiveSession));
    }

    public void ResumeSelectedRoute()
    {
        if (SelectedLiveSession is null) return;
        _sessions.ResumeRoute(SelectedLiveSession.SessionId);
        Raise(nameof(SelectedLiveSession));
    }

    public void StopSelectedRoute()
    {
        if (SelectedLiveSession is null) return;
        _sessions.StopRoute(SelectedLiveSession.SessionId);
        Raise(nameof(SelectedLiveSession));
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

    public void StartRouteRecording()
    {
        if (SelectedLiveSession is null) { Status = "Select a live session before recording."; return; }
        _paths.BeginRecording(SelectedLiveSession.SessionId, RecordingRouteName, SelectedLiveSession.CurrentRoomName);
        Raise(nameof(RouteRecording));
        Status = $"Recording route '{RecordingRouteName}' from {SelectedLiveSession.DisplayName}.";
    }

    public async Task FinishRouteRecordingAsync()
    {
        var endRoom = SelectedLiveSession?.CurrentRoomName ?? string.Empty;
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
        _paths.RecordCommand(e.SessionId, e.Command);
        Raise(nameof(RouteRecording));
    }

    private static bool IsRouteMovement(string command) => new[] { "n", "north", "ne", "northeast", "e", "east", "se", "southeast", "s", "south", "sw", "southwest", "w", "west", "nw", "northwest", "u", "up", "d", "down" }.Contains(command.Trim(), StringComparer.OrdinalIgnoreCase);

    private async void CoordinationCommandRequested(object? sender, CoordinatedCommandRequest request)
    {
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
        if (SelectedLiveSession is null) return;
        try { await _sessions.SendCommandAsync(SelectedLiveSession.SessionId, request.Command); Status = $"{request.RuleName}: {request.Command}"; }
        catch (Exception exception) { Status = $"Automation command failed: {exception.Message}"; }
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
        var context = new ClientAutomationContext(session.HealthPercent, session.ManaPercent, session.InCombat, session.IsResting, session.IsMoving, e.Line, e.Timestamp);
        _automation.Evaluate(context);
        ApplyClassicPartyDirective(session, e.Line);

        if (CoordinationProfile.AcceptChatCommands && e.Line.Contains('@'))
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
        if (!string.IsNullOrWhiteSpace(session.CurrentRoomName) || !string.IsNullOrWhiteSpace(session.CurrentRoomKey))
        {
            var roomSignature = $"{session.CurrentRoomKey}|{session.CurrentRoomName}";
            if (!_lastRecordedMapRoomBySession.TryGetValue(session.SessionId, out var previous) || !string.Equals(previous, roomSignature, StringComparison.OrdinalIgnoreCase))
            {
                _lastRecordedMapRoomBySession[session.SessionId] = roomSignature;
                SelectedMapRoom = _mapProfiles.RecordVisit(session.CurrentRoomName, session.CurrentRoomKey);
                RefreshMapRooms();
                Raise(nameof(MapProfile));
            }
        }
        Raise(nameof(SelectedLiveSession));
    }

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
