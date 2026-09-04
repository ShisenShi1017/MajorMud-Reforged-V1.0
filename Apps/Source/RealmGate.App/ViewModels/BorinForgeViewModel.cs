using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Input;
using RealmGate.App.Brains;
using RealmGate.App.Commands;
using RealmGate.App.Forge;
using RealmGate.App.BorinCapabilities;
using RealmGate.App.Models;

namespace RealmGate.App.ViewModels;

public sealed class BorinForgeViewModel : ViewModelBase
{
    private readonly IForgeStateStore _store;
    private readonly IForgeRepositoryService _repository;
    private readonly IBrain _brain;
    private readonly IBorinMemoryStore _memoryStore;
    private readonly IBorinReadOnlyAccess _readOnlyAccess;
    private readonly IBorinClassicTraceAccess _classicTraceAccess;
    private readonly IBorinKnowledgeIndex _knowledgeIndex;
    private readonly IBorinReforgedKnowledgeBootstrap _knowledgeBootstrap;
    private readonly IBorinEngineeringWorkspace _engineeringWorkspace;
    private readonly IBorinEngineeringOrchestrator _engineeringOrchestrator;
    private readonly IBorinEngineeringCorrectionLoop _engineeringCorrectionLoop;
    private readonly IBorinPlayerResearch _playerResearch;
    private readonly IBorinVerifiedKnowledgeLearning _verifiedKnowledgeLearning;
    private readonly IBorinMajorMudKnowledgeCatalog _majorMudKnowledgeCatalog;
    private readonly IBorinAcademy _academy;
    private readonly IBorinNaturalInitiativeStore _naturalInitiative;
    private readonly PlayViewModel _playerTerminalViewModel;
    private readonly IForgeDeveloperTools _tools;
    private readonly INvidiaNimCredentialStore _nvidiaCredentials;
    private readonly IAiProvider _lightningProvider;
    private readonly IAiProvider _superProvider;
    private readonly IAiProvider _ultraProvider;
    private readonly IAiRateBudget _lightningRateBudget;
    private readonly IAiRateBudget _superRateBudget;
    private readonly IAiRateBudget _ultraRateBudget;
    private readonly IAiRateBudget _overallNvidiaBudget;
    private readonly IBorinAiRouteTelemetry _routeTelemetry;
    private readonly INvidiaNimEndpointTelemetry _lightningEndpointTelemetry;
    private readonly INvidiaNimEndpointTelemetry _superEndpointTelemetry;
    private readonly INvidiaNimEndpointTelemetry _ultraEndpointTelemetry;
    private readonly BorinAiRoutingSettings _aiRoutingSettings;
    private readonly LocalGgufBrain? _localBrain;
    private readonly BorinHeadlessTrainingService _headlessTraining = new();
    private IBorinCapabilities? _guardianCapabilities;
    private ForgeConversationState _state = new();
    private ForgeRepositorySnapshot? _snapshot;
    private BorinMemorySnapshot _memorySnapshot = BorinMemorySnapshot.Empty;
    private BorinMemorySnapshot _lastRecallSnapshot = BorinMemorySnapshot.Empty;
    private BorinReadOnlyEvidenceSnapshot _lastProjectEvidence = BorinReadOnlyEvidenceSnapshot.Empty();
    private BorinClassicTraceSnapshot _lastClassicTrace = BorinClassicTraceSnapshot.Empty();
    private BorinKnowledgeIndexSnapshot _lastKnowledgeEvidence = BorinKnowledgeIndexSnapshot.Empty();
    private BorinKnowledgeIndexStatus _knowledgeIndexStatus = BorinKnowledgeIndexStatus.NotBuilt(string.Empty);
    private BorinMajorMudKnowledgeStatus _majorMudKnowledgeStatus = new("Not built", 0, 0, string.Empty, null, "MajorMUD knowledge is initializing.");
    private string _masteryEvaluationSummary = "No evidence-based mastery evaluation has run in this session.";
    private string _masteryEvaluationDetails = "Run the mastery test to see category evidence gates.";
    private BorinVerifiedLearningStatus _verifiedLearningStatus = new(0, 0, 0, string.Empty);
    private BorinAcademySnapshot? _academySnapshot;
    private BorinReforgedBootstrapSnapshot _lastKnowledgeBootstrap = BorinReforgedBootstrapSnapshot.Empty();
    private BorinReforgedBootstrapStatus _knowledgeBootstrapStatus = BorinReforgedBootstrapStatus.Empty();
    private BorinEngineeringWorkspaceStatus _engineeringWorkspaceStatus = BorinEngineeringWorkspaceStatus.NotCreated(string.Empty);
    private BorinEngineeringJobSnapshot _engineeringJob = BorinEngineeringJobSnapshot.None(string.Empty);
    private BorinEngineeringCorrectionStatus _engineeringCorrectionStatus = BorinEngineeringCorrectionStatus.Empty(string.Empty);
    private BorinPlayerSettings _playerSettings = new();
    private BorinPlayerRuntimeStatus _playerRuntimeStatus = BorinPlayerRuntimeStatus.Disabled(new BorinPlayerSettings());
    private BorinLoginStatus _playerLoginStatus = BorinLoginStatus.Initial();
    private BorinPlayerDecision _playerDecision = BorinPlayerDecision.None();
    private BorinPlayerJournalStatus _playerJournalStatus = BorinPlayerJournalStatus.Empty();
    private string _playerSessionId = "borin-training-01";
    private string _playerHostName = "omniversebbs.com";
    private int _playerPort = 3237;
    private string _playerLoginName = "borin";
    private string _playerCharacterName = "Borin";
    private string _playerServerProfileName = "Omniverse BBS";
    private string _playerWorldName = "Classic";
    private string _playerPrimaryTeacher = "William";
    private string _playerMentorAiName = "Gryndak";
    private string _playerCurrentGoal = "Explore MajorMUD safely, learn the world, and report only verified discoveries.";
    private string _playerCommandInput = string.Empty;
    private bool _playerEnabled;
    private bool _playerObservationEnabled;
    private bool _playerConversationEnabled;
    private bool _playerReflectionJournalEnabled = true;
    private bool _playerAutomaticReflectionEnabled;
    private bool _playerConversationLearningEnabled = true;
    private bool _playerAllowSafeCombat;
    private string _playerReflectionFocus = "What did I learn, how did my conversation go, and what should I do next?";
    private string _playerConversationReviewNote = string.Empty;
    private string _playerLastConversationSummary = "No external conversation observed yet.";
    private string _playerLastEvidenceSummary = "No gameplay evidence recorded yet.";
    private string _input = string.Empty;
    private string _status = "Warming the forge...";
    private bool _isBusy;
    private string _brainStatus = "Local GGUF brain not loaded";
    private string _brainPerformanceSummary = "No local-brain performance sample yet.";
    private string _lightningConnectionStatus = "Lightning key status not checked";
    private string _superConnectionStatus = "Super key status not checked";
    private string _ultraConnectionStatus = "Ultra key status not checked";
    private string _selectedBrainPerformancePreset = LocalGgufPerformancePresets.Auto;
    private CancellationTokenSource? _activeRequest;
    private CancellationTokenSource? _forgeInitiativeMonitorCts;
    private CancellationTokenSource? _forgeInitiativeRequest;
    private Task? _forgeInitiativeMonitorTask;
    private CancellationTokenSource? _academyIdleMonitorCts;
    private Task? _academyIdleMonitorTask;
    private int _forgeInitiativeRunning;
    private string _currentTask = "Idle — awaiting a commission.";
    private string _taskStage = "Forge ready";
    private int _taskProgress;
    private string _lastAuditReportPath = string.Empty;
    private string _pendingAuditRequest = string.Empty;
    private string _pendingAuditLastPage = string.Empty;
    private int _pendingAuditNextPage;
    private const int MaximumAuditPages = 4;

    public ObservableCollection<ForgeChatMessage> Messages { get; } = [];
    public ObservableCollection<ForgeAttachment> Attachments { get; } = [];
    public string Input
    {
        get => _input;
        set
        {
            if (!Set(ref _input, value))
                return;
            ((AsyncRelayCommand)SendCommand).Raise();
            if (DraftEngineeringCommand is AsyncRelayCommand draftCommand)
                draftCommand.Raise();
            if (StartEngineeringJobCommand is AsyncRelayCommand jobCommand)
                jobCommand.Raise();
        }
    }
    public string Status { get => _status; private set => Set(ref _status, value); }
    public string BrainStatus { get => _brainStatus; private set { if (Set(ref _brainStatus, value)) Raise(nameof(ModeSummary)); } }
    public string BrainPerformanceSummary { get => _brainPerformanceSummary; private set => Set(ref _brainPerformanceSummary, value); }
    public IReadOnlyList<string> BrainPerformancePresets => LocalGgufPerformancePresets.All;
    public string SelectedBrainPerformancePreset
    {
        get => _selectedBrainPerformancePreset;
        set => Set(ref _selectedBrainPerformancePreset, LocalGgufPerformancePresets.NormalizeName(value));
    }
    public bool IsBusy { get => _isBusy; private set { if (Set(ref _isBusy, value)) Raise(nameof(IsReady)); } }
    public bool IsReady => !IsBusy;
    public ForgeConfiguration Configuration => _state.Configuration;
    public string RepositorySummary => _snapshot is null
        ? "Repository not scanned"
        : $"{_snapshot.ProjectCount} projects • {_snapshot.SourceFileCount} source/UI files • {_snapshot.TestProjectCount} tests";
    public string ModeSummary => $"{Configuration.ProviderName} • {BrainStatus} • {Configuration.PermissionMode}";
    public IReadOnlyList<BorinBrainMode> BrainModes { get; } = Enum.GetValues<BorinBrainMode>();
    public BorinBrainMode SelectedBrainMode
    {
        get => Configuration.BrainMode;
        set
        {
            if (Configuration.BrainMode == value) return;
            Configuration.BrainMode = value;
            _aiRoutingSettings.Mode = value;
            Configuration.ProviderName = value switch
            {
                BorinBrainMode.LocalOnly => "RealmGate Direct GGUF",
                BorinBrainMode.NvidiaOnly => "NVIDIA NIM",
                _ => "Borin Hybrid AI"
            };
            Raise(); Raise(nameof(ModeSummary));
        }
    }
    public bool AutomaticallyEscalateDifficultTasks
    {
        get => Configuration.AutomaticallyEscalateDifficultTasks;
        set { Configuration.AutomaticallyEscalateDifficultTasks = value; _aiRoutingSettings.AutomaticallyEscalateDifficultTasks = value; Raise(); }
    }
    public bool VerifyCriticalTasks
    {
        get => Configuration.VerifyCriticalTasks;
        set { Configuration.VerifyCriticalTasks = value; _aiRoutingSettings.VerifyCriticalTasks = value; Raise(); }
    }
    public string LightningConnectionStatus { get => _lightningConnectionStatus; private set => Set(ref _lightningConnectionStatus, value); }
    public string SuperConnectionStatus { get => _superConnectionStatus; private set => Set(ref _superConnectionStatus, value); }
    public string UltraConnectionStatus { get => _ultraConnectionStatus; private set => Set(ref _ultraConnectionStatus, value); }
    public string LightningRateSummary
    {
        get
        {
            var rate = _lightningRateBudget.Snapshot;
            var reported = _lightningEndpointTelemetry.EndpointStatus.ReportedRequestLimit;
            return $"Lightning: {rate.RequestsInWindow} / {rate.HardLimit} scheduler RPM" + (reported.HasValue ? $" • endpoint reports {reported}/min" : " • endpoint limit not reported") + $" • {rate.WaitingRequests} queued";
        }
    }
    public string UltraRateSummary { get { var rate = _ultraRateBudget.Snapshot; var reported = _ultraEndpointTelemetry.EndpointStatus.ReportedRequestLimit; return $"Ultra: {rate.RequestsInWindow} / {rate.HardLimit} configured safety RPM" + (reported.HasValue ? $" • endpoint reports {reported}/min" : " • endpoint limit not reported") + $" • {rate.WaitingRequests} queued"; } }
    public string SuperRateSummary { get { var rate = _superRateBudget.Snapshot; var reported = _superEndpointTelemetry.EndpointStatus.ReportedRequestLimit; return $"Super: {rate.RequestsInWindow} / {rate.HardLimit} verifier RPM" + (reported.HasValue ? $" • endpoint reports {reported}/min" : " • endpoint limit not reported") + $" • {rate.WaitingRequests} queued"; } }
    public string OverallNvidiaRateSummary { get { var rate = _overallNvidiaBudget.Snapshot; return $"Overall scheduler: {rate.RequestsInWindow} / {rate.HardLimit} RPM"; } }
    public string LastAiRouteSummary { get { var route = _routeTelemetry.LastRoute; return $"Route: {route.Tier} • Key Slot: {route.KeySlot} • {route.Reason}" + (route.Fallback.Length == 0 ? string.Empty : $" • Fallback: {route.Fallback}"); } }
    public string MemorySummary => _memorySnapshot.StoragePath.Length == 0
        ? "Persistent memory is initializing..."
        : $"{_memorySnapshot.Summary} • AI\\Memory\\borin-memory.json";
    public string KnowledgeSummary =>
        $"{_state.SubsystemStudies.Count} studies • {_memorySnapshot.DurableCount} durable memories • {_state.CraftsmanJournal.Count} journal entries";
    public string BorinCapabilitiesSummary => _guardianCapabilities?.StatusSummary
        ?? "Borin capabilities are initializing.";
    public string MemoryRecallSummary => _lastRecallSnapshot.RecallSummary;
    public string ProjectAwarenessSummary => BorinProjectAwareness.Summary;
    public string ReadOnlyAccessSummary => _lastProjectEvidence.Summary;
    public string ClassicTraceSummary => _lastClassicTrace.Summary;
    public string KnowledgeIndexSummary => _knowledgeIndexStatus.Summary;
    public string MajorMudKnowledgeSummary =>
        $"{_majorMudKnowledgeStatus.State} • {_majorMudKnowledgeStatus.Records:N0} records • " +
        $"{_majorMudKnowledgeStatus.Relationships:N0} relationships" +
        (_majorMudKnowledgeStatus.UpdatedUtc is null ? string.Empty : $" • updated {_majorMudKnowledgeStatus.UpdatedUtc:yyyy-MM-dd HH:mm}");
    public string MasteryEvaluationSummary => _masteryEvaluationSummary;
    public string MasteryEvaluationDetails => _masteryEvaluationDetails;
    public string VerifiedLearningSummary =>
        $"Runtime learning • {_verifiedLearningStatus.Observations:N0} observations • {_verifiedLearningStatus.Investigations:N0} investigations • {_verifiedLearningStatus.Conflicts:N0} conflicts • {_verifiedLearningStatus.Procedures:N0} verified procedures";
    public string AcademyMajorMudMastery => _academySnapshot?.MajorMudMastery is double value ? $"MajorMUD mastery: {value:F1}%" : "MajorMUD mastery: not yet measured";
    public string AcademyReforgedMastery => _academySnapshot?.ReforgedMastery is double value ? $"RealmGate mastery: {value:F1}%" : "RealmGate mastery: not yet measured";
    public string AcademyCounts => _academySnapshot is null ? "Academy is initializing..." : $"Verified facts: {_academySnapshot.VerifiedFacts:N0} • Open gaps: {_academySnapshot.OpenGaps:N0} • Conflicts: {_academySnapshot.Conflicts:N0} • Unverified claims: {_academySnapshot.UnverifiedClaims:N0}";
    public string AcademyActivity => _academySnapshot is null ? "No Academy activity." : $"Current training: {_academySnapshot.CurrentTraining} • Task: {_academySnapshot.CurrentTask} • Model: {_academySnapshot.CurrentModel} • Ultra escalations: {_academySnapshot.UltraEscalations}";
    public string AcademySubjectSummary => _academySnapshot is null ? string.Empty : string.Join(" • ", _academySnapshot.Subjects.Where(s => s.Attempted > 0).Select(s => $"{s.Domain}: {s.Subject} {s.MasteryPercent:F0}%"));
    public bool AcademyTrainWhileIdle { get => _academySnapshot?.TrainWhileIdle ?? false; set => _ = SetAcademyIdleAsync(value); }
    public int AcademyIdleRequestsPerMinute { get => _academySnapshot?.IdleRequestsPerMinute ?? 5; set => _ = SetAcademyIdleRateAsync(value); }
    public string KnowledgeRecallSummary => _lastKnowledgeEvidence.Summary;
    public string KnowledgeBootstrapSummary => _knowledgeBootstrapStatus.Summary;
    public string KnowledgeBootstrapRecallSummary => _lastKnowledgeBootstrap.Summary;
    public string EngineeringWorkspaceSummary => _engineeringWorkspaceStatus.Summary;
    public string EngineeringWorkspacePath => _engineeringWorkspaceStatus.WorkspacePath;
    public string EngineeringJobSummary => _engineeringJob.Summary;
    public string EngineeringJobStage => _engineeringJob.Exists ? $"{_engineeringJob.Id} • {_engineeringJob.Stage}" : "No active job";
    public string EngineeringJobRequest => _engineeringJob.Request.Length == 0 ? "No commission recorded." : _engineeringJob.Request;
    public string EngineeringJobFiles => _engineeringJob.ProposedFiles.Count == 0 ? "No proposed files yet." : string.Join(" • ", _engineeringJob.ProposedFiles);
    public string EngineeringJobArtifact => _engineeringJob.ArtifactPath.Length == 0 ? "No package created." : _engineeringJob.ArtifactPath;
    public bool CanIncorporateEngineeringJob => !IsBusy && _engineeringJob.CanIncorporate;
    public string EngineeringCorrectionSummary => _engineeringCorrectionStatus.Summary;
    public string EngineeringCorrectionAttempts => FormatCorrectionAttempts(_engineeringCorrectionStatus);
    public string PlayerSessionId { get => _playerSessionId; set => Set(ref _playerSessionId, value ?? string.Empty); }
    public string PlayerHostName { get => _playerHostName; set => Set(ref _playerHostName, value ?? string.Empty); }
    public int PlayerPort { get => _playerPort; set => Set(ref _playerPort, Math.Clamp(value, 1, 65535)); }
    public string PlayerLoginName { get => _playerLoginName; set => Set(ref _playerLoginName, value ?? string.Empty); }
    public string PlayerCharacterName { get => _playerCharacterName; set => Set(ref _playerCharacterName, value ?? string.Empty); }
    public string PlayerServerProfileName { get => _playerServerProfileName; set => Set(ref _playerServerProfileName, value ?? string.Empty); }
    public string PlayerWorldName { get => _playerWorldName; set => Set(ref _playerWorldName, value ?? string.Empty); }
    public string PlayerPrimaryTeacher { get => _playerPrimaryTeacher; set => Set(ref _playerPrimaryTeacher, value ?? string.Empty); }
    public string PlayerMentorAiName { get => _playerMentorAiName; set => Set(ref _playerMentorAiName, value ?? string.Empty); }
    public string PlayerCurrentGoal
    {
        get => _playerCurrentGoal;
        set
        {
            if (Set(ref _playerCurrentGoal, value ?? string.Empty))
                RaisePlayerCommandStates();
        }
    }
    public string PlayerCommandInput
    {
        get => _playerCommandInput;
        set
        {
            if (Set(ref _playerCommandInput, value ?? string.Empty))
                RaisePlayerCommandStates();
        }
    }
    public bool PlayerEnabled { get => _playerEnabled; set => Set(ref _playerEnabled, value); }
    public bool PlayerObservationEnabled { get => _playerObservationEnabled; set => Set(ref _playerObservationEnabled, value); }
    public bool PlayerConversationEnabled { get => _playerConversationEnabled; set => Set(ref _playerConversationEnabled, value); }
    public bool PlayerReflectionJournalEnabled { get => _playerReflectionJournalEnabled; set => Set(ref _playerReflectionJournalEnabled, value); }
    public bool PlayerAutomaticReflectionEnabled { get => _playerAutomaticReflectionEnabled; set => Set(ref _playerAutomaticReflectionEnabled, value); }
    public bool PlayerConversationLearningEnabled { get => _playerConversationLearningEnabled; set => Set(ref _playerConversationLearningEnabled, value); }
    public bool PlayerAllowSafeCombat { get => _playerAllowSafeCombat; set => Set(ref _playerAllowSafeCombat, value); }
    public string PlayerReflectionFocus { get => _playerReflectionFocus; set => Set(ref _playerReflectionFocus, value ?? string.Empty); }
    public string PlayerConversationReviewNote { get => _playerConversationReviewNote; set => Set(ref _playerConversationReviewNote, value ?? string.Empty); }
    public PlayViewModel PlayerTerminalViewModel => _playerTerminalViewModel;
    public string PlayerStatusSummary => _playerRuntimeStatus.Summary;
    public string PlayerSafetySummary => _playerRuntimeStatus.SafetySummary;
    public string PlayerStatusDetail => _playerRuntimeStatus.Detail;
    public string PlayerLoginStatusSummary => _playerLoginStatus.Summary;
    public string PlayerLoginStatusDetail => _playerLoginStatus.Detail;
    public string PlayerPasswordStatus => _playerLoginStatus.PasswordSaved
        ? "Password saved securely in Windows Credential Manager"
        : "Password not saved";
    public string PlayerDecisionSummary => _playerDecision.Summary;
    public string PlayerEnvironmentSummary => _playerResearch.CurrentGameState.EnvironmentSummary;
    public string PlayerPerceptionSummary => _playerResearch.CurrentGameState.PerceptionSummary;
    public string PlayerAutonomousPlaySummary => _playerRuntimeStatus.AutonomousPlayStatus;
    public string PlayerLastIncomingTelepath => _playerResearch.LastIncomingTelepathSummary;
    public bool PlayerCanPlayByHimself => !IsBusy &&
        !_playerResearch.IsAutonomousPlayActive &&
        !string.IsNullOrWhiteSpace(PlayerCurrentGoal) &&
        _playerResearch.CurrentGameState.CanAutonomousPlay;
    public bool PlayerCanStopAutonomousPlay => !IsBusy && _playerResearch.IsAutonomousPlayActive;
    public string PlayerLastConversationSummary { get => _playerLastConversationSummary; private set => Set(ref _playerLastConversationSummary, value); }
    public string PlayerLastEvidenceSummary { get => _playerLastEvidenceSummary; private set => Set(ref _playerLastEvidenceSummary, value); }
    public string PlayerJournalPath => string.IsNullOrWhiteSpace(_playerRuntimeStatus.JournalPath) ? "Parity journal not started." : _playerRuntimeStatus.JournalPath;
    public string PlayerReflectionJournalSummary => _playerJournalStatus.Summary;
    public string PlayerCurrentReflection => _playerJournalStatus.LastReflection;
    public string PlayerConversationLessonSummary => _playerJournalStatus.LastConversationLesson;
    public string PlayerSpeakerProfileSummary => _playerJournalStatus.LastSpeakerSummary;
    public string PlayerReflectionJournalPath => string.IsNullOrWhiteSpace(_playerJournalStatus.StoragePath) ? "Reflection journal not started." : _playerJournalStatus.StoragePath;
    public string CurrentTask { get => _currentTask; private set => Set(ref _currentTask, value); }
    public string TaskStage { get => _taskStage; private set => Set(ref _taskStage, value); }
    public int TaskProgress { get => _taskProgress; private set => Set(ref _taskProgress, value); }

    public ICommand SendCommand { get; }
    public ICommand ScanCommand { get; }
    public ICommand BuildCommand { get; }
    public ICommand TestCommand { get; }
    public ICommand ReadLogsCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand SaveSettingsCommand { get; }
    public ICommand ToolStatusCommand { get; }
    public ICommand CreateWorkspaceCommand { get; }
    public ICommand WorkspaceStatusCommand { get; }
    public ICommand ApplyProposedDiffCommand { get; }
    public ICommand BuildWorkspaceCommand { get; }
    public ICommand TestWorkspaceCommand { get; }
    public ICommand ShowDiffCommand { get; }
    public ICommand ApplyWorkspaceCommand { get; }
    public ICommand RollbackCommand { get; }
    public ICommand OpenSolutionCommand { get; }
    public ICommand DetectLmStudioCommand { get; }
    public ICommand TestBrainCommand { get; }
    public ICommand ChangeLightningKeyCommand { get; }
    public ICommand TestLightningCommand { get; }
    public ICommand TestSuperCommand { get; }
    public ICommand TestUltraCommand { get; }
    public ICommand LoadModelsCommand { get; }
    public ICommand ApplyBrainPerformancePresetCommand { get; }
    public ICommand AttachFileCommand { get; }
    public ICommand ClearAttachmentsCommand { get; }
    public ICommand ChoosePortraitCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand StudyHostCommand { get; }
    public ICommand StudyRepositoryCommand { get; }
    public ICommand ImplementAttachedCommand { get; }
    public ICommand ShowMemoriesCommand { get; }
    public ICommand MemoryHelpCommand { get; }
    public ICommand ShowRecallCommand { get; }
    public ICommand ProjectAwarenessCommand { get; }
    public ICommand ShowEvidenceCommand { get; }
    public ICommand ReadOnlyAccessHelpCommand { get; }
    public ICommand ShowClassicTraceCommand { get; }
    public ICommand ClassicTraceHelpCommand { get; }
    public ICommand LearnReforgedCommand { get; }
    public ICommand LearnChangedCommand { get; }
    public ICommand LearnStatusCommand { get; }
    public ICommand KnowledgeHelpCommand { get; }
    public ICommand RunMasteryTestCommand { get; }
    public ICommand ViewKnowledgeConflictsCommand { get; }
    public ICommand ViewKnowledgeInvestigationsCommand { get; }
    public ICommand AcademyTrainCommand { get; }
    public ICommand AcademyTestCommand { get; }
    public ICommand AcademyGapsCommand { get; }
    public ICommand AcademyReplayCommand { get; }
    public ICommand DraftEngineeringCommand { get; }
    public ICommand PackageEngineeringDraftCommand { get; }
    public ICommand EngineeringHelpCommand { get; }
    public ICommand StartEngineeringJobCommand { get; }
    public ICommand ShowEngineeringJobCommand { get; }
    public ICommand ListEngineeringJobsCommand { get; }
    public ICommand ShowEngineeringChangesCommand { get; }
    public ICommand ApproveEngineeringJobCommand { get; }
    public ICommand RejectEngineeringJobCommand { get; }
    public ICommand BuildTestEngineeringJobCommand { get; }
    public ICommand PackageEngineeringJobCommand { get; }
    public ICommand IncorporateEngineeringJobCommand { get; }
    public ICommand CorrectEngineeringJobCommand { get; }
    public ICommand ShowCorrectionAttemptsCommand { get; }
    public ICommand StopEngineeringCorrectionCommand { get; }
    public ICommand SavePlayerProfileCommand { get; }
    public ICommand ConnectPlayerCommand { get; }
    public ICommand DisconnectPlayerCommand { get; }
    public ICommand TogglePlayerObservationCommand { get; }
    public ICommand TogglePlayerConversationCommand { get; }
    public ICommand TogglePlayerJournalCommand { get; }
    public ICommand ToggleAutomaticReflectionCommand { get; }
    public ICommand ToggleConversationLearningCommand { get; }
    public ICommand WritePlayerReflectionCommand { get; }
    public ICommand MarkConversationHelpfulCommand { get; }
    public ICommand MarkConversationAwkwardCommand { get; }
    public ICommand MarkConversationIncorrectCommand { get; }
    public ICommand ShowPlayerJournalCommand { get; }
    public ICommand ProposePlayerActionCommand { get; }
    public ICommand ApprovePlayerActionCommand { get; }
    public ICommand SendSupervisedPlayerCommand { get; }
    public ICommand PausePlayerCommand { get; }
    public ICommand ResumePlayerCommand { get; }
    public ICommand EmergencyStopPlayerCommand { get; }
    public ICommand PlayerHelpCommand { get; }
    public ICommand SavePlayerAssignmentCommand { get; }
    public ICommand StartAutonomousPlayerCommand { get; }
    public ICommand StopAutonomousPlayerCommand { get; }
    public ICommand CapabilityStatusCommand { get; }
    public ICommand CapabilityRealmCommand { get; }
    public ICommand CapabilityAnalysisCommand { get; }
    public ICommand CapabilityDiagnosticsCommand { get; }
    public ICommand CapabilityStopCommand { get; }

    public void AttachGuardianCapabilities(IBorinCapabilities guardianCapabilities)
    {
        _guardianCapabilities = guardianCapabilities ?? throw new ArgumentNullException(nameof(guardianCapabilities));
        Raise(nameof(BorinCapabilitiesSummary));
    }

    public BorinForgeViewModel(
        IForgeStateStore store,
        IForgeRepositoryService repository,
        IBrain brain,
        IBorinMemoryStore memoryStore,
        IBorinReadOnlyAccess readOnlyAccess,
        IBorinClassicTraceAccess classicTraceAccess,
        IBorinKnowledgeIndex knowledgeIndex,
        IBorinReforgedKnowledgeBootstrap knowledgeBootstrap,
        IBorinEngineeringWorkspace engineeringWorkspace,
        IBorinEngineeringOrchestrator engineeringOrchestrator,
        IBorinEngineeringCorrectionLoop engineeringCorrectionLoop,
        IBorinPlayerResearch playerResearch,
        IBorinVerifiedKnowledgeLearning verifiedKnowledgeLearning,
        IBorinMajorMudKnowledgeCatalog majorMudKnowledgeCatalog,
        IBorinAcademy academy,
        IBorinNaturalInitiativeStore naturalInitiative,
        PlayViewModel playerTerminalViewModel,
        IForgeDeveloperTools tools,
        IForgeBrainManager brainManager,
        INvidiaNimCredentialStore nvidiaCredentials,
        IAiProvider lightningProvider,
        IAiProvider superProvider,
        IAiProvider ultraProvider,
        IAiRateBudget lightningRateBudget,
        IAiRateBudget superRateBudget,
        IAiRateBudget ultraRateBudget,
        IAiRateBudget overallNvidiaBudget,
        IBorinAiRouteTelemetry routeTelemetry,
        INvidiaNimEndpointTelemetry lightningEndpointTelemetry,
        INvidiaNimEndpointTelemetry superEndpointTelemetry,
        INvidiaNimEndpointTelemetry ultraEndpointTelemetry,
        BorinAiRoutingSettings aiRoutingSettings,
        LocalGgufBrain localBrain)
    {
        _store = store;
        _repository = repository;
        _brain = brain;
        _memoryStore = memoryStore;
        _readOnlyAccess = readOnlyAccess;
        _classicTraceAccess = classicTraceAccess;
        _knowledgeIndex = knowledgeIndex;
        _knowledgeBootstrap = knowledgeBootstrap;
        _engineeringWorkspace = engineeringWorkspace;
        _engineeringOrchestrator = engineeringOrchestrator;
        _engineeringCorrectionLoop = engineeringCorrectionLoop;
        _playerResearch = playerResearch;
        _verifiedKnowledgeLearning = verifiedKnowledgeLearning;
        _majorMudKnowledgeCatalog = majorMudKnowledgeCatalog;
        _academy = academy;
        _naturalInitiative = naturalInitiative ?? throw new ArgumentNullException(nameof(naturalInitiative));
        _playerTerminalViewModel = playerTerminalViewModel;
        _playerResearch.StatusChanged += OnPlayerStatusChanged;
        _playerResearch.LoginStatusChanged += OnPlayerLoginStatusChanged;
        _playerResearch.ConversationObserved += OnPlayerConversationObserved;
        _playerResearch.EvidenceRecorded += OnPlayerEvidenceRecorded;
        _playerResearch.JournalChanged += OnPlayerJournalChanged;
        _knowledgeIndex.StatusChanged += OnKnowledgeIndexStatusChanged;
        _tools = tools;
        _nvidiaCredentials = nvidiaCredentials;
        _lightningProvider = lightningProvider;
        _superProvider = superProvider;
        _ultraProvider = ultraProvider;
        _lightningRateBudget = lightningRateBudget;
        _superRateBudget = superRateBudget;
        _ultraRateBudget = ultraRateBudget;
        _overallNvidiaBudget = overallNvidiaBudget;
        _routeTelemetry = routeTelemetry;
        _lightningEndpointTelemetry = lightningEndpointTelemetry;
        _superEndpointTelemetry = superEndpointTelemetry;
        _ultraEndpointTelemetry = ultraEndpointTelemetry;
        _aiRoutingSettings = aiRoutingSettings;
        _localBrain = localBrain;
        if (_localBrain is not null)
            _localBrain.StatusChanged += OnLocalBrainStatusChanged;
        _ = brainManager; // Retained for compatibility with existing app composition; Borin no longer uses LM Studio.
        SendCommand = new AsyncRelayCommand(SendAsync, () => !IsBusy && !string.IsNullOrWhiteSpace(Input));
        CapabilityStatusCommand = new AsyncRelayCommand(() => RunBorinCapabilityButtonAsync("Show all Borin capability status"), () => !IsBusy);
        CapabilityRealmCommand = new AsyncRelayCommand(() => RunBorinCapabilityButtonAsync("Check the realm health"), () => !IsBusy);
        CapabilityAnalysisCommand = new AsyncRelayCommand(() => RunBorinCapabilityButtonAsync("Analyze progression"), () => !IsBusy);
        CapabilityDiagnosticsCommand = new AsyncRelayCommand(() => RunBorinCapabilityButtonAsync("Run the capability self-test"), () => !IsBusy);
        CapabilityStopCommand = new AsyncRelayCommand(() => RunBorinCapabilityButtonAsync("Stop all capabilities"), () => !IsBusy);
        ScanCommand = new AsyncRelayCommand(ScanAsync, () => !IsBusy);
        BuildCommand = new AsyncRelayCommand(() => QuickAsync("Build the app"), () => !IsBusy);
        TestCommand = new AsyncRelayCommand(() => QuickAsync("Run tests"), () => !IsBusy);
        ReadLogsCommand = new AsyncRelayCommand(() => QuickAsync("Read the latest logs"), () => !IsBusy);
        ClearCommand = new AsyncRelayCommand(ClearAsync, () => !IsBusy);
        SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync, () => !IsBusy);
        ToolStatusCommand = new AsyncRelayCommand(() => ToolAsync("Inspecting the tool rack...", () => _tools.InspectToolsAsync(Configuration.RepositoryPath)), () => !IsBusy);
        CreateWorkspaceCommand = new AsyncRelayCommand(() => RunEngineeringWorkspaceCommandAsync("create"), () => !IsBusy);
        WorkspaceStatusCommand = new AsyncRelayCommand(() => RunEngineeringWorkspaceCommandAsync("status"), () => !IsBusy);
        ApplyProposedDiffCommand = new AsyncRelayCommand(() => RunEngineeringWorkspaceCommandAsync("apply"), () => !IsBusy);
        BuildWorkspaceCommand = new AsyncRelayCommand(() => RunEngineeringWorkspaceCommandAsync("build"), () => !IsBusy);
        TestWorkspaceCommand = new AsyncRelayCommand(() => RunEngineeringWorkspaceCommandAsync("test"), () => !IsBusy);
        ShowDiffCommand = new AsyncRelayCommand(() => RunEngineeringWorkspaceCommandAsync("diff"), () => !IsBusy);
        ApplyWorkspaceCommand = new AsyncRelayCommand(() => RunEngineeringWorkspaceCommandAsync("package"), () => !IsBusy);
        RollbackCommand = new AsyncRelayCommand(() => QuickAsync("/engineer-help"), () => !IsBusy);
        OpenSolutionCommand = new AsyncRelayCommand(() => ToolAsync("Opening the workshop plans...", () => _tools.OpenSolutionAsync(_state)), () => !IsBusy);
        LoadModelsCommand = new AsyncRelayCommand(LoadModelsAsync, () => !IsBusy);
        DetectLmStudioCommand = LoadModelsCommand;
        TestBrainCommand = new AsyncRelayCommand(TestBrainAsync, () => !IsBusy);
        ChangeLightningKeyCommand = new AsyncRelayCommand(ChangeNvidiaKeyAsync, () => !IsBusy);
        TestLightningCommand = new AsyncRelayCommand(() => TestNvidiaAsync(NvidiaNimKeySlot.Lightning), () => !IsBusy);
        TestSuperCommand = new AsyncRelayCommand(() => TestNvidiaAsync(NvidiaNimKeySlot.Shared), () => !IsBusy);
        TestUltraCommand = new AsyncRelayCommand(() => TestNvidiaAsync(NvidiaNimKeySlot.Ultra), () => !IsBusy);
        ApplyBrainPerformancePresetCommand = new AsyncRelayCommand(ApplyBrainPerformancePresetAsync, () => !IsBusy);
        AttachFileCommand = new AsyncRelayCommand(AttachFileAsync, () => !IsBusy);
        ClearAttachmentsCommand = new AsyncRelayCommand(ClearAttachmentsAsync, () => !IsBusy && Attachments.Count > 0);
        ChoosePortraitCommand = new AsyncRelayCommand(ChoosePortraitAsync, () => !IsBusy);
        CancelCommand = new RelayCommand(CancelActiveRequest, () => IsBusy);
        StudyHostCommand = new AsyncRelayCommand(() => QuickAsync("Audit the current local Host and Server runtime in the repository. Use fresh read-only source evidence, current reports, and exact paths/line ranges. The external player connection is unrelated to this local audit."), () => !IsBusy);
        StudyRepositoryCommand = new AsyncRelayCommand(() => QuickAsync("/learn realmgate"), () => !IsBusy);
        ImplementAttachedCommand = new AsyncRelayCommand(() => QuickAsync("/engineer-help"), () => !IsBusy);
        ShowMemoriesCommand = new AsyncRelayCommand(() => QuickAsync("/memories"), () => !IsBusy);
        MemoryHelpCommand = new AsyncRelayCommand(() => QuickAsync("/memory-help"), () => !IsBusy);
        ShowRecallCommand = new AsyncRelayCommand(() => QuickAsync("/recall"), () => !IsBusy);
        ProjectAwarenessCommand = new AsyncRelayCommand(() => QuickAsync("/project-rules"), () => !IsBusy);
        ShowEvidenceCommand = new AsyncRelayCommand(() => QuickAsync("/evidence"), () => !IsBusy);
        ReadOnlyAccessHelpCommand = new AsyncRelayCommand(() => QuickAsync("/access-help"), () => !IsBusy);
        ShowClassicTraceCommand = new AsyncRelayCommand(() => QuickAsync("/trace-last"), () => !IsBusy);
        ClassicTraceHelpCommand = new AsyncRelayCommand(() => QuickAsync("/trace-help"), () => !IsBusy);
        LearnReforgedCommand = new AsyncRelayCommand(() => QuickAsync("/learn realmgate"), () => !IsBusy);
        LearnChangedCommand = new AsyncRelayCommand(() => QuickAsync("/learn changed"), () => !IsBusy);
        LearnStatusCommand = new AsyncRelayCommand(() => QuickAsync("/learn status"), () => !IsBusy);
        KnowledgeHelpCommand = new AsyncRelayCommand(() => QuickAsync("/knowledge-help"), () => !IsBusy);
        RunMasteryTestCommand = new AsyncRelayCommand(() => QuickAsync("/knowledge mastery-test"), () => !IsBusy);
        ViewKnowledgeConflictsCommand = new AsyncRelayCommand(() => QuickAsync("/knowledge conflicts"), () => !IsBusy);
        ViewKnowledgeInvestigationsCommand = new AsyncRelayCommand(() => QuickAsync("/knowledge investigations"), () => !IsBusy);
        AcademyTrainCommand = new AsyncRelayCommand(() => RunAcademyAsync(false), () => !IsBusy);
        AcademyTestCommand = new AsyncRelayCommand(() => RunAcademyAsync(true), () => !IsBusy);
        AcademyGapsCommand = new AsyncRelayCommand(InvestigateAcademyGapAsync, () => !IsBusy);
        AcademyReplayCommand = new AsyncRelayCommand(ReplayAcademyHistoryAsync, () => !IsBusy);
        DraftEngineeringCommand = new AsyncRelayCommand(DraftEngineeringFromInputAsync, () => !IsBusy && !string.IsNullOrWhiteSpace(Input));
        PackageEngineeringDraftCommand = new AsyncRelayCommand(() => RunEngineeringWorkspaceCommandAsync("package"), () => !IsBusy);
        EngineeringHelpCommand = new AsyncRelayCommand(() => QuickAsync("/job help"), () => !IsBusy);
        StartEngineeringJobCommand = new AsyncRelayCommand(StartEngineeringJobFromInputAsync, () => !IsBusy && !string.IsNullOrWhiteSpace(Input));
        ShowEngineeringJobCommand = new AsyncRelayCommand(() => QuickAsync("/job status"), () => !IsBusy);
        ListEngineeringJobsCommand = new AsyncRelayCommand(() => QuickAsync("/job list"), () => !IsBusy);
        ShowEngineeringChangesCommand = new AsyncRelayCommand(() => QuickAsync("/job changes"), () => !IsBusy);
        ApproveEngineeringJobCommand = new AsyncRelayCommand(() => QuickAsync("/job approve"), () => !IsBusy);
        RejectEngineeringJobCommand = new AsyncRelayCommand(() => QuickAsync("/job reject"), () => !IsBusy);
        BuildTestEngineeringJobCommand = new AsyncRelayCommand(() => QuickAsync("/job build-test"), () => !IsBusy);
        PackageEngineeringJobCommand = new AsyncRelayCommand(() => QuickAsync("/job package"), () => !IsBusy);
        IncorporateEngineeringJobCommand = new AsyncRelayCommand(IncorporateEngineeringJobAsync, () => CanIncorporateEngineeringJob);
        CorrectEngineeringJobCommand = new AsyncRelayCommand(() => QuickAsync("/correct-build"), () => !IsBusy);
        ShowCorrectionAttemptsCommand = new AsyncRelayCommand(() => QuickAsync("/show-attempts"), () => !IsBusy);
        StopEngineeringCorrectionCommand = new AsyncRelayCommand(() => QuickAsync("/stop-correction"), () => !IsBusy);
        SavePlayerProfileCommand = new AsyncRelayCommand(SavePlayerProfileAsync, () => !IsBusy);
        ConnectPlayerCommand = new AsyncRelayCommand(ConnectPlayerAsync, () => !IsBusy);
        DisconnectPlayerCommand = new AsyncRelayCommand(DisconnectPlayerAsync, () => !IsBusy);
        TogglePlayerObservationCommand = new AsyncRelayCommand(TogglePlayerObservationAsync, () => !IsBusy);
        TogglePlayerConversationCommand = new AsyncRelayCommand(TogglePlayerConversationAsync, () => !IsBusy);
        TogglePlayerJournalCommand = new AsyncRelayCommand(TogglePlayerJournalAsync, () => !IsBusy);
        ToggleAutomaticReflectionCommand = new AsyncRelayCommand(ToggleAutomaticReflectionAsync, () => !IsBusy);
        ToggleConversationLearningCommand = new AsyncRelayCommand(ToggleConversationLearningAsync, () => !IsBusy);
        WritePlayerReflectionCommand = new AsyncRelayCommand(WritePlayerReflectionAsync, () => !IsBusy);
        MarkConversationHelpfulCommand = new AsyncRelayCommand(() => ReviewPlayerConversationAsync("Helpful"), () => !IsBusy);
        MarkConversationAwkwardCommand = new AsyncRelayCommand(() => ReviewPlayerConversationAsync("Awkward"), () => !IsBusy);
        MarkConversationIncorrectCommand = new AsyncRelayCommand(() => ReviewPlayerConversationAsync("Incorrect"), () => !IsBusy);
        ShowPlayerJournalCommand = new AsyncRelayCommand(ShowPlayerJournalAsync, () => !IsBusy);
        ProposePlayerActionCommand = new AsyncRelayCommand(ProposePlayerActionAsync, () => !IsBusy);
        ApprovePlayerActionCommand = new AsyncRelayCommand(ApprovePlayerActionAsync, () => !IsBusy && !string.IsNullOrWhiteSpace(_playerDecision.ProposedAction));
        SendSupervisedPlayerCommand = new AsyncRelayCommand(SendSupervisedPlayerCommandAsync, () => !IsBusy && !string.IsNullOrWhiteSpace(PlayerCommandInput));
        PausePlayerCommand = new AsyncRelayCommand(PausePlayerAsync, () => !IsBusy);
        ResumePlayerCommand = new AsyncRelayCommand(ResumePlayerAsync, () => !IsBusy);
        EmergencyStopPlayerCommand = new AsyncRelayCommand(EmergencyStopPlayerAsync, () => !IsBusy);
        PlayerHelpCommand = new AsyncRelayCommand(PlayerHelpAsync, () => !IsBusy);
        SavePlayerAssignmentCommand = new AsyncRelayCommand(SavePlayerAssignmentAsync, () => !IsBusy && !string.IsNullOrWhiteSpace(PlayerCurrentGoal));
        StartAutonomousPlayerCommand = new AsyncRelayCommand(StartAutonomousPlayerAsync, () => PlayerCanPlayByHimself);
        StopAutonomousPlayerCommand = new AsyncRelayCommand(StopAutonomousPlayerAsync, () => PlayerCanStopAutonomousPlay);
    }

    public async Task InitializeAsync()
    {
        _state = await _store.LoadAsync();
        if (string.IsNullOrWhiteSpace(_state.Configuration.RepositoryPath))
            _state.Configuration.RepositoryPath = _repository.DiscoverRepositoryRoot();
        if (string.IsNullOrWhiteSpace(_state.Configuration.PortraitPath))
            _state.Configuration.PortraitPath = "pack://application:,,,/Assets/Borin/BorinIronbeard.png";

        Configuration.ProviderName = Configuration.BrainMode == BorinBrainMode.LocalOnly ? "RealmGate Direct GGUF" : Configuration.BrainMode == BorinBrainMode.NvidiaOnly ? "NVIDIA NIM" : "Borin Hybrid AI";
        Configuration.ApiEndpoint = "https://integrate.api.nvidia.com/v1";
        Configuration.ModelsEndpoint = string.Empty;
        Configuration.AutoReconnectMasterBrain = false;
        Configuration.UseMasterBrainForGeneralConversation = true;
        Configuration.PermissionMode = ForgePermissionMode.Validate;
        Configuration.RequireApprovalBeforeMainApply = true;
        Configuration.AllowWorkspaceEdits = true;
        Configuration.AllowGitCommands = false;
        Configuration.AllowBuildCommands = true;
        Configuration.AllowTestCommands = true;
        Configuration.AllowLocalProgramLaunch = true;
        _aiRoutingSettings.Mode = Configuration.BrainMode;
        _aiRoutingSettings.FallBackToLocal = true;
        _aiRoutingSettings.AutomaticallyEscalateDifficultTasks = Configuration.AutomaticallyEscalateDifficultTasks;
        _aiRoutingSettings.VerifyCriticalTasks = Configuration.VerifyCriticalTasks;
        var keyConfigured = await _nvidiaCredentials.IsConfiguredAsync();
        LightningConnectionStatus = keyConfigured ? "Lightning: shared key configured" : "Lightning: key not configured";
        SuperConnectionStatus = keyConfigured ? "Super: shared key configured" : "Super: key not configured";
        UltraConnectionStatus = keyConfigured ? "Ultra: shared key configured" : "Ultra: key not configured";

        _playerSettings = await _playerResearch.LoadSettingsAsync(Configuration.RepositoryPath);
        ApplyPlayerSettings(_playerSettings);
        await _playerResearch.ConfigureAsync(Configuration.RepositoryPath, _playerSettings);
        ApplyPlayerStatus(_playerResearch.CurrentStatus);
        _playerLoginStatus = _playerResearch.CurrentLoginStatus;
        _playerDecision = _playerResearch.CurrentDecision;
        _playerJournalStatus = _playerResearch.CurrentJournalStatus;
        RaisePlayerProperties();
        BrainStatus = _localBrain?.CurrentStatus.Display ?? "Local GGUF brain unavailable";
        if (_localBrain is not null)
        {
            try
            {
                var brainContext = BrainContext.ForBorin(
                    "Read local brain performance settings.",
                    _state,
                    _snapshot,
                    "Performance settings");
                var settings = await _localBrain.GetSettingsAsync(brainContext);
                SelectedBrainPerformancePreset = settings.PerformancePreset;
                BrainPerformanceSummary =
                    $"{settings.PerformancePreset} • context {settings.ContextSize} • max reply {settings.MaxTokens} tokens • " +
                    $"{(settings.GpuLayerCount < 0 ? "maximum GPU offload" : $"{settings.GpuLayerCount} GPU layers")}";
            }
            catch (Exception ex)
            {
                BrainPerformanceSummary = $"Performance settings unavailable: {ex.Message}";
            }
        }

        Messages.Clear();
        foreach (var message in _state.Messages)
        {
            if (!message.Role.Equals("You", StringComparison.OrdinalIgnoreCase))
                message.Text = CleanStoredBorinReply(message.Text);
            Messages.Add(message);
        }
        var latestForgeHuman = _state.Messages
            .Where(message => message.Role.Equals("You", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(message => message.Timestamp)
            .FirstOrDefault();
        if (latestForgeHuman is not null)
        {
            await _naturalInitiative.RecordHumanActivityAsync(
                BorinNaturalInitiativeSurfaces.Forge,
                "William",
                latestForgeHuman.Timestamp);
        }
        Attachments.Clear();
        foreach (var attachment in _state.Attachments) Attachments.Add(attachment);
        await RefreshMemorySnapshotAsync(string.Empty);
        _lastRecallSnapshot = BorinMemorySnapshot.Empty;
        try
        {
            _knowledgeBootstrapStatus = await _knowledgeBootstrap.InitializeAsync(Configuration.RepositoryPath);
            _lastKnowledgeBootstrap = await _knowledgeBootstrap.PrepareAsync(
                Configuration.RepositoryPath,
                "RealmGate project architecture and authority orientation");
        }
        catch (Exception exception)
        {
            _knowledgeBootstrapStatus = new BorinReforgedBootstrapStatus(
                "Faulted",
                exception.Message,
                string.Empty,
                string.Empty,
                string.Empty,
                0,
                false,
                null);
            _lastKnowledgeBootstrap = BorinReforgedBootstrapSnapshot.Empty(Configuration.RepositoryPath);
        }
        Raise(nameof(KnowledgeBootstrapSummary));
        Raise(nameof(KnowledgeBootstrapRecallSummary));
        try
        {
            _knowledgeIndexStatus = await _knowledgeIndex.GetStatusAsync(Configuration.RepositoryPath);
            if (_knowledgeIndex is IBorinKnowledgeCore core)
                _majorMudKnowledgeStatus = await core.GetMajorMudStatusAsync(Configuration.RepositoryPath);
            _verifiedLearningStatus = await _verifiedKnowledgeLearning.GetStatusAsync();
            _academySnapshot = await _academy.GetSnapshotAsync(Configuration.RepositoryPath);
        }
        catch (Exception exception)
        {
            _knowledgeIndexStatus = new BorinKnowledgeIndexStatus(
                "Faulted",
                exception.Message,
                string.Empty,
                0, 0, 0, 0, 0, 0, 1,
                string.Empty,
                null);
        }
        try
        {
            _engineeringWorkspaceStatus = await _engineeringWorkspace.GetStatusAsync(Configuration.RepositoryPath);
        }
        catch (Exception exception)
        {
            _engineeringWorkspaceStatus = new BorinEngineeringWorkspaceStatus(
                "Faulted", string.Empty, false, 0, exception.Message);
        }
        Raise(nameof(EngineeringWorkspaceSummary));
        Raise(nameof(EngineeringWorkspacePath));
        try
        {
            _engineeringJob = await _engineeringOrchestrator.GetCurrentAsync(Configuration.RepositoryPath);
        }
        catch (Exception exception)
        {
            _engineeringJob = BorinEngineeringJobSnapshot.None(Configuration.RepositoryPath) with
            {
                Summary = "Engineering job ledger could not be loaded: " + exception.Message
            };
        }
        RaiseEngineeringJobProperties();
        try
        {
            _engineeringCorrectionStatus = _engineeringJob.Exists
                ? await _engineeringCorrectionLoop.GetStatusAsync(Configuration.RepositoryPath, _engineeringJob.Id)
                : BorinEngineeringCorrectionStatus.Empty(string.Empty);
        }
        catch (Exception exception)
        {
            _engineeringCorrectionStatus = new BorinEngineeringCorrectionStatus(
                _engineeringJob.Id,
                0,
                3,
                true,
                "Correction ledger could not be loaded: " + exception.Message,
                Array.Empty<BorinEngineeringCorrectionAttempt>());
        }
        RaiseCorrectionProperties();

        Raise(nameof(Configuration)); Raise(nameof(ModeSummary)); Raise(nameof(KnowledgeSummary)); Raise(nameof(MemorySummary));
        Raise(nameof(MemoryRecallSummary)); Raise(nameof(ProjectAwarenessSummary)); Raise(nameof(ReadOnlyAccessSummary)); Raise(nameof(ClassicTraceSummary));
        Raise(nameof(KnowledgeIndexSummary)); Raise(nameof(KnowledgeRecallSummary));
        Raise(nameof(MajorMudKnowledgeSummary));
        Raise(nameof(VerifiedLearningSummary));
        RaiseAcademyProperties();
        Raise(nameof(KnowledgeBootstrapSummary)); Raise(nameof(KnowledgeBootstrapRecallSummary));
        Status = "Borin is ready. Scan the repository when a fresh map is needed.";
        Raise(nameof(RepositorySummary));
        await PersistAsync();
        StartForgeNaturalInitiativeMonitor();
        StartAcademyIdleMonitor();
        RaiseCommandStates();
    }

    private async Task<bool> TryHandleHeadlessTrainingAsync(string request)
    {
        if (!_headlessTraining.IsTrainingRequest(request)) return false;
        Status = "Borin headless trainer";
        var response = await _headlessTraining.HandleAsync(Configuration.RepositoryPath, request, CancellationToken.None);
        Add("Borin", response);
        await _memoryStore.RecordTurnAsync(request, response, CancellationToken.None);
        await PersistAsync();
        return true;
    }

    private async Task SendAsync()
    {
        var request = Input.Trim();
        Input = string.Empty;
        CancelForgeNaturalInitiative();
        await _naturalInitiative.RecordHumanActivityAsync(
            BorinNaturalInitiativeSurfaces.Forge,
            "William",
            DateTimeOffset.Now);
        if (await TryHandleAuditReportCommandAsync(request) ||
            await TryHandleEngineeringOrchestrationAsync(request) ||
            await TryHandleEngineeringCommandAsync(request) ||
            await TryHandleBootstrapCommandAsync(request) ||
            await TryHandleKnowledgeCommandAsync(request) ||
            await TryHandleClassicTraceCommandAsync(request) ||
            await TryHandleReadOnlyAccessCommandAsync(request) ||
            await TryHandleMemoryCommandAsync(request))
        {
            RaiseCommandStates();
            return;
        }

        await CaptureAcademyCorrectionCandidateAsync(request);

        Add("You", request);
        if (await TryHandleHeadlessTrainingAsync(request))
        {
            RaiseCommandStates();
            return;
        }
        if (await TryHandleGuardianCapabilityAsync(request))
        {
            RaiseCommandStates();
            return;
        }
        if (await TryHandleForgeConnectionActionAsync(request))
        {
            RaiseCommandStates();
            return;
        }
        if (await TryHandleVerifiedForgePlayerActionAsync(request))
        {
            RaiseCommandStates();
            return;
        }
        if (await TryHandleAutonomousPlayChatAsync(request))
        {
            RaiseCommandStates();
            return;
        }

        await RefreshVerifiedPerceptionForQuestionAsync(request);

        if (TryBuildVerifiedPlayerStateAnswer(request, out var verifiedPlayerAnswer))
        {
            Add("Borin", verifiedPlayerAnswer);
            await _memoryStore.RecordTurnAsync(request, verifiedPlayerAnswer, CancellationToken.None);
            await RefreshMemorySnapshotAsync(request, CancellationToken.None);
            Status = "Answered from verified live Client state.";
            await PersistAsync();
            RaiseCommandStates();
            return;
        }

        IsBusy = true;
        _activeRequest?.Dispose();
        _activeRequest = new CancellationTokenSource();
        CurrentTask = request;
        TaskStage = "Recalling relevant memory";
        TaskProgress = 15;
        Status = "Borin is checking his memory ledger...";
        RaiseCommandStates();
        var intent = BorinIntentRouter.Classify(request);
        var localResearchHold = intent == BorinRequestIntent.RepositoryResearch;
        if (localResearchHold)
        {
            try
            {
                _playerResearch.SetLocalResearchBusy(true);
            }
            catch
            {
                // Repository research still proceeds if the optional player bridge is unavailable.
            }
        }
        try
        {
            await CaptureDurableMemoryAsync(request, _activeRequest.Token);
            await RefreshMemorySnapshotAsync(request, _activeRequest.Token);
            _lastRecallSnapshot = _memorySnapshot;
            Raise(nameof(MemoryRecallSummary));

            if (intent == BorinRequestIntent.Conversation)
            {
                TaskStage = "Continuing conversation";
                TaskProgress = 55;
                Status = "Borin is continuing the conversation from memory and living-character state...";
                _lastKnowledgeBootstrap = BorinReforgedBootstrapSnapshot.Empty(Configuration.RepositoryPath);
                _lastKnowledgeEvidence = BorinKnowledgeIndexSnapshot.Empty(
                    Configuration.RepositoryPath,
                    request,
                    "Repository indexing was intentionally skipped for this ordinary conversation.");
                _lastProjectEvidence = BorinReadOnlyEvidenceSnapshot.Empty(
                    Configuration.RepositoryPath,
                    "Repository evidence gathering was intentionally skipped for this ordinary conversation.");
                _lastClassicTrace = BorinClassicTraceSnapshot.Empty(
                    Configuration.RepositoryPath,
                    request,
                    "Classic tracing was intentionally skipped for this ordinary conversation.");
                Raise(nameof(KnowledgeBootstrapRecallSummary));
                Raise(nameof(KnowledgeRecallSummary));
                Raise(nameof(ReadOnlyAccessSummary));
                Raise(nameof(ClassicTraceSummary));
            }
            else
            {
                TaskStage = "Loading compact RealmGate orientation";
                TaskProgress = 20;
                Status = "Borin is selecting the smallest authoritative subsystem cards for this question...";
                _lastKnowledgeBootstrap = await _knowledgeBootstrap.PrepareAsync(
                    Configuration.RepositoryPath,
                    request,
                    _activeRequest.Token);
                _knowledgeBootstrapStatus = _lastKnowledgeBootstrap.Status;
                Raise(nameof(KnowledgeBootstrapSummary));
                Raise(nameof(KnowledgeBootstrapRecallSummary));

                TaskStage = "Retrieving indexed RealmGate knowledge";
                TaskProgress = 24;
                Status = intent == BorinRequestIntent.RepositoryResearch
                    ? "Borin is retrieving current local project evidence and the most relevant indexed chunks..."
                    : "Borin is retrieving only the most relevant indexed project chunks...";
                _lastKnowledgeEvidence = await _knowledgeIndex.SearchAsync(
                    Configuration.RepositoryPath,
                    request,
                    maximumResults: intent == BorinRequestIntent.RepositoryResearch ? 20 : 8,
                    _activeRequest.Token);
                Raise(nameof(KnowledgeRecallSummary));

                var traceRequested = _classicTraceAccess.ShouldTrace(request) ||
                                     (intent == BorinRequestIntent.RepositoryResearch && BorinIntentRouter.IsParityResearch(request));
                TaskStage = "Gathering read-only project evidence";
                TaskProgress = 32;
                Status = "Borin is reading approved project files without changing them...";
                _lastProjectEvidence = await _readOnlyAccess.GatherAsync(
                    Configuration.RepositoryPath,
                    request,
                    maximumResults: intent == BorinRequestIntent.RepositoryResearch
                        ? 30
                        : (traceRequested ? 8 : 12),
                    _activeRequest.Token);
                Raise(nameof(ReadOnlyAccessSummary));

                if (traceRequested)
                {
                    TaskStage = "Tracing Classic provenance chain";
                    TaskProgress = 45;
                    Status = "Borin is tracing decoded evidence through runtime and parity gates...";
                    _lastClassicTrace = await _classicTraceAccess.TraceAsync(
                        Configuration.RepositoryPath,
                        request,
                        maximumResultsPerStage: intent == BorinRequestIntent.RepositoryResearch ? 3 : 2,
                        _activeRequest.Token);
                }
                else
                {
                    _lastClassicTrace = BorinClassicTraceSnapshot.Empty(
                        Configuration.RepositoryPath,
                        request,
                        "This turn did not request a structured Classic trace. Use /trace <question> when needed.");
                }
                Raise(nameof(ClassicTraceSummary));
            }

            TaskStage = "Routing Borin's hybrid brain";
            TaskProgress = 62;
            BrainStatus = "Selecting Local, Lightning, or Ultra from current task metadata...";
            var brainContext = BrainContext.ForBorin(
                    BuildIntentAwareBrainRequest(request, intent),
                    _state,
                    _snapshot,
                    purpose: BorinIntentRouter.GetPurpose(intent),
                    memory: _memorySnapshot,
                    evidence: _lastProjectEvidence,
                    classicTrace: _lastClassicTrace,
                    knowledge: _lastKnowledgeEvidence,
                    bootstrap: _lastKnowledgeBootstrap,
                    gameState: _playerResearch.CurrentGameState);
            var response = await _brain.ThinkAsync(brainContext, _activeRequest.Token);
            var autonomousAction = await ExecuteStructuredPlayerDecisionAsync(response.Decision, brainContext, intent, _activeRequest.Token);
            var responseText = CleanStoredBorinReply(response.Text);
            if (!string.IsNullOrWhiteSpace(autonomousAction))
                responseText = string.IsNullOrWhiteSpace(responseText) ? autonomousAction : $"{responseText}\n\n{autonomousAction}";
            var noReplyRecovered = false;
            if (responseText.Length == 0)
            {
                responseText = intent == BorinRequestIntent.ActivePlayer
                    ? "I heard the instruction, but I did not send a terminal command because I could not verify a safe real-terminal action."
                    : "I heard you, but I do not have enough verified information to answer that honestly yet.";
                noReplyRecovered = true;
                Status = "An internal silence marker was recovered before it reached Forge.";
            }
            var inventedTerminalTranscriptBlocked = false;
            if (intent == BorinRequestIntent.ActivePlayer &&
                string.IsNullOrWhiteSpace(autonomousAction) &&
                BorinForgeActionRouter.ContainsInventedTerminalTranscript(responseText))
            {
                var gameState = _playerResearch.CurrentGameState;
                responseText =
                    $"I did not send a terminal command. Forge blocked an invented game transcript. " +
                    $"The real active server is {gameState.GameServerIdentity}; " +
                    $"the current room is {(gameState.HasVerifiedRoom ? gameState.CurrentRoom : "not verified")}. " +
                    "Give me a direct gameplay instruction or use the Assignment window, and I will route it through the real terminal.";
                inventedTerminalTranscriptBlocked = true;
                Status = "Invented terminal narration was blocked.";
            }
            Add("Borin", responseText);
            if (intent == BorinRequestIntent.RepositoryResearch)
            {
                _lastAuditReportPath = await SaveAuditReportAsync(request, responseText, CancellationToken.None);
                _pendingAuditLastPage = responseText;
                if (response.ReachedTokenLimit)
                {
                    _pendingAuditRequest = request;
                    _pendingAuditNextPage = 2;
                    Add("Borin",
                        $"Audit page 1 reached the local reply limit. The complete gathered evidence is preserved at `{_lastAuditReportPath}`. " +
                        "Type `/report next` for the next analysis page, or `/report status` for the report path.");
                }
                else
                {
                    ClearPendingAuditContinuation();
                    Add("Borin", $"Full gathered audit evidence was saved to `{_lastAuditReportPath}`.");
                }
            }
            await _memoryStore.RecordTurnAsync(request, responseText, CancellationToken.None);
            await RefreshMemorySnapshotAsync(request, CancellationToken.None);
            ApplyBrainStatus(response);
            TaskStage = response.WasCanceled ? "Reply stopped" : "Response complete";
            TaskProgress = response.WasCanceled ? 0 : 100;
            Status = response.WasCanceled
                ? "Reply stopped. Local brain remains loaded."
                : inventedTerminalTranscriptBlocked
                    ? "Invented terminal narration was blocked; no game action was claimed."
                    : noReplyRecovered
                        ? "Internal NO_REPLY marker was suppressed and replaced with an honest private-Forge response."
                        : "Ready by the hearth.";
        }
        catch (OperationCanceledException)
        {
            Add("Borin", "Aye, I stopped before a readable reply was formed. Me local brain remains loaded.");
            TaskStage = "Canceled";
            TaskProgress = 0;
            Status = "Task canceled. Local brain remains loaded.";
            BrainStatus = _localBrain?.CurrentStatus.Display ?? "Local GGUF request canceled";
        }
        catch (Exception ex)
        {
            Add("Borin", $"Bah, the hammer slipped: {ex.Message}. No source files were changed.");
            Status = "Request failed safely.";
        }
        finally
        {
            if (localResearchHold)
            {
                try
                {
                    _playerResearch.SetLocalResearchBusy(false);
                }
                catch
                {
                    // The local audit must never tear down the live player session during cleanup.
                }
            }
            _activeRequest?.Dispose();
            _activeRequest = null;
            IsBusy = false;
            await PersistAsync();
            RaiseCommandStates();
        }
    }

    private async Task<bool> TryHandleGuardianCapabilityAsync(string request)
    {
        if (_guardianCapabilities is null)
            return false;

        try
        {
            var result = await _guardianCapabilities.ExecuteAsync(request, CancellationToken.None);
            if (!result.Handled)
                return false;

            Add("Borin", result.Summary);
            Status = result.Succeeded
                ? "Borin completed the requested capability operation."
                : "Borin handled the request without performing an unsafe or unavailable operation.";
            await _memoryStore.RecordTurnAsync(request, result.Summary, CancellationToken.None);
            await RefreshMemorySnapshotAsync(request, CancellationToken.None);
            await PersistAsync();
            return true;
        }
        catch (OperationCanceledException)
        {
            Add("Borin", "The capability operation was cancelled before completion.");
            Status = "Borin capability operation cancelled.";
            return true;
        }
        catch (Exception exception)
        {
            Add("Borin", $"The capability operation failed safely: {exception.Message}");
            Status = "Borin capability operation failed safely.";
            return true;
        }
    }

    private async Task RunBorinCapabilityButtonAsync(string request)
    {
        Add("You", request);
        await TryHandleGuardianCapabilityAsync(request);
        Raise(nameof(BorinCapabilitiesSummary));
        RaiseCommandStates();
    }

    private async Task RefreshVerifiedPerceptionForQuestionAsync(string request)
    {
        var normalized = request.Trim().ToLowerInvariant();
        if (normalized.Length == 0)
            return;

        var asksPrompt = normalized.Contains("hp prompt", StringComparison.Ordinal) ||
                         normalized.Contains("see the prompt", StringComparison.Ordinal) ||
                         normalized.Contains("see hp", StringComparison.Ordinal) ||
                         normalized.Contains("current hp", StringComparison.Ordinal);
        var asksExits = normalized.Contains("exit", StringComparison.Ordinal);
        var asksRoom = normalized.Contains("current room", StringComparison.Ordinal) ||
                       normalized.Contains("what room", StringComparison.Ordinal) ||
                       normalized.Contains("where are you", StringComparison.Ordinal) ||
                       normalized.Contains("your location", StringComparison.Ordinal);

        if (!(asksPrompt || asksExits || asksRoom))
            return;

        var state = _playerResearch.CurrentGameState;
        if (!state.IsConnected || !state.IsLoggedIntoMajorMud)
            return;

        var needsRefresh = (asksPrompt && !state.HasVerifiedPrompt) ||
                           (asksExits && !state.HasVerifiedExits) ||
                           (asksRoom && !state.HasVerifiedRoom);
        if (!needsRefresh)
            return;

        Status = "Borin is using LOOK on the real terminal to refresh live perception...";
        try
        {
            await ConfigurePlayerAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var refreshed = await _playerResearch.RefreshPerceptionAsync(timeout.Token);
            Status = refreshed
                ? "Live room, exits, and HP prompt were refreshed from Borin's real terminal."
                : "LOOK was sent, but the terminal did not provide enough fresh evidence to verify perception.";
        }
        catch (OperationCanceledException)
        {
            Status = "LOOK timed out before the terminal returned complete verified perception.";
        }
        catch (Exception exception)
        {
            Status = $"Borin could not refresh live perception with LOOK: {exception.Message}";
        }

        RaisePlayerProperties();
    }

    private async Task<bool> TryHandleAutonomousPlayChatAsync(string request)
    {
        var normalized = Regex.Replace(request.Trim().ToLowerInvariant(), @"\s+", " ");
        var stopRequested = normalized.Contains("stop playing by yourself", StringComparison.Ordinal) ||
                            normalized.Contains("stop autonomous", StringComparison.Ordinal) ||
                            normalized.Contains("stop exploring", StringComparison.Ordinal) ||
                            normalized.Contains("pause your exploration", StringComparison.Ordinal);
        if (stopRequested)
        {
            await _playerResearch.StopAutonomousPlayAsync("Stopped by William through Borin chat.");
            Add("Borin", "Aye. I stopped autonomous play and left the live session connected.");
            Status = "Autonomous play stopped from chat.";
            RaisePlayerProperties();
            return true;
        }

        var startRequested = normalized.Contains("play by yourself", StringComparison.Ordinal) ||
                             normalized.Contains("play on your own", StringComparison.Ordinal) ||
                             normalized.Contains("explore by yourself", StringComparison.Ordinal) ||
                             normalized.Contains("explore on your own", StringComparison.Ordinal) ||
                             normalized.Contains("start autonomous play", StringComparison.Ordinal) ||
                             normalized.Contains("start exploring and learn", StringComparison.Ordinal) ||
                             normalized.Contains("explore and learn by yourself", StringComparison.Ordinal);
        if (!startRequested)
            return false;

        if (_playerResearch.IsAutonomousPlayActive)
        {
            Add("Borin", $"Aye, I am already playing by myself. Current assignment: {PlayerCurrentGoal}");
            return true;
        }

        try
        {
            await ConfigurePlayerAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var state = _playerResearch.CurrentGameState;
            if (!state.IsConnected || !state.IsLoggedIntoMajorMud)
            {
                Add("Borin", $"I cannot start autonomous play yet: {state.AutonomousPlayBlockReason}");
                Status = "Autonomous play is waiting for a verified game login.";
                return true;
            }

            Status = "Borin is refreshing the real room and exits before autonomous play...";
            var refreshed = await _playerResearch.RefreshPerceptionAsync(timeout.Token);
            if (!refreshed)
            {
                var blocked = _playerResearch.CurrentGameState;
                Add("Borin", $"I sent LOOK, but cannot start exploring until the terminal verifies the room, exits, and prompt: {blocked.AutonomousPlayBlockReason}");
                Status = "Autonomous perception refresh was incomplete.";
                return true;
            }

            var assignment = normalized.Contains("explore", StringComparison.Ordinal)
                ? "Explore the current MajorMUD world independently, learn from verified observations, and record anomalies."
                : PlayerCurrentGoal;
            PlayerCurrentGoal = assignment;
            await _playerResearch.StartAutonomousPlayAsync(assignment, timeout.Token);
            Add("Borin", $"Aye. I am now playing by myself on {_playerResearch.CurrentGameState.GameServerIdentity}. I will explore, learn from verified results, and stop if the live state becomes unsafe or uncertain.");
            Status = "Borin is playing and exploring autonomously.";
        }
        catch (OperationCanceledException)
        {
            Add("Borin", "The real terminal did not return complete perception before the autonomous-start timeout.");
            Status = "Autonomous start timed out.";
        }
        catch (Exception exception)
        {
            Add("Borin", $"I could not start autonomous play: {exception.Message}");
            Status = "Autonomous play remained stopped.";
        }
        finally
        {
            RaisePlayerProperties();
        }
        return true;
    }

    private async Task<string?> ExecuteStructuredPlayerDecisionAsync(
        BorinAiDecision? decision,
        BrainContext requestedBrainContext,
        BorinRequestIntent requestIntent,
        CancellationToken token)
    {
        if (requestIntent != BorinRequestIntent.ActivePlayer || decision is null || !decision.RequestsAction)
            return null;

        var contextBuilder = new BorinContextBuilder();
        var requestedContext = contextBuilder.Build(requestedBrainContext);
        var currentBrainContext = requestedBrainContext with
        {
            Conversation = requestedBrainContext.Conversation with { GameState = _playerResearch.CurrentGameState }
        };
        var validation = new BorinActionValidator(contextBuilder: contextBuilder)
            .Validate(decision, requestedContext, currentBrainContext);
        if (!validation.Allowed)
            return $"I decided on `{decision.Intent}`, but did not send it because current local validation rejected it: {validation.Reason}";

        if (decision.Intent == BorinAiIntent.ConnectAndLogin)
        {
            await _playerResearch.ConnectAsync(token);
            return "I sent the connect-and-login request to my real Omniverse session.";
        }
        if (decision.Intent == BorinAiIntent.Disconnect)
        {
            await _playerResearch.DisconnectAsync(token);
            return "I disconnected my real Omniverse session.";
        }

        var target = decision.Target?.Trim() ?? string.Empty;
        var command = decision.Intent switch
        {
            BorinAiIntent.LookRoom => "look",
            BorinAiIntent.Move => target,
            BorinAiIntent.Inventory => "inventory",
            BorinAiIntent.Equipment => "equipment",
            BorinAiIntent.Attack => $"attack {target}",
            BorinAiIntent.Cast => $"cast {target}",
            BorinAiIntent.GameCommand => target,
            BorinAiIntent.Interact or BorinAiIntent.Shop or BorinAiIntent.Quest => target,
            _ => string.Empty,
        };
        if (string.IsNullOrWhiteSpace(command))
            return $"I decided on `{decision.Intent}`, but that action does not yet translate to one MajorMUD command.";

        var result = await _playerResearch.SendAutonomousCommandAsync(command, decision.Reason, token);
        return result.Sent
            ? $"Sent to my real terminal: `{result.Command}`."
            : $"I decided to use `{command}`, but the live player gateway rejected it: {result.Reason}";
    }

    private bool TryBuildVerifiedPlayerStateAnswer(string request, out string answer)
    {
        answer = string.Empty;
        var normalized = request.Trim().ToLowerInvariant();
        if (normalized.Length == 0)
            return false;

        var asksPrompt = normalized.Contains("hp prompt", StringComparison.Ordinal) ||
                         normalized.Contains("see the prompt", StringComparison.Ordinal) ||
                         normalized.Contains("see hp", StringComparison.Ordinal);
        var asksExits = normalized.Contains("exit", StringComparison.Ordinal);
        var asksRoom = normalized.Contains("current room", StringComparison.Ordinal) ||
                       normalized.Contains("what room", StringComparison.Ordinal) ||
                       normalized.Contains("where are you", StringComparison.Ordinal) ||
                       normalized.Contains("your location", StringComparison.Ordinal);
        // A server name appearing anywhere in the sentence is not a server-status
        // question. Keep unrelated Omniverse/Reforged questions in their actual
        // conversational or gameplay intent instead of returning connection boilerplate.
        var asksServer = BorinForgeActionRouter.IsServerStateQuestion(request);
        var asksTelepath = normalized.Contains("telepath", StringComparison.Ordinal);
        var asksCommandOwner = normalized.Contains("who typed", StringComparison.Ordinal) ||
                               normalized.Contains("did you type", StringComparison.Ordinal) ||
                               normalized.Contains("did you send", StringComparison.Ordinal) ||
                               normalized.Contains("last command", StringComparison.Ordinal);
        var asksControl = normalized.Contains("actually playing", StringComparison.Ordinal) ||
                          normalized.Contains("typing in the terminal", StringComparison.Ordinal) ||
                          normalized.Contains("type in the terminal", StringComparison.Ordinal) ||
                          normalized.Contains("controlling the game", StringComparison.Ordinal) ||
                          normalized.Contains("playing by yourself", StringComparison.Ordinal) ||
                          normalized.Contains("play by yourself", StringComparison.Ordinal);
        var asksDestination = asksExits &&
                              (normalized.Contains("where does", StringComparison.Ordinal) ||
                               normalized.Contains("where do", StringComparison.Ordinal) ||
                               normalized.Contains("lead", StringComparison.Ordinal) ||
                               normalized.Contains("go to", StringComparison.Ordinal));

        if (!(asksPrompt || asksExits || asksRoom || asksServer || asksTelepath || asksCommandOwner || asksControl))
            return false;

        var state = _playerResearch.CurrentGameState;
        var parts = new List<string>();

        if (asksServer)
            parts.Add($"I am using the RealmGate application and my active character is {ServerActivityPhrase(state)}.");

        if (asksPrompt)
        {
            parts.Add(state.HasVerifiedPrompt
                ? $"Yes. The Client saw a fresh HP prompt ({state.PromptFreshness}); HP is {FormatVerifiedVital(state.HealthCurrent, state.HealthMaximum, state.HealthPercent)}."
                : $"No. A current HP prompt is not verified: {state.AutonomousPlayBlockReason}");
        }

        if (asksRoom)
        {
            parts.Add(state.HasVerifiedRoom
                ? $"My verified room is {state.CurrentRoom}."
                : "My current room is not verified from the terminal yet.");
        }

        if (asksExits)
        {
            if (!state.HasVerifiedExits)
            {
                parts.Add("The current exits are not verified from the terminal. I will not guess them.");
            }
            else
            {
                parts.Add($"The exact visible exits are {string.Join(", ", state.CurrentExits)}.");
                if (asksDestination)
                    parts.Add("Those exit names do not prove their destinations; I will name a destination only after a verified transition or authoritative decoded map evidence.");
            }
        }

        if (asksTelepath)
        {
            var telepath = _playerResearch.LastIncomingTelepathSummary;
            parts.Add(telepath.StartsWith("No incoming", StringComparison.OrdinalIgnoreCase)
                ? "No incoming telepath has been parsed on the active session yet."
                : $"Yes. The last parsed incoming telepath was {telepath}.");
        }

        if (asksCommandOwner)
        {
            parts.Add(string.IsNullOrWhiteSpace(state.LastCommand)
                ? "No terminal command owner is currently captured."
                : $"The last captured command was '{state.LastCommand}', sent by {DescribeCommandOwner(state.LastCommandOrigin)}.");
        }

        if (asksControl)
        {
            var activeSessionId = string.IsNullOrWhiteSpace(PlayerSessionId)
                ? "the active Borin session"
                : PlayerSessionId.Trim();
            parts.Add(_playerResearch.IsAutonomousPlayActive
                ? $"Yes. My player controller is currently sending real commands to {state.GameServerIdentity} through session {activeSessionId}."
                : $"I am observing {state.GameServerIdentity}, but I am not currently sending commands by myself. Direct Forge instructions use the real supervised terminal gateway; ordinary chat never simulates command results.");
        }

        answer = string.Join(" ", parts);
        return true;
    }

    public static string ServerActivityPhrase(BorinGameStateSnapshot state)
    {
        if (!state.IsConnected)
        {
            return state.HostKind switch
            {
                BorinGameHostKind.OmniverseBbs => $"configured for Omniverse BBS in world {state.World}, but currently disconnected",
                BorinGameHostKind.ReforgedServer => $"configured for the RealmGate server in world {state.World}, but currently disconnected",
                BorinGameHostKind.ExternalBbs => $"configured for {state.Server} in world {state.World}, but currently disconnected",
                _ => "not connected to a verified game server",
            };
        }

        if (!state.IsLoggedIntoMajorMud)
            return $"connected to {state.GameServerIdentity}, but not yet verified inside the MajorMUD realm";

        return state.HostKind switch
        {
            BorinGameHostKind.OmniverseBbs => $"playing on Omniverse BBS in world {state.World}",
            BorinGameHostKind.ReforgedServer => $"playing on the RealmGate server in world {state.World}",
            BorinGameHostKind.ExternalBbs => $"playing on {state.Server} in world {state.World}",
            _ => "inside MajorMUD on a server whose identity is not verified",
        };
    }

    private static string DescribeCommandOwner(ClientCommandOrigin origin)
    {
        var originName = origin.ToString();

        if (originName.Equals("Borin", StringComparison.OrdinalIgnoreCase))
            return "Borin's player controller";

        return origin switch
        {
            ClientCommandOrigin.Human => "William through the terminal",
            ClientCommandOrigin.RouteAutomation => "the RealmGate route automation",
            ClientCommandOrigin.SystemLogin => "the RealmGate login controller",
            _ => originName,
        };
    }

    private static string FormatVerifiedVital(int current, int maximum, int percent) =>
        maximum > 0
            ? $"{current}/{maximum} ({percent}%)"
            : current > 0
                ? $"{current} current; maximum not verified"
                : "not captured";

    private async Task<bool> TryHandleAuditReportCommandAsync(string request)
    {
        var trimmed = request.Trim();
        if (!trimmed.StartsWith("/report", StringComparison.OrdinalIgnoreCase))
            return false;

        var action = trimmed.Length > "/report".Length
            ? trimmed["/report".Length..].Trim().ToLowerInvariant()
            : "status";

        if (action is "help" or "")
        {
            Add("Borin",
                "Repository report commands:\n\n" +
                "`/report next` — continue a repository audit that reached the local reply limit.\n" +
                "`/report status` — show the saved full-evidence report and continuation state.\n" +
                "`/report clear` — discard the pending continuation without deleting the saved report.");
            return true;
        }

        if (action == "status")
        {
            var continuation = _pendingAuditNextPage > 0
                ? $"Page {_pendingAuditNextPage} is ready to generate with `/report next`."
                : "No continuation page is pending.";
            Add("Borin",
                $"Saved audit report: `{(string.IsNullOrWhiteSpace(_lastAuditReportPath) ? "No report has been created yet." : _lastAuditReportPath)}`\n\n{continuation}");
            return true;
        }

        if (action == "clear")
        {
            ClearPendingAuditContinuation();
            Add("Borin", "The pending audit continuation was cleared. Any saved report file remains on disk.");
            return true;
        }

        if (action != "next")
        {
            Add("Borin", "Unknown report command. Use `/report help`.");
            return true;
        }

        if (_pendingAuditNextPage <= 0 || string.IsNullOrWhiteSpace(_pendingAuditRequest))
        {
            Add("Borin", "No audit continuation is pending. Ask for a repository audit first.");
            return true;
        }

        IsBusy = true;
        _activeRequest?.Dispose();
        _activeRequest = new CancellationTokenSource();
        var pageNumber = _pendingAuditNextPage;
        TaskStage = $"Generating audit page {pageNumber}";
        TaskProgress = 60;
        Status = "Borin is continuing the saved repository audit without disturbing Omniverse.";
        try
        {
            _playerResearch.SetLocalResearchBusy(true);
        }
        catch
        {
        }
        RaiseCommandStates();
        try
        {
            var previousTail = _pendingAuditLastPage.Length <= 1200
                ? _pendingAuditLastPage
                : _pendingAuditLastPage[^1200..];
            var continuationRequest = $"""
Continue the same local repository audit requested below.
This is analysis page {pageNumber} of at most {MaximumAuditPages}.
Do not repeat findings, evidence blocks, introductions, or conclusions from earlier pages.
Begin with the next unfinished supported finding. Use only the supplied current evidence and exact paths/line ranges.
If no new supported finding remains, reply exactly [REPORT_COMPLETE].

ORIGINAL REQUEST:
{_pendingAuditRequest}

TAIL OF PRIOR PAGE:
{previousTail}
""";

            var response = await _brain.ThinkAsync(
                BrainContext.ForBorin(
                    continuationRequest,
                    _state,
                    _snapshot,
                    purpose: "Local repository research continuation",
                    memory: _memorySnapshot,
                    evidence: _lastProjectEvidence,
                    classicTrace: _lastClassicTrace,
                    knowledge: _lastKnowledgeEvidence,
                    bootstrap: _lastKnowledgeBootstrap),
                _activeRequest.Token);

            if (response.Text.Trim().StartsWith("[REPORT_COMPLETE]", StringComparison.OrdinalIgnoreCase))
            {
                ClearPendingAuditContinuation();
                Add("Borin", $"The audit analysis is complete. Full gathered evidence remains at `{_lastAuditReportPath}`.");
            }
            else
            {
                Add("Borin", $"Audit continuation — page {pageNumber}\n\n{response.Text}");
                await AppendAuditReportPageAsync(pageNumber, response.Text, CancellationToken.None);
                _pendingAuditLastPage = response.Text;
                if (response.ReachedTokenLimit && pageNumber < MaximumAuditPages)
                {
                    _pendingAuditNextPage = pageNumber + 1;
                    Add("Borin", $"Page {pageNumber} reached the local reply limit. Type `/report next` for page {_pendingAuditNextPage}.");
                }
                else
                {
                    ClearPendingAuditContinuation();
                    Add("Borin", $"The audit continuation is complete. Full gathered evidence remains at `{_lastAuditReportPath}`.");
                }
            }

            TaskStage = "Audit continuation complete";
            TaskProgress = 100;
            Status = "Ready by the hearth.";
            await PersistAsync();
            return true;
        }
        catch (OperationCanceledException)
        {
            Add("Borin", "The audit continuation was canceled. The saved report and pending page remain available.");
            Status = "Audit continuation canceled.";
            return true;
        }
        catch (Exception exception)
        {
            Add("Borin", $"The audit continuation failed safely: {exception.Message}. The Omniverse session was not disconnected.");
            Status = "Audit continuation failed safely.";
            return true;
        }
        finally
        {
            try
            {
                _playerResearch.SetLocalResearchBusy(false);
            }
            catch
            {
            }
            _activeRequest?.Dispose();
            _activeRequest = null;
            IsBusy = false;
            RaiseCommandStates();
        }
    }

    private async Task<string> SaveAuditReportAsync(string request, string firstPage, CancellationToken token)
    {
        var reportRoot = Path.Combine(Configuration.RepositoryPath, "Reports", "BorinBrain", "RepositoryAudits");
        Directory.CreateDirectory(reportRoot);
        var path = Path.Combine(reportRoot, $"Borin-Repository-Audit-{DateTime.Now:yyyyMMdd-HHmmss}.md");
        var builder = new StringBuilder();
        builder.AppendLine("# Borin Repository Audit");
        builder.AppendLine();
        builder.AppendLine($"Generated: {DateTimeOffset.Now:O}");
        builder.AppendLine($"Request: {request}");
        builder.AppendLine();
        builder.AppendLine("## Borin analysis — page 1");
        builder.AppendLine();
        builder.AppendLine(firstPage);
        builder.AppendLine();
        builder.AppendLine("## Complete gathered read-only evidence");
        builder.AppendLine();
        builder.AppendLine(_lastProjectEvidence.Summary);
        builder.AppendLine();
        builder.AppendLine(_lastProjectEvidence.PromptText);
        builder.AppendLine();
        builder.AppendLine("## Structured Classic trace evidence");
        builder.AppendLine();
        builder.AppendLine(_lastClassicTrace.Summary);
        builder.AppendLine();
        builder.AppendLine(_lastClassicTrace.PromptText);
        builder.AppendLine();
        builder.AppendLine("## Ranked persistent knowledge evidence");
        builder.AppendLine();
        builder.AppendLine(_lastKnowledgeEvidence.Summary);
        builder.AppendLine();
        builder.AppendLine(_lastKnowledgeEvidence.PromptText);
        builder.AppendLine();
        builder.AppendLine("## Compact RealmGate bootstrap orientation");
        builder.AppendLine();
        builder.AppendLine(_lastKnowledgeBootstrap.Summary);
        builder.AppendLine();
        builder.AppendLine(_lastKnowledgeBootstrap.PromptText);
        builder.AppendLine();
        builder.AppendLine("---");
        builder.AppendLine("This report preserves gathered evidence. Matching text is not proof that every runtime trace stage is complete.");
        await File.WriteAllTextAsync(path, builder.ToString(), Encoding.UTF8, token);
        return path;
    }

    private async Task AppendAuditReportPageAsync(int pageNumber, string text, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(_lastAuditReportPath))
            return;

        var section = $"{Environment.NewLine}{Environment.NewLine}## Borin analysis — page {pageNumber}{Environment.NewLine}{Environment.NewLine}{text}{Environment.NewLine}";
        await File.AppendAllTextAsync(_lastAuditReportPath, section, Encoding.UTF8, token);
    }

    private void ClearPendingAuditContinuation()
    {
        _pendingAuditRequest = string.Empty;
        _pendingAuditLastPage = string.Empty;
        _pendingAuditNextPage = 0;
    }

    private Task QuickAsync(string text) { Input = text; return SendAsync(); }

    private async Task ScanAsync()
    {
        IsBusy = true;
        Status = "Walking the repository halls...";
        try
        {
            _snapshot = await _repository.ScanAsync(Configuration.RepositoryPath);
            Status = "Repository mapped. No files changed.";
            Raise(nameof(RepositorySummary));
        }
        catch (Exception ex)
        {
            Status = $"Repository scan failed: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    private async Task ClearAsync()
    {
        _state.Messages.Clear(); Messages.Clear();
        Add("Borin", "Fresh slate, same workshop. What are we buildin' today?");
        await PersistAsync();
    }

    private async Task SaveSettingsAsync()
    {
        Configuration.ProviderName = Configuration.BrainMode == BorinBrainMode.LocalOnly ? "RealmGate Direct GGUF" : Configuration.BrainMode == BorinBrainMode.NvidiaOnly ? "NVIDIA NIM" : "Borin Hybrid AI";
        Configuration.ApiEndpoint = "https://integrate.api.nvidia.com/v1";
        Configuration.ModelsEndpoint = string.Empty;
        Configuration.AutoReconnectMasterBrain = false;
        Configuration.UseMasterBrainForGeneralConversation = true;
        Configuration.RequireApprovalBeforeMainApply = true;
        _aiRoutingSettings.Mode = Configuration.BrainMode;
        _aiRoutingSettings.FallBackToLocal = true;
        await PersistAsync();
        Raise(nameof(Configuration)); Raise(nameof(ModeSummary)); Raise(nameof(KnowledgeSummary));
        await ScanAsync();
        Add("Borin", "Aye, the settings are stored. The NVIDIA key remains outside the file in Windows Credential Manager.");
        await PersistAsync();
    }

    private async Task ChangeNvidiaKeyAsync()
    {
        var dialog = new RealmGate.App.Views.NvidiaApiKeyWindow { Owner = Application.Current.MainWindow, Title = "Borin Shared NVIDIA API Key" };
        if (dialog.ShowDialog() != true) return;
        await _nvidiaCredentials.SaveApiKeyAsync(dialog.ApiKey);
        LightningConnectionStatus = "Lightning: shared key configured; connection not yet tested";
        SuperConnectionStatus = "Super: shared key configured; connection not yet tested";
        UltraConnectionStatus = "Ultra: shared key configured; connection not yet tested";
        Add("Borin", "Aye, the one shared NVIDIA key is secured in Windows Credential Manager for Lightning, Super, and Ultra. It was not written to Forge settings or chat.");
        await PersistAsync();
    }

    private async Task TestNvidiaAsync(NvidiaNimKeySlot slot)
    {
        var label = slot == NvidiaNimKeySlot.Ultra ? "Ultra" : slot == NvidiaNimKeySlot.Shared ? "Super" : "Lightning";
        var provider = slot == NvidiaNimKeySlot.Ultra ? _ultraProvider : slot == NvidiaNimKeySlot.Shared ? _superProvider : _lightningProvider;
        IsBusy = true;
        _activeRequest?.Dispose();
        _activeRequest = new CancellationTokenSource();
        if (slot == NvidiaNimKeySlot.Ultra) UltraConnectionStatus = "Ultra: testing...";
        else if (slot == NvidiaNimKeySlot.Shared) SuperConnectionStatus = "Super: testing...";
        else LightningConnectionStatus = "Lightning: testing...";
        RaiseCommandStates();
        try
        {
            var response = await provider.ThinkAsync(
                BrainContext.ForBorin(
                    "Return a short Borin greeting in speech. This is a connection test; request no tool action and keep all routing metadata out of visible speech.",
                    _state, _snapshot, $"NVIDIA {label} connection test", memory: _memorySnapshot,
                    gameState: _playerResearch.CurrentGameState),
                _activeRequest.Token);
            if (slot == NvidiaNimKeySlot.Ultra) UltraConnectionStatus = $"Ultra: Connected • {response.Model}";
            else if (slot == NvidiaNimKeySlot.Shared) SuperConnectionStatus = $"Super: Connected • {response.Model}";
            else LightningConnectionStatus = $"Lightning: Connected • {response.Model}";
            BrainStatus = $"NVIDIA {label} connected • {response.Model}";
            Add("Borin", response.Decision?.Speech ?? $"Aye, NVIDIA {label} answered the connection test.");
        }
        catch (Exception ex)
        {
            if (slot == NvidiaNimKeySlot.Ultra) UltraConnectionStatus = $"Ultra failed: {AiSecretRedactor.Redact(ex.Message)}";
            else if (slot == NvidiaNimKeySlot.Shared) SuperConnectionStatus = $"Super failed: {AiSecretRedactor.Redact(ex.Message)}";
            else LightningConnectionStatus = $"Lightning failed: {AiSecretRedactor.Redact(ex.Message)}";
            Add("Borin", $"NVIDIA {label} connection test failed safely: {AiSecretRedactor.Redact(ex.Message)}");
        }
        finally
        {
            Raise(nameof(LightningRateSummary)); Raise(nameof(SuperRateSummary)); Raise(nameof(UltraRateSummary)); Raise(nameof(OverallNvidiaRateSummary));
            _activeRequest?.Dispose();
            _activeRequest = null;
            IsBusy = false;
            RaiseCommandStates();
        }
    }


    private async Task TestBrainAsync()
    {
        IsBusy = true;
        _activeRequest?.Dispose();
        _activeRequest = new CancellationTokenSource();
        CurrentTask = "Test local GGUF brain";
        TaskStage = "Generating test reply";
        TaskProgress = 35;
        Status = "Testing Borin's local GGUF brain...";
        BrainStatus = "Testing local GGUF brain...";
        RaiseCommandStates();
        try
        {
            await RefreshMemorySnapshotAsync("local brain test", _activeRequest.Token);
            _lastRecallSnapshot = _memorySnapshot;
            Raise(nameof(MemoryRecallSummary));
            var response = await _brain.ThinkAsync(
                BrainContext.ForBorin(
                    "Reply in one short sentence confirming that you can hear me. Do not use role labels.",
                    _state,
                    _snapshot,
                    "Local brain test",
                    memory: _memorySnapshot,
                    bootstrap: _lastKnowledgeBootstrap),
                _activeRequest.Token);
            Add("Borin", response.Text);
            ApplyBrainStatus(response);
            TaskStage = response.WasCanceled ? "Test stopped" : "Local brain test complete";
            TaskProgress = response.WasCanceled ? 0 : 100;
            Status = response.WasCanceled ? "Local brain test stopped." : "Local GGUF brain answered successfully.";
            await PersistAsync();
        }
        catch (OperationCanceledException)
        {
            BrainStatus = _localBrain?.CurrentStatus.Display ?? "Local GGUF test canceled";
            TaskStage = "Test canceled";
            TaskProgress = 0;
            Status = "Local brain test canceled.";
        }
        catch (Exception ex)
        {
            Add("Borin", $"Nay, me local brain test failed: {ex.Message}");
            BrainStatus = _localBrain?.CurrentStatus.Display ?? "Local GGUF fault";
            TaskStage = "Test failed";
            TaskProgress = 0;
            Status = "Local GGUF brain test failed safely.";
        }
        finally
        {
            _activeRequest?.Dispose();
            _activeRequest = null;
            IsBusy = false;
            RaiseCommandStates();
        }
    }

    private async Task AttachFileAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Attach a file for Borin",
            Multiselect = true,
            Filter = "Supported files|*.txt;*.md;*.json;*.xml;*.yaml;*.yml;*.cs;*.xaml;*.csproj;*.sln;*.log;*.zip;*.patch;*.diff;*.ps1;*.bat;*.cmd|All files|*.*"
        };

        if (dialog.ShowDialog() != true)
            return;

        var inbox = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RealmGate",
            "ForgeGuardian",
            "Inbox");
        Directory.CreateDirectory(inbox);

        foreach (var source in dialog.FileNames)
        {
            var safeName = Path.GetFileName(source);
            var stored = Path.Combine(inbox, $"{DateTime.Now:yyyyMMdd_HHmmssfff}_{safeName}");
            File.Copy(source, stored, overwrite: false);
            var info = new FileInfo(stored);
            var attachment = new ForgeAttachment
            {
                OriginalName = safeName,
                StoredPath = stored,
                SizeBytes = info.Length,
                AddedAt = DateTimeOffset.Now
            };
            _state.Attachments.Add(attachment);
            Attachments.Add(attachment);
        }

        Add("Borin", $"Aye, {dialog.FileNames.Length} new scroll(s) be pinned to the workbench. I will read scripts as text only and will not execute attachments.");
        ((AsyncRelayCommand)ClearAttachmentsCommand).Raise();
        ((AsyncRelayCommand)ImplementAttachedCommand).Raise();
        await PersistAsync();
    }

    private async Task ClearAttachmentsAsync()
    {
        foreach (var attachment in _state.Attachments)
        {
            try
            {
                if (File.Exists(attachment.StoredPath))
                    File.Delete(attachment.StoredPath);
            }
            catch { }
        }

        _state.Attachments.Clear();
        Attachments.Clear();
        Add("Borin", "The attachment bench be cleared.");
        ((AsyncRelayCommand)ClearAttachmentsCommand).Raise();
        ((AsyncRelayCommand)ImplementAttachedCommand).Raise();
        await PersistAsync();
    }

    private async Task ChoosePortraitAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose Borin portrait",
            Filter = "Images|*.png;*.jpg;*.jpeg;*.webp;*.bmp|All files|*.*"
        };
        if (dialog.ShowDialog() != true)
            return;

        var artFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RealmGate",
            "ForgeGuardian",
            "Art");
        Directory.CreateDirectory(artFolder);
        var destination = Path.Combine(artFolder, Path.GetFileName(dialog.FileName));
        File.Copy(dialog.FileName, destination, overwrite: true);
        Configuration.PortraitPath = destination;
        Raise(nameof(Configuration));
        Add("Borin", "Aye, that portrait suits the old beard better. The Forge will use it from now on.");
        await PersistAsync();
    }

    private async Task ApplyBrainPerformancePresetAsync()
    {
        if (_localBrain is null)
        {
            BrainStatus = "Local GGUF brain unavailable";
            return;
        }

        IsBusy = true;
        _activeRequest?.Dispose();
        _activeRequest = new CancellationTokenSource();
        CurrentTask = $"Apply {SelectedBrainPerformancePreset} local-brain preset";
        TaskStage = "Tuning and reloading model";
        TaskProgress = 20;
        Status = $"Applying the {SelectedBrainPerformancePreset} brain preset...";
        RaiseCommandStates();
        try
        {
            var runtime = await _localBrain.ApplyPerformancePresetAsync(
                BrainContext.ForBorin(
                    $"Apply the {SelectedBrainPerformancePreset} performance preset.",
                    _state,
                    _snapshot,
                    "Performance tuning"),
                SelectedBrainPerformancePreset,
                _activeRequest.Token);
            BrainStatus = runtime.Display;
            BrainPerformanceSummary = string.IsNullOrWhiteSpace(runtime.PerformanceDisplay)
                ? $"{SelectedBrainPerformancePreset} preset loaded."
                : runtime.PerformanceDisplay;
            Add("Borin",
                $"Aye, the {SelectedBrainPerformancePreset} brain preset be active. " +
                $"The model reloaded with {runtime.GpuLayerDisplay}. Give me a question and the speed meter will record the result.");
            TaskStage = "Performance preset ready";
            TaskProgress = 100;
            Status = $"{SelectedBrainPerformancePreset} local-brain preset is ready.";
        }
        catch (OperationCanceledException)
        {
            BrainStatus = _localBrain.CurrentStatus.Display;
            TaskStage = "Tuning canceled";
            TaskProgress = 0;
            Status = "Brain performance tuning canceled.";
        }
        catch (Exception ex)
        {
            BrainStatus = _localBrain.CurrentStatus.Display;
            Add("Borin", $"Nay, the performance preset could not be applied: {ex.Message}");
            TaskStage = "Tuning failed";
            TaskProgress = 0;
            Status = "Brain performance tuning failed safely.";
        }
        finally
        {
            _activeRequest?.Dispose();
            _activeRequest = null;
            IsBusy = false;
            await PersistAsync();
            RaiseCommandStates();
        }
    }

    private async Task LoadModelsAsync()
    {
        if (_localBrain is null)
        {
            Add("Borin", "Nay, the direct local GGUF brain was not registered in this app build.");
            BrainStatus = "Local GGUF brain unavailable";
            return;
        }

        IsBusy = true;
        _activeRequest?.Dispose();
        _activeRequest = new CancellationTokenSource();
        CurrentTask = "Load local GGUF brain";
        TaskStage = "Loading model weights";
        TaskProgress = 20;
        Status = "Loading Borin's local GGUF brain...";
        BrainStatus = "Loading local GGUF brain...";
        RaiseCommandStates();
        try
        {
            var runtime = await _localBrain.LoadAsync(
                BrainContext.ForBorin("Load Borin's local brain.", _state, _snapshot, "Local brain load"),
                _activeRequest.Token);
            Configuration.ModelName = runtime.ModelName;
            Configuration.AutoReconnectMasterBrain = false;
            Configuration.UseMasterBrainForGeneralConversation = true;
            BrainStatus = runtime.Display;
            Add("Borin", $"Aye, me local brain be ready through `{runtime.ModelName}` with {runtime.GpuLayerCount} GPU layers. LM Studio is not being used.");
            TaskStage = "Local brain ready";
            TaskProgress = 100;
            Status = "Local GGUF brain loaded and ready.";
            Raise(nameof(Configuration));
            Raise(nameof(ModeSummary));
            await PersistAsync();
        }
        catch (OperationCanceledException)
        {
            BrainStatus = _localBrain.CurrentStatus.Display;
            TaskStage = "Load canceled";
            TaskProgress = 0;
            Status = "Local brain loading canceled.";
        }
        catch (Exception ex)
        {
            BrainStatus = _localBrain.CurrentStatus.Display;
            Add("Borin", $"Nay, me local brain could not load: {ex.Message}");
            TaskStage = "Load failed";
            TaskProgress = 0;
            Status = "Local GGUF brain load failed safely.";
        }
        finally
        {
            _activeRequest?.Dispose();
            _activeRequest = null;
            IsBusy = false;
            RaiseCommandStates();
        }
    }

    private async Task CreateWorkspaceAsync()
    {
        IsBusy = true; Status = "Preparing a safe Git workshop...";
        try
        {
            var result = await _tools.CreateWorkspaceAsync(_state);
            Add("Borin", result.Exists ? $"Aye, a safe workshop be ready at `{result.WorkspacePath}`. Main repository remains untouched." : result.Summary);
            Status = result.Exists ? "Safe workspace ready." : "Workspace creation failed safely.";
        }
        catch (Exception ex) { Add("Borin", $"Nay, the workshop could not be raised: {ex.Message}"); Status = "Workspace creation failed safely."; }
        finally { IsBusy = false; await PersistAsync(); }
    }

    private async Task WorkspaceStatusAsync()
    {
        IsBusy = true; Status = "Inspecting the safe workshop...";
        try
        {
            var result = await _tools.GetWorkspaceStatusAsync(_state);
            Add("Borin", $"Workspace: `{result.WorkspacePath}`\nExists: {result.Exists}\nChanged files: {result.ChangedFileCount}\n\n{result.Summary}");
            Status = "Workspace inspected.";
        }
        catch (Exception ex) { Add("Borin", ex.Message); Status = "Workspace inspection failed safely."; }
        finally { IsBusy = false; await PersistAsync(); }
    }

    private async Task ToolAsync(string busyText, Func<Task<string>> action)
    {
        IsBusy = true; Status = busyText;
        try { Add("Borin", await action()); Status = "Ready by the hearth."; }
        catch (Exception ex) { Add("Borin", $"Bah, the tool slipped: {ex.Message}. No unapproved main-repository change was made."); Status = "Tool action failed safely."; }
        finally { IsBusy = false; await PersistAsync(); }
    }

    private async Task WorkspaceCommandAsync(string busyText, Func<Task<ForgeCommandResult>> action)
    {
        IsBusy = true; Status = busyText;
        try
        {
            var result = await action();
            var tail = result.Output.Length > 16000 ? result.Output[^16000..] : result.Output;
            Add("Borin", $"`{result.Command}` finished with exit code {result.ExitCode}.\n\n{tail}\n\nThis ran only inside the Forge workspace.");
            Status = result.Succeeded ? "Workspace command passed." : "Workspace command found faults.";
            RecordJournal(result.Command, tail, result.Succeeded);
        }
        catch (Exception ex) { Add("Borin", $"Nay, the workspace command failed: {ex.Message}"); Status = "Workspace command failed safely."; }
        finally { IsBusy = false; await PersistAsync(); }
    }

    private void OnLocalBrainStatusChanged(LocalGgufBrainRuntimeStatus status)
    {
        void ApplyStatus()
        {
            BrainStatus = status.Display;
            if (!string.IsNullOrWhiteSpace(status.PerformancePreset))
                SelectedBrainPerformancePreset = status.PerformancePreset;
            if (!string.IsNullOrWhiteSpace(status.PerformanceDisplay))
                BrainPerformanceSummary = status.PerformanceDisplay;
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            ApplyStatus();
            return;
        }

        _ = dispatcher.BeginInvoke(ApplyStatus);
    }

    private void ApplyBrainStatus(BrainResponse response)
    {
        BrainStatus = response.UsedFallback
            ? $"Fallback: {response.Provider} • {response.FallbackReason ?? "provider unavailable"}"
            : $"Ready: {response.Provider}";
        if (response.Performance is not null)
            BrainPerformanceSummary = response.Performance.Display;
        Raise(nameof(LastAiRouteSummary));
        Raise(nameof(LightningRateSummary));
        Raise(nameof(SuperRateSummary));
        Raise(nameof(UltraRateSummary));
        Raise(nameof(OverallNvidiaRateSummary));
        Raise(nameof(ModeSummary));
    }

    private void CancelActiveRequest()
    {
        _activeRequest?.Cancel();
        TaskStage = "Stopping reply";
        Status = "Stopping Borin's reply...";
    }

    private async Task StudyAsync(string area, string relativePath)
    {
        if (!Configuration.EnableOfflineStudyMode)
        {
            Add("Borin", "Offline Study Mode be disabled in Settings.");
            return;
        }
        IsBusy = true;
        Status = $"Studying {area} without using the local GGUF brain...";
        RaiseCommandStates();
        try
        {
            var record = await _repository.StudyAsync(Configuration.RepositoryPath, area, relativePath);
            _state.SubsystemStudies[area] = record;
            var memory = $"Studied {area} on {record.StudiedAt:g}: {record.SourceFileCount} source/UI files, {record.ClassCount} types, {record.NamespaceCount} namespaces. Summary: {record.SummaryPath}";
            _state.LearnedMemories.RemoveAll(x => x.StartsWith($"Studied {area} ", StringComparison.OrdinalIgnoreCase));
            _state.LearnedMemories.Add(memory);
            RecordJournal($"Study {area}", memory, true);
            Add("Borin", $"Aye, I studied **{area}** whilst staying offline. I mapped {record.SourceFileCount} source/UI files, {record.ClassCount} types, and {record.NamespaceCount} namespaces. The study ledger be stored at `{record.SummaryPath}`.");
            Status = $"{area} study complete.";
            Raise(nameof(KnowledgeSummary));
            await PersistAsync();
        }
        catch (Exception ex)
        {
            RecordJournal($"Study {area}", ex.Message, false);
            Add("Borin", $"Nay, the study was interrupted: {ex.Message}");
            Status = "Study failed safely.";
        }
        finally
        {
            IsBusy = false;
            RaiseCommandStates();
        }
    }


    private async Task ImplementAttachedCommissionAsync()
    {
        if (Attachments.Count == 0)
        {
            Add("Borin", "No commission scroll be attached. Pin a handoff, log, patch, or design file to the workbench first.");
            return;
        }

        IsBusy = true;
        _activeRequest?.Dispose();
        _activeRequest = new CancellationTokenSource();
        CurrentTask = $"Implement attached commission: {Attachments[0].OriginalName}";
        TaskStage = "Creating safe workspace";
        TaskProgress = 8;
        Status = "Raising a protected workshop for the commission...";
        RaiseCommandStates();

        try
        {
            var workspace = await _tools.CreateWorkspaceAsync(_state, _activeRequest.Token);
            if (!workspace.Exists)
            {
                Add("Borin", workspace.Summary);
                Status = "Workspace creation failed safely.";
                TaskStage = "Stopped — no workspace";
                return;
            }

            Add("Borin", $"Aye, the protected workshop be ready at `{workspace.WorkspacePath}`. I will keep the main repository untouched.");
            TaskStage = "Inspecting attachment and repository";
            TaskProgress = 22;

            var prompt = """
Implement the attached commission in the safe workspace. First inspect the attached file and current repository context. Return a complete unified Git diff only for the smallest architecture-consistent implementation. Preserve existing behavior, never modify generated Classic data, and include tests or validation changes where appropriate.
""";

            TaskStage = "Consulting local GGUF brain";
            TaskProgress = 40;
            await RefreshMemorySnapshotAsync(prompt, _activeRequest.Token);
            _lastRecallSnapshot = _memorySnapshot;
            Raise(nameof(MemoryRecallSummary));
            var response = await _brain.ThinkAsync(
                BrainContext.ForBorin(
                    prompt,
                    _state,
                    _snapshot,
                    "Attached commission implementation",
                    memory: _memorySnapshot,
                    bootstrap: _lastKnowledgeBootstrap),
                _activeRequest.Token);
            Add("Borin", response.Text);
            ApplyBrainStatus(response);
            if (response.WasCanceled)
            {
                Status = "Commission response stopped before any diff was applied.";
                TaskStage = "Canceled before apply";
                TaskProgress = 0;
                return;
            }

            TaskStage = "Applying proposed diff to workspace";
            TaskProgress = 58;
            var applyResult = await _tools.ApplyLatestDiffToWorkspaceAsync(_state, _activeRequest.Token);
            Add("Borin", applyResult);

            if (applyResult.Contains("could not", StringComparison.OrdinalIgnoreCase) ||
                applyResult.Contains("no fenced", StringComparison.OrdinalIgnoreCase) ||
                applyResult.Contains("failed", StringComparison.OrdinalIgnoreCase))
            {
                Status = "A proposed diff was not safely applicable.";
                TaskStage = "Waiting for review";
                TaskProgress = 65;
                return;
            }

            TaskStage = "Building workspace";
            TaskProgress = 72;
            var build = await _tools.BuildWorkspaceAsync(_state, _activeRequest.Token);
            Add("Borin", $"Workspace build exit code: {build.ExitCode}\n\n{(build.Output.Length > 12000 ? build.Output[^12000..] : build.Output)}");
            RecordJournal("Implement attached commission — build", build.Output, build.Succeeded);

            if (!build.Succeeded)
            {
                Status = "Workspace build found faults. Main repository remains untouched.";
                TaskStage = "Build needs repair";
                TaskProgress = 78;
                return;
            }

            TaskStage = "Testing workspace";
            TaskProgress = 86;
            var tests = await _tools.TestWorkspaceAsync(_state, _activeRequest.Token);
            Add("Borin", $"Workspace tests exit code: {tests.ExitCode}\n\n{(tests.Output.Length > 12000 ? tests.Output[^12000..] : tests.Output)}");
            RecordJournal("Implement attached commission — tests", tests.Output, tests.Succeeded);

            TaskStage = tests.Succeeded ? "Ready for diff review and approval" : "Tests need attention";
            TaskProgress = tests.Succeeded ? 100 : 92;
            Status = tests.Succeeded
                ? "Commission forged in the safe workspace. Review the diff before applying."
                : "Code built, but tests found faults. Main repository remains untouched.";
            Add("Borin", tests.Succeeded
                ? "Aye, lad. The attached commission be forged and tested in the safe workshop. Use **Show Workspace Diff**, then **Apply Approved Patch** only after ye approve every change."
                : "The build be sound, but the trials found trouble. I kept every change in the safe workshop for review.");
        }
        catch (OperationCanceledException)
        {
            Add("Borin", "Aye, the commission be halted. No unapproved main-repository file was changed.");
            Status = "Commission cancelled safely.";
            TaskStage = "Cancelled";
        }
        catch (Exception ex)
        {
            Add("Borin", $"Nay, the commission struck a fault: {ex.Message}. The main repository remains untouched.");
            Status = "Commission failed safely.";
            TaskStage = "Failed safely";
        }
        finally
        {
            _activeRequest?.Dispose();
            _activeRequest = null;
            IsBusy = false;
            await PersistAsync();
            RaiseCommandStates();
        }
    }

    private async Task StartEngineeringJobFromInputAsync()
    {
        var request = Input.Trim();
        if (request.Length == 0)
            return;
        Input = "/job start " + request;
        await SendAsync();
    }

    private async Task CaptureAcademyCorrectionCandidateAsync(string request)
    {
        var normalized = request.ToLowerInvariant();
        if (!(normalized.Contains("that's wrong", StringComparison.Ordinal) || normalized.Contains("that is wrong", StringComparison.Ordinal) ||
              normalized.StartsWith("correction", StringComparison.Ordinal) || normalized.Contains("actually,", StringComparison.Ordinal)))
            return;
        var previous = Messages.LastOrDefault(message => message.Role.Equals("Borin", StringComparison.OrdinalIgnoreCase))?.Text;
        if (string.IsNullOrWhiteSpace(previous)) return;
        await _academy.RecordCorrectionCandidateAsync(Configuration.RepositoryPath, previous, request,
            normalized.Contains("authoritative", StringComparison.Ordinal) || normalized.Contains("i confirm", StringComparison.Ordinal));
        _academySnapshot = await _academy.GetSnapshotAsync(Configuration.RepositoryPath);
        RaiseAcademyProperties();
    }

    private async Task RunAcademyAsync(bool cleanEvaluation, int requestedCount = 0)
    {
        IsBusy = true;
        _activeRequest?.Dispose();
        _activeRequest = new CancellationTokenSource();
        RaiseCommandStates();
        var token = _activeRequest.Token;
        try
        {
            var count = requestedCount > 0 ? Math.Clamp(requestedCount, 1, 100) : cleanEvaluation ? 16 : 8;
            var mode = cleanEvaluation ? "Clean mastery evaluation" : "Weakest subjects accelerated training";
            _academySnapshot = await _academy.MarkActivityAsync(Configuration.RepositoryPath, mode, "Generating authoritative questions", "Local retrieval", true, token);
            RaiseAcademyProperties();
            Status = cleanEvaluation ? "Running a clean evidence-based Academy evaluation..." : "Borin is training from authoritative local evidence...";
            TaskStage = mode;
            TaskProgress = 5;
            if (_knowledgeIndex is IBorinKnowledgeCore academyCore)
            {
                var indexStatus = await academyCore.GetStatusAsync(Configuration.RepositoryPath, token);
                if (indexStatus.IndexedFiles == 0)
                {
                    TaskStage = "Bulk authoritative knowledge bootstrap";
                    await academyCore.BuildAsync(Configuration.RepositoryPath, false, token);
                }
            }
            var questions = await _academy.GenerateQuestionsAsync(Configuration.RepositoryPath, cleanEvaluation ? "All Subjects" : "Weakest Subjects", count, token);
            if (questions.Count == 0)
            {
                Add("Borin", "The Academy could not generate an evidence-backed question. Reindex RealmGate and MajorMUD knowledge first; no mastery score was invented.");
                return;
            }
            var attempts = new List<BorinAcademyAttempt>();
            for (var i = 0; i < questions.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                var question = questions[i];
                var evidence = await _knowledgeIndex.SearchAsync(Configuration.RepositoryPath, question.EvidenceQuery, 20, token);
                var purpose = question.Difficulty == BorinAcademyDifficulty.Advanced
                    ? "Borin Academy deep reasoning with conflicting or multi-hop evidence"
                    : "Borin Academy evidence-based training";
                _academySnapshot = await _academy.MarkActivityAsync(Configuration.RepositoryPath, question.Subject,
                    $"Question {i + 1} / {questions.Count}", question.Difficulty == BorinAcademyDifficulty.Advanced ? "Ultra eligible" : "Lightning", true, token);
                RaiseAcademyProperties();
                var response = await _brain.ThinkAsync(BrainContext.ForBorin(
                    $"ACADEMY EVALUATION. Answer this question using only supplied authoritative evidence. Cite an exact local file/data location and state confidence. If evidence is insufficient, say UNKNOWN.\n\n{question.Prompt}",
                    _state, _snapshot, purpose: purpose, memory: _memorySnapshot, evidence: _lastProjectEvidence,
                    classicTrace: _lastClassicTrace, knowledge: evidence, bootstrap: _lastKnowledgeBootstrap), token);
                var attempt = await _academy.EvaluateAsync(Configuration.RepositoryPath, question, response.Text, response.Provider, token);
                attempts.Add(attempt);
                TaskProgress = 10 + (int)(85d * (i + 1) / questions.Count);
            }
            _academySnapshot = await _academy.MarkActivityAsync(Configuration.RepositoryPath, mode,
                $"Completed {attempts.Count} questions; {attempts.Count(a => a.Passed)} passed", "Local evaluation", false, token);
            RaiseAcademyProperties();
            var average = attempts.Average(a => a.Score.Total);
            Add("Borin", $"Academy {(cleanEvaluation ? "evaluation" : "training")} complete: {attempts.Count(a => a.Passed)}/{attempts.Count} passed; evidence-weighted score {average:F1}%. " +
                $"Failures created or updated knowledge gaps. Current MajorMUD mastery: {FormatMastery(_academySnapshot.MajorMudMastery)}; RealmGate mastery: {FormatMastery(_academySnapshot.ReforgedMastery)}.");
            TaskProgress = 100;
            TaskStage = "Academy session complete";
            Status = "Ready by the hearth.";
        }
        catch (OperationCanceledException)
        {
            _academySnapshot = await _academy.MarkActivityAsync(Configuration.RepositoryPath, "Paused", "Training canceled safely", "Local", false, CancellationToken.None);
            Add("Borin", "Academy training stopped safely. Completed attempts were retained; no partial score was invented.");
            Status = "Academy training canceled.";
        }
        finally
        {
            _activeRequest?.Dispose(); _activeRequest = null; IsBusy = false; RaiseAcademyProperties(); RaiseCommandStates();
        }
    }

    private async Task ShowAcademyGapsAsync()
    {
        var gaps = await _academy.GetGapsAsync(Configuration.RepositoryPath);
        var open = gaps.Where(g => g.Status != BorinKnowledgeGapStatus.Resolved).Take(30).ToArray();
        Add("Borin", open.Length == 0 ? "The Academy has no open knowledge gaps." : "Open Academy gaps:\n\n" + string.Join("\n\n", open.Select(g =>
            $"{g.Id} • {g.Priority} • {g.Status}\nSubject: {g.Subject}\nQuestion: {g.Question}\nMissing: {g.Missing}\nRecommended: {g.RecommendedTest}")));
    }

    private async Task InvestigateAcademyGapAsync()
    {
        var gaps = await _academy.GetGapsAsync(Configuration.RepositoryPath);
        var gap = gaps.FirstOrDefault(item => item.Status != BorinKnowledgeGapStatus.Resolved);
        if (gap is null) { Add("Borin", "The Academy has no open knowledge gaps."); return; }
        gap = await _academy.BeginGapInvestigationAsync(Configuration.RepositoryPath, gap.Id);
        var evidence = await _knowledgeIndex.SearchAsync(Configuration.RepositoryPath, gap.Question, 12);
        var proof = evidence.Matches.FirstOrDefault(item => item.Confidence == BorinKnowledgeConfidence.Proven && item.Sources is { Count: > 0 });
        var needsRuntime = gap.Missing.Contains("runtime", StringComparison.OrdinalIgnoreCase) || gap.Missing.Contains("conflict", StringComparison.OrdinalIgnoreCase) || gap.RecommendedTest.Contains("Test Realm", StringComparison.OrdinalIgnoreCase);
        if (proof is not null && !needsRuntime)
        {
            gap = await _academy.ResolveGapAsync(Configuration.RepositoryPath, gap.Id, proof.Location);
            Add("Borin", $"Resolved Academy gap `{gap.Id}` from PROVEN local evidence at `{proof.Location}`. The answer will be retested in the next clean evaluation.");
        }
        else
        {
            Add("Borin", $"Academy gap `{gap.Id}` is now under investigation. " + (needsRuntime
                ? "It requires controlled Test Realm/runtime evidence and was not marked resolved."
                : "No PROVEN source evidence was found, so it remains open rather than being guessed."));
        }
        _academySnapshot = await _academy.GetSnapshotAsync(Configuration.RepositoryPath); RaiseAcademyProperties();
    }

    private async Task<string?> BuildActiveAcademyGapCommissionAsync(string followUp)
    {
        var gap = (await _academy.GetGapsAsync(Configuration.RepositoryPath))
            .Where(item => item.Status == BorinKnowledgeGapStatus.Investigating)
            .OrderByDescending(item => item.UpdatedUtc)
            .FirstOrDefault();
        if (gap is null) return null;
        return $"{followUp}\n\nACTIVE BORIN ACADEMY GAP:\nID: {gap.Id}\nSubject: {gap.Subject}\nQuestion: {gap.Question}\nKnown evidence: {gap.KnownEvidence}\nMissing evidence: {gap.Missing}\nRecommended verification: {gap.RecommendedTest}";
    }

    private async Task ReplayAcademyHistoryAsync()
    {
        IsBusy = true; RaiseCommandStates();
        try
        {
            Status = "Replaying verified historical evidence locally...";
            _academySnapshot = await _academy.ReplayHistoryAsync(Configuration.RepositoryPath);
            _verifiedLearningStatus = await _verifiedKnowledgeLearning.GetStatusAsync();
            RaiseAcademyProperties(); Raise(nameof(VerifiedLearningSummary));
            Add("Borin", _academySnapshot.CurrentTask + " Original logs and reports remain authoritative; Academy stored references and verified lessons only.");
        }
        finally { IsBusy = false; Status = "Ready by the hearth."; RaiseCommandStates(); }
    }

    private async Task SetAcademyIdleAsync(bool enabled)
    {
        await _academy.SetIdleTrainingAsync(Configuration.RepositoryPath, enabled, AcademyIdleRequestsPerMinute);
        _academySnapshot = await _academy.GetSnapshotAsync(Configuration.RepositoryPath); RaiseAcademyProperties();
    }

    private async Task SetAcademyIdleRateAsync(int value)
    {
        await _academy.SetIdleTrainingAsync(Configuration.RepositoryPath, AcademyTrainWhileIdle, value);
        _academySnapshot = await _academy.GetSnapshotAsync(Configuration.RepositoryPath); RaiseAcademyProperties();
    }

    private void StartAcademyIdleMonitor()
    {
        if (_academyIdleMonitorTask is { IsCompleted: false }) return;
        _academyIdleMonitorCts = new CancellationTokenSource();
        var token = _academyIdleMonitorCts.Token;
        _academyIdleMonitorTask = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMinutes(1), token);
                if (_academySnapshot?.TrainWhileIdle != true || IsBusy || PlayerEnabled ||
                    _engineeringJob.Stage is BorinEngineeringJobStages.Investigating or BorinEngineeringJobStages.AppliedToWorkspace or BorinEngineeringJobStages.Built ||
                    _overallNvidiaBudget.Snapshot.RemainingHardRequests < 8)
                    continue;
                await Application.Current.Dispatcher.InvokeAsync(() => RunAcademyAsync(false, 1)).Task.Unwrap();
            }
        }, token);
    }

    private void RaiseAcademyProperties()
    {
        Raise(nameof(AcademyMajorMudMastery)); Raise(nameof(AcademyReforgedMastery)); Raise(nameof(AcademyCounts));
        Raise(nameof(AcademyActivity)); Raise(nameof(AcademySubjectSummary)); Raise(nameof(AcademyTrainWhileIdle)); Raise(nameof(AcademyIdleRequestsPerMinute));
    }

    private static string FormatMastery(double? value) => value is double score ? $"{score:F1}%" : "not yet measured";

    private async Task RecordVerifiedEngineeringLearningAsync(BorinEngineeringJobActionResult result, CancellationToken token)
    {
        if (!result.Succeeded || result.Job.Stage != BorinEngineeringJobStages.Validated)
            return;

        var evidence = $"Job {result.Job.Id}; build={result.Job.BuildSummary}; tests={result.Job.ValidationSummary}";
        await _verifiedKnowledgeLearning.RecordInvestigationAsync(
            result.Job.Request,
            "The isolated patch built and passed its configured validation. Main repository incorporation remains approval-gated.",
            evidence,
            true,
            result.Job.ProposedFiles,
            token);
        await _verifiedKnowledgeLearning.PromoteProcedureAsync(
            "Investigate and repair RealmGate defect",
            [
                "Capture the requested symptom and gather repository evidence.",
                "Trace the relevant source and references before drafting a focused patch.",
                "Apply the proposed patch only in Borin's isolated engineering workspace.",
                "Build the actual solution and run its configured validation suite.",
                "Present the verified diff for explicit incorporation approval; do not deploy or push automatically."
            ],
            true,
            evidence,
            token);
        _verifiedLearningStatus = await _verifiedKnowledgeLearning.GetStatusAsync(token);
        Raise(nameof(VerifiedLearningSummary));
    }

    private async Task IncorporateEngineeringJobAsync()
    {
        _engineeringJob = await _engineeringOrchestrator.GetCurrentAsync(Configuration.RepositoryPath);
        RaiseEngineeringJobProperties();
        if (!_engineeringJob.CanIncorporate)
        {
            Add("Borin", "Main-repository incorporation is unavailable until the isolated patch has passed its build and tests.");
            return;
        }

        var review = await _engineeringOrchestrator.ShowChangesAsync(Configuration.RepositoryPath);
        if (!review.Succeeded || string.IsNullOrWhiteSpace(review.Output))
        {
            Add("Borin", review.Summary);
            return;
        }

        var preview = review.Output.Length <= 8000 ? review.Output : review.Output[..8000] + "\n\n[Preview truncated; use Show Changes for the complete Forge view.]";
        var confirmation = MessageBox.Show(
            Application.Current.MainWindow,
            $"INCORPORATE VERIFIED BORIN PATCH\n\nJob: {_engineeringJob.Id}\nStage: {_engineeringJob.Stage}\n\nBuild: {_engineeringJob.BuildSummary}\nTests: {_engineeringJob.ValidationSummary}\n\nFiles:\n{string.Join("\n", _engineeringJob.ProposedFiles.Select(path => "- " + path))}\n\nDIFF PREVIEW\n{preview}\n\nApply this verified patch to the main RealmGate working tree? No commit, push, publish, install, or deployment will occur.",
            "William Approval Required",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (confirmation != MessageBoxResult.Yes)
        {
            Add("Borin", "Main-repository incorporation canceled. The verified patch remains only in the isolated workspace.");
            return;
        }

        IsBusy = true;
        Status = "Applying William-approved verified patch to the main repository...";
        RaiseCommandStates();
        try
        {
            var result = await _engineeringOrchestrator.IncorporateAsync(Configuration.RepositoryPath);
            _engineeringJob = result.Job;
            RaiseEngineeringJobProperties();
            Add("Borin", result.Summary + (string.IsNullOrWhiteSpace(result.Output) ? string.Empty : "\n\n" + result.Output));
            Status = result.Succeeded
                ? "Verified patch incorporated. No commit, push, publish, install, or deployment occurred."
                : "Incorporation was blocked safely; no target file was overwritten.";
            await PersistAsync();
        }
        finally
        {
            IsBusy = false;
            RaiseCommandStates();
        }
    }

    private async Task<bool> TryHandleEngineeringOrchestrationAsync(string request)
    {
        var trimmed = request.Trim();
        var normalized = trimmed.ToLowerInvariant();
        var action = string.Empty;
        var commission = string.Empty;
        var academyGapCommission = BorinEngineeringRequestRouter.IsContextualRepairFollowUp(trimmed)
            ? await BuildActiveAcademyGapCommissionAsync(trimmed)
            : null;

        if (normalized is "/correct-build" or "/correct build")
        {
            action = "correct";
        }
        else if (normalized is "/retry-validation" or "/retry validation")
        {
            action = "retry-validation";
        }
        else if (normalized is "/correction-status" or "/correction status")
        {
            action = "correction-status";
        }
        else if (normalized is "/show-attempts" or "/show attempts")
        {
            action = "attempts";
        }
        else if (normalized is "/stop-correction" or "/stop correction")
        {
            action = "stop-correction";
        }
        else if (normalized.StartsWith("/job", StringComparison.Ordinal))
        {
            var remainder = trimmed.Length > "/job".Length ? trimmed["/job".Length..].Trim() : string.Empty;
            var separator = remainder.IndexOf(' ');
            action = separator < 0 ? remainder.ToLowerInvariant() : remainder[..separator].ToLowerInvariant();
            commission = separator < 0 ? string.Empty : remainder[(separator + 1)..].Trim();
            if (action.Length == 0)
                action = "status";
            if (action is not ("start" or "status" or "list" or "changes" or "diff" or "approve" or "reject" or "build" or "build-test" or "validate" or "test" or "package" or "zip" or "correct" or "retry-validation" or "correction-status" or "attempts" or "stop-correction" or "help"))
            {
                commission = remainder;
                action = "start";
            }
        }
        else if (normalized is "approve this draft" or "approve the draft" or "apply this draft" or "approve it")
        {
            action = "approve";
        }
        else if (normalized is "reject this draft" or "reject the draft" or "reject it")
        {
            action = "reject";
        }
        else if (normalized is "correct the build" or "correct build" or "fix the build errors" or "repair the build" or "try to fix the build")
        {
            action = "correct";
        }
        else if (normalized is "retry validation" or "retry the validation" or "run validation again")
        {
            action = "retry-validation";
        }
        else if (normalized is "show correction status" or "correction status")
        {
            action = "correction-status";
        }
        else if (normalized is "show attempts" or "show correction attempts" or "show me the attempts")
        {
            action = "attempts";
        }
        else if (normalized is "stop correction" or "stop the correction loop")
        {
            action = "stop-correction";
        }
        // A new natural-language commission may include requested downstream steps such
        // as "build and test it". Recognize the commission before interpreting those
        // embedded words as a follow-up command for an already-active job.
        else if (BorinEngineeringRequestRouter.TryGetContextualCommission(
                     trimmed,
                     Messages.AsEnumerable().Reverse().Select(message => $"{message.Role}: {message.Text}"),
                     out var contextualCommission))
        {
            action = "start";
            commission = contextualCommission;
        }
        else if (!string.IsNullOrWhiteSpace(academyGapCommission))
        {
            action = "start";
            commission = academyGapCommission;
        }
        else if (BorinEngineeringRequestRouter.TryGetCommission(trimmed, out var naturalCommission))
        {
            action = "start";
            commission = naturalCommission;
        }
        else if (normalized.Contains("build and test", StringComparison.Ordinal) ||
                 normalized.Contains("build and validate", StringComparison.Ordinal))
        {
            action = "build-test";
        }
        else if (normalized is "create the update zip" or "create update zip" or "package this update" or "package the update" or "make the update zip")
        {
            action = "package";
        }
        else if (normalized is "show me the proposed changes" or "show proposed changes" or "show the draft" or "show me the draft")
        {
            action = "changes";
        }
        else if (normalized is "show engineering job" or "show the engineering job" or "what is the job status" or "engineering job status")
        {
            action = "status";
        }
        else
        {
            return false;
        }

        Add("You", request);
        IsBusy = true;
        _activeRequest?.Dispose();
        _activeRequest = new CancellationTokenSource();
        CurrentTask = request;
        TaskProgress = 0;
        RaiseCommandStates();
        try
        {
            if (action == "help")
            {
                Add("Borin", "Autonomous engineering job commands:\n\n" +
                    "- Say `Fix ...` or use `/job start <request>` — investigate and draft one isolated patch.\n" +
                    "- `/job status` — show the active job and its evidence state.\n" +
                    "- `/job list` — show recent jobs.\n" +
                    "- `/job changes` — show the proposed draft or isolated workspace diff.\n" +
                    "- New drafts apply automatically only to the isolated workspace; `/job approve` remains a legacy retry for drafts already waiting at that gate.\n" +
                    "- `/job reject` — reject the draft without changing the main repository.\n" +
                    "- Say `Build and test it` or use `/job build-test` — build and validate only the isolated workspace.\n" +
                    "- `/correct-build` or `/job correct` — inspect compiler/validator output, draft a scoped correction, apply it only to the isolated workspace, and retry up to three times.\n" +
                    "- `/retry-validation` — rerun the isolated build and validators without generating another correction.\n" +
                    "- `/correction-status` and `/show-attempts` — show the bounded correction ledger.\n" +
                    "- `/stop-correction` — stop further correction attempts for the current job.\n" +
                    "- Say `Create the update zip` or use `/job package` — package only after validation passes.\n\n" +
                    "Job stages are Investigating → Drafted → workspace apply → Built → Validated → Packaged. Corrections are limited to the exact drafted file set and no stage applies or installs changes in the main repository.");
                TaskStage = "Engineering job help shown";
                Status = "Ready by the hearth.";
                return true;
            }

            if (action == "start")
            {
                if (commission.Length == 0)
                {
                    Add("Borin", "Tell me the engineering commission after `/job start`, lad. No files were changed.");
                    TaskStage = "Engineering request needed";
                    Status = "Ready by the hearth.";
                    return true;
                }

                TaskStage = "Investigating engineering job";
                TaskProgress = 5;
                _engineeringJob = await _engineeringOrchestrator.BeginAsync(
                    Configuration.RepositoryPath,
                    commission,
                    _activeRequest.Token);
                _engineeringCorrectionStatus = BorinEngineeringCorrectionStatus.Empty(_engineeringJob.Id);
                RaiseEngineeringJobProperties();
                RaiseCorrectionProperties();

                Status = "Borin is gathering read-only evidence for the engineering job...";
                await CaptureDurableMemoryAsync(commission, _activeRequest.Token);
                await RefreshMemorySnapshotAsync(commission, _activeRequest.Token);
                _lastRecallSnapshot = _memorySnapshot;
                Raise(nameof(MemoryRecallSummary));

                TaskStage = "Loading compact RealmGate subsystem rules";
                TaskProgress = 18;
                _lastKnowledgeBootstrap = await _knowledgeBootstrap.PrepareAsync(
                    Configuration.RepositoryPath,
                    commission,
                    _activeRequest.Token);
                _knowledgeBootstrapStatus = _lastKnowledgeBootstrap.Status;
                Raise(nameof(KnowledgeBootstrapSummary));
                Raise(nameof(KnowledgeBootstrapRecallSummary));

                TaskStage = "Retrieving indexed source evidence";
                TaskProgress = 22;
                _lastKnowledgeEvidence = await _knowledgeIndex.SearchAsync(
                    Configuration.RepositoryPath,
                    commission,
                    maximumResults: 12,
                    _activeRequest.Token);
                Raise(nameof(KnowledgeRecallSummary));

                TaskStage = "Reading exact repository evidence";
                TaskProgress = 38;
                _lastProjectEvidence = await _readOnlyAccess.GatherAsync(
                    Configuration.RepositoryPath,
                    commission,
                    maximumResults: 16,
                    _activeRequest.Token);
                Raise(nameof(ReadOnlyAccessSummary));

                if (_classicTraceAccess.ShouldTrace(commission))
                {
                    TaskStage = "Tracing Classic provenance";
                    TaskProgress = 50;
                    _lastClassicTrace = await _classicTraceAccess.TraceAsync(
                        Configuration.RepositoryPath,
                        commission,
                        maximumResultsPerStage: 2,
                        _activeRequest.Token);
                }
                else
                {
                    _lastClassicTrace = BorinClassicTraceSnapshot.Empty(
                        Configuration.RepositoryPath,
                        commission,
                        "No structured Classic trace was required for this engineering job.");
                }
                Raise(nameof(ClassicTraceSummary));

                TaskStage = "Drafting isolated-workspace patch";
                TaskProgress = 68;
                Status = "Borin is drafting one reviewable patch. No files are being changed...";
                var prompt = $"""
Autonomous engineering job { _engineeringJob.Id }:

{commission}

Use only the supplied Reforged evidence. Give a concise diagnosis, assumptions, and exact files involved. Then provide exactly one fenced ```diff unified patch using repository-relative `a/` and `b/` paths. Do not include binary changes or file deletions. Keep the patch focused and compilable. After the patch, list the build and validation commands that still need to run. Local tooling will autonomously apply this proposal only inside Borin's isolated workspace, then build and test it. Never request or claim main-repository incorporation, installation, commit, push, or production modification.
""";
                var response = await _brain.ThinkAsync(
                    BrainContext.ForBorin(
                        prompt,
                        _state,
                        _snapshot,
                        "Autonomous engineering job draft",
                        _memorySnapshot,
                        _lastProjectEvidence,
                        _lastClassicTrace,
                        _lastKnowledgeEvidence,
                        _lastKnowledgeBootstrap),
                    _activeRequest.Token);
                ApplyBrainStatus(response);
                if (response.WasCanceled)
                {
                    var canceled = await _engineeringOrchestrator.FailAsync(
                        Configuration.RepositoryPath,
                        "Engineering draft was canceled before approval. No files changed.",
                        CancellationToken.None);
                    _engineeringJob = canceled.Job;
                    TaskStage = "Engineering draft canceled";
                    TaskProgress = 0;
                    Status = "Engineering draft canceled. Main repository remains locked.";
                    RaiseEngineeringJobProperties();
                    return true;
                }

                _engineeringJob = await _engineeringOrchestrator.RecordDraftAsync(
                    Configuration.RepositoryPath,
                    response.Text,
                    _activeRequest.Token);
                RaiseEngineeringJobProperties();

                if (!Configuration.AllowWorkspaceEdits)
                {
                    TaskStage = "Workspace editing disabled";
                    TaskProgress = 100;
                    Status = "Draft ready, but isolated workspace editing is disabled.";
                    Add("Borin", $"Job `{_engineeringJob.Id}` has a reviewable draft, but isolated workspace editing is disabled. The main repository remains untouched.");
                    return true;
                }

                TaskStage = "Applying draft to isolated workspace";
                TaskProgress = 76;
                Status = "Applying the draft only inside Borin's isolated workspace...";
                var workspaceApply = await _engineeringOrchestrator.ApproveAsync(Configuration.RepositoryPath, _activeRequest.Token);
                _engineeringJob = workspaceApply.Job;
                RaiseEngineeringJobProperties();
                if (!workspaceApply.Succeeded)
                {
                    Add("Borin", "I rejected the proposed patch before presenting it as viable. " + workspaceApply.Summary +
                        (string.IsNullOrWhiteSpace(workspaceApply.Output) ? string.Empty : "\n\n" + LimitEngineeringText(workspaceApply.Output, 4000)));
                    TaskStage = "Workspace patch failed safely";
                    TaskProgress = 0;
                    Status = "The isolated patch could not be applied. Main repository remains untouched.";
                    return true;
                }

                Add("Borin", response.Text);
                Add("Borin", workspaceApply.Summary + (string.IsNullOrWhiteSpace(workspaceApply.Output) ? string.Empty : "\n\n" + LimitEngineeringText(workspaceApply.Output, 12000)));

                TaskStage = "Building and testing isolated workspace";
                TaskProgress = 84;
                Status = "Building and validating Borin's isolated workspace...";
                var verification = await _engineeringOrchestrator.BuildAndTestAsync(Configuration.RepositoryPath, _activeRequest.Token);
                _engineeringJob = verification.Job;
                RaiseEngineeringJobProperties();
                Add("Borin", verification.Summary + (string.IsNullOrWhiteSpace(verification.Output) ? string.Empty : "\n\n" + LimitEngineeringText(verification.Output, 30000)));
                if (!verification.Succeeded)
                {
                    TaskStage = "Running bounded correction loop";
                    var corrected = await RunBuildCorrectionLoopAsync(_activeRequest.Token);
                    _engineeringJob = corrected.Job;
                    RaiseEngineeringJobProperties();
                    Add("Borin", corrected.Summary + (string.IsNullOrWhiteSpace(corrected.Output) ? string.Empty : "\n\n" + LimitEngineeringText(corrected.Output, 30000)));
                    verification = corrected;
                }

                await RecordVerifiedEngineeringLearningAsync(verification, _activeRequest.Token);

                TaskProgress = verification.Succeeded ? 100 : 0;
                TaskStage = verification.Succeeded ? "Verified patch ready for review" : "Unable to verify safely";
                Status = verification.Succeeded
                    ? "The isolated patch built and tested. Main-repository incorporation awaits William's permission."
                    : "Unable to verify safely. Human review is required; the main repository remains untouched.";
                return true;
            }

            if (action == "status")
            {
                _engineeringJob = await _engineeringOrchestrator.GetCurrentAsync(Configuration.RepositoryPath, _activeRequest.Token);
                RaiseEngineeringJobProperties();
                Add("Borin", FormatEngineeringJob(_engineeringJob));
                TaskStage = "Engineering job status read";
                Status = "Ready by the hearth.";
                return true;
            }

            if (action == "list")
            {
                var jobs = await _engineeringOrchestrator.ListAsync(Configuration.RepositoryPath, _activeRequest.Token);
                var output = jobs.Count == 0
                    ? "No autonomous engineering jobs have been recorded."
                    : string.Join("\n", jobs.Select(job => $"- `{job.Id}` • {job.Stage} • {job.Request}"));
                Add("Borin", "Recent engineering jobs:\n\n" + output);
                TaskStage = "Engineering jobs listed";
                Status = "Ready by the hearth.";
                return true;
            }

            if (action is "correction-status" or "attempts")
            {
                _engineeringJob = await _engineeringOrchestrator.GetCurrentAsync(Configuration.RepositoryPath, _activeRequest.Token);
                _engineeringCorrectionStatus = _engineeringJob.Exists
                    ? await _engineeringCorrectionLoop.GetStatusAsync(Configuration.RepositoryPath, _engineeringJob.Id, _activeRequest.Token)
                    : BorinEngineeringCorrectionStatus.Empty(string.Empty);
                RaiseEngineeringJobProperties();
                RaiseCorrectionProperties();
                Add("Borin", action == "attempts"
                    ? FormatCorrectionAttempts(_engineeringCorrectionStatus)
                    : _engineeringCorrectionStatus.Summary);
                TaskStage = "Correction ledger read";
                TaskProgress = 100;
                Status = "Ready by the hearth.";
                return true;
            }

            if (action == "stop-correction")
            {
                _engineeringJob = await _engineeringOrchestrator.GetCurrentAsync(Configuration.RepositoryPath, _activeRequest.Token);
                if (!_engineeringJob.Exists)
                {
                    Add("Borin", "No autonomous engineering job is active.");
                    TaskStage = "No correction job active";
                    Status = "Ready by the hearth.";
                    return true;
                }
                _engineeringCorrectionStatus = await _engineeringCorrectionLoop.StopAsync(
                    Configuration.RepositoryPath,
                    _engineeringJob.Id,
                    "Correction loop stopped by explicit user command.",
                    _activeRequest.Token);
                RaiseCorrectionProperties();
                Add("Borin", _engineeringCorrectionStatus.Summary);
                TaskStage = "Correction loop stopped";
                TaskProgress = 100;
                Status = "Correction loop stopped. Main repository remains locked.";
                return true;
            }

            BorinEngineeringJobActionResult result;
            switch (action)
            {
                case "changes":
                case "diff":
                    TaskStage = "Reading proposed changes";
                    TaskProgress = 45;
                    result = await _engineeringOrchestrator.ShowChangesAsync(Configuration.RepositoryPath, _activeRequest.Token);
                    break;

                case "approve":
                    TaskStage = "Applying approved draft to isolated workspace";
                    TaskProgress = 35;
                    Status = "Applying the approved draft only inside Borin's isolated workspace...";
                    result = await _engineeringOrchestrator.ApproveAsync(Configuration.RepositoryPath, _activeRequest.Token);
                    break;

                case "reject":
                    TaskStage = "Rejecting engineering draft";
                    TaskProgress = 60;
                    result = await _engineeringOrchestrator.RejectAsync(Configuration.RepositoryPath, _activeRequest.Token);
                    break;

                case "build":
                case "build-test":
                case "validate":
                case "test":
                    TaskStage = "Building and validating isolated workspace";
                    TaskProgress = 30;
                    Status = "Building and validating only Borin's isolated workspace...";
                    result = await _engineeringOrchestrator.BuildAndTestAsync(Configuration.RepositoryPath, _activeRequest.Token);
                    break;

                case "correct":
                    TaskStage = "Running bounded build-correction loop";
                    TaskProgress = 20;
                    Status = "Borin is correcting only the approved isolated-workspace files...";
                    result = await RunBuildCorrectionLoopAsync(_activeRequest.Token);
                    break;

                case "retry-validation":
                    TaskStage = "Retrying isolated build and validation";
                    TaskProgress = 35;
                    Status = "Retrying the isolated build and validators without generating another patch...";
                    result = await _engineeringOrchestrator.BuildAndTestAsync(Configuration.RepositoryPath, _activeRequest.Token);
                    _engineeringJob = result.Job;
                    if (_engineeringJob.Exists)
                    {
                        _engineeringCorrectionStatus = await _engineeringCorrectionLoop.CompleteLatestAsync(
                            Configuration.RepositoryPath,
                            _engineeringJob.Id,
                            result.Succeeded || !string.IsNullOrWhiteSpace(_engineeringJob.ValidationSummary),
                            result.Succeeded,
                            result.Summary + (string.IsNullOrWhiteSpace(result.Output) ? string.Empty : "\n" + result.Output),
                            _activeRequest.Token);
                        RaiseCorrectionProperties();
                    }
                    break;

                case "package":
                case "zip":
                    TaskStage = "Packaging validated engineering job";
                    TaskProgress = 55;
                    Status = "Creating a review-only update ZIP without installing it...";
                    result = await _engineeringOrchestrator.PackageAsync(Configuration.RepositoryPath, _activeRequest.Token);
                    break;

                default:
                    Add("Borin", "Unknown engineering job command. Use `/job help`.");
                    TaskStage = "Unknown engineering job command";
                    Status = "Ready by the hearth.";
                    return true;
            }

            _engineeringJob = result.Job;
            if (action is "build" or "build-test" or "validate" or "test" or "retry-validation" or "correct")
                await RecordVerifiedEngineeringLearningAsync(result, _activeRequest.Token);
            RaiseEngineeringJobProperties();
            if (_engineeringJob.Exists)
            {
                _engineeringCorrectionStatus = await _engineeringCorrectionLoop.GetStatusAsync(
                    Configuration.RepositoryPath,
                    _engineeringJob.Id,
                    CancellationToken.None);
                RaiseCorrectionProperties();
            }
            var text = new StringBuilder(result.Summary);
            if (!string.IsNullOrWhiteSpace(result.ArtifactPath))
                text.Append($"\n\nArtifact: `{result.ArtifactPath}`");
            if (!string.IsNullOrWhiteSpace(result.Output))
                text.Append("\n\n").Append(result.Output.Length > 60000 ? result.Output[^60000..] : result.Output);
            Add("Borin", text.ToString());
            RecordJournal("Engineering job " + action, result.Summary, result.Succeeded);

            try
            {
                _engineeringWorkspaceStatus = await _engineeringWorkspace.GetStatusAsync(Configuration.RepositoryPath, CancellationToken.None);
                Raise(nameof(EngineeringWorkspaceSummary));
                Raise(nameof(EngineeringWorkspacePath));
            }
            catch
            {
                // Job result remains authoritative even if the secondary workspace summary cannot refresh.
            }

            TaskStage = result.Succeeded ? _engineeringJob.Stage : "Engineering job needs attention";
            TaskProgress = result.Succeeded ? 100 : 0;
            Status = result.Succeeded
                ? "Engineering job advanced safely. Main repository remains locked."
                : "Engineering job stopped safely. Main repository remains locked.";
            return true;
        }
        catch (OperationCanceledException)
        {
            Add("Borin", "Aye, the engineering job stopped. The main repository remains untouched.");
            try
            {
                var canceled = await _engineeringOrchestrator.FailAsync(
                    Configuration.RepositoryPath,
                    "Engineering job canceled. Main repository remains untouched.",
                    CancellationToken.None);
                _engineeringJob = canceled.Job;
                RaiseEngineeringJobProperties();
            }
            catch
            {
                // Preserve the original cancellation.
            }
            if (_engineeringJob.Exists)
            {
                try
                {
                    _engineeringCorrectionStatus = await _engineeringCorrectionLoop.StopAsync(
                        Configuration.RepositoryPath,
                        _engineeringJob.Id,
                        "Correction loop canceled by the user.",
                        CancellationToken.None);
                    RaiseCorrectionProperties();
                }
                catch
                {
                    // Preserve the original cancellation.
                }
            }
            TaskStage = "Engineering job canceled";
            TaskProgress = 0;
            Status = "Engineering job canceled safely.";
            return true;
        }
        catch (Exception exception)
        {
            Add("Borin", $"The autonomous engineering job failed safely: {exception.Message}\n\nThe main repository was not changed.");
            try
            {
                var failed = await _engineeringOrchestrator.FailAsync(
                    Configuration.RepositoryPath,
                    exception.Message,
                    CancellationToken.None);
                _engineeringJob = failed.Job;
                RaiseEngineeringJobProperties();
            }
            catch
            {
                // Keep the original failure visible.
            }
            TaskStage = "Engineering job failed safely";
            TaskProgress = 0;
            Status = "Engineering job failed safely.";
            return true;
        }
        finally
        {
            _activeRequest?.Dispose();
            _activeRequest = null;
            IsBusy = false;
            await PersistAsync();
            RaiseCommandStates();
        }
    }

    private async Task<BorinEngineeringJobActionResult> RunBuildCorrectionLoopAsync(CancellationToken token)
    {
        _engineeringJob = await _engineeringOrchestrator.GetCurrentAsync(Configuration.RepositoryPath, token);
        RaiseEngineeringJobProperties();
        if (!_engineeringJob.Exists)
            return new(false, _engineeringJob, "No autonomous engineering job is active.");
        if (_engineeringJob.Stage is BorinEngineeringJobStages.AwaitingApproval or BorinEngineeringJobStages.Investigating or BorinEngineeringJobStages.Drafted)
            return new(false, _engineeringJob, "Approve and apply the reviewed draft to the isolated workspace before starting corrections.");
        if (_engineeringJob.Stage is BorinEngineeringJobStages.Packaged or BorinEngineeringJobStages.Rejected)
            return new(false, _engineeringJob, $"Job {_engineeringJob.Id} is already {_engineeringJob.Stage} and cannot enter correction.");

        _engineeringCorrectionStatus = await _engineeringCorrectionLoop.GetStatusAsync(
            Configuration.RepositoryPath,
            _engineeringJob.Id,
            token);
        RaiseCorrectionProperties();
        if (_engineeringCorrectionStatus.IsStopped)
            return new(false, _engineeringJob, _engineeringCorrectionStatus.Summary);

        var latestPatchNeedsVerification = _engineeringCorrectionStatus.Latest?.Outcome == BorinEngineeringCorrectionOutcomes.Applied;
        BorinEngineeringJobActionResult verification = new(false, _engineeringJob, "Correction loop did not run.");

        while (true)
        {
            token.ThrowIfCancellationRequested();
            TaskStage = latestPatchNeedsVerification
                ? $"Verifying correction attempt {_engineeringCorrectionStatus.AttemptCount}"
                : "Reproducing isolated build failure";
            TaskProgress = Math.Min(85, 20 + (_engineeringCorrectionStatus.AttemptCount * 20));
            Status = "Building and validating only the isolated workspace...";

            verification = await _engineeringOrchestrator.BuildAndTestAsync(Configuration.RepositoryPath, token);
            _engineeringJob = verification.Job;
            RaiseEngineeringJobProperties();

            if (latestPatchNeedsVerification)
            {
                _engineeringCorrectionStatus = await _engineeringCorrectionLoop.CompleteLatestAsync(
                    Configuration.RepositoryPath,
                    _engineeringJob.Id,
                    verification.Succeeded || !string.IsNullOrWhiteSpace(_engineeringJob.ValidationSummary),
                    verification.Succeeded,
                    verification.Summary + (string.IsNullOrWhiteSpace(verification.Output) ? string.Empty : "\n" + LimitEngineeringText(verification.Output, 30000)),
                    token);
                RaiseCorrectionProperties();
            }

            if (verification.Succeeded)
            {
                TaskStage = "Correction loop validated";
                TaskProgress = 100;
                Status = "The isolated workspace now builds and validates. Main repository remains locked.";
                return new(true, _engineeringJob,
                    $"Build correction succeeded after {_engineeringCorrectionStatus.AttemptCount} correction attempt(s).",
                    verification.Output);
            }

            if (!_engineeringCorrectionStatus.CanAttempt)
            {
                TaskStage = "Correction retry limit reached";
                TaskProgress = 0;
                Status = "Correction stopped at the bounded retry limit. Main repository remains locked.";
                return new(false, _engineeringJob,
                    $"The isolated workspace still fails and the {_engineeringCorrectionStatus.MaximumAttempts}-attempt correction limit has been reached.",
                    verification.Output);
            }

            var diff = await _engineeringOrchestrator.ShowChangesAsync(Configuration.RepositoryPath, token);
            var diagnosticText = string.IsNullOrWhiteSpace(verification.Output)
                ? verification.Summary
                : verification.Output;
            var approvedFiles = string.Join("\n", _engineeringJob.ProposedFiles.Select(path => "- " + path));
            var prompt = $"""
Build-correction attempt {_engineeringCorrectionStatus.AttemptCount + 1} of {_engineeringCorrectionStatus.MaximumAttempts} for engineering job {_engineeringJob.Id}.

Original approved request:
{_engineeringJob.Request}

Exact approved file scope (do not touch any other path):
{approvedFiles}

Current isolated-workspace build/validation failure:
{LimitEngineeringText(diagnosticText, 24000)}

Current isolated-workspace diff:
{LimitEngineeringText(diff.Output, 24000)}

Produce exactly one fenced ```diff unified patch that corrects only the reported compiler or validator failure. Use repository-relative a/ and b/ paths. Every changed path must be in the exact approved file scope above. Do not delete files, add binaries, broaden the feature, change the main repository, install anything, commit, or push. Prefer the smallest compilable correction. After the diff, give one short sentence describing what the next isolated build must verify. Do not claim the correction passed until the tool result proves it.
""";

            TaskStage = $"Drafting correction attempt {_engineeringCorrectionStatus.AttemptCount + 1}";
            Status = "Borin is drafting one scope-locked correction from the actual build output...";
            var response = await _brain.ThinkAsync(
                BrainContext.ForBorin(
                    prompt,
                    _state,
                    _snapshot,
                    "Isolated workspace build correction",
                    _memorySnapshot,
                    _lastProjectEvidence,
                    _lastClassicTrace,
                    _lastKnowledgeEvidence,
                    _lastKnowledgeBootstrap),
                token);
            Add("Borin", response.Text);
            ApplyBrainStatus(response);
            if (response.WasCanceled)
                return new(false, _engineeringJob, "Correction drafting was canceled. The main repository was untouched.");

            var applied = await _engineeringCorrectionLoop.ApplyCorrectionAsync(
                Configuration.RepositoryPath,
                _engineeringJob,
                diagnosticText,
                response.Text,
                token);
            _engineeringCorrectionStatus = applied.Status;
            RaiseCorrectionProperties();
            Add("Borin", applied.Summary + (string.IsNullOrWhiteSpace(applied.Output) ? string.Empty : "\n\n" + LimitEngineeringText(applied.Output, 12000)));
            if (!applied.Succeeded)
                return new(false, _engineeringJob, applied.Summary, applied.Output);

            latestPatchNeedsVerification = true;
        }
    }

    private static string LimitEngineeringText(string value, int maximumCharacters)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "[No output was captured.]";
        value = value.Trim();
        return value.Length <= maximumCharacters ? value : value[^maximumCharacters..];
    }

    private static string FormatEngineeringJob(BorinEngineeringJobSnapshot job)
    {
        if (!job.Exists)
            return job.Summary;
        var files = job.ProposedFiles.Count == 0 ? "None yet" : string.Join("\n", job.ProposedFiles.Select(path => "- " + path));
        var artifact = job.ArtifactPath.Length == 0 ? "None" : job.ArtifactPath;
        var history = job.History.Count == 0
            ? "No stage history recorded."
            : string.Join("\n", job.History.Select(item => $"- {item.At:yyyy-MM-dd HH:mm:ss} • {item.Stage} • {item.Summary}"));
        return $"Engineering job `{job.Id}`\n\nStage: **{job.Stage}**\nRequest: {job.Request}\n\n{job.Summary}\n\nProposed files:\n{files}\n\nStage history:\n{history}\n\nArtifact: `{artifact}`";
    }

    private static string FormatCorrectionAttempts(BorinEngineeringCorrectionStatus status)
    {
        if (status.Attempts.Count == 0)
            return status.Summary;
        var lines = status.Attempts.Select(attempt =>
            $"- Attempt {attempt.Number}/{status.MaximumAttempts} • {attempt.Outcome} • {string.Join(", ", attempt.Files)} • {attempt.ResultSummary}");
        return status.Summary + "\n" + string.Join("\n", lines);
    }

    private void RaiseCorrectionProperties()
    {
        Raise(nameof(EngineeringCorrectionSummary));
        Raise(nameof(EngineeringCorrectionAttempts));
    }

    private void RaiseEngineeringJobProperties()
    {
        Raise(nameof(EngineeringJobSummary));
        Raise(nameof(EngineeringJobStage));
        Raise(nameof(EngineeringJobRequest));
        Raise(nameof(EngineeringJobFiles));
        Raise(nameof(EngineeringJobArtifact));
        Raise(nameof(CanIncorporateEngineeringJob));
    }

    private async Task DraftEngineeringFromInputAsync()
    {
        var request = Input.Trim();
        if (request.Length == 0)
            return;
        Input = string.Empty;
        if (!request.StartsWith("/draft", StringComparison.OrdinalIgnoreCase))
            request = "/draft " + request;
        await TryHandleEngineeringCommandAsync(request);
    }

    private Task RunEngineeringWorkspaceCommandAsync(string action)
    {
        Input = "/workspace " + action;
        return SendAsync();
    }

    private async Task<bool> TryHandleEngineeringCommandAsync(string request)
    {
        var trimmed = request.Trim();
        var isDraft = trimmed.StartsWith("/draft", StringComparison.OrdinalIgnoreCase);
        var isWorkspace = trimmed.StartsWith("/workspace", StringComparison.OrdinalIgnoreCase);
        var isHelp = trimmed.Equals("/engineer-help", StringComparison.OrdinalIgnoreCase) ||
                     trimmed.Equals("/engineering-help", StringComparison.OrdinalIgnoreCase);
        if (!isDraft && !isWorkspace && !isHelp)
            return false;

        Add("You", request);
        IsBusy = true;
        _activeRequest?.Dispose();
        _activeRequest = new CancellationTokenSource();
        CurrentTask = request;
        TaskProgress = 0;
        RaiseCommandStates();
        try
        {
            if (isHelp || trimmed.Equals("/workspace", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("/workspace help", StringComparison.OrdinalIgnoreCase))
            {
                Add("Borin", "Controlled engineering commands:\n\n" +
                    "- `/draft <request>` — prepare one evidence-grounded unified diff without changing files.\n" +
                    "- `/workspace create` — copy the current local repository into Borin's isolated snapshot.\n" +
                    "- `/workspace recreate` — discard the old snapshot and copy the current repository again.\n" +
                    "- `/workspace status` — show the private workspace state.\n" +
                    "- `/workspace apply` — apply the latest fenced diff only inside the private workspace.\n" +
                    "- `/workspace diff` — show all workspace source changes.\n" +
                    "- `/workspace build` — compile only the private workspace.\n" +
                    "- `/workspace test` — test only the private workspace.\n" +
                    "- `/workspace package` — create a reviewable update ZIP under `AI\\Workspaces\\Borin\\Packages`.\n\n" +
                    "There is no command in this stage that applies changes to the main repository, runs an installer, commits, or pushes.");
                TaskStage = "Engineering help shown";
                Status = "Ready by the hearth.";
                return true;
            }

            if (isDraft)
            {
                var commission = trimmed.Length > "/draft".Length
                    ? trimmed["/draft".Length..].Trim()
                    : string.Empty;
                if (commission.Length == 0)
                {
                    Add("Borin", "Tell me what to draft after `/draft`, lad. No workspace files were changed.");
                    TaskStage = "Draft request needed";
                    Status = "Ready by the hearth.";
                    return true;
                }

                TaskStage = "Recalling engineering constraints";
                TaskProgress = 10;
                Status = "Borin is recalling project rules and relevant memories...";
                await CaptureDurableMemoryAsync(commission, _activeRequest.Token);
                await RefreshMemorySnapshotAsync(commission, _activeRequest.Token);
                _lastRecallSnapshot = _memorySnapshot;
                Raise(nameof(MemoryRecallSummary));

                TaskStage = "Loading compact RealmGate subsystem rules";
                TaskProgress = 20;
                _lastKnowledgeBootstrap = await _knowledgeBootstrap.PrepareAsync(
                    Configuration.RepositoryPath,
                    commission,
                    _activeRequest.Token);
                _knowledgeBootstrapStatus = _lastKnowledgeBootstrap.Status;
                Raise(nameof(KnowledgeBootstrapSummary));
                Raise(nameof(KnowledgeBootstrapRecallSummary));

                TaskStage = "Retrieving indexed source evidence";
                TaskProgress = 25;
                _lastKnowledgeEvidence = await _knowledgeIndex.SearchAsync(
                    Configuration.RepositoryPath,
                    commission,
                    maximumResults: 12,
                    _activeRequest.Token);
                Raise(nameof(KnowledgeRecallSummary));

                TaskStage = "Reading exact repository evidence";
                TaskProgress = 40;
                _lastProjectEvidence = await _readOnlyAccess.GatherAsync(
                    Configuration.RepositoryPath,
                    commission,
                    maximumResults: 16,
                    _activeRequest.Token);
                Raise(nameof(ReadOnlyAccessSummary));

                if (_classicTraceAccess.ShouldTrace(commission))
                {
                    TaskStage = "Tracing Classic provenance before drafting";
                    TaskProgress = 52;
                    _lastClassicTrace = await _classicTraceAccess.TraceAsync(
                        Configuration.RepositoryPath,
                        commission,
                        maximumResultsPerStage: 2,
                        _activeRequest.Token);
                }
                else
                {
                    _lastClassicTrace = BorinClassicTraceSnapshot.Empty(
                        Configuration.RepositoryPath,
                        commission,
                        "No structured Classic trace was requested for this engineering draft.");
                }
                Raise(nameof(ClassicTraceSummary));

                TaskStage = "Drafting isolated patch";
                TaskProgress = 68;
                Status = "Borin is drafting a reviewable patch. No files are being changed...";
                var prompt = $"""
Prepare a controlled engineering draft for this request:

{commission}

Use only the supplied Reforged evidence. First give a concise diagnosis and assumptions. Then provide exactly one fenced ```diff unified patch using repository-relative `a/` and `b/` paths. Do not include binary changes or file deletions. Keep the patch focused and compilable. After the patch, list the validations that still need to run. Do not claim the patch was applied, built, tested, installed, validated, committed, or pushed.
""";
                var response = await _brain.ThinkAsync(
                    BrainContext.ForBorin(
                        prompt,
                        _state,
                        _snapshot,
                        "Controlled engineering draft",
                        _memorySnapshot,
                        _lastProjectEvidence,
                        _lastClassicTrace,
                        _lastKnowledgeEvidence,
                        _lastKnowledgeBootstrap),
                    _activeRequest.Token);
                Add("Borin", response.Text);
                ApplyBrainStatus(response);
                TaskStage = response.WasCanceled ? "Draft stopped" : "Draft ready for review";
                TaskProgress = response.WasCanceled ? 0 : 100;
                Status = response.WasCanceled
                    ? "Engineering draft stopped. No files changed."
                    : "Draft ready. Review it, then use /workspace apply if ye approve the diff for the isolated workspace.";
                return true;
            }

            var action = trimmed.Length > "/workspace".Length
                ? trimmed["/workspace".Length..].Trim().ToLowerInvariant()
                : "status";
            BorinEngineeringOperationResult? operation = null;
            switch (action)
            {
                case "create":
                    TaskStage = "Creating private repository snapshot";
                    TaskProgress = 25;
                    Status = "Copying the current local repository into Borin's controlled workspace...";
                    _engineeringWorkspaceStatus = await _engineeringWorkspace.CreateAsync(
                        Configuration.RepositoryPath,
                        recreate: false,
                        _activeRequest.Token);
                    Add("Borin", $"{_engineeringWorkspaceStatus.Summary}\n\nWorkspace: `{_engineeringWorkspaceStatus.WorkspacePath}`");
                    break;

                case "recreate":
                case "reset":
                    TaskStage = "Recreating private repository snapshot";
                    TaskProgress = 25;
                    Status = "Refreshing Borin's controlled workspace from the current repository...";
                    _engineeringWorkspaceStatus = await _engineeringWorkspace.CreateAsync(
                        Configuration.RepositoryPath,
                        recreate: true,
                        _activeRequest.Token);
                    Add("Borin", $"{_engineeringWorkspaceStatus.Summary}\n\nWorkspace: `{_engineeringWorkspaceStatus.WorkspacePath}`");
                    break;

                case "status":
                    TaskStage = "Inspecting controlled workspace";
                    TaskProgress = 50;
                    _engineeringWorkspaceStatus = await _engineeringWorkspace.GetStatusAsync(
                        Configuration.RepositoryPath,
                        _activeRequest.Token);
                    Add("Borin", $"{_engineeringWorkspaceStatus.Summary}\n\nWorkspace: `{_engineeringWorkspaceStatus.WorkspacePath}`\nChanged files: {_engineeringWorkspaceStatus.ChangedFileCount}");
                    break;

                case "apply":
                    TaskStage = "Validating draft patch";
                    TaskProgress = 40;
                    Status = "Validating and applying the latest draft only inside the private workspace...";
                    var latestReply = _state.Messages
                        .LastOrDefault(message => !message.Role.Equals("You", StringComparison.OrdinalIgnoreCase) &&
                                                  message.Text.Contains("```diff", StringComparison.OrdinalIgnoreCase))
                        ?.Text ?? string.Empty;
                    operation = await _engineeringWorkspace.ApplyDraftAsync(
                        Configuration.RepositoryPath,
                        latestReply,
                        _activeRequest.Token);
                    break;

                case "diff":
                    TaskStage = "Reading controlled workspace diff";
                    TaskProgress = 55;
                    operation = await _engineeringWorkspace.ShowDiffAsync(Configuration.RepositoryPath, _activeRequest.Token);
                    break;

                case "build":
                    TaskStage = "Building private workspace";
                    TaskProgress = 35;
                    Status = "Building only Borin's controlled workspace...";
                    operation = await _engineeringWorkspace.BuildAsync(Configuration.RepositoryPath, _activeRequest.Token);
                    break;

                case "test":
                    TaskStage = "Testing private workspace";
                    TaskProgress = 35;
                    Status = "Testing only Borin's controlled workspace...";
                    operation = await _engineeringWorkspace.TestAsync(Configuration.RepositoryPath, _activeRequest.Token);
                    break;

                case "package":
                case "zip":
                    TaskStage = "Packaging review-only update ZIP";
                    TaskProgress = 55;
                    Status = "Packaging changed workspace files without installing them...";
                    operation = await _engineeringWorkspace.CreateUpdateZipAsync(Configuration.RepositoryPath, _activeRequest.Token);
                    break;

                default:
                    Add("Borin", "Unknown workspace command. Type `/engineer-help` for the controlled Stage 6 commands.");
                    TaskStage = "Unknown engineering command";
                    Status = "Ready by the hearth.";
                    return true;
            }

            if (operation is not null)
            {
                var text = new StringBuilder(operation.Summary);
                if (!string.IsNullOrWhiteSpace(operation.ArtifactPath))
                    text.Append($"\n\nArtifact: `{operation.ArtifactPath}`");
                if (!string.IsNullOrWhiteSpace(operation.Output))
                    text.Append("\n\n").Append(operation.Output);
                Add("Borin", text.ToString());
                RecordJournal("Controlled workspace " + action, operation.Summary, operation.Succeeded);
            }

            _engineeringWorkspaceStatus = await _engineeringWorkspace.GetStatusAsync(
                Configuration.RepositoryPath,
                CancellationToken.None);
            Raise(nameof(EngineeringWorkspaceSummary));
            Raise(nameof(EngineeringWorkspacePath));
            TaskStage = operation is null || operation.Succeeded ? "Engineering operation complete" : "Engineering operation needs attention";
            TaskProgress = operation is null || operation.Succeeded ? 100 : 0;
            Status = operation is null || operation.Succeeded
                ? "Ready by the hearth. Main repository remains locked."
                : "Workspace operation failed safely. Main repository remains locked.";
            return true;
        }
        catch (OperationCanceledException)
        {
            Add("Borin", "Aye, the engineering operation stopped. The main repository remains untouched.");
            TaskStage = "Engineering operation canceled";
            TaskProgress = 0;
            Status = "Engineering operation canceled safely.";
            return true;
        }
        catch (Exception exception)
        {
            Add("Borin", $"The controlled engineering operation failed safely: {exception.Message}\n\nThe main repository was not changed.");
            TaskStage = "Engineering operation failed safely";
            TaskProgress = 0;
            Status = "Engineering operation failed safely.";
            return true;
        }
        finally
        {
            _activeRequest?.Dispose();
            _activeRequest = null;
            IsBusy = false;
            await PersistAsync();
            RaiseCommandStates();
        }
    }

    private async Task<bool> TryHandleBootstrapCommandAsync(string request)
    {
        var trimmed = request.Trim();
        if (!trimmed.StartsWith("/bootstrap", StringComparison.OrdinalIgnoreCase))
            return false;

        IsBusy = true;
        _activeRequest?.Dispose();
        _activeRequest = new CancellationTokenSource();
        CurrentTask = trimmed;
        TaskProgress = 20;
        RaiseCommandStates();
        try
        {
            var action = trimmed.Length > "/bootstrap".Length
                ? trimmed["/bootstrap".Length..].Trim().ToLowerInvariant()
                : "status";

            switch (action)
            {
                case "help":
                    Add("Borin",
                        "Compact RealmGate bootstrap commands:\n\n" +
                        "`/bootstrap status` — show card and stale-source status.\n" +
                        "`/bootstrap refresh` — accept the current repository fingerprint as the new baseline.\n" +
                        "`/learn changed` — refresh the larger file/chunk index after source changes.\n\n" +
                        "The bootstrap makes subsystem routing fast; it never replaces exact source, runtime, report, or validation evidence.");
                    TaskStage = "Bootstrap help shown";
                    TaskProgress = 100;
                    Status = "Ready by the hearth.";
                    return true;

                case "refresh":
                    TaskStage = "Refreshing RealmGate bootstrap baseline";
                    Status = "Refreshing compact knowledge fingerprints...";
                    _knowledgeBootstrapStatus = await _knowledgeBootstrap.RefreshAsync(
                        Configuration.RepositoryPath,
                        _activeRequest.Token);
                    _lastKnowledgeBootstrap = await _knowledgeBootstrap.PrepareAsync(
                        Configuration.RepositoryPath,
                        "RealmGate project architecture and authority orientation",
                        _activeRequest.Token);
                    Add("Borin", _knowledgeBootstrapStatus.Summary +
                        "\n\nThe compact baseline is current. Run `/learn changed` if source files changed so the larger persistent index is refreshed too.");
                    break;

                case "status":
                case "":
                    _knowledgeBootstrapStatus = await _knowledgeBootstrap.GetStatusAsync(
                        Configuration.RepositoryPath,
                        _activeRequest.Token);
                    Add("Borin",
                        _knowledgeBootstrapStatus.Summary +
                        $"\n\nCards: `{_knowledgeBootstrapStatus.CardsRoot}`\nState: `{_knowledgeBootstrapStatus.StatePath}`");
                    break;

                default:
                    Add("Borin", "Unknown bootstrap command. Use `/bootstrap help`.");
                    break;
            }

            Raise(nameof(KnowledgeBootstrapSummary));
            Raise(nameof(KnowledgeBootstrapRecallSummary));
            TaskStage = "Bootstrap command complete";
            TaskProgress = 100;
            Status = "Ready by the hearth.";
            return true;
        }
        catch (OperationCanceledException)
        {
            TaskStage = "Bootstrap command canceled";
            TaskProgress = 0;
            Status = "Bootstrap command canceled.";
            return true;
        }
        catch (Exception exception)
        {
            Add("Borin", $"The compact RealmGate bootstrap failed safely: {exception.Message}");
            TaskStage = "Bootstrap command failed safely";
            TaskProgress = 0;
            Status = "Bootstrap command failed safely.";
            return true;
        }
        finally
        {
            _activeRequest?.Dispose();
            _activeRequest = null;
            IsBusy = false;
            RaiseCommandStates();
        }
    }

    private async Task<bool> TryHandleKnowledgeCommandAsync(string request)
    {
        var trimmed = request.Trim();
        if (trimmed.Equals("learn", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("learn realmgate", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("learn reforged", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("learn the repository", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("study the repository", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = "/learn realmgate";
        }
        else if (trimmed.Equals("learn changed files", StringComparison.OrdinalIgnoreCase) ||
                 trimmed.Equals("refresh your knowledge", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = "/learn changed";
        }

        var isLearn = trimmed.StartsWith("/learn", StringComparison.OrdinalIgnoreCase);
        var isKnowledge = trimmed.StartsWith("/knowledge", StringComparison.OrdinalIgnoreCase);
        if (!isLearn && !isKnowledge)
            return false;

        Add("You", request);
        IsBusy = true;
        _activeRequest?.Dispose();
        _activeRequest = new CancellationTokenSource();
        CurrentTask = request;
        TaskProgress = 0;
        RaiseCommandStates();
        try
        {
            if (trimmed.Equals("/knowledge-help", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("/learn help", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("/learn", StringComparison.OrdinalIgnoreCase))
            {
                Add("Borin", "Persistent knowledge commands:\n\n" +
                    "- `/learn realmgate` — scan approved RealmGate text (`/learn reforged` remains a legacy alias) and create or resume the persistent chunk index.\n" +
                    "- `/learn changed` — refresh changed, new, and deleted files while reusing unchanged chunks.\n" +
                    "- `/learn status` — show file/chunk counts and the last learning state.\n" +
                    "- `/learn cancel` — use the visible Cancel button while a learning pass is running.\n" +
                    "- `/knowledge <question>` — search the index directly without invoking Qwen.\n\n" +
                    "This is retrieval, not model retraining. The index writes only under `AI\\Memory\\Knowledge`. Imported databases remain reference-only.");
                TaskStage = "Knowledge help shown";
                Status = "Ready by the hearth.";
                return true;
            }

            if (trimmed.Equals("/learn status", StringComparison.OrdinalIgnoreCase))
            {
                _knowledgeIndexStatus = await _knowledgeIndex.GetStatusAsync(
                    Configuration.RepositoryPath,
                    _activeRequest.Token);
                if (_knowledgeIndex is IBorinKnowledgeCore statusCore)
                    _majorMudKnowledgeStatus = await statusCore.GetMajorMudStatusAsync(Configuration.RepositoryPath, _activeRequest.Token);
                Raise(nameof(KnowledgeIndexSummary));
                Raise(nameof(MajorMudKnowledgeSummary));
                Add("Borin", $"Knowledge index status:\n\n{_knowledgeIndexStatus.Summary}\n\nStored at `{_knowledgeIndexStatus.StoragePath}`.");
                TaskStage = "Knowledge status read";
                Status = "Ready by the hearth.";
                return true;
            }

            if (trimmed.Equals("/learn cancel", StringComparison.OrdinalIgnoreCase))
            {
                Add("Borin", "Use the visible Cancel button while the indexer is running. It saves a resumable checkpoint before stopping.");
                TaskStage = "Cancel guidance shown";
                Status = "Ready by the hearth.";
                return true;
            }

            if (trimmed.Equals("/knowledge mastery-test", StringComparison.OrdinalIgnoreCase))
            {
                if (_knowledgeIndex is not IBorinKnowledgeCore masteryCore)
                {
                    Add("Borin", "The shared Knowledge Core is not available, so no mastery score was invented.");
                    return true;
                }
                TaskStage = "Running evidence-based mastery evaluation";
                Status = "Borin is testing retrieval, relationships, provenance, confidence, and navigation...";
                var report = await masteryCore.RunMajorMudMasteryEvaluationAsync(Configuration.RepositoryPath, _activeRequest.Token);
                _masteryEvaluationSummary = report.Summary;
                _masteryEvaluationDetails = string.Join(" • ", report.Cases.Select(item => $"{item.Category}: {(item.Passed ? "PASS" : "FAIL")}"));
                Raise(nameof(MasteryEvaluationSummary));
                Raise(nameof(MasteryEvaluationDetails));
                var failures = report.Cases.Where(item => !item.Passed).Select(item => $"- {item.Category}: {item.Detail}").ToArray();
                Add("Borin", $"Mastery evaluation complete: {report.Summary}.\n\n" +
                    $"Catalog fingerprint: `{report.CatalogFingerprint}`\nReport: `{report.StoragePath}`" +
                    (failures.Length == 0 ? "\n\nAll current evidence gates passed." : "\n\nOpen gates:\n" + string.Join('\n', failures)));
                TaskStage = "Mastery evaluation complete";
                TaskProgress = 100;
                Status = "Ready by the hearth.";
                return true;
            }

            if (trimmed.Equals("/knowledge conflicts", StringComparison.OrdinalIgnoreCase))
            {
                var conflicts = await _verifiedKnowledgeLearning.GetConflictsAsync(_activeRequest.Token);
                var text = conflicts.Count == 0 ? "No runtime knowledge conflicts are recorded." : string.Join("\n\n", conflicts.Take(30).Select(item =>
                    $"{item.Id} • {item.Status} • attempts {item.ControlledAttempts}\nSubject: {item.Subject}\nExpected: {item.Expected}\nObserved: {item.Observed}" +
                    (string.IsNullOrWhiteSpace(item.ResolutionNote) ? string.Empty : $"\nResolution: {item.ResolutionNote}\nEvidence: {item.ResolutionEvidence}")));
                Add("Borin", text);
                TaskStage = "Knowledge conflicts shown";
                Status = "Ready by the hearth.";
                return true;
            }

            if (trimmed.Equals("/knowledge investigations", StringComparison.OrdinalIgnoreCase))
            {
                var investigations = await _verifiedKnowledgeLearning.GetInvestigationsAsync(_activeRequest.Token);
                var text = investigations.Count == 0 ? "No evidence-backed investigations are recorded." : string.Join("\n\n", investigations.Take(30).Select(item =>
                    $"{item.Id} • {item.Status}\nObjective: {item.Objective}\nResult: {item.Result}\nEvidence: {item.Evidence}" +
                    (item.RelatedEntityIds.Count == 0 ? string.Empty : $"\nRelated records/files: {string.Join(", ", item.RelatedEntityIds)}")));
                Add("Borin", text);
                TaskStage = "Knowledge investigations shown";
                Status = "Ready by the hearth.";
                return true;
            }

            if (trimmed.StartsWith("/knowledge resolve ", StringComparison.OrdinalIgnoreCase))
            {
                var parts = trimmed["/knowledge resolve ".Length..].Split('|', StringSplitOptions.TrimEntries);
                if (parts.Length != 3)
                {
                    Add("Borin", "Use `/knowledge resolve <conflict-id> | <verified resolution> | <evidence path or test result>`. Resolution requires explicit evidence.");
                    return true;
                }
                var resolved = await _verifiedKnowledgeLearning.ResolveConflictAsync(parts[0], parts[1], parts[2], _activeRequest.Token);
                _verifiedLearningStatus = await _verifiedKnowledgeLearning.GetStatusAsync(_activeRequest.Token);
                Raise(nameof(VerifiedLearningSummary));
                Add("Borin", $"Resolved {resolved.Id} with retained evidence. The original expected and observed claims were preserved.");
                return true;
            }

            if (trimmed.StartsWith("/knowledge", StringComparison.OrdinalIgnoreCase))
            {
                var query = trimmed.Length > "/knowledge".Length
                    ? trimmed["/knowledge".Length..].Trim()
                    : string.Empty;
                if (query.Length == 0)
                {
                    Add("Borin", "Use `/knowledge <question>` to search the persistent index, or `/knowledge-help` for all commands.");
                    TaskStage = "Knowledge query needed";
                    Status = "Ready by the hearth.";
                    return true;
                }

                TaskStage = "Searching persistent RealmGate knowledge";
                TaskProgress = 50;
                Status = "Borin is retrieving ranked indexed chunks...";
                _lastKnowledgeEvidence = await _knowledgeIndex.SearchAsync(
                    Configuration.RepositoryPath,
                    query,
                    maximumResults: 12,
                    _activeRequest.Token);
                Raise(nameof(KnowledgeRecallSummary));
                Add("Borin", FormatKnowledgeEvidence(_lastKnowledgeEvidence));
                TaskStage = "Knowledge search complete";
                TaskProgress = 100;
                Status = "Ready by the hearth.";
                return true;
            }

            var changedOnly = trimmed.Equals("/learn changed", StringComparison.OrdinalIgnoreCase);
            if (!changedOnly &&
                !trimmed.Equals("/learn realmgate", StringComparison.OrdinalIgnoreCase) &&
                !trimmed.Equals("/learn reforged", StringComparison.OrdinalIgnoreCase))
            {
                Add("Borin", "Unknown knowledge command. Type `/knowledge-help` for the available commands.");
                TaskStage = "Unknown knowledge command";
                Status = "Ready by the hearth.";
                return true;
            }

            TaskStage = changedOnly ? "Refreshing changed project knowledge" : "Indexing RealmGate knowledge";
            Status = changedOnly
                ? "Borin is refreshing changed files and reusing unchanged chunks..."
                : "Borin is building or resuming the persistent RealmGate knowledge index...";
            _knowledgeIndexStatus = await _knowledgeIndex.BuildAsync(
                Configuration.RepositoryPath,
                changedOnly,
                _activeRequest.Token);
            if (_knowledgeIndex is IBorinKnowledgeCore builtCore)
                _majorMudKnowledgeStatus = await builtCore.GetMajorMudStatusAsync(Configuration.RepositoryPath, _activeRequest.Token);
            Raise(nameof(KnowledgeIndexSummary));
            Raise(nameof(MajorMudKnowledgeSummary));
            Add("Borin", $"Aye, the knowledge index is ready.\n\n{_knowledgeIndexStatus.Summary}\n\nStored at `{_knowledgeIndexStatus.StoragePath}`. This did not retrain Qwen or modify project source/data.");
            TaskStage = "Knowledge indexing complete";
            TaskProgress = 100;
            Status = "Ready by the hearth.";
            return true;
        }
        catch (OperationCanceledException)
        {
            _knowledgeIndexStatus = await _knowledgeIndex.GetStatusAsync(Configuration.RepositoryPath, CancellationToken.None);
            Raise(nameof(KnowledgeIndexSummary));
            Add("Borin", "Aye, the learning pass stopped. The checkpoint was saved, so `/learn realmgate` can resume it.");
            TaskStage = "Knowledge indexing canceled";
            TaskProgress = 0;
            Status = "Knowledge checkpoint saved.";
            return true;
        }
        catch (Exception exception)
        {
            Add("Borin", $"The knowledge index failed safely: {exception.Message}. Project source and gameplay data were not changed.");
            TaskStage = "Knowledge indexing failed safely";
            TaskProgress = 0;
            Status = "Knowledge indexing failed safely.";
            return true;
        }
        finally
        {
            _activeRequest?.Dispose();
            _activeRequest = null;
            IsBusy = false;
            await PersistAsync();
            RaiseCommandStates();
        }
    }

    private static string FormatKnowledgeEvidence(BorinKnowledgeIndexSnapshot snapshot)
    {
        if (snapshot.Matches.Count == 0)
            return snapshot.Summary;

        var builder = new StringBuilder();
        builder.AppendLine(snapshot.Summary);
        builder.AppendLine();
        foreach (var match in snapshot.Matches)
        {
            builder.AppendLine(match.Display);
            builder.AppendLine();
        }
        builder.Append("These are indexed observations only. Imported-reference chunks are not authoritative gameplay evidence.");
        return builder.ToString().Trim();
    }

    private void OnKnowledgeIndexStatusChanged(BorinKnowledgeIndexStatus status)
    {
        void Apply()
        {
            _knowledgeIndexStatus = status;
            Raise(nameof(KnowledgeIndexSummary));
            if (status.IsRunning)
            {
                TaskStage = "Indexing RealmGate knowledge";
                TaskProgress = status.ProgressPercent;
                Status = string.IsNullOrWhiteSpace(status.CurrentFile)
                    ? "Borin is indexing approved project text..."
                    : $"Indexing {status.CurrentFile}";
            }
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
            Apply();
        else
            dispatcher.BeginInvoke((Action)Apply);
    }

    private async Task<bool> TryHandleClassicTraceCommandAsync(string request)
    {
        if (!request.StartsWith("/trace", StringComparison.OrdinalIgnoreCase))
            return false;

        Add("You", request);
        IsBusy = true;
        _activeRequest?.Dispose();
        _activeRequest = new CancellationTokenSource();
        RaiseCommandStates();
        try
        {
            if (request.Equals("/trace-help", StringComparison.OrdinalIgnoreCase))
            {
                Add("Borin", "Classic trace commands:\n/trace <question> — trace decoded source → Librarium → generated Data/Classic → runtime catalog → live runtime consumer → persistence/reset/cleanup → parity validation\n/trace-last — show the last structured trace and its exact paths/lines\n\nImported databases are shown separately as reference-only evidence and never satisfy an authoritative stage. A matching file is evidence to inspect, not proof that a stage is complete.");
                return true;
            }

            if (request.Equals("/trace", StringComparison.OrdinalIgnoreCase) ||
                request.Equals("/trace-last", StringComparison.OrdinalIgnoreCase))
            {
                Add("Borin", FormatClassicTrace(_lastClassicTrace));
                return true;
            }

            var query = request["/trace".Length..].Trim();
            if (string.IsNullOrWhiteSpace(query))
            {
                Add("Borin", "Use `/trace <question>` and name the mechanic, field, command, item, NPC, quest, room, or behavior ye want traced.");
                return true;
            }

            CurrentTask = $"Classic trace: {query}";
            TaskStage = "Tracing seven evidence stages";
            TaskProgress = 35;
            Status = "Tracing Classic evidence read-only...";
            _lastClassicTrace = await _classicTraceAccess.TraceAsync(
                Configuration.RepositoryPath,
                query,
                maximumResultsPerStage: 6,
                _activeRequest.Token);
            Raise(nameof(ClassicTraceSummary));
            Add("Borin", FormatClassicTrace(_lastClassicTrace));
            TaskStage = "Trace complete";
            TaskProgress = 100;
            Status = "Classic trace complete. No files changed and no command executed.";
            return true;
        }
        catch (OperationCanceledException)
        {
            Add("Borin", "Aye, the trace was stopped. No file was changed and no command was executed.");
            TaskStage = "Trace canceled";
            TaskProgress = 0;
            Status = "Classic trace canceled safely.";
            return true;
        }
        catch (Exception ex)
        {
            Add("Borin", $"The read-only trace struck a fault: {ex.Message}. No file was changed and no command was executed.");
            TaskStage = "Trace failed safely";
            TaskProgress = 0;
            Status = "Classic trace failed safely.";
            return true;
        }
        finally
        {
            _activeRequest?.Dispose();
            _activeRequest = null;
            IsBusy = false;
            await PersistAsync();
            RaiseCommandStates();
        }
    }

    private static string FormatClassicTrace(BorinClassicTraceSnapshot snapshot)
    {
        if (snapshot.Stages.Count == 0)
            return snapshot.Summary;

        var builder = new StringBuilder();
        builder.AppendLine(snapshot.Summary);
        builder.AppendLine("Evidence at one stage does not prove the next stage.");
        builder.AppendLine();
        foreach (var stage in snapshot.Stages)
        {
            builder.AppendLine($"{stage.StageName}: {(stage.HasEvidence ? "evidence observed" : "MISSING")}");
            builder.AppendLine(stage.Requirement);
            foreach (var item in stage.Evidence)
                builder.AppendLine($"  • `{item.Location}` — {item.Snippet}");
            builder.AppendLine();
        }

        if (snapshot.ImportedReferenceEvidence.Count > 0)
        {
            builder.AppendLine("Imported references — reference-only, not authoritative:");
            foreach (var item in snapshot.ImportedReferenceEvidence)
                builder.AppendLine($"  • `{item.Location}` — {item.Snippet}");
            builder.AppendLine();
        }

        builder.Append("No file was changed, no command was executed, and missing stages remain unresolved.");
        return builder.ToString();
    }

    private async Task<bool> TryHandleReadOnlyAccessCommandAsync(string request)
    {
        if (!request.StartsWith("/evidence", StringComparison.OrdinalIgnoreCase) &&
            !request.Equals("/access-help", StringComparison.OrdinalIgnoreCase))
            return false;

        Add("You", request);
        IsBusy = true;
        RaiseCommandStates();
        try
        {
            if (request.Equals("/access-help", StringComparison.OrdinalIgnoreCase))
            {
                Add("Borin", "Read-only project access:\n/evidence — show the exact file paths and lines supplied to the last answer\n/evidence <question> — run a read-only search without asking the model\n\nThe reader can inspect approved text files in source, Librarium/data, reports, logs, and project documentation. It cannot edit or delete files, execute scripts, run builds/tests, restart services, install updates, or claim a defect is fixed.");
                return true;
            }

            var query = request.Length > "/evidence".Length
                ? request["/evidence".Length..].Trim()
                : string.Empty;
            if (!string.IsNullOrWhiteSpace(query))
            {
                _lastProjectEvidence = await _readOnlyAccess.GatherAsync(
                    Configuration.RepositoryPath,
                    query,
                    maximumResults: 20);
                Raise(nameof(ReadOnlyAccessSummary));
            }

            Add("Borin", _lastProjectEvidence.Items.Count == 0
                ? _lastProjectEvidence.Summary
                : $"Read-only evidence from the last search:\n\n{string.Join("\n\n", _lastProjectEvidence.Items.Select(item => $"[{item.Kind}] `{item.Location}`\n{item.Snippet}"))}\n\nNo file was changed and no command was executed.");
            return true;
        }
        finally
        {
            IsBusy = false;
            await PersistAsync();
            RaiseCommandStates();
        }
    }

    private async Task<bool> TryHandleMemoryCommandAsync(string request)
    {
        if (string.IsNullOrWhiteSpace(request) || !request.StartsWith("/", StringComparison.Ordinal))
            return false;

        Add("You", request);
        IsBusy = true;
        RaiseCommandStates();
        try
        {
            if (request.Equals("/memory-help", StringComparison.OrdinalIgnoreCase))
            {
                Add("Borin", "Memory commands:\n/memories — show active durable memories\n/memories all — include archived memories\n/recall — show the exact memories used for the last answer\n/remember [instruction|decision|task|bug|result] <text> — save a permanent memory\n/edit <id> <text> — edit a saved memory\n/mark <id> [discussed|decided|installed|built|validated] — set its evidence\n/complete <id> [result] — archive a task and optionally save its result\n/archive <id> [result] — archive any memory\n/forget <id> — permanently delete one memory\n/project-rules — show Borin's fixed RealmGate awareness\n/clear-memory confirm — erase the full local memory ledger\n\nRaw conversation turns are temporary and automatically compacted into persistent summaries. Memory is stored locally in AI\\Memory\\borin-memory.json.");
                return true;
            }

            if (request.Equals("/project-rules", StringComparison.OrdinalIgnoreCase))
            {
                Add("Borin", $"Fixed RealmGate awareness ({BorinProjectAwareness.Version}):\n\n{string.Join("\n", BorinProjectAwareness.CoreRules.Select(rule => $"• {rule}"))}");
                return true;
            }

            if (request.Equals("/recall", StringComparison.OrdinalIgnoreCase))
            {
                Add("Borin", _lastRecallSnapshot.RelevantMemories.Count == 0
                    ? "No saved memories were supplied to me last answer."
                    : $"Memories used for the last answer:\n\n{string.Join("\n\n", _lastRecallSnapshot.RelevantMemories.Select(memory => memory.Display))}");
                return true;
            }

            if (request.Equals("/memories", StringComparison.OrdinalIgnoreCase) ||
                request.Equals("/memories all", StringComparison.OrdinalIgnoreCase))
            {
                var includeArchived = request.EndsWith(" all", StringComparison.OrdinalIgnoreCase);
                var memories = await _memoryStore.ListAsync(
                    includeConversation: false,
                    includeArchived: includeArchived);
                Add("Borin", memories.Count == 0
                    ? "Me durable memory ledger be empty, lad. Use `/remember instruction <text>` or simply tell me `remember that ...`."
                    : $"{(includeArchived ? "All durable memories" : "Active durable memories")}:\n\n" +
                      string.Join("\n\n", memories.Take(50).Select(memory => memory.Display)));
                await RefreshMemorySnapshotAsync(string.Empty);
                return true;
            }

            if (request.StartsWith("/remember ", StringComparison.OrdinalIgnoreCase))
            {
                var remainder = request["/remember ".Length..].Trim();
                var (category, text) = ParseMemoryCategory(remainder);
                if (string.IsNullOrWhiteSpace(text))
                {
                    Add("Borin", "Give me something to remember, lad. Example: `/remember decision Imported databases are reference-only.`");
                    return true;
                }

                var memory = await _memoryStore.RememberAsync(
                    category,
                    text,
                    BorinMemoryEvidence.Decided,
                    pinned: true,
                    source: "Explicit user command");
                Add("Borin", $"Aye. Active memory `{memory.Id}` is {memory.Category}, {memory.Scope}, with evidence {memory.Evidence}. Duplicate and superseded memories are consolidated or archived automatically.");
                await RefreshMemorySnapshotAsync(text);
                return true;
            }

            if (request.StartsWith("/edit ", StringComparison.OrdinalIgnoreCase))
            {
                var remainder = request["/edit ".Length..].Trim();
                var split = remainder.IndexOf(' ');
                if (split <= 0 || split >= remainder.Length - 1)
                {
                    Add("Borin", "Use `/edit <memory-id> <replacement text>`. The memory id is shown by `/memories`. ");
                    return true;
                }

                var id = remainder[..split].Trim();
                var text = remainder[(split + 1)..].Trim();
                var memory = await _memoryStore.UpdateAsync(id, text);
                Add("Borin", memory is null
                    ? $"I found no memory with id `{id}`."
                    : $"Memory `{memory.Id}` was updated without creating a duplicate. It remains {memory.Category} with evidence {memory.Evidence}.");
                await RefreshMemorySnapshotAsync(text);
                return true;
            }

            if (request.StartsWith("/mark ", StringComparison.OrdinalIgnoreCase))
            {
                var remainder = request["/mark ".Length..].Trim();
                var split = remainder.IndexOf(' ');
                if (split <= 0 || split >= remainder.Length - 1 ||
                    !TryParseEvidence(remainder[(split + 1)..].Trim(), out var evidence))
                {
                    Add("Borin", "Use `/mark <memory-id> discussed|decided|installed|built|validated`. Evidence is never promoted automatically.");
                    return true;
                }

                var id = remainder[..split].Trim();
                var memory = await _memoryStore.MarkEvidenceAsync(id, evidence);
                Add("Borin", memory is null
                    ? $"I found no memory with id `{id}`."
                    : $"Memory `{memory.Id}` is now marked {memory.Evidence}. That label is treated literally.");
                await RefreshMemorySnapshotAsync(string.Empty);
                return true;
            }

            if (request.StartsWith("/complete ", StringComparison.OrdinalIgnoreCase) ||
                request.StartsWith("/archive ", StringComparison.OrdinalIgnoreCase))
            {
                var prefix = request.StartsWith("/complete ", StringComparison.OrdinalIgnoreCase)
                    ? "/complete "
                    : "/archive ";
                var remainder = request[prefix.Length..].Trim();
                var split = remainder.IndexOf(' ');
                var id = split < 0 ? remainder : remainder[..split].Trim();
                var result = split < 0 ? null : remainder[(split + 1)..].Trim();
                var memory = await _memoryStore.ArchiveAsync(id, result);
                Add("Borin", memory is null
                    ? $"I found no memory with id `{id}`."
                    : string.IsNullOrWhiteSpace(result)
                        ? $"Memory `{memory.Id}` is archived. It remains in history but is no longer recalled as an active instruction or task."
                        : $"Memory `{memory.Id}` is archived and its result was recorded separately.");
                await RefreshMemorySnapshotAsync(result ?? string.Empty);
                return true;
            }

            if (request.StartsWith("/forget ", StringComparison.OrdinalIgnoreCase))
            {
                var id = request["/forget ".Length..].Trim();
                var removed = await _memoryStore.ForgetAsync(id);
                Add("Borin", removed
                    ? $"Memory `{id}` has been permanently removed from the local ledger."
                    : $"I found no memory with id `{id}`.");
                await RefreshMemorySnapshotAsync(string.Empty);
                return true;
            }

            if (request.Equals("/clear-memory confirm", StringComparison.OrdinalIgnoreCase))
            {
                var removed = await _memoryStore.ClearAsync(includeConversation: true);
                _lastRecallSnapshot = BorinMemorySnapshot.Empty;
                Raise(nameof(MemoryRecallSummary));
                Add("Borin", $"The local memory ledger has been cleared. {removed} entries were removed. The Forge chat display remains until ye press Clear.");
                await RefreshMemorySnapshotAsync(string.Empty);
                return true;
            }

            if (request.StartsWith("/clear-memory", StringComparison.OrdinalIgnoreCase))
            {
                Add("Borin", "That clears every durable memory, archived record, saved summary, and temporary conversation turn. Type `/clear-memory confirm` to do it.");
                return true;
            }

            Add("Borin", "Unknown memory command. Type `/memory-help` for the available commands.");
            return true;
        }
        finally
        {
            IsBusy = false;
            await PersistAsync();
            RaiseCommandStates();
        }
    }

    private async Task CaptureDurableMemoryAsync(string request, CancellationToken token)
    {
        var trimmed = request.Trim();
        var candidates = new[]
        {
            (Prefix: "remember that ", Category: BorinMemoryCategory.StandingInstruction),
            (Prefix: "from now on ", Category: BorinMemoryCategory.StandingInstruction),
            (Prefix: "going forward ", Category: BorinMemoryCategory.StandingInstruction),
            (Prefix: "we decided ", Category: BorinMemoryCategory.ProjectDecision),
            (Prefix: "the current task is ", Category: BorinMemoryCategory.CurrentTask),
            (Prefix: "bug: ", Category: BorinMemoryCategory.Bug),
            (Prefix: "the bug is ", Category: BorinMemoryCategory.Bug),
        };

        foreach (var candidate in candidates)
        {
            if (!trimmed.StartsWith(candidate.Prefix, StringComparison.OrdinalIgnoreCase))
                continue;

            var text = trimmed[candidate.Prefix.Length..].Trim();
            if (text.Length == 0)
                return;

            await _memoryStore.RememberAsync(
                candidate.Category,
                text,
                BorinMemoryEvidence.Decided,
                pinned: true,
                source: "Automatic explicit-language capture",
                token: token);
            return;
        }
    }

    private async Task RefreshMemorySnapshotAsync(
        string request,
        CancellationToken token = default)
    {
        _memorySnapshot = await _memoryStore.RecallAsync(request, 12, token);
        Raise(nameof(MemorySummary));
        Raise(nameof(KnowledgeSummary));
    }

    private static (BorinMemoryCategory Category, string Text) ParseMemoryCategory(string value)
    {
        var trimmed = value.Trim();
        var separator = trimmed.IndexOf(' ');
        var label = separator < 0 ? trimmed : trimmed[..separator];
        var text = separator < 0 ? string.Empty : trimmed[(separator + 1)..].Trim();

        return label.ToLowerInvariant() switch
        {
            "instruction" or "rule" => (BorinMemoryCategory.StandingInstruction, text),
            "decision" => (BorinMemoryCategory.ProjectDecision, text),
            "task" => (BorinMemoryCategory.CurrentTask, text),
            "bug" => (BorinMemoryCategory.Bug, text),
            "result" => (BorinMemoryCategory.WorkResult, text),
            _ => (BorinMemoryCategory.StandingInstruction, trimmed),
        };
    }

    private static bool TryParseEvidence(string value, out BorinMemoryEvidence evidence)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "discussed": evidence = BorinMemoryEvidence.Discussed; return true;
            case "decided": evidence = BorinMemoryEvidence.Decided; return true;
            case "installed": evidence = BorinMemoryEvidence.Installed; return true;
            case "built": evidence = BorinMemoryEvidence.Built; return true;
            case "validated": evidence = BorinMemoryEvidence.Validated; return true;
            default: evidence = BorinMemoryEvidence.Discussed; return false;
        }
    }

    private void StartForgeNaturalInitiativeMonitor()
    {
        if (_forgeInitiativeMonitorTask is { IsCompleted: false })
            return;

        try
        {
            _forgeInitiativeMonitorCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
        _forgeInitiativeMonitorCts?.Dispose();
        _forgeInitiativeMonitorCts = new CancellationTokenSource();
        _forgeInitiativeMonitorTask = MonitorForgeNaturalInitiativeAsync(_forgeInitiativeMonitorCts.Token);
    }

    private void CancelForgeNaturalInitiative()
    {
        try
        {
            _forgeInitiativeRequest?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async Task MonitorForgeNaturalInitiativeAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(30), token);
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await TryForgeNaturalInitiativeAsync(token);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    RunOnUi(() => Status = $"Forge natural initiative paused for this check: {exception.Message}");
                }
                await Task.Delay(TimeSpan.FromSeconds(60), token);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            RunOnUi(() => Status = $"Forge natural initiative monitor stopped safely: {exception.Message}");
        }
    }

    private async Task TryForgeNaturalInitiativeAsync(CancellationToken token)
    {
        if (Interlocked.CompareExchange(ref _forgeInitiativeRunning, 1, 0) != 0)
            return;

        try
        {
            var surfaceBusy = false;
            var userTyping = false;
            var generalConversationEnabled = false;
            var characterName = "Borin";
            var characterTitle = "old dwarf companion";
            var mission = "Be William's steady companion.";
            var addressUserAs = "friend";
            ForgeChatMessage[] messages = [];

            void CaptureUiState()
            {
                surfaceBusy = IsBusy;
                userTyping = !string.IsNullOrWhiteSpace(Input);
                generalConversationEnabled = Configuration.UseMasterBrainForGeneralConversation;
                characterName = Configuration.CharacterName;
                characterTitle = Configuration.CharacterTitle;
                mission = Configuration.Mission;
                addressUserAs = Configuration.AddressUserAs;
                messages = _state.Messages
                    .Where(message => message.Role.Equals("You", StringComparison.OrdinalIgnoreCase) ||
                                      message.Role.Equals("Borin", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(message => message.Timestamp)
                    .ToArray();
            }

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is null || dispatcher.CheckAccess())
                CaptureUiState();
            else
                await dispatcher.InvokeAsync(CaptureUiState);

            if (surfaceBusy || userTyping || _localBrain?.CurrentStatus.IsReady != true)
                return;

            var lastHuman = messages.LastOrDefault(message => message.Role.Equals("You", StringComparison.OrdinalIgnoreCase));
            var lastBorin = messages.LastOrDefault(message => message.Role.Equals("Borin", StringComparison.OrdinalIgnoreCase));
            var capturedHumanMessageCount = messages.Count(message => message.Role.Equals("You", StringComparison.OrdinalIgnoreCase));
            var capturedLastHumanAt = lastHuman?.Timestamp ?? DateTimeOffset.MinValue;
            var hasPriorConversation = lastHuman is not null && lastBorin is not null;
            var pendingQuestion = lastBorin is not null &&
                                  lastBorin.Timestamp >= (lastHuman?.Timestamp ?? DateTimeOffset.MinValue) &&
                                  lastBorin.Text.TrimEnd().EndsWith("?", StringComparison.Ordinal);
            var recentContext = string.Join(
                " | ",
                messages.TakeLast(6).Select(message =>
                    $"{message.Role}: {TrimNaturalInitiativeText(message.Text, 240)}"));
            var gameState = _playerResearch.CurrentGameState;
            var now = DateTimeOffset.Now;
            var decision = await _naturalInitiative.EvaluateAsync(
                new BorinNaturalInitiativeRequest(
                    BorinNaturalInitiativeSurfaces.Forge,
                    "William",
                    now,
                    generalConversationEnabled && _playerSettings.NaturalInitiativeEnabled,
                    hasPriorConversation,
                    surfaceBusy,
                    userTyping,
                    true,
                    true,
                    false,
                    false,
                    100,
                    30,
                    8,
                    30,
                    1,
                    false,
                    pendingQuestion,
                    "Continue being William's steady adventuring and engineering companion.",
                    gameState.HasVerifiedLiveState ? gameState.CurrentRoom : "The Forge",
                    recentContext,
                    gameState.HasVerifiedLiveState
                        ? $"The external character is verified {gameState.Summary}."
                        : "This is private Forge conversation. No live external game state is being claimed."),
                token);
            if (!decision.ShouldSpeak)
                return;

            var isolatedState = new ForgeConversationState();
            isolatedState.Configuration.CharacterName = characterName;
            isolatedState.Configuration.CharacterTitle = characterTitle;
            isolatedState.Configuration.Mission = mission;
            isolatedState.Configuration.CharacterInstructions =
                "Speak as Borin, William's old dwarf companion. Be warm, grounded, and natural. Never invent work or demand a reply.";
            isolatedState.Configuration.AddressUserAs = addressUserAs;

            var requestCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            _forgeInitiativeRequest = requestCts;
            BrainResponse response;
            try
            {
                response = await _brain.ThinkAsync(
                    BrainContext.ForBorin(
                        decision.PromptText,
                        isolatedState,
                        repositorySnapshot: null,
                        purpose: "Natural initiative generation",
                        gameState: gameState),
                    requestCts.Token);
            }
            finally
            {
                if (ReferenceEquals(_forgeInitiativeRequest, requestCts))
                    _forgeInitiativeRequest = null;
                requestCts.Dispose();
            }

            var text = CleanStoredBorinReply(response.Text);
            if (text.Equals("[NO_REPLY]", StringComparison.OrdinalIgnoreCase))
                text = string.Empty;
            text = TrimNaturalInitiativeText(text, decision.PreferredMaximumCharacters);
            if (text.Length == 0)
            {
                await _naturalInitiative.RecordOutcomeAsync(decision, "[NO_REPLY]", false, token);
                return;
            }

            bool CanAcceptInitiative()
            {
                var currentHumanMessages = _state.Messages
                    .Where(message => message.Role.Equals("You", StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                return !IsBusy &&
                       string.IsNullOrWhiteSpace(Input) &&
                       Configuration.UseMasterBrainForGeneralConversation &&
                       _playerSettings.NaturalInitiativeEnabled &&
                       currentHumanMessages.Length == capturedHumanMessageCount &&
                       currentHumanMessages.All(message => message.Timestamp <= capturedLastHumanAt);
            }

            var accepted = false;
            if (dispatcher is null || dispatcher.CheckAccess())
            {
                if (CanAcceptInitiative())
                {
                    Add("Borin", text);
                    accepted = true;
                }
            }
            else
            {
                await dispatcher.InvokeAsync(() =>
                {
                    if (CanAcceptInitiative())
                    {
                        Add("Borin", text);
                        accepted = true;
                    }
                });
            }

            await _naturalInitiative.RecordOutcomeAsync(decision, text, accepted, token);
            if (accepted)
                await PersistAsync();
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            Interlocked.Exchange(ref _forgeInitiativeRunning, 0);
        }
    }

    private static string TrimNaturalInitiativeText(string text, int maximumCharacters)
    {
        var cleaned = string.Join(" ", (text ?? string.Empty)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
        maximumCharacters = Math.Clamp(maximumCharacters, 40, 800);
        if (cleaned.Length <= maximumCharacters)
            return cleaned;
        var contentLimit = maximumCharacters - 1;
        var cut = cleaned.LastIndexOf(' ', contentLimit - 1, contentLimit);
        if (cut < maximumCharacters / 2)
            cut = contentLimit;
        return cleaned[..cut].TrimEnd() + "…";
    }

    private static string CleanStoredBorinReply(string text)
    {
        if (BorinDialogueSafety.IsNoReplyMarker(text))
            return string.Empty;

        var cleaned = text
            .Replace("<|im_end|>", string.Empty, StringComparison.Ordinal)
            .Replace("<|endoftext|>", string.Empty, StringComparison.Ordinal)
            .Trim();

        if (cleaned.StartsWith("Assistant:", StringComparison.OrdinalIgnoreCase))
            cleaned = cleaned["Assistant:".Length..].TrimStart();

        var markers = new[]
        {
            "<|im_start|>",
            "\r\nAssistant:",
            "\nAssistant:",
            " Assistant:",
            "\r\nUser:",
            "\nUser:",
            " User:",
        };
        var cutAt = cleaned.Length;
        foreach (var marker in markers)
        {
            var index = cleaned.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index >= 0 && index < cutAt)
                cutAt = index;
        }

        if (cutAt < cleaned.Length)
            cleaned = cleaned[..cutAt];
        if (BorinDialogueSafety.IsNoReplyMarker(cleaned))
            return string.Empty;
        if (cleaned.Length > 12000)
            cleaned = cleaned[..12000] + "\n\n[Older runaway response trimmed by the local-brain repair.]";
        return cleaned.Trim();
    }

    private void RecordJournal(string action, string result, bool succeeded)
    {
        _state.CraftsmanJournal.Add(new ForgeJournalEntry
        {
            Action = action,
            Result = result.Length > 4000 ? result[..4000] : result,
            Succeeded = succeeded,
            WorkspacePath = _state.ActiveWorkspacePath,
            Timestamp = DateTimeOffset.Now
        });
        while (_state.CraftsmanJournal.Count > 500) _state.CraftsmanJournal.RemoveAt(0);
        Raise(nameof(KnowledgeSummary));
    }

    private async Task<bool> TryHandleForgeConnectionActionAsync(string request)
    {
        var action = BorinForgeActionRouter.ClassifyConnectionAction(request);
        if (action == BorinForgeConnectionAction.None)
            return false;

        switch (action)
        {
            case BorinForgeConnectionAction.ConnectAndLogin:
                await SelectNamedGameServerAsync(request);
                await ConnectPlayerAsync();
                return true;
            case BorinForgeConnectionAction.Disconnect:
                await DisconnectPlayerAsync();
                var disconnectedState = _playerResearch.CurrentGameState;
                Add("Borin", disconnectedState.IsConnected
                    ? $"The disconnect request was sent, but the Client still reports {disconnectedState.ConnectionState}."
                    : "The external game session is disconnected. I am not claiming to be playing on a server now.");
                return true;
            default:
                return false;
        }
    }

    private async Task SelectNamedGameServerAsync(string request)
    {
        var normalized = request.Trim().ToLowerInvariant();
        var omniverse = normalized.Contains("omni", StringComparison.Ordinal) ||
                        normalized.Contains("omniverse", StringComparison.Ordinal);
        var reforged = normalized.Contains("reforged", StringComparison.Ordinal) &&
                       !normalized.Contains("application", StringComparison.Ordinal);
        if (!omniverse && !reforged)
            return;

        var requestedHost = omniverse ? "omniversebbs.com" : "127.0.0.1";
        var requestedPort = omniverse ? 3237 : 4000;
        var requestedProfile = omniverse ? "Omniverse BBS" : "RealmGate Server";
        var state = _playerResearch.CurrentGameState;
        var targetChanged = !PlayerHostName.Equals(requestedHost, StringComparison.OrdinalIgnoreCase) ||
                            PlayerPort != requestedPort;
        if (targetChanged && state.IsConnected)
            await _playerResearch.DisconnectAsync();

        PlayerHostName = requestedHost;
        PlayerPort = requestedPort;
        PlayerServerProfileName = requestedProfile;
        PlayerWorldName = "Classic";
        if (!PlayerEnabled)
            PlayerEnabled = true;
        await ConfigurePlayerAsync();
        Add("Borin", $"Selected {requestedProfile} at {requestedHost}:{requestedPort}. I will use my existing Borin session and secure login flow.");
    }

    private async Task<bool> TryHandleVerifiedForgePlayerActionAsync(string request)
    {
        var plan = BorinForgeActionRouter.CreatePlan(request);
        if (!plan.IsActionRequest)
            return false;

        IsBusy = true;
        _activeRequest?.Dispose();
        _activeRequest = new CancellationTokenSource();
        CurrentTask = request;
        TaskStage = "Routing real terminal action";
        TaskProgress = 20;
        Status = "Borin is routing William's instruction to the verified external terminal...";
        RaiseCommandStates();

        string reply;
        try
        {
            if (!plan.CanExecute)
            {
                reply = $"I did not send a terminal command. {plan.Reason}";
                Status = "Gameplay instruction blocked before the local model could invent a result.";
            }
            else
            {
                await ConfigurePlayerAsync();
                TaskStage = "Sending supervised terminal sequence";
                TaskProgress = 45;
                var result = await _playerResearch.ExecuteAuthorizedSequenceAsync(
                    plan.Commands,
                    plan.Assignment,
                    _activeRequest.Token);
                reply = result.Summary;
                Status = result.Completed
                    ? "Real terminal sequence completed with parsed server evidence."
                    : "Real terminal sequence stopped safely before any unverified continuation.";
            }

            Add("Borin", reply);
            await _memoryStore.RecordTurnAsync(request, reply, CancellationToken.None);
            await RefreshMemorySnapshotAsync(request, CancellationToken.None);
            TaskStage = "Terminal action routing complete";
            TaskProgress = 100;
            await PersistAsync();
        }
        catch (OperationCanceledException)
        {
            reply = "The real terminal action was canceled. I did not invent a result.";
            Add("Borin", reply);
            Status = "Terminal action canceled.";
            TaskStage = "Terminal action stopped";
            TaskProgress = 0;
        }
        catch (Exception exception)
        {
            reply = $"I did not send an invented game result. The real terminal action was blocked: {exception.Message}";
            Add("Borin", reply);
            Status = "Terminal action blocked safely.";
            TaskStage = "Terminal action blocked";
            TaskProgress = 0;
        }
        finally
        {
            IsBusy = false;
            CurrentTask = "Idle — awaiting a commission.";
            RaisePlayerProperties();
            RaiseCommandStates();
        }

        return true;
    }

    private string BuildIntentAwareBrainRequest(string request, BorinRequestIntent intent)
    {
        return intent switch
        {
            BorinRequestIntent.ActivePlayer => BuildPlayerAwareBrainRequest(request),
            BorinRequestIntent.RepositoryResearch => BuildRepositoryResearchBrainRequest(request),
            BorinRequestIntent.Engineering => BuildLocalEngineeringBrainRequest(request),
            _ => request,
        };
    }

    private static string BuildRepositoryResearchBrainRequest(string request) =>
        $"""
USER REQUEST:
{request}

REQUEST MODE: LOCAL REPOSITORY RESEARCH
- This request concerns files, decoded data, Librarium projections, generated Classic data, runtime consumers, reports, logs, or parity evidence on this computer.
- Borin's external MajorMUD player connection is unrelated and is deliberately omitted. A disconnected game session never blocks repository research.
- Use the fresh read-only evidence, current repository snapshot, structured trace, and ranked knowledge chunks supplied with this turn.
- Treat indexed knowledge as retrieval context that may be stale. Do not present an indexed claim as a fresh audit unless current read-only evidence supports it.
- For an audit, cite exact supplied repository paths and line ranges. If no current evidence supports a claim, say the audit could not establish it.
- Do not route this request to Offline Study/ProjectBrain and do not claim a server login is required.
""";

    private static string BuildLocalEngineeringBrainRequest(string request) =>
        $"""
USER REQUEST:
{request}

REQUEST MODE: LOCAL ENGINEERING DISCUSSION
- This concerns the local Reforged repository and controlled engineering workspace.
- External MajorMUD connection state is unrelated and must not be used as a reason work cannot be investigated locally.
- Respect the current read-only or isolated-workspace permission boundary and report evidence states literally.
""";

    private string BuildPlayerAwareBrainRequest(string request)
    {
        var playerStatus = _playerResearch.CurrentStatus;
        var loginStatus = _playerResearch.CurrentLoginStatus;
        var gameState = _playerResearch.CurrentGameState;
        return $"""
USER REQUEST:
{request}

CURRENT BORIN ACTIVE-PLAYER STATE (observed from Reforged's exact client session):
- Application: RealmGate. The Forge is a private application workspace, not a game room or server.
- Session ID: {playerStatus.SessionId}
- Environment identity: {gameState.EnvironmentSummary}
- Connected game server: {gameState.GameServerIdentity}
- Network connection: {gameState.ConnectionState}
- Login state: {gameState.LoginState}
- Verified phase: {gameState.Phase}
- Login detail: {gameState.PhaseDetail}
- Character: {gameState.CharacterName}
- Server/world profile: {gameState.Server} / {gameState.World}
- Fresh HP prompt verified: {gameState.HasVerifiedPrompt} ({gameState.PromptFreshness})
- Current room verified: {gameState.HasVerifiedRoom} • {gameState.CurrentRoom} ({gameState.RoomFreshness})
- Current exits verified: {gameState.HasVerifiedExits} • {(gameState.HasVerifiedExits ? string.Join(", ", gameState.CurrentExits) : "not verified")} ({gameState.ExitFreshness})
- Last command owner/action: {(gameState.LastCommand.Length == 0 ? "none captured" : $"{gameState.LastCommandOrigin}: {gameState.LastCommand}")}
- Last incoming telepath parsed: {_playerResearch.LastIncomingTelepathSummary}
- HP/Mana: {gameState.HealthPercent}% / {gameState.ManaPercent}%
- In combat/resting/moving: {gameState.InCombat}/{gameState.IsResting}/{gameState.IsMoving}
- Observation enabled: {playerStatus.ObservationEnabled}
- Conversation enabled: {playerStatus.ConversationEnabled}
- Autonomous play: {(_playerResearch.IsAutonomousPlayActive ? "playing by himself" : gameState.CanAutonomousPlay ? "ready" : $"locked — {gameState.AutonomousPlayBlockReason}")}
- Secure password saved: {loginStatus.PasswordSaved}
- Player control state: {(playerStatus.EmergencyStopped ? "Emergency stopped" : _playerResearch.IsAutonomousPlayActive ? "Playing by himself" : playerStatus.Paused ? "Paused" : "Supervised")}
- Connect & Login rule: Disconnected is the normal starting state. The Connect & Login button exists to establish the connection and must not be described as unavailable merely because the network state is Disconnected.

Treat this block as authoritative current client-session state. Reforged is the application. Omniverse BBS is an external server; the Reforged server is a separate server. State which one is active exactly from Environment identity. Connected does not mean logged into MajorMUD unless Login state is Ready and the HP prompt is fresh. Report only the exact verified room and exits. Never invent where an exit leads. Never claim a Human command as Borin's action. If live evidence is missing, say it is not verified. Never expose or ask for the saved password in chat. Never print simulated terminal commands, fake room descriptions, purchases, equipment changes, or other imagined server results. A real gameplay instruction is routed through the supervised terminal gateway before the model; this prompt is for discussion only.
""";
    }

    private BorinPlayerSettings BuildPlayerSettings() => new BorinPlayerSettings
    {
        Enabled = PlayerEnabled,
        ObservationEnabled = PlayerObservationEnabled,
        ConversationEnabled = PlayerConversationEnabled,
        ReflectionJournalEnabled = PlayerReflectionJournalEnabled,
        AutomaticReflectionEnabled = PlayerAutomaticReflectionEnabled,
        ConversationLearningEnabled = PlayerConversationLearningEnabled,
        NaturalInitiativeEnabled = _playerSettings.NaturalInitiativeEnabled,
        NaturalInitiativeQuietMinutes = _playerSettings.NaturalInitiativeQuietMinutes,
        NaturalInitiativeCooldownMinutes = _playerSettings.NaturalInitiativeCooldownMinutes,
        MaximumNaturalInitiativesPerHour = _playerSettings.MaximumNaturalInitiativesPerHour,
        AutomaticCommandsEnabled = false,
        AllowSafeCombat = PlayerAllowSafeCombat,
        SessionId = PlayerSessionId,
        DisplayName = $"{PlayerCharacterName} — {PlayerServerProfileName}",
        LoginName = PlayerLoginName,
        CharacterName = PlayerCharacterName,
        ServerProfileName = PlayerServerProfileName,
        WorldName = PlayerWorldName,
        HostName = PlayerHostName,
        Port = PlayerPort,
        AutoReconnect = false,
        PrimaryTeacher = PlayerPrimaryTeacher,
        MentorAiName = PlayerMentorAiName,
        CurrentGoal = PlayerCurrentGoal,
        MaximumAiReplies = 3,
        ResponseCooldownSeconds = 4,
        MaximumRepliesPerMinute = 6,
        MaximumReplyCharacters = BorinGameStateSnapshot.HardInGameReplyCharacterLimit,
        DangerHealthPercent = 30,
        AutomaticReflectionEventInterval = 20,
        AutomaticReflectionCooldownMinutes = 10,
    }.Normalize();

    private void ApplyPlayerSettings(BorinPlayerSettings settings)
    {
        _playerSettings = settings.Normalize();
        PlayerEnabled = _playerSettings.Enabled;
        PlayerObservationEnabled = _playerSettings.ObservationEnabled;
        PlayerConversationEnabled = _playerSettings.ConversationEnabled;
        PlayerReflectionJournalEnabled = _playerSettings.ReflectionJournalEnabled;
        PlayerAutomaticReflectionEnabled = _playerSettings.AutomaticReflectionEnabled;
        PlayerConversationLearningEnabled = _playerSettings.ConversationLearningEnabled;
        PlayerAllowSafeCombat = _playerSettings.AllowSafeCombat;
        PlayerSessionId = _playerSettings.SessionId;
        PlayerHostName = _playerSettings.HostName;
        PlayerPort = _playerSettings.Port;
        PlayerLoginName = _playerSettings.LoginName;
        PlayerCharacterName = _playerSettings.CharacterName;
        PlayerServerProfileName = _playerSettings.ServerProfileName;
        PlayerWorldName = _playerSettings.WorldName;
        PlayerPrimaryTeacher = _playerSettings.PrimaryTeacher;
        PlayerMentorAiName = _playerSettings.MentorAiName;
        PlayerCurrentGoal = _playerSettings.CurrentGoal;
    }

    private async Task ConfigurePlayerAsync()
    {
        _playerSettings = BuildPlayerSettings();
        await _playerResearch.ConfigureAsync(Configuration.RepositoryPath, _playerSettings);
        ApplyPlayerSettings(_playerResearch.CurrentSettings);
        ApplyPlayerStatus(_playerResearch.CurrentStatus);
    }

    public async Task SavePlayerPasswordFromUiAsync(string password)
    {
        try
        {
            await ConfigurePlayerAsync();
            await _playerResearch.SavePasswordAsync(password);
            _playerLoginStatus = _playerResearch.CurrentLoginStatus;
            Add("Borin", "My password is saved securely in Windows Credential Manager. It was not written to the repository, logs, memory, journal, or model prompt.");
            Status = "Borin password saved securely.";
        }
        catch (Exception exception)
        {
            Add("Borin", $"The password could not be saved securely: {exception.Message}");
            Status = "Secure password save failed.";
        }
        finally
        {
            RaisePlayerProperties();
        }
    }

    public async Task ClearPlayerPasswordFromUiAsync()
    {
        try
        {
            await ConfigurePlayerAsync();
            await _playerResearch.ClearPasswordAsync();
            _playerLoginStatus = _playerResearch.CurrentLoginStatus;
            Add("Borin", "My saved external-server password has been cleared from Windows Credential Manager.");
            Status = "Borin saved password cleared.";
        }
        catch (Exception exception)
        {
            Add("Borin", $"The saved password could not be cleared: {exception.Message}");
        }
        finally
        {
            RaisePlayerProperties();
        }
    }

    public void PreparePlayerTerminal()
    {
        var resolvedSettings = _playerResearch.CurrentSettings;
        var resolvedSessionId = resolvedSettings.SessionId;
        var session = _playerTerminalViewModel.LiveSessions.FirstOrDefault(item =>
                item.SessionId.Equals(resolvedSessionId, StringComparison.OrdinalIgnoreCase))
            ?? _playerTerminalViewModel.LiveSessions.FirstOrDefault(item =>
                item.CharacterName.Equals(resolvedSettings.CharacterName, StringComparison.OrdinalIgnoreCase) &&
                item.HostName.Equals(resolvedSettings.HostName, StringComparison.OrdinalIgnoreCase) &&
                item.Port == resolvedSettings.Port);

        if (session is null)
        {
            throw new InvalidOperationException(
                "Borin's saved client session was not restored. Press Save Profile once, then open the terminal again.");
        }

        PlayerSessionId = session.SessionId;
        _playerTerminalViewModel.SelectedLiveSession = session;
    }

    private async Task SavePlayerProfileAsync()
    {
        try
        {
            await ConfigurePlayerAsync();
            Add("Borin", "Aye. My existing Borin session profile is saved. The password remains separate in Windows Credential Manager, and autonomous commands remain disabled.");
            Status = "Borin player profile saved.";
        }
        catch (Exception exception)
        {
            Add("Borin", $"The external-player profile could not be saved: {exception.Message}");
            Status = "Player profile save failed safely.";
        }
        finally
        {
            RaisePlayerCommandStates();
        }
    }

    private async Task ConnectPlayerAsync()
    {
        try
        {
            // Connect & Login is the activation action. A disconnected session is the expected
            // starting state, so do not require the profile to be enabled before this button can run.
            // Enabling here keeps the UI from trapping the user behind a disabled connection button.
            if (!PlayerEnabled)
                PlayerEnabled = true;

            await ConfigurePlayerAsync();
            await _playerResearch.ConnectAsync();
            _playerLoginStatus = _playerResearch.CurrentLoginStatus;
            ApplyPlayerStatus(_playerResearch.CurrentStatus);
            var gameState = _playerResearch.CurrentGameState;
            if (gameState.IsLoggedIntoMajorMud)
            {
                Add("Borin", $"I am connected and verified inside MajorMUD on {gameState.GameServerIdentity}. The current HP prompt is live.");
                Status = "Borin is connected and verified inside MajorMUD.";
            }
            else if (gameState.IsConnected)
            {
                Add("Borin", $"The network connection to {gameState.GameServerIdentity} is live. I am not claiming to be in the realm yet; the login controller is at {gameState.LoginState}: {gameState.PhaseDetail}");
                Status = "Borin is connected; MajorMUD login is still being verified.";
            }
            else
            {
                Add("Borin", $"I am not connected. The connection attempt ended in {gameState.ConnectionState}: {gameState.PhaseDetail}");
                Status = "Borin connection is not live.";
            }
        }
        catch (Exception exception)
        {
            Add("Borin", $"The external session could not connect: {exception.Message}");
            Status = "External connection failed safely.";
        }
        finally
        {
            RaisePlayerCommandStates();
        }
    }

    private async Task DisconnectPlayerAsync()
    {
        try
        {
            await _playerResearch.DisconnectAsync();
            Status = "Borin external session disconnected.";
        }
        catch (Exception exception)
        {
            Add("Borin", $"The external session could not disconnect cleanly: {exception.Message}");
        }
        finally
        {
            RaisePlayerCommandStates();
        }
    }

    private async Task TogglePlayerObservationAsync()
    {
        PlayerObservationEnabled = !PlayerObservationEnabled;
        await _playerResearch.SetObservationEnabledAsync(PlayerObservationEnabled);
        ApplyPlayerStatus(_playerResearch.CurrentStatus);
        Add("Borin", PlayerObservationEnabled
            ? "Parity journal is active. I will record my own commands and the server's results."
            : "Parity journal observation is off.");
        RaisePlayerCommandStates();
    }

    private async Task TogglePlayerConversationAsync()
    {
        PlayerConversationEnabled = !PlayerConversationEnabled;
        await _playerResearch.SetConversationEnabledAsync(PlayerConversationEnabled);
        ApplyPlayerStatus(_playerResearch.CurrentStatus);
        Add("Borin", PlayerConversationEnabled
            ? "Conversation is active. I will answer telepaths, explicit room or group addresses, and active-thread follow-ups. I will ignore speech directed to others, yield in busy public chat, and stop AI loops after three replies."
            : "Automatic external conversation is muted.");
        RaisePlayerCommandStates();
    }

    private async Task TogglePlayerJournalAsync()
    {
        PlayerReflectionJournalEnabled = !PlayerReflectionJournalEnabled;
        await _playerResearch.SetReflectionJournalEnabledAsync(PlayerReflectionJournalEnabled);
        Add("Borin", PlayerReflectionJournalEnabled
            ? "My private reflection journal is open. I will record high-level lessons, not hidden reasoning or credentials."
            : "My private reflection journal is paused. Existing entries remain intact.");
        RaisePlayerProperties();
    }

    private async Task ToggleAutomaticReflectionAsync()
    {
        PlayerAutomaticReflectionEnabled = !PlayerAutomaticReflectionEnabled;
        await _playerResearch.SetAutomaticReflectionEnabledAsync(PlayerAutomaticReflectionEnabled);
        Add("Borin", PlayerAutomaticReflectionEnabled
            ? "Automatic private reflection is active when enough meaningful events occur and I am safe."
            : "Automatic reflection is off. You can still ask me to write one manually.");
        RaisePlayerProperties();
    }

    private async Task ToggleConversationLearningAsync()
    {
        PlayerConversationLearningEnabled = !PlayerConversationLearningEnabled;
        await _playerResearch.SetConversationLearningEnabledAsync(PlayerConversationLearningEnabled);
        Add("Borin", PlayerConversationLearningEnabled
            ? "I will use William-verified conversation lessons and player summaries to improve future replies."
            : "Conversation learning is paused; I will not retrieve stored social lessons for new replies.");
        RaisePlayerProperties();
    }

    private async Task WritePlayerReflectionAsync()
    {
        try
        {
            var entry = await _playerResearch.WriteReflectionAsync(PlayerReflectionFocus, "manual");
            Add("Borin's Journal", entry.Text);
            Status = "Borin wrote a private player reflection.";
        }
        catch (Exception exception)
        {
            Add("Borin", $"I could not write the reflection safely: {exception.Message}");
            Status = "Player reflection failed safely.";
        }
        finally
        {
            RaisePlayerProperties();
        }
    }

    private async Task ReviewPlayerConversationAsync(string rating)
    {
        try
        {
            var reviewed = await _playerResearch.ReviewLastConversationAsync(rating, PlayerConversationReviewNote);
            if (reviewed is null)
                Add("Borin", "There is no directly addressed external conversation to review yet.");
            else
                Add("Borin", $"Conversation marked {reviewed.Rating}: {reviewed.ReviewNote}");
            PlayerConversationReviewNote = string.Empty;
            Status = reviewed is null ? "No conversation available for review." : "Conversation lesson saved and verified by William.";
        }
        catch (Exception exception)
        {
            Add("Borin", $"The conversation review could not be saved: {exception.Message}");
        }
        finally
        {
            RaisePlayerProperties();
        }
    }

    private async Task ShowPlayerJournalAsync()
    {
        try
        {
            Add("Borin's Journal", await _playerResearch.GetReflectionJournalSummaryAsync());
        }
        catch (Exception exception)
        {
            Add("Borin", $"The private journal could not be read: {exception.Message}");
        }
    }

    private async Task SavePlayerAssignmentAsync()
    {
        try
        {
            await ConfigurePlayerAsync();
            await _playerResearch.SaveAssignmentAsync(PlayerCurrentGoal);
            ApplyPlayerSettings(_playerResearch.CurrentSettings);
            ApplyPlayerStatus(_playerResearch.CurrentStatus);
            Add("Borin", $"Assignment saved: {PlayerCurrentGoal}");
            Status = "Borin assignment saved.";
        }
        catch (Exception exception)
        {
            Add("Borin", $"The assignment could not be saved: {exception.Message}");
            Status = "Assignment save failed safely.";
        }
        finally
        {
            RaisePlayerProperties();
        }
    }

    private async Task StartAutonomousPlayerAsync()
    {
        try
        {
            await ConfigurePlayerAsync();
            await _playerResearch.StartAutonomousPlayAsync(PlayerCurrentGoal);
            ApplyPlayerStatus(_playerResearch.CurrentStatus);
            Add("Borin", $"I am playing by myself on {_playerResearch.CurrentGameState.GameServerIdentity}. Assignment: {PlayerCurrentGoal}");
            Status = "Borin is playing by himself.";
        }
        catch (Exception exception)
        {
            Add("Borin", $"I cannot start playing by myself yet: {exception.Message}");
            Status = "Autonomous play remained locked safely.";
        }
        finally
        {
            RaisePlayerProperties();
        }
    }

    private async Task StopAutonomousPlayerAsync()
    {
        try
        {
            await _playerResearch.StopAutonomousPlayAsync("Stopped by William from the Assignment window.");
            ApplyPlayerStatus(_playerResearch.CurrentStatus);
            Add("Borin", "Play by Himself stopped. William has control.");
            Status = "Autonomous play stopped.";
        }
        catch (Exception exception)
        {
            Add("Borin", $"Autonomous play could not be stopped cleanly: {exception.Message}");
        }
        finally
        {
            RaisePlayerProperties();
        }
    }

    private async Task ProposePlayerActionAsync()
    {
        try
        {
            await ConfigurePlayerAsync();
            _playerDecision = await _playerResearch.ProposeNextActionAsync();
            Add("Borin", _playerDecision.Summary);
            if (!string.IsNullOrWhiteSpace(_playerDecision.Question))
                Add("Borin", _playerDecision.Question);
            Raise(nameof(PlayerDecisionSummary));
            Status = "Borin proposed a supervised next action.";
        }
        catch (Exception exception)
        {
            Add("Borin", $"I could not propose a safe next action: {exception.Message}");
            Status = "Player decision failed safely.";
        }
        finally
        {
            RaisePlayerCommandStates();
        }
    }

    private async Task ApprovePlayerActionAsync()
    {
        try
        {
            var result = await _playerResearch.ApproveProposedActionAsync();
            Add("Borin", result.Summary);
            Status = result.Sent ? "Approved action sent to the external server." : "Approved action was blocked safely.";
        }
        catch (Exception exception)
        {
            Add("Borin", $"The approved action could not be sent: {exception.Message}");
        }
        finally
        {
            RaisePlayerCommandStates();
        }
    }

    private async Task SendSupervisedPlayerCommandAsync()
    {
        var command = PlayerCommandInput.Trim();
        PlayerCommandInput = string.Empty;
        try
        {
            await ConfigurePlayerAsync();
            var result = await _playerResearch.SendSupervisedCommandAsync(command, "Explicitly sent by William from Borin's supervised player panel.");
            Add("Borin", result.Summary);
            Status = result.Sent ? "Supervised command sent." : "Supervised command blocked safely.";
        }
        catch (Exception exception)
        {
            Add("Borin", $"The supervised command could not be sent: {exception.Message}");
        }
        finally
        {
            RaisePlayerCommandStates();
        }
    }

    private async Task PausePlayerAsync()
    {
        await _playerResearch.PauseAsync("Paused by William.");
        ApplyPlayerStatus(_playerResearch.CurrentStatus);
        Add("Borin", "Aye. Player control and automatic replies are paused.");
        RaisePlayerCommandStates();
    }

    private async Task ResumePlayerAsync()
    {
        await _playerResearch.ResumeAsync();
        ApplyPlayerStatus(_playerResearch.CurrentStatus);
        Add("Borin", "Supervised player control resumed. Commands still require approval.");
        RaisePlayerCommandStates();
    }

    private async Task EmergencyStopPlayerAsync()
    {
        await _playerResearch.EmergencyStopAsync();
        PlayerConversationEnabled = false;
        ApplyPlayerStatus(_playerResearch.CurrentStatus);
        Add("Borin", "Emergency stop engaged. I will send no speech or gameplay commands until you resume me.");
        RaisePlayerCommandStates();
    }

    private Task PlayerHelpAsync()
    {
        Add("Borin", "Use the Assignment window to give me a goal. RealmGate is the application; the active terminal may be Omniverse BBS, the RealmGate server, or another configured server. Play by Himself stays locked until I have a fresh HP prompt, room title, and exit list from that exact session. Human commands stop autonomous play immediately. Passwords remain outside files, logs, memory, journals, and model prompts.");
        return Task.CompletedTask;
    }

    private void OnPlayerStatusChanged(object? sender, BorinPlayerRuntimeStatus status) =>
        RunOnUi(() => ApplyPlayerStatus(status));

    private void OnPlayerLoginStatusChanged(object? sender, BorinLoginStatus status) =>
        RunOnUi(() =>
        {
            _playerLoginStatus = status;
            Raise(nameof(PlayerLoginStatusSummary));
            Raise(nameof(PlayerLoginStatusDetail));
            Raise(nameof(PlayerPasswordStatus));
            RaisePlayerCommandStates();
        });

    private void OnPlayerConversationObserved(object? sender, BorinPlayerConversationEvent conversation) =>
        RunOnUi(() =>
        {
            PlayerLastConversationSummary = conversation.Summary;
            if (!conversation.Suppressed && !string.IsNullOrWhiteSpace(conversation.Response))
                Add("Borin External", conversation.Summary);
        });

    private async void OnPlayerEvidenceRecorded(object? sender, BorinPlayerEvidenceEvent evidence)
    {
        RunOnUi(() =>
        {
            PlayerLastEvidenceSummary = evidence.Summary;
            Raise(nameof(PlayerJournalPath));
        });
        try
        {
            await _verifiedKnowledgeLearning.RecordObservationAsync(new BorinRuntimeKnowledgeObservation(
                $"{evidence.Kind}/{evidence.Classification}" + (evidence.Command.Length == 0 ? string.Empty : $" after {evidence.Command}"),
                evidence.RawLine.Length == 0 ? evidence.Summary : evidence.RawLine,
                PlayerServerProfileName,
                PlayerWorldName,
                PlayerCharacterName,
                evidence.Room,
                evidence.EvidencePath,
                evidence.Timestamp));
            if (IsBlockedMovementEvidence(evidence) && !string.IsNullOrWhiteSpace(evidence.RoomKey))
            {
                var expectedExit = await _majorMudKnowledgeCatalog.GetExitAsync(
                    Configuration.RepositoryPath, evidence.RoomKey, evidence.Command);
                if (expectedExit is not null)
                {
                    var destination = expectedExit.Relationships.FirstOrDefault(edge => edge.Contains("to_room", StringComparison.OrdinalIgnoreCase)) ?? expectedExit.Id;
                    await _verifiedKnowledgeLearning.ObserveComparisonAsync(
                        $"{evidence.RoomKey} {evidence.Command} movement",
                        $"Generated exit exists: {destination}",
                        "Movement blocked at runtime",
                        expectedExit.Provenance.RelativePath,
                        evidence.EvidencePath,
                        requiredAttempts: 3);
                }
            }
            _verifiedLearningStatus = await _verifiedKnowledgeLearning.GetStatusAsync();
            RunOnUi(() => Raise(nameof(VerifiedLearningSummary)));
        }
        catch
        {
            // The existing gameplay journal remains authoritative if optional knowledge consolidation fails.
        }
    }

    private static bool IsBlockedMovementEvidence(BorinPlayerEvidenceEvent evidence)
    {
        var command = evidence.Command.Trim().ToUpperInvariant();
        var movement = command is "N" or "S" or "E" or "W" or "NE" or "NW" or "SE" or "SW" or "U" or "D" or
            "NORTH" or "SOUTH" or "EAST" or "WEST" or "NORTHEAST" or "NORTHWEST" or "SOUTHEAST" or "SOUTHWEST" or "UP" or "DOWN";
        if (!movement) return false;
        return evidence.RawLine.Contains("cannot go", StringComparison.OrdinalIgnoreCase) ||
               evidence.RawLine.Contains("can't go", StringComparison.OrdinalIgnoreCase) ||
               evidence.RawLine.Contains("cannot move", StringComparison.OrdinalIgnoreCase) ||
               evidence.Classification.Contains("blocked", StringComparison.OrdinalIgnoreCase) ||
               evidence.Classification.Contains("failure", StringComparison.OrdinalIgnoreCase);
    }

    private void OnPlayerJournalChanged(object? sender, BorinPlayerJournalStatus status) =>
        RunOnUi(() =>
        {
            _playerJournalStatus = status;
            Raise(nameof(PlayerReflectionJournalSummary));
            Raise(nameof(PlayerCurrentReflection));
            Raise(nameof(PlayerConversationLessonSummary));
            Raise(nameof(PlayerSpeakerProfileSummary));
            Raise(nameof(PlayerReflectionJournalPath));
        });

    private void ApplyPlayerStatus(BorinPlayerRuntimeStatus status)
    {
        _playerRuntimeStatus = status;
        PlayerObservationEnabled = status.ObservationEnabled;
        PlayerConversationEnabled = status.ConversationEnabled;
        RaisePlayerProperties();
    }

    private void RaisePlayerProperties()
    {
        Raise(nameof(PlayerStatusSummary));
        Raise(nameof(PlayerSafetySummary));
        Raise(nameof(PlayerStatusDetail));
        Raise(nameof(PlayerLoginStatusSummary));
        Raise(nameof(PlayerLoginStatusDetail));
        Raise(nameof(PlayerPasswordStatus));
        Raise(nameof(PlayerDecisionSummary));
        Raise(nameof(PlayerEnvironmentSummary));
        Raise(nameof(PlayerPerceptionSummary));
        Raise(nameof(PlayerAutonomousPlaySummary));
        Raise(nameof(PlayerLastIncomingTelepath));
        Raise(nameof(PlayerCanPlayByHimself));
        Raise(nameof(PlayerCanStopAutonomousPlay));
        Raise(nameof(PlayerJournalPath));
        Raise(nameof(PlayerReflectionJournalSummary));
        Raise(nameof(PlayerCurrentReflection));
        Raise(nameof(PlayerConversationLessonSummary));
        Raise(nameof(PlayerSpeakerProfileSummary));
        Raise(nameof(PlayerReflectionJournalPath));
        RaisePlayerCommandStates();
    }

    private void RaisePlayerCommandStates()
    {
        if (SavePlayerProfileCommand is AsyncRelayCommand save) save.Raise();
        if (ConnectPlayerCommand is AsyncRelayCommand connect) connect.Raise();
        if (DisconnectPlayerCommand is AsyncRelayCommand disconnect) disconnect.Raise();
        if (TogglePlayerObservationCommand is AsyncRelayCommand observe) observe.Raise();
        if (TogglePlayerConversationCommand is AsyncRelayCommand conversation) conversation.Raise();
        if (TogglePlayerJournalCommand is AsyncRelayCommand journal) journal.Raise();
        if (ToggleAutomaticReflectionCommand is AsyncRelayCommand automaticReflection) automaticReflection.Raise();
        if (ToggleConversationLearningCommand is AsyncRelayCommand conversationLearning) conversationLearning.Raise();
        if (WritePlayerReflectionCommand is AsyncRelayCommand writeReflection) writeReflection.Raise();
        if (MarkConversationHelpfulCommand is AsyncRelayCommand helpful) helpful.Raise();
        if (MarkConversationAwkwardCommand is AsyncRelayCommand awkward) awkward.Raise();
        if (MarkConversationIncorrectCommand is AsyncRelayCommand incorrect) incorrect.Raise();
        if (ShowPlayerJournalCommand is AsyncRelayCommand showJournal) showJournal.Raise();
        if (ProposePlayerActionCommand is AsyncRelayCommand propose) propose.Raise();
        if (ApprovePlayerActionCommand is AsyncRelayCommand approve) approve.Raise();
        if (SendSupervisedPlayerCommand is AsyncRelayCommand send) send.Raise();
        if (PausePlayerCommand is AsyncRelayCommand pause) pause.Raise();
        if (ResumePlayerCommand is AsyncRelayCommand resume) resume.Raise();
        if (EmergencyStopPlayerCommand is AsyncRelayCommand stop) stop.Raise();
        if (PlayerHelpCommand is AsyncRelayCommand help) help.Raise();
        if (SavePlayerAssignmentCommand is AsyncRelayCommand saveAssignment) saveAssignment.Raise();
        if (StartAutonomousPlayerCommand is AsyncRelayCommand startAutonomous) startAutonomous.Raise();
        if (StopAutonomousPlayerCommand is AsyncRelayCommand stopAutonomous) stopAutonomous.Raise();
        Raise(nameof(PlayerCanPlayByHimself));
        Raise(nameof(PlayerCanStopAutonomousPlay));
    }

    private static void RunOnUi(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
            action();
        else
            _ = dispatcher.InvokeAsync(action);
    }

    private void RaiseCommandStates()
    {
        ((AsyncRelayCommand)SendCommand).Raise();
        ((AsyncRelayCommand)CapabilityStatusCommand).Raise();
        ((AsyncRelayCommand)CapabilityRealmCommand).Raise();
        ((AsyncRelayCommand)CapabilityAnalysisCommand).Raise();
        ((AsyncRelayCommand)CapabilityDiagnosticsCommand).Raise();
        ((AsyncRelayCommand)CapabilityStopCommand).Raise();
        ((RelayCommand)CancelCommand).Raise();
        ((AsyncRelayCommand)LoadModelsCommand).Raise();
        ((AsyncRelayCommand)TestBrainCommand).Raise();
        ((AsyncRelayCommand)ChangeLightningKeyCommand).Raise();
        ((AsyncRelayCommand)TestLightningCommand).Raise();
        ((AsyncRelayCommand)TestSuperCommand).Raise();
        ((AsyncRelayCommand)TestUltraCommand).Raise();
        ((AsyncRelayCommand)ApplyBrainPerformancePresetCommand).Raise();
        ((AsyncRelayCommand)ImplementAttachedCommand).Raise();
        ((AsyncRelayCommand)ShowMemoriesCommand).Raise();
        ((AsyncRelayCommand)MemoryHelpCommand).Raise();
        ((AsyncRelayCommand)ShowRecallCommand).Raise();
        ((AsyncRelayCommand)ProjectAwarenessCommand).Raise();
        ((AsyncRelayCommand)ShowEvidenceCommand).Raise();
        ((AsyncRelayCommand)ReadOnlyAccessHelpCommand).Raise();
        ((AsyncRelayCommand)ShowClassicTraceCommand).Raise();
        ((AsyncRelayCommand)ClassicTraceHelpCommand).Raise();
        ((AsyncRelayCommand)LearnReforgedCommand).Raise();
        ((AsyncRelayCommand)LearnChangedCommand).Raise();
        ((AsyncRelayCommand)LearnStatusCommand).Raise();
        ((AsyncRelayCommand)KnowledgeHelpCommand).Raise();
        ((AsyncRelayCommand)CreateWorkspaceCommand).Raise();
        ((AsyncRelayCommand)WorkspaceStatusCommand).Raise();
        ((AsyncRelayCommand)ApplyProposedDiffCommand).Raise();
        ((AsyncRelayCommand)BuildWorkspaceCommand).Raise();
        ((AsyncRelayCommand)TestWorkspaceCommand).Raise();
        ((AsyncRelayCommand)ShowDiffCommand).Raise();
        ((AsyncRelayCommand)ApplyWorkspaceCommand).Raise();
        ((AsyncRelayCommand)DraftEngineeringCommand).Raise();
        ((AsyncRelayCommand)PackageEngineeringDraftCommand).Raise();
        ((AsyncRelayCommand)EngineeringHelpCommand).Raise();
        ((AsyncRelayCommand)StartEngineeringJobCommand).Raise();
        ((AsyncRelayCommand)ShowEngineeringJobCommand).Raise();
        ((AsyncRelayCommand)ListEngineeringJobsCommand).Raise();
        ((AsyncRelayCommand)ShowEngineeringChangesCommand).Raise();
        ((AsyncRelayCommand)ApproveEngineeringJobCommand).Raise();
        ((AsyncRelayCommand)RejectEngineeringJobCommand).Raise();
        ((AsyncRelayCommand)BuildTestEngineeringJobCommand).Raise();
        ((AsyncRelayCommand)PackageEngineeringJobCommand).Raise();
        ((AsyncRelayCommand)IncorporateEngineeringJobCommand).Raise();
        ((AsyncRelayCommand)CorrectEngineeringJobCommand).Raise();
        ((AsyncRelayCommand)ShowCorrectionAttemptsCommand).Raise();
        ((AsyncRelayCommand)StopEngineeringCorrectionCommand).Raise();
        RaisePlayerCommandStates();
    }

    private void Add(string role, string text)
    {
        if (role.Equals("Borin", StringComparison.OrdinalIgnoreCase))
        {
            text = CleanStoredBorinReply(text);
            if (text.Length == 0)
                return;
        }

        var message = new ForgeChatMessage { Role = role, Text = text, Timestamp = DateTimeOffset.Now };
        _state.Messages.Add(message); Messages.Add(message);
        while (_state.Messages.Count > 500) { _state.Messages.RemoveAt(0); if (Messages.Count > 0) Messages.RemoveAt(0); }
    }

    private Task PersistAsync() => _store.SaveAsync(_state);
}
