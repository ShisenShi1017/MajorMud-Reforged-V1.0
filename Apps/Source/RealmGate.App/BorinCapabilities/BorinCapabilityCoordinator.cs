using RealmGate.App.Brains;
using System.IO;
using System.Text;

namespace RealmGate.App.BorinCapabilities;

/// <summary>
/// The single local authority behind Borin's former Guardian capabilities.
/// It owns no model and no second player session: reasoning stays with Borin's
/// hybrid brain and player operations read the authoritative IBorinPlayerResearch.
/// </summary>
public sealed class BorinCapabilityCoordinator : IBorinCapabilities
{
    private readonly IBorinCapabilityStateStore _store;
    private readonly IBorinPlayerResearch _player;
    private readonly IRealmKeeperTelemetryService _realmTelemetry;
    private readonly IAnalystTelemetryService _analysisTelemetry;
    private readonly ArchivistService _evidence;
    private readonly BorinCapabilityDiagnosticsService _diagnostics;
    private readonly BorinCapabilityScheduler _scheduler = new();
    private readonly string _repositoryRoot;
    private BorinCapabilityState _state = new();

    public BorinCapabilityCoordinator(
        IBorinCapabilityStateStore store,
        string repositoryRoot,
        IBorinPlayerResearch player)
    {
        _store = store;
        _player = player;
        _repositoryRoot = repositoryRoot;
        _realmTelemetry = new RealmKeeperTelemetryService(repositoryRoot);
        _analysisTelemetry = new AnalystTelemetryService(repositoryRoot);
        _evidence = new ArchivistService(store.RootDirectory);
        _diagnostics = new BorinCapabilityDiagnosticsService(store);
    }

    public string StatusSummary { get; private set; } = "Borin capabilities are initializing.";

    public async Task InitializeAsync(CancellationToken token = default)
    {
        _state = await _store.LoadAsync(token) ?? new BorinCapabilityState();
        _state.Profiles = BorinCapabilityProfileCatalog.CreateDefaults();
        foreach (var job in _state.Orchestration.Jobs.Where(job => job.Status == BorinCapabilityJobStatus.Running))
        {
            job.Status = BorinCapabilityJobStatus.Queued;
            job.Message = "Recovered for Borin capability coordinator";
        }
        await _store.SaveAsync(_state, token);
        StatusSummary = "Realm, player, analysis, evidence, diagnostics, and scheduling capabilities ready; Worldsmith withheld.";
    }

    public async Task<BorinCapabilityResult> ExecuteAsync(string request, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        var route = BorinCapabilityRequestRouter.Route(request);
        var result = route.Capability switch
        {
            BorinCapabilityOperation.None => BorinCapabilityResult.NotHandled,
            BorinCapabilityOperation.WorldsmithWithheld => Result(false,
                "Worldsmith is installed but withheld. No world content was inspected or changed."),
            BorinCapabilityOperation.StopAll => await StopAllAsync(token),
            BorinCapabilityOperation.PauseScheduler => await SetSchedulerAsync(true, token),
            BorinCapabilityOperation.ResumeScheduler => await SetSchedulerAsync(false, token),
            BorinCapabilityOperation.RunScheduledJobs => await RunJobsAsync(token),
            BorinCapabilityOperation.CapabilityDiagnostics => await RunDiagnosticsAsync(token),
            BorinCapabilityOperation.DecodeEvidenceQueue => await DecodeEvidenceAsync(token),
            BorinCapabilityOperation.EvidenceSearch => SearchEvidence(route.Argument),
            BorinCapabilityOperation.ExportAnalysisReport => await ExportAnalysisAsync(token),
            BorinCapabilityOperation.ExportEvidenceReport => await ExportEvidenceAsync(route.Argument, token),
            BorinCapabilityOperation.Analysis => await AnalyzeAsync(route.Argument, token),
            BorinCapabilityOperation.RealmMonitoring => await MonitorRealmAsync(token),
            BorinCapabilityOperation.PlayerTelemetry => PlayerTelemetry(),
            BorinCapabilityOperation.Overview => await OverviewAsync(token),
            _ => BorinCapabilityResult.NotHandled,
        };
        if (result.Handled) StatusSummary = result.Summary;
        return result;
    }

    private async Task<BorinCapabilityResult> MonitorRealmAsync(CancellationToken token)
    {
        var snapshot = await _realmTelemetry.CaptureAsync(_state.RealmKeeper, token);
        Apply(snapshot);
        await _store.SaveAsync(_state, token);
        return Result(snapshot.RepositoryAvailable,
            $"{snapshot.HealthSummary} {snapshot.ActivitySummary} Source: {snapshot.SourceSummary}");
    }

    private BorinCapabilityResult PlayerTelemetry()
    {
        var game = _player.CurrentGameState;
        var runtime = _player.CurrentStatus;
        return Result(game.IsConnected,
            $"Borin player session: {runtime.Summary} Server: {game.GameServerIdentity}. " +
            $"Authenticated: {game.IsLoggedIntoMajorMud}. Room: {(game.HasVerifiedRoom ? game.CurrentRoom : "not verified")}. " +
            $"Autonomous play: {(_player.IsAutonomousPlayActive ? "running" : "stopped")}.");
    }

    private async Task<BorinCapabilityResult> AnalyzeAsync(string target, CancellationToken token)
    {
        _state.Analyst.AnalysisTarget = target;
        var snapshot = await _analysisTelemetry.CaptureAsync(_state.Analyst, token);
        Apply(snapshot);
        await _store.SaveAsync(_state, token);
        return Result(snapshot.DataAvailable,
            $"Analysis target: {target}. {snapshot.SourceSummary} {snapshot.ComparisonStatus}");
    }

    private BorinCapabilityResult SearchEvidence(string query)
    {
        _state.Archivist.SearchText = query;
        var results = _evidence.SearchEvidence(_state.Archivist, query);
        _state.Archivist.SearchResults.Clear();
        foreach (var item in results) _state.Archivist.SearchResults.Add(item);
        var first = results.FirstOrDefault();
        return Result(results.Count > 0, results.Count == 0
            ? $"No indexed evidence matched '{query}'."
            : $"Found {results.Count} evidence result(s) for '{query}'. Best match: {first!.Name} — {first.MatchSummary}");
    }

    private async Task<BorinCapabilityResult> DecodeEvidenceAsync(CancellationToken token)
    {
        if (_state.Archivist.DecodeQueue.Count == 0)
            return Result(false, "The evidence decode queue is empty; nothing was processed.");
        await _evidence.DecodeAsync(_state.Archivist, _state.Archivist.DecodeQueue, token);
        await _store.SaveAsync(_state, token);
        return Result(_state.Archivist.FailedFiles == 0, _state.Archivist.DecodeStatus);
    }

    private async Task<BorinCapabilityResult> ExportAnalysisAsync(CancellationToken token)
    {
        var snapshot = await _analysisTelemetry.CaptureAsync(_state.Analyst, token);
        Apply(snapshot);
        var folder = Path.Combine(_repositoryRoot, "Reports", "Borin", "Analysis");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"analysis-{DateTime.UtcNow:yyyyMMdd-HHmmss}.md");
        var body = new StringBuilder()
            .AppendLine("# Borin Analysis Report")
            .AppendLine().AppendLine($"Target: {_state.Analyst.AnalysisTarget}")
            .AppendLine($"Captured UTC: {snapshot.CapturedUtc:O}")
            .AppendLine($"Source files: {snapshot.SourceFiles}")
            .AppendLine($"Source records: {snapshot.SourceRecords}")
            .AppendLine().AppendLine(snapshot.SourceSummary)
            .AppendLine().AppendLine(snapshot.ComparisonStatus)
            .ToString();
        await File.WriteAllTextAsync(path, body, token);
        return Result(true, $"Analysis report exported: {path}");
    }

    private async Task<BorinCapabilityResult> ExportEvidenceAsync(string format, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var path = await _evidence.GenerateReportAsync(_state.Archivist, format);
        return Result(true, $"{format} evidence report generated: {path}");
    }

    private async Task<BorinCapabilityResult> RunDiagnosticsAsync(CancellationToken token)
    {
        await _diagnostics.RunAsync(_state, _state.Diagnostics, token);
        return Result(_state.Diagnostics.FailedChecks == 0, _state.Diagnostics.OverallStatus);
    }

    private async Task<BorinCapabilityResult> SetSchedulerAsync(bool paused, CancellationToken token)
    {
        _state.Orchestration.Paused = paused;
        _state.Orchestration.RuntimeStatus = paused ? "Borin capability scheduler paused" : "Borin capability scheduler resumed";
        await _store.SaveAsync(_state, token);
        return Result(true, _state.Orchestration.RuntimeStatus);
    }

    private async Task<BorinCapabilityResult> RunJobsAsync(CancellationToken token)
    {
        await _scheduler.RunDueJobsAsync(_state.Orchestration, ExecuteJobAsync, token);
        await _store.SaveAsync(_state, token);
        return Result(true, _state.Orchestration.RuntimeStatus);
    }

    private async Task<string> ExecuteJobAsync(BorinCapabilityArea role)
    {
        return role switch
        {
            BorinCapabilityArea.RealmKeeper => (await MonitorRealmAsync(CancellationToken.None)).Summary,
            BorinCapabilityArea.Player => PlayerTelemetry().Summary,
            BorinCapabilityArea.Analyst => (await AnalyzeAsync(_state.Analyst.AnalysisTarget, CancellationToken.None)).Summary,
            BorinCapabilityArea.Archivist => (await DecodeEvidenceAsync(CancellationToken.None)).Summary,
            BorinCapabilityArea.Worldsmith => throw new InvalidOperationException("Worldsmith is withheld."),
            _ => throw new InvalidOperationException("Unknown Borin capability job."),
        };
    }

    private async Task<BorinCapabilityResult> StopAllAsync(CancellationToken token)
    {
        await _player.StopAutonomousPlayAsync("Stopped by Borin capability emergency stop.", token);
        _state.Orchestration.Paused = true;
        foreach (var job in _state.Orchestration.Jobs.Where(job => job.Status == BorinCapabilityJobStatus.Running))
            job.Status = BorinCapabilityJobStatus.Cancelled;
        await _store.SaveAsync(_state, token);
        return Result(true, "Borin autonomous play and capability scheduling stopped.");
    }

    private async Task<BorinCapabilityResult> OverviewAsync(CancellationToken token)
    {
        var realm = await MonitorRealmAsync(token);
        var player = PlayerTelemetry();
        var analysis = await AnalyzeAsync(_state.Analyst.AnalysisTarget, token);
        return Result(true, $"Realm: {realm.Summary} Player: {player.Summary} Analysis: {analysis.Summary} Worldsmith: withheld.");
    }

    private void Apply(RealmKeeperTelemetrySnapshot snapshot)
    {
        _state.RealmKeeper.TelemetryLive = snapshot.RepositoryAvailable;
        _state.RealmKeeper.ConnectedHosts = snapshot.ConnectedHosts;
        _state.RealmKeeper.CoveredWorlds = snapshot.CoveredWorlds;
        _state.RealmKeeper.GeneratedRecords = snapshot.GeneratedRecords;
        _state.RealmKeeper.ActiveSaveFiles = snapshot.ActiveSaveFiles;
        _state.RealmKeeper.CurrentRealmHealth = snapshot.HealthSummary;
        _state.RealmKeeper.LiveActivity = snapshot.ActivitySummary;
        _state.RealmKeeper.TelemetryStatus = snapshot.SourceSummary;
    }

    private void Apply(AnalystTelemetrySnapshot snapshot)
    {
        _state.Analyst.DataAvailable = snapshot.DataAvailable;
        _state.Analyst.SourceFiles = snapshot.SourceFiles;
        _state.Analyst.SourceRecords = snapshot.SourceRecords;
        _state.Analyst.ReportFiles = snapshot.ReportFiles;
        _state.Analyst.DataSources = snapshot.DataSources;
        _state.Analyst.Graphs = snapshot.GraphSummary;
        _state.Analyst.HistoricalTrends = snapshot.HistoricalTrends;
        _state.Analyst.RuntimeStatus = snapshot.SourceSummary;
        _state.Analyst.ComparisonStatus = snapshot.ComparisonStatus;
    }

    private static BorinCapabilityResult Result(bool success, string summary) => new(true, success, summary);
}
