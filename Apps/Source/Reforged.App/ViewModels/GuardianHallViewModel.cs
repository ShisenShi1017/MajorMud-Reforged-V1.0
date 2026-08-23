using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using Reforged.App.Commands;
using Reforged.App.Engineering;
using Reforged.App.Forge;
using Reforged.App.Guardians;

namespace Reforged.App.ViewModels;

public sealed class GuardianHallViewModel : ViewModelBase
{
    private readonly IGuardianAssignmentStore _store;
    private readonly IGuardianBrainRouter _router;
    private readonly ArchivistService _archivistService;
    private readonly IRealmKeeperTelemetryService _realmKeeperTelemetry;
    private readonly IWandererTelemetryService _wandererTelemetry;
    private readonly IAnalystTelemetryService _analystTelemetry;
    private readonly IWorldsmithRuntimeService _worldsmithRuntime;
    private readonly DispatcherTimer _telemetryTimer;
    private readonly DispatcherTimer _orchestrationTimer;
    private readonly DispatcherTimer _wandererAutonomyTimer;
    private bool _wandererStepRunning;
    private readonly GuardianOrchestrator _orchestrator = new();
    private readonly GuardianHallHardeningService _hardeningService;
    private readonly EngineeringPersistence _engineeringPersistence;
    private readonly EngineeringOrchestrator _engineeringOrchestrator;
    private CancellationTokenSource _orchestrationCancellation = new();
    private GuardianProfile? _selectedGuardian;
    private DecodeQueueItem? _selectedDecodeItem;
    private EvidenceSearchResult? _selectedEvidenceResult;
    private LocalToolRegistration? _selectedTool;
    private RealmHostEndpoint? _selectedRemoteHost;
    private string _hallStatus = "Guardian Hall ready";

    public ObservableCollection<GuardianProfile> Guardians { get; } = [];
    public RealmKeeperWorkspace RealmKeeper { get; private set; } = new();
    public WandererWorkspace Wanderer { get; private set; } = new();
    public AnalystWorkspace Analyst { get; private set; } = new();
    public WorldsmithWorkspace Worldsmith { get; private set; } = new();
    public ArchivistWorkspace Archivist { get; private set; } = new();
    public GuardianOrchestrationWorkspace Orchestration { get; private set; } = new();
    public GuardianHallDiagnosticsWorkspace Diagnostics { get; private set; } = new();
    public EngineeringWorkspace Engineering { get; private set; } = new();
    public IReadOnlyList<string> ServerProfiles { get; } = ["Local Reforged Host", "LAN Host", "Internet Host"];
    public IReadOnlyList<string> Worlds { get; } = ["Classic", "Custom", "AI World", "Imported World"];
    public IReadOnlyList<string> Realms { get; } = ["Live Realm", "Test Realm", "Development Realm", "Offline Projection"];
    public IReadOnlyList<string> ExplorationStyles { get; } = ["Methodical", "Natural Player", "Completionist", "Risk-Aware Scout"];
    public IReadOnlyList<string> AnalysisTargets { get; } = ["Combat", "Economy", "Drops", "NPC Balance", "Quest Balance", "Experience Curve", "Item Balance", "Progression"];
    public IReadOnlyList<string> ContentTypes { get; } = ["Rooms", "NPCs", "Items", "Quests", "Encounters", "Dungeons", "World Events"];
    public Array Permissions => Enum.GetValues<GuardianPermission>();
    public Array Schedules => Enum.GetValues<GuardianSchedule>();

    public GuardianProfile? SelectedGuardian { get => _selectedGuardian; set { if (Set(ref _selectedGuardian, value)) RaiseSelection(); } }
    public DecodeQueueItem? SelectedDecodeItem { get => _selectedDecodeItem; set => Set(ref _selectedDecodeItem, value); }
    public EvidenceSearchResult? SelectedEvidenceResult
    {
        get => _selectedEvidenceResult;
        set
        {
            if (!Set(ref _selectedEvidenceResult, value) || value is null) return;
            Archivist.RelationshipViewer = _archivistService.BuildRelationshipDetail(Archivist, value);
            Raise(nameof(Archivist));
        }
    }
    public LocalToolRegistration? SelectedTool { get => _selectedTool; set => Set(ref _selectedTool, value); }
    public RealmHostEndpoint? SelectedRemoteHost { get => _selectedRemoteHost; set => Set(ref _selectedRemoteHost, value); }
    public string HallStatus { get => _hallStatus; private set => Set(ref _hallStatus, value); }
    public string SelectedIdentity => SelectedGuardian is null ? "Select a Guardian" : $"{SelectedGuardian.Name} — {SelectedGuardian.Title}";
    public string SelectedDescription => SelectedGuardian?.Mission ?? "";
    public string SelectedPersonality => SelectedGuardian?.Personality ?? "";
    public string SelectedAppearance => SelectedGuardian?.Appearance ?? "";

    public bool IsRealmKeeper => SelectedGuardian?.Role == GuardianRole.RealmKeeper;
    public bool IsWanderer => SelectedGuardian?.Role == GuardianRole.Wanderer;
    public bool IsAnalyst => SelectedGuardian?.Role == GuardianRole.Analyst;
    public bool IsWorldsmith => SelectedGuardian?.Role == GuardianRole.Worldsmith;
    public bool IsArchivist => SelectedGuardian?.Role == GuardianRole.Archivist;

    public ICommand SaveCommand { get; }
    public ICommand StartGuardianCommand { get; }
    public ICommand PauseGuardianCommand { get; }
    public ICommand StopGuardianCommand { get; }
    public ICommand StopAllCommand { get; }
    public ICommand QueueFilesCommand { get; }
    public ICommand QueueFolderCommand { get; }
    public ICommand GenerateMarkdownCommand { get; }
    public ICommand GenerateJsonCommand { get; }
    public ICommand GenerateHtmlCommand { get; }
    public ICommand GeneratePdfCommand { get; }
    public ICommand ExportCodexCommand { get; }
    public ICommand DecodeSelectedCommand { get; }
    public ICommand DecodeAllCommand { get; }
    public ICommand SaveEvidenceCommand { get; }
    public ICommand SearchEvidenceCommand { get; }
    public ICommand ClearEvidenceSearchCommand { get; }
    public ICommand AddToolCommand { get; }
    public ICommand RemoveToolCommand { get; }
    public ICommand BrowseToolCommand { get; }
    public ICommand ValidateToolCommand { get; }
    public ICommand LaunchToolCommand { get; }
    public ICommand GenerateProposalCommand { get; }
    public ICommand SimulateProposalCommand { get; }
    public ICommand ApproveProposalCommand { get; }
    public ICommand ApplyApprovedProposalCommand { get; }
    public ICommand ExportAnalystCommand { get; }
    public ICommand RefreshRealmKeeperCommand { get; }
    public ICommand AddRemoteHostCommand { get; }
    public ICommand RemoveRemoteHostCommand { get; }
    public ICommand RefreshWandererCommand { get; }
    public ICommand DispatchWandererMissionCommand { get; }
    public ICommand StartWandererAutonomyCommand { get; }
    public ICommand PauseWandererAutonomyCommand { get; }
    public ICommand RunWandererAutonomyStepCommand { get; }
    public ICommand RefreshAnalystCommand { get; }
    public ICommand RefreshWorldsmithCommand { get; }
    public ICommand QueueGuardianJobCommand { get; }
    public ICommand RunGuardianJobsCommand { get; }
    public ICommand PauseSchedulerCommand { get; }
    public ICommand ResumeSchedulerCommand { get; }
    public ICommand CancelRunningJobsCommand { get; }
    public ICommand RunHardeningChecksCommand { get; }
    public ICommand StartEngineeringCommand { get; }
    public ICommand StopEngineeringCommand { get; }
    public ICommand EmergencyStopEngineeringCommand { get; }
    public ICommand QueueEngineeringPhasesCommand { get; }
    public ICommand RunEngineeringStepCommand { get; }
    public ICommand SaveEngineeringCommand { get; }
    public ICommand QueueRuntimeDisconnectObjectiveCommand { get; }

    public GuardianHallViewModel(
        IGuardianAssignmentStore store,
        IGuardianBrainRouter router,
        Reforged.App.Services.IClientSessionService? clientSessions = null,
        IForgeStateStore? forgeStore = null,
        IForgeRepositoryService? forgeRepository = null,
        IForgeBrain? forgeBrain = null,
        IForgeDeveloperTools? forgeTools = null)
    {
        _store = store;
        _router = router;
        _archivistService = new ArchivistService(store.RootDirectory);
        _hardeningService = new GuardianHallHardeningService(store);
        var repositoryRoot = forgeRepository?.DiscoverRepositoryRoot()
            ?? Directory.GetParent(store.RootDirectory)?.FullName
            ?? store.RootDirectory;
        _engineeringPersistence = new EngineeringPersistence(store.RootDirectory);
        var repairEngine = forgeStore is not null && forgeRepository is not null && forgeBrain is not null && forgeTools is not null
            ? new BorinAutonomousRepairEngine(forgeStore, forgeRepository, forgeBrain, forgeTools)
            : null;
        _engineeringOrchestrator = new EngineeringOrchestrator(repositoryRoot, _engineeringPersistence, repairEngine);
        _realmKeeperTelemetry = new RealmKeeperTelemetryService(repositoryRoot);
        _wandererTelemetry = new WandererTelemetryService(repositoryRoot, clientSessions);
        _analystTelemetry = new AnalystTelemetryService(repositoryRoot);
        _worldsmithRuntime = new WorldsmithRuntimeService(repositoryRoot, store.RootDirectory);
        _telemetryTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _telemetryTimer.Tick += async (_, _) => await RefreshRealmKeeperAsync(false);
        _orchestrationTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
        _orchestrationTimer.Tick += async (_, _) => await RunGuardianJobsAsync(false);
        _wandererAutonomyTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _wandererAutonomyTimer.Tick += async (_, _) => await RunWandererAutonomyStepAsync(false);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        StartGuardianCommand = new RelayCommand(() => SetStatus(GuardianStatus.Working, "Workspace started"), () => SelectedGuardian is not null);
        PauseGuardianCommand = new RelayCommand(() => SetStatus(GuardianStatus.Paused, "Guardian paused"), () => SelectedGuardian is not null);
        StopGuardianCommand = new RelayCommand(() => SetStatus(GuardianStatus.Idle, "Guardian stopped"), () => SelectedGuardian is not null);
        StopAllCommand = new RelayCommand(StopAll);
        QueueFilesCommand = new RelayCommand(ChooseFiles);
        QueueFolderCommand = new RelayCommand(ChooseFolder);
        GenerateMarkdownCommand = new AsyncRelayCommand(() => GenerateReportAsync("Markdown"));
        GenerateJsonCommand = new AsyncRelayCommand(() => GenerateReportAsync("JSON"));
        GenerateHtmlCommand = new AsyncRelayCommand(() => GenerateReportAsync("HTML"));
        GeneratePdfCommand = new AsyncRelayCommand(() => GenerateReportAsync("PDF"));
        ExportCodexCommand = new AsyncRelayCommand(ExportCodexAsync);
        DecodeSelectedCommand = new AsyncRelayCommand(DecodeSelectedAsync);
        DecodeAllCommand = new AsyncRelayCommand(DecodeAllAsync);
        SaveEvidenceCommand = new AsyncRelayCommand(SaveEvidenceAsync);
        SearchEvidenceCommand = new RelayCommand(SearchEvidence);
        ClearEvidenceSearchCommand = new RelayCommand(ClearEvidenceSearch);
        AddToolCommand = new RelayCommand(() => { var t = new LocalToolRegistration(); Archivist.LocalTools.Add(t); SelectedTool = t; HallStatus = "Editable local tool registration added"; });
        RemoveToolCommand = new RelayCommand(() => { if (SelectedTool is not null) Archivist.LocalTools.Remove(SelectedTool); });
        BrowseToolCommand = new RelayCommand(BrowseTool);
        ValidateToolCommand = new RelayCommand(ValidateTool);
        LaunchToolCommand = new RelayCommand(LaunchTool);
        GenerateProposalCommand = new RelayCommand(GenerateProposal);
        SimulateProposalCommand = new RelayCommand(SimulateProposal);
        ApproveProposalCommand = new RelayCommand(ApproveProposal);
        ApplyApprovedProposalCommand = new RelayCommand(ApplyApprovedProposal);
        ExportAnalystCommand = new AsyncRelayCommand(ExportAnalystAsync);
        RefreshRealmKeeperCommand = new AsyncRelayCommand(() => RefreshRealmKeeperAsync(true));
        AddRemoteHostCommand = new RelayCommand(() => { var host = new RealmHostEndpoint(); RealmKeeper.RemoteHosts.Add(host); SelectedRemoteHost = host; HallStatus = "Remote telemetry host added; configure URL and token environment variable"; });
        RemoveRemoteHostCommand = new RelayCommand(() => { if (SelectedRemoteHost is not null) { RealmKeeper.RemoteHosts.Remove(SelectedRemoteHost); SelectedRemoteHost = null; HallStatus = "Remote telemetry host removed"; } });
        RefreshWandererCommand = new AsyncRelayCommand(() => RefreshWandererAsync(true));
        DispatchWandererMissionCommand = new AsyncRelayCommand(DispatchWandererMissionAsync);
        StartWandererAutonomyCommand = new AsyncRelayCommand(StartWandererAutonomyAsync);
        PauseWandererAutonomyCommand = new RelayCommand(PauseWandererAutonomy);
        RunWandererAutonomyStepCommand = new AsyncRelayCommand(RunSingleWandererAutonomyStepAsync);
        RefreshAnalystCommand = new AsyncRelayCommand(() => RefreshAnalystAsync(true));
        RefreshWorldsmithCommand = new AsyncRelayCommand(() => RefreshWorldsmithAsync(true));
        QueueGuardianJobCommand = new RelayCommand(QueueSelectedGuardianJob, () => SelectedGuardian is not null);
        RunGuardianJobsCommand = new AsyncRelayCommand(() => RunGuardianJobsAsync(true));
        PauseSchedulerCommand = new RelayCommand(() => { Orchestration.Paused = true; Orchestration.RuntimeStatus = "Scheduler paused"; HallStatus = Orchestration.RuntimeStatus; });
        ResumeSchedulerCommand = new RelayCommand(() => { Orchestration.Paused = false; Orchestration.RuntimeStatus = "Scheduler resumed"; HallStatus = Orchestration.RuntimeStatus; });
        CancelRunningJobsCommand = new RelayCommand(CancelRunningJobs);
        RunHardeningChecksCommand = new AsyncRelayCommand(RunHardeningChecksAsync);
        StartEngineeringCommand = new AsyncRelayCommand(StartEngineeringAsync);
        StopEngineeringCommand = new AsyncRelayCommand(StopEngineeringAsync);
        EmergencyStopEngineeringCommand = new AsyncRelayCommand(EmergencyStopEngineeringAsync);
        QueueEngineeringPhasesCommand = new AsyncRelayCommand(QueueEngineeringPhasesAsync);
        RunEngineeringStepCommand = new AsyncRelayCommand(RunEngineeringStepAsync);
        SaveEngineeringCommand = new AsyncRelayCommand(SaveEngineeringAsync);
        QueueRuntimeDisconnectObjectiveCommand = new AsyncRelayCommand(QueueRuntimeDisconnectObjectiveAsync);
    }

    public async Task InitializeAsync()
    {
        GuardianHallState? state = null;
        try { state = await _store.LoadAsync(); } catch { HallStatus = "Previous Guardian state could not be read; defaults loaded"; }
        state ??= new GuardianHallState { Guardians = GuardianCatalog.CreateDefaults() };
        Engineering = await _engineeringPersistence.LoadAsync();
        Engineering.Status.Running = false;
        Engineering.Status.CurrentTaskId = "";
        Engineering.Status.Summary = "Engineering stopped after application start; configure Borin capabilities and press Start Working";
        if (state.Guardians.Count == 0) state.Guardians = GuardianCatalog.CreateDefaults();
        foreach (var profile in state.Guardians) Guardians.Add(profile);
        RealmKeeper = state.RealmKeeper ?? new(); Wanderer = state.Wanderer ?? new(); Analyst = state.Analyst ?? new(); Worldsmith = state.Worldsmith ?? new(); Archivist = state.Archivist ?? new(); Orchestration = state.Orchestration ?? new(); Diagnostics = state.Diagnostics ?? new();
        SelectedGuardian = Guardians.FirstOrDefault();
        RaiseWorkspaceProperties();
        HallStatus = state.MigrationStatus.StartsWith("Migrated", StringComparison.OrdinalIgnoreCase)
            ? $"{Guardians.Count} role-specific workspaces ready · {state.MigrationStatus}"
            : $"{Guardians.Count} role-specific Guardian workspaces ready";
        await RefreshRealmKeeperAsync(false);
        await RefreshWandererAsync(false);
        await RefreshAnalystAsync(false);
        await RefreshWorldsmithAsync(false);
        _telemetryTimer.Start();
        _orchestrationTimer.Interval = TimeSpan.FromSeconds(Math.Max(15, Orchestration.TickSeconds));
        _orchestrationTimer.Start();
        Wanderer.AutonomousRunning = false;
        Wanderer.AutonomousStatus = "Autonomous play is stopped after application start; press Start Auto Play to resume";
        RecoverInterruptedJobs();
    }

    private async Task StartEngineeringAsync()
    {
        await _engineeringOrchestrator.StartAsync(Engineering);
        Raise(nameof(Engineering));
        HallStatus = Engineering.Status.Summary;
    }

    private async Task StopEngineeringAsync()
    {
        await _engineeringOrchestrator.StopAsync(Engineering);
        Raise(nameof(Engineering));
        HallStatus = Engineering.Status.Summary;
    }

    private async Task EmergencyStopEngineeringAsync()
    {
        await _engineeringOrchestrator.EmergencyStopAsync(Engineering);
        Raise(nameof(Engineering));
        HallStatus = Engineering.Status.Summary;
    }

    private async Task QueueEngineeringPhasesAsync()
    {
        await _engineeringOrchestrator.QueueAllPhasesAsync(Engineering);
        Raise(nameof(Engineering));
        HallStatus = "Guardian Hall engineering phases queued";
    }

    private async Task RunEngineeringStepAsync()
    {
        await _engineeringOrchestrator.RunOneAsync(Engineering);
        Raise(nameof(Engineering));
        HallStatus = Engineering.Status.Summary;
    }

    private async Task SaveEngineeringAsync()
    {
        await _engineeringPersistence.SaveAsync(Engineering);
        HallStatus = "Engineering configuration and memory saved";
    }

    private async Task QueueRuntimeDisconnectObjectiveAsync()
    {
        await _engineeringOrchestrator.QueueRuntimeDisconnectObjectiveAsync(Engineering);
        Raise(nameof(Engineering));
        HallStatus = "Borin assigned: find and fix runtime disconnects";
    }

    public void QueueDroppedFiles(IEnumerable<string> paths)
    {
        var expanded = paths.SelectMany(p => Directory.Exists(p) ? Directory.EnumerateFiles(p, "*", SearchOption.AllDirectories) : [p]);
        var items = _archivistService.QueueFiles(expanded).ToList();
        foreach (var item in items) Archivist.DecodeQueue.Add(item);
        HallStatus = $"Archivist queued {items.Count} file(s)";
    }

    private GuardianHallState CreateState() => new()
    {
        Guardians = Guardians.ToList(), RealmKeeper = RealmKeeper, Wanderer = Wanderer, Analyst = Analyst, Worldsmith = Worldsmith, Archivist = Archivist, Orchestration = Orchestration, Diagnostics = Diagnostics
    };

    private async Task SaveAsync()
    {
        await _store.SaveAsync(CreateState());
        HallStatus = "Guardian Hall workspaces, tools, queues, and settings saved";
    }


    private async Task RunHardeningChecksAsync()
    {
        await _hardeningService.RunAsync(CreateState(), Diagnostics);
        Raise(nameof(Diagnostics));
        HallStatus = Diagnostics.OverallStatus;
    }

    private void QueueSelectedGuardianJob()
    {
        if (SelectedGuardian is null) return;
        var jobType = SelectedGuardian.Role switch
        {
            GuardianRole.RealmKeeper => "Telemetry and report refresh",
            GuardianRole.Wanderer => "Mission queue step",
            GuardianRole.Analyst => "Scheduled analysis report",
            GuardianRole.Archivist => "Decode queue batch",
            GuardianRole.Worldsmith => "Manual approval required",
            _ => "Refresh"
        };
        if (SelectedGuardian.Role == GuardianRole.Worldsmith)
        {
            HallStatus = "Worldsmith apply operations remain manual and approval-gated";
            return;
        }
        _orchestrator.Queue(Orchestration, SelectedGuardian.Role, jobType);
        HallStatus = $"Queued {SelectedGuardian.Name}: {jobType}";
    }

    private async Task RunGuardianJobsAsync(bool announce)
    {
        await _orchestrator.RunDueJobsAsync(Orchestration, ExecuteGuardianJobAsync, _orchestrationCancellation.Token);
        await _store.SaveAsync(CreateState());
        Raise(nameof(Orchestration));
        if (announce) HallStatus = Orchestration.RuntimeStatus;
    }

    private async Task<string> ExecuteGuardianJobAsync(GuardianRole role)
    {
        switch (role)
        {
            case GuardianRole.RealmKeeper:
                await RefreshRealmKeeperAsync(false);
                return "Realm telemetry refreshed";
            case GuardianRole.Wanderer:
                await RefreshWandererAsync(false);
                if (!Wanderer.SessionAvailable) throw new InvalidOperationException("Blocked: no real assigned Wanderer session is available");
                if (Wanderer.LiveConnectorEnabled && Wanderer.AllowMissionDispatch) return await _wandererTelemetry.ExecuteMissionStepAsync(Wanderer);
                return "Wanderer mission queue telemetry refreshed; dispatch remains disabled";
            case GuardianRole.Analyst:
                await RefreshAnalystAsync(false);
                if (!Analyst.DataAvailable) throw new InvalidOperationException("Blocked: Analyst data sources are unavailable");
                await ExportAnalystAsync();
                return "Analyst report refreshed and exported";
            case GuardianRole.Archivist:
                if (Archivist.DecodeQueue.Count == 0) throw new InvalidOperationException("Blocked: Archivist decode queue is empty");
                await DecodeAllAsync();
                return "Archivist decode queue processed";
            case GuardianRole.Worldsmith:
                throw new InvalidOperationException("Blocked: Worldsmith operations require manual approval");
            default:
                throw new InvalidOperationException("Unsupported Guardian role");
        }
    }

    private void CancelRunningJobs()
    {
        _orchestrationCancellation.Cancel();
        foreach (var job in Orchestration.Jobs.Where(x => x.Status == GuardianJobStatus.Running))
        {
            job.Status = GuardianJobStatus.Cancelled;
            job.Message = "Cancelled by operator";
            job.CompletedUtc = DateTime.UtcNow.ToString("O");
        }
        _orchestrationCancellation.Dispose();
        _orchestrationCancellation = new CancellationTokenSource();
        Orchestration.RuntimeStatus = "Running Guardian jobs cancelled";
        HallStatus = Orchestration.RuntimeStatus;
    }

    private void RecoverInterruptedJobs()
    {
        foreach (var job in Orchestration.Jobs.Where(x => x.Status == GuardianJobStatus.Running))
        {
            job.Status = GuardianJobStatus.Queued;
            job.Message = "Recovered after application restart";
            job.ScheduledUtc = DateTime.UtcNow.ToString("O");
        }
        Orchestration.NextRunUtc = DateTime.UtcNow.AddSeconds(Math.Max(15, Orchestration.TickSeconds)).ToString("O");
    }

    private async Task RefreshRealmKeeperAsync(bool announce)
    {
        try
        {
            var snapshot = await _realmKeeperTelemetry.CaptureAsync(RealmKeeper);
            RealmKeeper.TelemetryLive = snapshot.RepositoryAvailable;
            RealmKeeper.ConnectedHosts = snapshot.ConnectedHosts;
            RealmKeeper.CoveredWorlds = snapshot.CoveredWorlds;
            RealmKeeper.GeneratedRecords = snapshot.GeneratedRecords;
            RealmKeeper.ActiveSaveFiles = snapshot.ActiveSaveFiles;
            RealmKeeper.CurrentRealmHealth = snapshot.HealthSummary;
            RealmKeeper.LiveActivity = snapshot.ActivitySummary;
            RealmKeeper.TelemetryStatus = snapshot.SourceSummary;
            RealmKeeper.LastRefresh = snapshot.CapturedUtc.ToLocalTime().ToString("g");
            foreach (var entry in snapshot.TimelineEntries.Reverse())
            {
                if (!RealmKeeper.RealmTimeline.Contains(entry)) RealmKeeper.RealmTimeline.Insert(0, entry);
            }
            while (RealmKeeper.RealmTimeline.Count > 30) RealmKeeper.RealmTimeline.RemoveAt(RealmKeeper.RealmTimeline.Count - 1);
            if (announce) HallStatus = "Realm Keeper local and configured remote telemetry refreshed";
        }
        catch (Exception ex)
        {
            RealmKeeper.TelemetryLive = false;
            RealmKeeper.TelemetryStatus = $"Telemetry refresh failed: {ex.Message}";
            if (announce) HallStatus = RealmKeeper.TelemetryStatus;
        }
    }

    private async Task RefreshWandererAsync(bool announce)
    {
        try
        {
            var snapshot = await _wandererTelemetry.CaptureAsync(Wanderer);
            Wanderer.SessionAvailable = snapshot.SessionAvailable;
            Wanderer.DiscoveredRooms = snapshot.DiscoveredRooms;
            Wanderer.JournalEntries = snapshot.JournalEntries;
            Wanderer.InventoryFiles = snapshot.InventoryFiles;
            Wanderer.CurrentRoute = snapshot.CurrentRoute;
            Wanderer.Inventory = snapshot.InventorySummary;
            Wanderer.Progress = snapshot.ProgressSummary;
            Wanderer.RuntimeStatus = snapshot.SourceSummary;
            Wanderer.LastRefresh = snapshot.CapturedUtc.ToLocalTime().ToString("g");
            Wanderer.MissionQueue.Clear();
            foreach (var mission in snapshot.MissionQueue) Wanderer.MissionQueue.Add(mission);
            if (announce) HallStatus = "Wanderer character-session telemetry refreshed";
        }
        catch (Exception ex)
        {
            Wanderer.SessionAvailable = false;
            Wanderer.RuntimeStatus = $"Wanderer telemetry refresh failed: {ex.Message}";
            if (announce) HallStatus = Wanderer.RuntimeStatus;
        }
    }

    private async Task StartWandererAutonomyAsync()
    {
        await RefreshWandererAsync(false);
        if (!Wanderer.SessionAvailable)
        {
            HallStatus = "Wanderer auto play blocked: no matching connected Play session";
            return;
        }
        if (!Wanderer.AllowMissionDispatch || Wanderer.Permission < GuardianPermission.Interact)
        {
            HallStatus = "Wanderer auto play blocked: enable guarded dispatch and set permission to Interact or higher";
            return;
        }
        Wanderer.AutonomousRunning = true;
        Wanderer.AutonomousStatus = "Autonomous play running";
        _wandererAutonomyTimer.Interval = TimeSpan.FromSeconds(Math.Clamp(Wanderer.AutonomousIntervalSeconds, 2, 30));
        _wandererAutonomyTimer.Start();
        await _store.SaveAsync(CreateState());
        HallStatus = "Wanderer autonomous play started";
        await RunWandererAutonomyStepAsync(true);
    }

    private void PauseWandererAutonomy()
    {
        Wanderer.AutonomousRunning = false;
        Wanderer.AutonomousStatus = "Autonomous play paused by operator";
        _wandererAutonomyTimer.Stop();
        HallStatus = Wanderer.AutonomousStatus;
    }

    private async Task RunSingleWandererAutonomyStepAsync()
    {
        await RefreshWandererAsync(false);
        if (!Wanderer.SessionAvailable)
        {
            HallStatus = "Wanderer step blocked: no matching connected Play session";
            return;
        }
        if (!Wanderer.AllowMissionDispatch || Wanderer.Permission < GuardianPermission.Interact)
        {
            HallStatus = "Wanderer step blocked: enable guarded dispatch and set permission to Interact or higher";
            return;
        }

        var wasRunning = Wanderer.AutonomousRunning;
        Wanderer.AutonomousRunning = true;
        await RunWandererAutonomyStepAsync(true);
        if (!wasRunning && Wanderer.AutonomousRunning)
        {
            Wanderer.AutonomousRunning = false;
            Wanderer.AutonomousStatus = "Single autonomous step completed; auto play remains paused";
            await _store.SaveAsync(CreateState());
        }
    }

    private async Task RunWandererAutonomyStepAsync(bool announce)
    {
        if (_wandererStepRunning || !Wanderer.AutonomousRunning) return;
        _wandererStepRunning = true;
        try
        {
            var result = await _wandererTelemetry.ExecuteAutonomousStepAsync(Wanderer);
            if (!Wanderer.AutonomousRunning) _wandererAutonomyTimer.Stop();
            if (announce) HallStatus = result;
            await RefreshWandererAsync(false);
            await _store.SaveAsync(CreateState());
        }
        catch (Exception ex)
        {
            Wanderer.AutonomousRunning = false;
            Wanderer.AutonomousStatus = $"Auto play stopped: {ex.Message}";
            _wandererAutonomyTimer.Stop();
            HallStatus = Wanderer.AutonomousStatus;
        }
        finally { _wandererStepRunning = false; }
    }

    private async Task DispatchWandererMissionAsync()
    {
        try
        {
            var result = await _wandererTelemetry.ExecuteMissionStepAsync(Wanderer);
            HallStatus = result;
            await RefreshWandererAsync(false);
            await _store.SaveAsync(CreateState());
        }
        catch (Exception ex)
        {
            HallStatus = $"Wanderer mission dispatch blocked: {ex.Message}";
        }
    }

    private async Task RefreshAnalystAsync(bool announce)
    {
        try
        {
            var snapshot = await _analystTelemetry.CaptureAsync(Analyst);
            Analyst.DataAvailable = snapshot.DataAvailable;
            Analyst.SourceFiles = snapshot.SourceFiles;
            Analyst.SourceRecords = snapshot.SourceRecords;
            Analyst.ReportFiles = snapshot.ReportFiles;
            Analyst.DataSources = snapshot.DataSources;
            Analyst.Graphs = snapshot.GraphSummary;
            Analyst.HistoricalTrends = snapshot.HistoricalTrends;
            Analyst.RuntimeStatus = snapshot.SourceSummary;
            Analyst.HistorySnapshots = snapshot.HistorySnapshots;
            Analyst.ComparisonStatus = snapshot.ComparisonStatus;
            Analyst.ChartSeries.Clear();
            foreach (var series in snapshot.ChartSeries) Analyst.ChartSeries.Add(series);
            Analyst.LastRefresh = snapshot.CapturedUtc.ToLocalTime().ToString("g");
            if (announce) HallStatus = "Analyst read-only data scan refreshed";
        }
        catch (Exception ex)
        {
            Analyst.DataAvailable = false;
            Analyst.RuntimeStatus = $"Analyst telemetry refresh failed: {ex.Message}";
            if (announce) HallStatus = Analyst.RuntimeStatus;
        }
    }

    private void SetStatus(GuardianStatus status, string message)
    {
        if (SelectedGuardian is null) return;
        SelectedGuardian.Status = status;
        SelectedGuardian.LastActivity = $"{DateTime.Now:g}: {message}";
        HallStatus = $"{SelectedGuardian.Name}: {message}";
        RaiseSelection();
    }

    private void StopAll()
    {
        foreach (var guardian in Guardians) guardian.Status = GuardianStatus.Paused;
        _ = _engineeringOrchestrator.StopAsync(Engineering);
        HallStatus = "Emergency stop: all Guardians and engineering paused";
        Raise(nameof(Guardians)); RaiseSelection();
    }

    private void ChooseFiles()
    {
        var dialog = new OpenFileDialog { Multiselect = true, Filter = "Supported evidence|*.wcc;*.wbb;*.dll;*.json;*.xml;*.csv;*.txt;*.mdb;*.bin|All files|*.*" };
        if (dialog.ShowDialog() == true) QueueDroppedFiles(dialog.FileNames);
    }

    private void ChooseFolder()
    {
        var dialog = new OpenFolderDialog { Multiselect = false, Title = "Choose an evidence folder" };
        if (dialog.ShowDialog() == true) QueueDroppedFiles([dialog.FolderName]);
    }

    private async Task DecodeSelectedAsync()
    {
        if (SelectedDecodeItem is null) { HallStatus = "Select an evidence file to decode"; return; }
        await DecodeAsync([SelectedDecodeItem]);
    }

    private async Task DecodeAllAsync() => await DecodeAsync(Archivist.DecodeQueue);

    private async Task DecodeAsync(IEnumerable<DecodeQueueItem> items)
    {
        try
        {
            HallStatus = "Archivist is inspecting evidence";
            await _archivistService.DecodeAsync(Archivist, items);
            if (!string.IsNullOrWhiteSpace(Archivist.SearchText)) SearchEvidence();
            Raise(nameof(Archivist));
            HallStatus = Archivist.DecodeStatus;
        }
        catch (Exception ex) { HallStatus = $"Archivist decode blocked: {ex.Message}"; }
    }

    private void SearchEvidence()
    {
        var results = _archivistService.SearchEvidence(Archivist, Archivist.SearchText);
        Archivist.SearchResults.Clear();
        foreach (var result in results) Archivist.SearchResults.Add(result);
        Archivist.SearchStatus = results.Count == 0
            ? "No indexed evidence matched the search"
            : $"{results.Count} indexed evidence result(s) · select a result to inspect relationships";
        SelectedEvidenceResult = results.FirstOrDefault();
        Raise(nameof(Archivist));
        HallStatus = Archivist.SearchStatus;
    }

    private void ClearEvidenceSearch()
    {
        Archivist.SearchText = "";
        Archivist.SearchResults.Clear();
        Archivist.SearchStatus = "Evidence search cleared";
        SelectedEvidenceResult = null;
        Archivist.RelationshipViewer = "Select evidence to inspect cross references";
        Raise(nameof(Archivist));
        HallStatus = Archivist.SearchStatus;
    }

    private async Task SaveEvidenceAsync()
    {
        try
        {
            var path = await _archivistService.SaveEvidenceAsync(Archivist);
            Archivist.GeneratedKnowledge = $"Evidence snapshot saved: {path}";
            Raise(nameof(Archivist));
            HallStatus = Archivist.GeneratedKnowledge;
        }
        catch (Exception ex) { HallStatus = $"Save evidence failed: {ex.Message}"; }
    }

    private async Task GenerateReportAsync(string format)
    {
        var path = await _archivistService.GenerateReportAsync(Archivist, format);
        Archivist.GeneratedKnowledge = $"{format} report generated: {path}";
        HallStatus = Archivist.GeneratedKnowledge;
    }

    private async Task ExportCodexAsync()
    {
        var path = await _archivistService.ExportCodexAsync(Archivist);
        Archivist.GeneratedKnowledge = $"Codex package generated: {path}";
        HallStatus = Archivist.GeneratedKnowledge;
    }

    private void BrowseTool()
    {
        if (SelectedTool is null) return;
        var dialog = new OpenFileDialog { Filter = "Executable files|*.exe;*.bat;*.cmd|All files|*.*" };
        if (dialog.ShowDialog() == true) { SelectedTool.ExecutablePath = dialog.FileName; Raise(nameof(Archivist)); }
    }

    private void ValidateTool()
    {
        if (SelectedTool is null) { HallStatus = "Select a registered local tool"; return; }
        var result = _archivistService.ValidateTool(SelectedTool);
        SelectedTool.ValidationStatus = result.Message;
        SelectedTool.LastError = result.IsValid ? "" : result.Message;
        Archivist.LastToolStatus = result.Message;
        HallStatus = result.IsValid ? $"{SelectedTool.Name} is ready" : $"Tool validation blocked: {result.Message}";
        Raise(nameof(Archivist));
    }

    private void LaunchTool()
    {
        if (SelectedTool is null) { HallStatus = "Select a registered local tool"; return; }
        try
        {
            var status = _archivistService.LaunchTool(SelectedTool, SelectedDecodeItem?.Path);
            Archivist.LastToolStatus = status;
            HallStatus = $"Launched {SelectedTool.Name}";
            Raise(nameof(Archivist));
        }
        catch (Exception ex)
        {
            SelectedTool.LastError = ex.Message;
            Archivist.LastToolStatus = $"Tool launch blocked: {ex.Message}";
            HallStatus = Archivist.LastToolStatus;
            Raise(nameof(Archivist));
        }
    }

    private async void GenerateProposal()
    {
        try
        {
            var decision = _router.Route(new(GuardianRole.Worldsmith, "proposal", Worldsmith.BuildProject, true));
            var path = await _worldsmithRuntime.GenerateProposalAsync(Worldsmith);
            Worldsmith.LastProposalPath = path;
            Worldsmith.ApprovedProposalPath = "";
            Worldsmith.ApprovalStatus = "Awaiting approval code";
            using (var proposal = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(path)))
                Worldsmith.GeneratedApprovalCode = proposal.RootElement.GetProperty("approvalCode").GetString() ?? "";
            Worldsmith.ApprovalCode = "";
            Worldsmith.Preview = $"Preview proposal saved: {path}. Approval code generated. Approval required: {Worldsmith.ApprovalRequired}. {decision.Reason}";
            HallStatus = "Worldsmith proposal generated as an approval-gated preview";
        }
        catch (Exception ex) { Worldsmith.Preview = ex.Message; HallStatus = $"Worldsmith proposal blocked: {ex.Message}"; }
    }

    private async void ApproveProposal()
    {
        try
        {
            var approvalPath = await _worldsmithRuntime.ApproveProposalAsync(Worldsmith);
            Worldsmith.ApprovalStatus = $"Approved: {approvalPath}";
            HallStatus = "Worldsmith proposal approved for isolated test-realm staging";
        }
        catch (Exception ex) { Worldsmith.ApprovalStatus = ex.Message; HallStatus = $"Worldsmith approval blocked: {ex.Message}"; }
    }

    private async void ApplyApprovedProposal()
    {
        try
        {
            var result = await _worldsmithRuntime.ApplyApprovedProposalAsync(Worldsmith);
            Worldsmith.LastApplyPath = result.AppliedPath;
            Worldsmith.LastApplyReceipt = result.ReceiptPath;
            Worldsmith.ApprovalStatus = $"Applied to isolated staging: {result.AppliedPath}. Receipt: {result.ReceiptPath}";
            HallStatus = "Worldsmith approved proposal staged with receipt and rollback backup";
        }
        catch (Exception ex) { Worldsmith.ApprovalStatus = ex.Message; HallStatus = $"Worldsmith apply blocked: {ex.Message}"; }
    }

    private async void SimulateProposal()
    {
        try
        {
            var path = await _worldsmithRuntime.SimulateAsync(Worldsmith);
            Worldsmith.LastSimulationPath = path;
            Worldsmith.Simulation = $"Safe simulation report saved: {path}. No repository runtime data was changed.";
            HallStatus = "Worldsmith test-realm simulation completed";
        }
        catch (Exception ex) { Worldsmith.Simulation = ex.Message; HallStatus = $"Worldsmith simulation blocked: {ex.Message}"; }
    }

    private async Task RefreshWorldsmithAsync(bool announce)
    {
        try
        {
            var snapshot = await _worldsmithRuntime.InspectAsync(Worldsmith);
            Worldsmith.TargetIsSafe = snapshot.TargetIsSafe;
            Worldsmith.BaselineFiles = snapshot.BaselineFiles;
            Worldsmith.BaselineRecords = snapshot.BaselineRecords;
            Worldsmith.RuntimeStatus = $"{snapshot.SourceSummary}. {snapshot.SafetySummary}";
            Worldsmith.LastRefresh = snapshot.CapturedUtc.ToLocalTime().ToString("g");
            if (announce) HallStatus = snapshot.TargetIsSafe ? "Worldsmith test-realm baseline refreshed" : snapshot.SafetySummary;
        }
        catch (Exception ex)
        {
            Worldsmith.TargetIsSafe = false;
            Worldsmith.RuntimeStatus = $"Worldsmith baseline inspection failed: {ex.Message}";
            if (announce) HallStatus = Worldsmith.RuntimeStatus;
        }
    }

    private async Task ExportAnalystAsync()
    {
        await RefreshAnalystAsync(false);
        var configured = string.IsNullOrWhiteSpace(Analyst.OutputLocation) ? "Analyst/Reports" : Analyst.OutputLocation.Trim();
        var dir = Path.IsPathRooted(configured) ? configured : Path.GetFullPath(Path.Combine(_store.RootDirectory, configured));
        var guardianRoot = Path.GetFullPath(_store.RootDirectory) + Path.DirectorySeparatorChar;
        if (!Path.IsPathRooted(configured) && !dir.StartsWith(guardianRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Relative Analyst output locations must remain inside GuardianHall.");
        Directory.CreateDirectory(dir);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var markdownPath = Path.Combine(dir, $"Analyst-{stamp}.md");
        var jsonPath = Path.Combine(dir, $"Analyst-{stamp}.json");
        var csvPath = Path.Combine(dir, $"Analyst-{stamp}-series.csv");
        var chartLines = string.Join("\n", Analyst.ChartSeries.Select(x => $"- {x.Display}"));
        await File.WriteAllTextAsync(markdownPath, $"# Analyst Report\n\nTarget: {Analyst.AnalysisTarget}\n\nSources: {Analyst.DataSources}\n\nRange: {Analyst.ComparisonRange}\n\nGraphs: {Analyst.Graphs}\n\nHistorical Trends: {Analyst.HistoricalTrends}\n\nComparison: {Analyst.ComparisonStatus}\n\nPersisted Snapshots: {Analyst.HistorySnapshots}\n\n## Chart Series\n{chartLines}\n\nSource Files: {Analyst.SourceFiles}\n\nSource Records: {Analyst.SourceRecords}\n\nExisting Reports: {Analyst.ReportFiles}\n\nRuntime Status: {Analyst.RuntimeStatus}\n");
        await File.WriteAllTextAsync(jsonPath, System.Text.Json.JsonSerializer.Serialize(new { generatedUtc = DateTime.UtcNow, target = Analyst.AnalysisTarget, range = Analyst.ComparisonRange, Analyst.SourceFiles, Analyst.SourceRecords, Analyst.ReportFiles, Analyst.HistorySnapshots, comparison = Analyst.ComparisonStatus, series = Analyst.ChartSeries.Select(x => new { x.Label, x.Count, x.Percent }) }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        await File.WriteAllLinesAsync(csvPath, new[] { "label,count,percent" }.Concat(Analyst.ChartSeries.Select(x => $"\"{x.Label.Replace("\"", "\"\"")}\",{x.Count},{x.Percent:F2}")));
        HallStatus = $"Analyst report package exported: {dir}";
    }

    private void RaiseSelection()
    {
        Raise(nameof(SelectedIdentity)); Raise(nameof(SelectedDescription)); Raise(nameof(SelectedPersonality)); Raise(nameof(SelectedAppearance));
        Raise(nameof(IsRealmKeeper)); Raise(nameof(IsWanderer)); Raise(nameof(IsAnalyst)); Raise(nameof(IsWorldsmith)); Raise(nameof(IsArchivist));
    }
    private void RaiseWorkspaceProperties() { Raise(nameof(RealmKeeper)); Raise(nameof(Wanderer)); Raise(nameof(Analyst)); Raise(nameof(Worldsmith)); Raise(nameof(Archivist)); }
}
