using System.Collections.ObjectModel;
using Reforged.App.Guardians;

namespace Reforged.App.Engineering;

public enum EngineeringTaskStatus { Pending, Queued, Running, Paused, Completed, Failed, Cancelled, Blocked }
public enum EngineeringTaskPriority { Low, Normal, High, Critical }
public enum EngineeringTaskCategory { Foundation, Tooling, Workspace, Build, Validation, Runtime, RepositoryAnalysis, CandidatePatch, Diagnosis, AutonomousEngineering }
public enum EngineeringHealth { Ready, Working, Warning, Failed, Stopped }
public enum EngineeringAuthority { ReadRepository, WriteWorkspace, RunBuild, RunValidation, RunRuntime, CreateCandidatePackage, WriteLiveRepository, GitWrite, Publish, Install, Delete }

public sealed class EngineeringConfiguration : ObservableModel
{
    private bool _autonomousEngineeringEnabled;
    private bool _allowLiveRepositoryEdits;
    private int _maximumTasksPerSession = 25;
    private int _cycleDelaySeconds = 5;
    public bool AutonomousEngineeringEnabled { get => _autonomousEngineeringEnabled; set => Set(ref _autonomousEngineeringEnabled, value); }
    public bool AllowLiveRepositoryEdits { get => _allowLiveRepositoryEdits; set => Set(ref _allowLiveRepositoryEdits, value); }
    public bool CreateIsolatedWorkspace { get; set; } = true;
    public bool RunBuilds { get; set; } = true;
    public bool RunValidation { get; set; } = true;
    public bool RunRuntimeDiagnostics { get; set; } = true;
    public bool GenerateCandidatePackages { get; set; } = true;
    public bool RequireApprovalForLiveApply { get; set; } = true;
    public int MaximumTasksPerSession { get => _maximumTasksPerSession; set => Set(ref _maximumTasksPerSession, Math.Clamp(value, 1, 500)); }
    public int CycleDelaySeconds { get => _cycleDelaySeconds; set => Set(ref _cycleDelaySeconds, Math.Clamp(value, 1, 300)); }
    public string BuildCommand { get; set; } = "dotnet build MajorMud.Reforged.sln --configuration Release --no-restore";
    public string ValidationCommand { get; set; } = "dotnet run --project Tests/Source/Reforged.Core.Tests/Reforged.Core.Tests.csproj --configuration Release --no-build";
    public string RuntimeCommand { get; set; } = "dotnet run --project Apps/Source/Reforged.Server/Reforged.Server.csproj --configuration Release --no-build -- --smoke-core";
}

public sealed class EngineeringTask : ObservableModel
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "Engineering task";
    public string Description { get; set; } = "";
    public EngineeringTaskStatus Status { get; set; } = EngineeringTaskStatus.Pending;
    public EngineeringTaskPriority Priority { get; set; } = EngineeringTaskPriority.Normal;
    public EngineeringTaskCategory Category { get; set; } = EngineeringTaskCategory.RepositoryAnalysis;
    public ObservableCollection<string> Dependencies { get; set; } = [];
    public int EstimatedMinutes { get; set; }
    public int ActualSeconds { get; set; }
    public string AssignedGuardian { get; set; } = "Borin Ironbeard";
    public string SessionId { get; set; } = "";
    public string CreatedUtc { get; set; } = DateTime.UtcNow.ToString("O");
    public string StartedUtc { get; set; } = "";
    public string CompletedUtc { get; set; } = "";
    public string Result { get; set; } = "Awaiting execution";
}

public sealed class EngineeringSession
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Guardian { get; set; } = "Borin Ironbeard";
    public string StartedUtc { get; set; } = DateTime.UtcNow.ToString("O");
    public string StoppedUtc { get; set; } = "";
    public string WorkspacePath { get; set; } = "";
    public int CompletedTasks { get; set; }
    public int FailedTasks { get; set; }
    public int WarningCount { get; set; }
}

public sealed class EngineeringMemoryEntry
{
    public string TimestampUtc { get; set; } = DateTime.UtcNow.ToString("O");
    public string Category { get; set; } = "Observation";
    public string Summary { get; set; } = "";
    public string SessionId { get; set; } = "";
    public string TaskId { get; set; } = "";
}

public sealed class EngineeringReport
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string SessionId { get; set; } = "";
    public string CreatedUtc { get; set; } = DateTime.UtcNow.ToString("O");
    public EngineeringHealth Health { get; set; } = EngineeringHealth.Ready;
    public ObservableCollection<string> Warnings { get; set; } = [];
    public ObservableCollection<string> Errors { get; set; } = [];
    public ObservableCollection<string> Recommendations { get; set; } = [];
    public string Metrics { get; set; } = "";
    public string FutureWork { get; set; } = "";
    public string Path { get; set; } = "";
}

public sealed class EngineeringStatistics
{
    public int Sessions { get; set; }
    public int TasksCompleted { get; set; }
    public int TasksFailed { get; set; }
    public int BuildsPassed { get; set; }
    public int BuildsFailed { get; set; }
    public int ValidationsPassed { get; set; }
    public int ValidationsFailed { get; set; }
    public long TotalExecutionSeconds { get; set; }
}

public sealed class EngineeringStatus : ObservableModel
{
    private string _summary = "Engineering ready; autonomous engineering is off";
    private EngineeringHealth _health = EngineeringHealth.Ready;
    private bool _running;
    public string Summary { get => _summary; set => Set(ref _summary, value); }
    public EngineeringHealth Health { get => _health; set => Set(ref _health, value); }
    public bool Running { get => _running; set => Set(ref _running, value); }
    public string CurrentSessionId { get; set; } = "";
    public string CurrentTaskId { get; set; } = "";
    public string WorkspacePath { get; set; } = "";
    public string LastActivityUtc { get; set; } = "Never";
}

public sealed class EngineeringWorkspace
{
    public EngineeringConfiguration Configuration { get; set; } = new();
    public EngineeringStatus Status { get; set; } = new();
    public EngineeringSession? CurrentSession { get; set; }
    public ObservableCollection<EngineeringTask> Tasks { get; set; } = [];
    public ObservableCollection<EngineeringMemoryEntry> Memory { get; set; } = [];
    public ObservableCollection<EngineeringReport> Reports { get; set; } = [];
    public EngineeringStatistics Statistics { get; set; } = new();
    public ObservableCollection<string> Events { get; set; } = [];
    public ObservableCollection<string> CapabilityStatus { get; set; } =
    [
        "Phase 1 · Engineering foundation · Ready",
        "Phase 2 · Engineering tools · Ready",
        "Phase 3 · Isolated workspace · Ready",
        "Phase 4 · Build automation · Ready",
        "Phase 5 · Validation automation · Ready",
        "Phase 6 · Runtime automation · Ready",
        "Phase 7 · Repository analysis · Ready",
        "Phase 8 · Candidate patch generation · Ready",
        "Phase 9 · Autonomous diagnosis · Ready",
        "Phase 10 · Autonomous engineering · Operator controlled"
    ];
}
