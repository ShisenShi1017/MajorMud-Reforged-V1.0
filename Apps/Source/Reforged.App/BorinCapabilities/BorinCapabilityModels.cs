using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Reforged.App.BorinCapabilities;

public enum BorinCapabilityArea { RealmKeeper, Player, Analyst, Worldsmith, Archivist }
public enum BorinCapabilityStatus { Sleeping, Idle, Working, Paused, Blocked }
public enum BorinCapabilityPermission { Observe, Interact, Test, Draft, ApplyWithApproval, Administrator }
public enum BorinCapabilitySchedule { Manual, Continuous, Nightly, Scheduled }
public enum EvidenceConfidence { Proven, StronglyCorrelated, Hypothesis, Unknown }
public enum BorinCapabilityJobStatus { Queued, Running, Completed, Failed, Blocked, Paused, Cancelled }

public abstract class ObservableModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }
}

public sealed class BorinCapabilityProfile : ObservableModel
{
    private BorinCapabilityStatus _status = BorinCapabilityStatus.Sleeping;
    private string _currentMission = "Awaiting assignment";
    private string _lastActivity = "No activity recorded";
    public BorinCapabilityArea Role { get; init; }
    public string Name { get; init; } = "";
    public string Title { get; init; } = "";
    public string Icon { get; init; } = "?";
    public string Appearance { get; init; } = "";
    public string Personality { get; init; } = "";
    public string Mission { get; init; } = "";
    public string BorinCapabilityName => Role switch
    {
        BorinCapabilityArea.RealmKeeper => "Realm Operations",
        BorinCapabilityArea.Player => "Player Operations",
        BorinCapabilityArea.Analyst => "Analysis",
        BorinCapabilityArea.Worldsmith => "Worldsmith (Withheld)",
        BorinCapabilityArea.Archivist => "Evidence & Librarium",
        _ => Role.ToString(),
    };
    public BorinCapabilityStatus Status { get => _status; set => Set(ref _status, value); }
    public string CurrentMission { get => _currentMission; set => Set(ref _currentMission, value); }
    public string LastActivity { get => _lastActivity; set => Set(ref _lastActivity, value); }
}

public sealed class RealmHostEndpoint : ObservableModel
{
    public string Name { get; set; } = "New Host";
    public string Scope { get; set; } = "LAN";
    public string BaseUrl { get; set; } = "http://localhost:5000";
    public string TelemetryPath { get; set; } = "/api/guardian/telemetry";
    public string ApiTokenEnvironmentVariable { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public int TimeoutSeconds { get; set; } = 5;
    public string Status { get; set; } = "Not contacted";
    public string LastContactUtc { get; set; } = "Never";
    public string LastError { get; set; } = "";
}

public sealed class RealmKeeperWorkspace : ObservableModel
{
    private string _currentRealmHealth = "All connected realms awaiting telemetry";
    private string _liveActivity = "No live activity received";
    private string _telemetryStatus = "Telemetry has not been refreshed";
    private string _lastRefresh = "Never";
    private int _connectedHosts;
    private int _coveredWorlds;
    private int _generatedRecords;
    private int _activeSaveFiles;
    private bool _telemetryLive;

    public bool LocalHost { get; set; } = true;
    public bool LanHost { get; set; } = true;
    public bool InternetHost { get; set; } = true;
    public bool Classic { get; set; } = true;
    public bool Custom { get; set; } = true;
    public bool AiWorlds { get; set; } = true;
    public bool TestRealms { get; set; } = true;
    public bool Population { get; set; } = true;
    public bool Economy { get; set; } = true;
    public bool Events { get; set; } = true;
    public bool ServerHealth { get; set; } = true;
    public bool Combat { get; set; } = true;
    public bool Crashes { get; set; } = true;
    public bool Scheduler { get; set; } = true;
    public bool SpawnTimers { get; set; } = true;
    public bool HardResets { get; set; } = true;
    public string AlertThresholds { get; set; } = "Warning 70% · Critical 90%";
    public string AutoReports { get; set; } = "Daily realm health digest";
    public string MaintenanceWindow { get; set; } = "Sunday 03:00–05:00 local";
    public string CurrentRealmHealth { get => _currentRealmHealth; set => Set(ref _currentRealmHealth, value); }
    public string LiveActivity { get => _liveActivity; set => Set(ref _liveActivity, value); }
    public string TelemetryStatus { get => _telemetryStatus; set => Set(ref _telemetryStatus, value); }
    public string LastRefresh { get => _lastRefresh; set => Set(ref _lastRefresh, value); }
    public int ConnectedHosts { get => _connectedHosts; set => Set(ref _connectedHosts, value); }
    public int CoveredWorlds { get => _coveredWorlds; set => Set(ref _coveredWorlds, value); }
    public int GeneratedRecords { get => _generatedRecords; set => Set(ref _generatedRecords, value); }
    public int ActiveSaveFiles { get => _activeSaveFiles; set => Set(ref _activeSaveFiles, value); }
    public bool TelemetryLive { get => _telemetryLive; set => Set(ref _telemetryLive, value); }
    public ObservableCollection<string> RealmTimeline { get; set; } = ["Borin capabilities initialized", "Global monitoring scope configured"];
    public ObservableCollection<RealmHostEndpoint> RemoteHosts { get; set; } = [];
}

public sealed record RealmKeeperTelemetrySnapshot(
    DateTime CapturedUtc,
    bool RepositoryAvailable,
    int ConnectedHosts,
    int CoveredWorlds,
    int GeneratedRecords,
    int ActiveSaveFiles,
    string HealthSummary,
    string ActivitySummary,
    string SourceSummary,
    IReadOnlyList<string> TimelineEntries);

public sealed class AnalystChartSeries : ObservableModel
{
    public string Label { get; set; } = "Series";
    public int Count { get; set; }
    public double Percent { get; set; }
    public string Display => $"{Label}: {Count:N0} ({Percent:N1}%)";
}

public sealed class AnalystWorkspace : ObservableModel
{
    private string _runtimeStatus = "Analysis telemetry has not been refreshed";
    private string _lastRefresh = "Never";
    private int _sourceFiles;
    private int _sourceRecords;
    private int _reportFiles;
    private bool _dataAvailable;
    private int _historySnapshots;
    private string _comparisonStatus = "No historical baseline";
    public string AnalysisTarget { get; set; } = "Combat";
    public bool Combat { get; set; } = true;
    public bool Economy { get; set; }
    public bool Drops { get; set; }
    public bool NpcBalance { get; set; }
    public bool QuestBalance { get; set; }
    public bool ExperienceCurve { get; set; }
    public bool ItemBalance { get; set; }
    public bool Progression { get; set; }
    public string DataSources { get; set; } = "Runtime telemetry, Librarium, imported records";
    public string ComparisonRange { get; set; } = "Last 30 days";
    public string Graphs { get; set; } = "Damage distribution · survival · reward efficiency";
    public string HistoricalTrends { get; set; } = "No historical analysis generated";
    public string ReportFrequency { get; set; } = "Weekly";
    public string OutputLocation { get; set; } = "Reports/Borin/Analysis";
    public string RuntimeStatus { get => _runtimeStatus; set => Set(ref _runtimeStatus, value); }
    public string LastRefresh { get => _lastRefresh; set => Set(ref _lastRefresh, value); }
    public int SourceFiles { get => _sourceFiles; set => Set(ref _sourceFiles, value); }
    public int SourceRecords { get => _sourceRecords; set => Set(ref _sourceRecords, value); }
    public int ReportFiles { get => _reportFiles; set => Set(ref _reportFiles, value); }
    public bool DataAvailable { get => _dataAvailable; set => Set(ref _dataAvailable, value); }
    public int HistorySnapshots { get => _historySnapshots; set => Set(ref _historySnapshots, value); }
    public string ComparisonStatus { get => _comparisonStatus; set => Set(ref _comparisonStatus, value); }
    public ObservableCollection<AnalystChartSeries> ChartSeries { get; set; } = [];
}

public sealed record AnalystTelemetrySnapshot(
    DateTime CapturedUtc,
    bool DataAvailable,
    int SourceFiles,
    int SourceRecords,
    int ReportFiles,
    string DataSources,
    string GraphSummary,
    string HistoricalTrends,
    string SourceSummary,
    int HistorySnapshots,
    string ComparisonStatus,
    IReadOnlyList<AnalystChartSeries> ChartSeries);

public sealed class WorldsmithWorkspace : ObservableModel
{
    private string _runtimeStatus = "Test-realm workspace has not been inspected";
    private string _lastRefresh = "Never";
    private int _baselineFiles;
    private int _baselineRecords;
    private bool _targetIsSafe;
    public string TargetTestRealm { get; set; } = "Development Realm";
    public string BuildProject { get; set; } = "New proposal";
    public string ContentType { get; set; } = "Rooms";
    public bool Rooms { get; set; } = true;
    public bool Npcs { get; set; }
    public bool Items { get; set; }
    public bool Quests { get; set; }
    public bool Encounters { get; set; }
    public bool Dungeons { get; set; }
    public bool WorldEvents { get; set; }
    public string Constraints { get; set; } = "Preserve canonical IDs and test-realm isolation";
    public bool ClassicSafe { get; set; } = true;
    public bool Custom { get; set; }
    public bool AiWorld { get; set; }
    public bool ApprovalRequired { get; set; } = true;
    public string Preview { get; set; } = "No proposal generated";
    public string Simulation { get; set; } = "Not simulated";
    public string RuntimeStatus { get => _runtimeStatus; set => Set(ref _runtimeStatus, value); }
    public string LastRefresh { get => _lastRefresh; set => Set(ref _lastRefresh, value); }
    public int BaselineFiles { get => _baselineFiles; set => Set(ref _baselineFiles, value); }
    public int BaselineRecords { get => _baselineRecords; set => Set(ref _baselineRecords, value); }
    public bool TargetIsSafe { get => _targetIsSafe; set => Set(ref _targetIsSafe, value); }
    public string LastProposalPath { get; set; } = "";
    public string LastSimulationPath { get; set; } = "";
    public string GeneratedApprovalCode { get; set; } = "";
    public string ApprovalCode { get; set; } = "";
    public string ApprovalStatus { get; set; } = "No proposal approved";
    public string ApprovedProposalPath { get; set; } = "";
    public string LastApplyPath { get; set; } = "";
    public string LastApplyReceipt { get; set; } = "";
}

public sealed record WorldsmithRuntimeSnapshot(
    DateTime CapturedUtc,
    bool TargetIsSafe,
    int BaselineFiles,
    int BaselineRecords,
    string SourceSummary,
    string SafetySummary);

public sealed class ArchivistWorkspace : ObservableModel
{
    public ObservableCollection<DecodeQueueItem> DecodeQueue { get; set; } = [];
    public ObservableCollection<EvidenceNode> EvidenceTree { get; set; } = [];
    public ObservableCollection<LocalToolRegistration> LocalTools { get; set; } = [];
    public ObservableCollection<EvidenceSearchResult> SearchResults { get; set; } = [];
    public string SearchText { get; set; } = "";
    public string SearchStatus { get; set; } = "No evidence search run";
    public string LibrariumPreview { get; set; } = "Preview only — Librarium is never overwritten without approval.";
    public string GeneratedKnowledge { get; set; } = "No knowledge package generated";
    public string RelationshipViewer { get; set; } = "Select evidence to inspect cross references";
    public string DecodeStatus { get; set; } = "No evidence decoded";
    public string LastEvidencePath { get; set; } = "";
    public string LastReportPath { get; set; } = "";
    public string LastCodexPath { get; set; } = "";
    public string LastToolStatus { get; set; } = "No local tool validated or launched";
    public int DecodedFiles { get; set; }
    public int FailedFiles { get; set; }
    public int SavedEvidenceCount { get; set; }
}

public sealed class DecodeQueueItem : ObservableModel
{
    public string Path { get; set; } = "";
    public string FileName { get; set; } = "";
    public string Type { get; set; } = "UNKNOWN";
    public string Status { get; set; } = "Queued";
    public EvidenceConfidence Confidence { get; set; } = EvidenceConfidence.Unknown;
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = "";
    public string Findings { get; set; } = "";
}

public sealed class EvidenceNode : ObservableModel
{
    public string Name { get; set; } = "";
    public string Source { get; set; } = "";
    public EvidenceConfidence Confidence { get; set; } = EvidenceConfidence.Unknown;
    public string Relationships { get; set; } = "";
}


public sealed class EvidenceSearchResult : ObservableModel
{
    public string Name { get; set; } = "";
    public string Source { get; set; } = "";
    public string Type { get; set; } = "Evidence";
    public EvidenceConfidence Confidence { get; set; } = EvidenceConfidence.Unknown;
    public string MatchSummary { get; set; } = "";
    public string Relationships { get; set; } = "";
    public string Sha256 { get; set; } = "";
}

public sealed class LocalToolRegistration : ObservableModel
{
    public string Name { get; set; } = "New Tool";
    public string ExecutablePath { get; set; } = "";
    public string Arguments { get; set; } = "{file}";
    public string WorkingDirectory { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public string ValidationStatus { get; set; } = "Not validated";
    public string LastLaunchUtc { get; set; } = "";
    public string LastError { get; set; } = "";
}


public sealed class BorinCapabilityJob : ObservableModel
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public BorinCapabilityArea Role { get; set; }
    public string JobType { get; set; } = "Refresh";
    public BorinCapabilityJobStatus Status { get; set; } = BorinCapabilityJobStatus.Queued;
    public string ScheduledUtc { get; set; } = DateTime.UtcNow.ToString("O");
    public string StartedUtc { get; set; } = "";
    public string CompletedUtc { get; set; } = "";
    public string Message { get; set; } = "Queued";
    public int Attempt { get; set; }
}

public sealed class BorinCapabilitySchedulerState : ObservableModel
{
    public bool Enabled { get; set; } = true;
    public bool Paused { get; set; }
    public int TickSeconds { get; set; } = 60;
    public string NextRunUtc { get; set; } = "Not scheduled";
    public string LastRunUtc { get; set; } = "Never";
    public string RuntimeStatus { get; set; } = "Scheduler ready";
    public ObservableCollection<BorinCapabilityJob> Jobs { get; set; } = [];
}


public sealed class BorinCapabilityDiagnostics : ObservableModel
{
    public string LastRunUtc { get; set; } = "Never";
    public string OverallStatus { get; set; } = "Hardening checks have not been run";
    public int PassedChecks { get; set; }
    public int FailedChecks { get; set; }
    public ObservableCollection<string> Results { get; set; } = [];
}

public sealed class BorinCapabilityState
{
    public int SchemaVersion { get; set; } = 7;
    public string MigrationStatus { get; set; } = "Native role-specific state";
    public string MigratedFrom { get; set; } = "";
    public string MigratedUtc { get; set; } = "";
    public List<BorinCapabilityProfile> Profiles { get; set; } = [];
    public RealmKeeperWorkspace RealmKeeper { get; set; } = new();
    public AnalystWorkspace Analyst { get; set; } = new();
    public WorldsmithWorkspace Worldsmith { get; set; } = new();
    public ArchivistWorkspace Archivist { get; set; } = new();
    public BorinCapabilitySchedulerState Orchestration { get; set; } = new();
    public BorinCapabilityDiagnostics Diagnostics { get; set; } = new();
}
