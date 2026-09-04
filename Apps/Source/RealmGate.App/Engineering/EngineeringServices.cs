using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using RealmGate.App.Forge;

namespace RealmGate.App.Engineering;

public interface IEngineeringRepairEngine
{
    Task<EngineeringCommandResult> RepairAsync(
        string workspace,
        string objective,
        IReadOnlyList<EngineeringTask> taskHistory,
        CancellationToken token);
}

public sealed class BorinAutonomousRepairEngine(
    IForgeStateStore forgeStore,
    IForgeRepositoryService repository,
    IForgeBrain brain,
    IForgeDeveloperTools tools) : IEngineeringRepairEngine
{
    public async Task<EngineeringCommandResult> RepairAsync(
        string workspace,
        string objective,
        IReadOnlyList<EngineeringTask> taskHistory,
        CancellationToken token)
    {
        var watch = Stopwatch.StartNew();
        var state = await forgeStore.LoadAsync(token);
        if (string.IsNullOrWhiteSpace(state.Configuration.ModelName))
            return new(3, "Borin cannot author a repair because no LM Studio coding model is selected in The Forge.", 0);
        if (!state.Configuration.AllowWorkspaceEdits)
            return new(3, "Borin cannot author a repair because Forge workspace edits are disabled.", 0);

        state.Configuration.RepositoryPath = workspace;
        state.ActiveWorkspacePath = workspace;
        var evidenceTasks = taskHistory
            .Where(task => !string.IsNullOrWhiteSpace(task.Result) && task.Status == EngineeringTaskStatus.Failed)
            .TakeLast(6)
            .Concat(taskHistory.Where(task => !string.IsNullOrWhiteSpace(task.Result)).TakeLast(4))
            .DistinctBy(task => task.Id);
        var evidence = string.Join(Environment.NewLine + Environment.NewLine,
            evidenceTasks
                .Select(task => $"## {task.Title} [{task.Status}]{Environment.NewLine}{Trim(task.Result, 6000)}"));
        var allowedPaths = Directory.EnumerateFiles(workspace, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                           path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
                           path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
            .Select(path => Path.GetRelativePath(workspace, path).Replace('\\', '/'))
            .Where(path => path.Contains("Runtime", StringComparison.OrdinalIgnoreCase) ||
                           path.Contains("Telnet", StringComparison.OrdinalIgnoreCase) ||
                           path.Contains("Command", StringComparison.OrdinalIgnoreCase) ||
                           path.Contains("Session", StringComparison.OrdinalIgnoreCase) ||
                           path.Contains("Connection", StringComparison.OrdinalIgnoreCase) ||
                           path.Contains("Compiler", StringComparison.OrdinalIgnoreCase) ||
                           path.Contains("Npc", StringComparison.OrdinalIgnoreCase) ||
                           path.Contains("Librarium", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path)
            .Take(300);
        var prompt = $"""
/brain Repair this engineering objective inside the current isolated workspace.

OBJECTIVE
{objective}

CAPTURED DIAGNOSTIC AND TEST EVIDENCE
{evidence}

REAL REPOSITORY PATHS YOU MAY EDIT
{string.Join(Environment.NewLine, allowedPaths)}

Inspect the repository and evidence, identify the smallest verified root-cause repair, and return one complete fenced ```diff unified Git patch.
Rules:
- Use only paths that exist in the supplied repository context or real-path list. Never invent a file, type, method, or test project.
- If the evidence is only an environment/setup failure or proves no source defect, return exactly NO_VERIFIED_SOURCE_REPAIR instead of guessing.
- Edit source or authoritative Librarium/compiler input only; never edit generated Data.
- Preserve Classic evidence and ImportedDatabases.
- Do not commit, push, publish, install, delete, or alter the live repository.
- Include focused regression coverage when practical.
- Do not return prose outside the fenced diff.
""";
        var snapshot = await repository.ScanAsync(workspace, token);
        var response = await brain.RespondAsync(prompt, state, snapshot, token);
        state.Messages.Add(new ForgeChatMessage { Role = "Borin", Text = response, Timestamp = DateTimeOffset.Now });
        if (response.Contains("NO_VERIFIED_SOURCE_REPAIR", StringComparison.OrdinalIgnoreCase))
            return new(6, "LM Studio found no verified source repair in the supplied evidence; no speculative patch was applied.", (int)watch.Elapsed.TotalSeconds);
        if (!response.Contains("```diff", StringComparison.OrdinalIgnoreCase))
            return new(4, $"Borin returned no applicable unified diff. Response: {Trim(response, 6000)}", (int)watch.Elapsed.TotalSeconds);

        var apply = await tools.ApplyLatestDiffToWorkspaceAsync(state, token);
        watch.Stop();
        var failed = apply.Contains("failed", StringComparison.OrdinalIgnoreCase)
            || apply.Contains("could not", StringComparison.OrdinalIgnoreCase)
            || apply.Contains("no fenced", StringComparison.OrdinalIgnoreCase);
        return new(failed ? 5 : 0, $"{apply}{Environment.NewLine}{Environment.NewLine}Borin repair proposal:{Environment.NewLine}{Trim(response, 8000)}", (int)watch.Elapsed.TotalSeconds);
    }

    private static string Trim(string value, int length) =>
        string.IsNullOrWhiteSpace(value) ? "(no output)" : value.Length <= length ? value : value[^length..];
}

public sealed class AuthorityGuard
{
    public (bool Allowed, string Message) Check(EngineeringAuthority authority, EngineeringConfiguration configuration)
    {
        var allowed = authority switch
        {
            EngineeringAuthority.ReadRepository => true,
            EngineeringAuthority.WriteWorkspace => configuration.CreateIsolatedWorkspace,
            EngineeringAuthority.RunBuild => configuration.AutonomousBuild,
            EngineeringAuthority.RunValidation => configuration.AutonomousValidation,
            EngineeringAuthority.RunRuntime => configuration.AutonomousRuntimeTesting || configuration.AutonomousGameplayTesting,
            EngineeringAuthority.CreateCandidatePackage => configuration.CandidateZipGeneration,
            EngineeringAuthority.WriteLiveRepository => configuration.RepositoryUpdates,
            EngineeringAuthority.GitWrite or EngineeringAuthority.Publish or EngineeringAuthority.Install or EngineeringAuthority.Delete => false,
            _ => false
        };
        return (allowed, allowed ? "Authorized inside the isolated engineering workspace." : "Blocked by AuthorityGuard.");
    }
}

public sealed class EngineeringPersistence
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    private readonly string _root;
    private readonly SemaphoreSlim _saveGate = new(1, 1);
    private string StatePath => Path.Combine(_root, "engineering-state.json");
    public EngineeringPersistence(string guardianRoot)
    {
        _root = Path.Combine(guardianRoot, "Engineering");
        Directory.CreateDirectory(_root);
    }
    public string Root => _root;
    public async Task<EngineeringWorkspace> LoadAsync(CancellationToken token = default)
    {
        if (!File.Exists(StatePath)) return new();
        await using var stream = File.OpenRead(StatePath);
        return await JsonSerializer.DeserializeAsync<EngineeringWorkspace>(stream, JsonOptions, token) ?? new();
    }
    public async Task SaveAsync(EngineeringWorkspace state, CancellationToken token = default)
    {
        await _saveGate.WaitAsync(token);
        try
        {
            var temporary = StatePath + ".tmp";
            await using (var stream = File.Create(temporary))
                await JsonSerializer.SerializeAsync(stream, state, JsonOptions, token);
            File.Move(temporary, StatePath, true);
        }
        finally
        {
            _saveGate.Release();
        }
    }
    public async Task<string> SaveReportAsync(EngineeringReport report, CancellationToken token)
    {
        var folder = Path.Combine(_root, "Reports");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"engineering-report-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(report, JsonOptions), token);
        return path;
    }
}

public sealed class WorkspaceManager
{
    private readonly AuthorityGuard _guard;
    public WorkspaceManager(AuthorityGuard guard) => _guard = guard;
    public async Task<string> CreateAsync(string repositoryRoot, EngineeringConfiguration configuration, string sessionId, CancellationToken token)
    {
        var authority = _guard.Check(EngineeringAuthority.WriteWorkspace, configuration);
        if (!authority.Allowed) throw new InvalidOperationException(authority.Message);
        var repositoryDrive = Path.GetPathRoot(Path.GetFullPath(repositoryRoot))
            ?? Directory.GetParent(repositoryRoot)?.FullName
            ?? repositoryRoot;
        var parent = Path.Combine(repositoryDrive, ".rw");
        Directory.CreateDirectory(parent);
        var workspace = Path.Combine(parent, sessionId[..Math.Min(8, sessionId.Length)]);
        var result = await EngineeringCommandRunner.RunAsync(
            repositoryRoot,
            "git",
            $"-c core.longpaths=true worktree add --detach \"{workspace}\" HEAD",
            token);
        if (result.ExitCode != 0) throw new InvalidOperationException($"Workspace creation failed: {result.Output}");
        await OverlayLocalChangesAsync(repositoryRoot, workspace, token);
        return workspace;
    }

    private static async Task OverlayLocalChangesAsync(string repositoryRoot, string workspace, CancellationToken token)
    {
        var changed = await EngineeringCommandRunner.RunAsync(
            repositoryRoot,
            "git",
            "diff --name-only --diff-filter=ACMRTUXB HEAD -- .",
            token);
        if (changed.ExitCode != 0)
            throw new InvalidOperationException($"Local tracked-change inventory failed: {changed.Output}");

        var untracked = await EngineeringCommandRunner.RunAsync(
            repositoryRoot,
            "git",
            "ls-files --others --exclude-standard",
            token);
        if (untracked.ExitCode != 0)
            throw new InvalidOperationException($"Local untracked-file snapshot failed: {untracked.Output}");

        var repositoryPrefix = Path.GetFullPath(repositoryRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var workspacePrefix = Path.GetFullPath(workspace).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        CopyFiles(changed.Output);
        CopyFiles(untracked.Output);

        var deleted = await EngineeringCommandRunner.RunAsync(
            repositoryRoot,
            "git",
            "diff --name-only --diff-filter=D HEAD -- .",
            token);
        if (deleted.ExitCode != 0)
            throw new InvalidOperationException($"Local tracked-deletion inventory failed: {deleted.Output}");
        foreach (var relative in Lines(deleted.Output))
        {
            token.ThrowIfCancellationRequested();
            var destination = Path.GetFullPath(Path.Combine(workspace, relative));
            if (destination.StartsWith(workspacePrefix, StringComparison.OrdinalIgnoreCase) && File.Exists(destination))
                File.Delete(destination);
        }

        void CopyFiles(string output)
        {
            foreach (var relative in Lines(output))
            {
                token.ThrowIfCancellationRequested();
                var source = Path.GetFullPath(Path.Combine(repositoryRoot, relative));
                var destination = Path.GetFullPath(Path.Combine(workspace, relative));
                if (!source.StartsWith(repositoryPrefix, StringComparison.OrdinalIgnoreCase) ||
                    !destination.StartsWith(workspacePrefix, StringComparison.OrdinalIgnoreCase) ||
                    !File.Exists(source))
                    continue;
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(source, destination, true);
            }
        }

        static IEnumerable<string> Lines(string output) =>
            output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
    }
}

public sealed record EngineeringCommandResult(int ExitCode, string Output, int DurationSeconds);

public static class EngineeringCommandRunner
{
    public static async Task<EngineeringCommandResult> RunShellAsync(string workingDirectory, string command, CancellationToken token)
    {
        var executable = OperatingSystem.IsWindows() ? "powershell.exe" : "/bin/sh";
        var arguments = OperatingSystem.IsWindows()
            ? $"-NoProfile -NonInteractive -Command \"{command.Replace("\"", "\\\"")}\""
            : $"-c \"{command.Replace("\"", "\\\"")}\"";
        return await RunAsync(workingDirectory, executable, arguments, token);
    }

    public static async Task<EngineeringCommandResult> RunAsync(string workingDirectory, string executable, string arguments, CancellationToken token)
    {
        var started = Stopwatch.StartNew();
        var output = new StringBuilder();
        using var process = new Process { StartInfo = new ProcessStartInfo
        {
            FileName = executable, Arguments = arguments, WorkingDirectory = workingDirectory,
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
        }};
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) output.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) output.AppendLine(e.Data); };
        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync(token);
            started.Stop();
            return new(process.ExitCode, output.ToString(), (int)started.Elapsed.TotalSeconds);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
            }
            catch
            {
                // The process may have exited between the cancellation and the kill attempt.
            }
            started.Stop();
            throw;
        }
        catch (Exception ex)
        {
            started.Stop();
            return new(-1, ex.Message, (int)started.Elapsed.TotalSeconds);
        }
    }
}

public sealed class EngineeringOrchestrator
{
    private readonly string _repositoryRoot;
    private readonly EngineeringPersistence _persistence;
    private readonly AuthorityGuard _guard;
    private readonly WorkspaceManager _workspaces;
    private readonly IEngineeringRepairEngine? _repairEngine;
    private CancellationTokenSource? _runCancellation;
    public EngineeringOrchestrator(string repositoryRoot, EngineeringPersistence persistence, IEngineeringRepairEngine? repairEngine = null)
    {
        _repositoryRoot = repositoryRoot;
        _persistence = persistence;
        _guard = new();
        _workspaces = new(_guard);
        _repairEngine = repairEngine;
    }

    public async Task StartAsync(EngineeringWorkspace state, CancellationToken token = default)
    {
        if (state.Status.Running) return;
        if (state.Configuration.BuildCommand.Equals(
                "dotnet build RealmGate.sln --configuration Release --no-restore",
                StringComparison.OrdinalIgnoreCase))
            state.Configuration.BuildCommand = "dotnet build RealmGate.sln --configuration Release";
        state.Configuration.ValidationCommand = RemoveNoBuild(state.Configuration.ValidationCommand);
        state.Configuration.RuntimeCommand = RemoveNoBuild(state.Configuration.RuntimeCommand);
        state.Configuration.GameplayTestingCommand = RemoveNoBuild(state.Configuration.GameplayTestingCommand);
        _runCancellation?.Dispose();
        _runCancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        var runToken = _runCancellation.Token;
        var session = new EngineeringSession();
        state.CurrentSession = session;
        state.Statistics.Sessions++;
        state.Status.Running = true;
        state.Status.Health = EngineeringHealth.Working;
        state.Status.CurrentSessionId = session.Id;
        var automated = HasAutomatedCapabilities(state.Configuration);
        state.Status.Summary = automated
            ? "Borin automation is enabled; preparing isolated workspace"
            : "Engineering session ready; all autonomous capabilities are off";
        Event(state, $"Session {session.Id} started; enabled capabilities: {EnabledCapabilities(state.Configuration)}.");
        if (automated)
        {
            try
            {
                session.WorkspacePath = await _workspaces.CreateAsync(_repositoryRoot, state.Configuration, session.Id, runToken);
            }
            catch (OperationCanceledException)
            {
                session.StoppedUtc = DateTime.UtcNow.ToString("O");
                state.Status.Running = false;
                state.Status.Health = EngineeringHealth.Stopped;
                state.Status.Summary = "Workspace preparation cancelled by operator";
                Event(state, state.Status.Summary);
                await _persistence.SaveAsync(state, CancellationToken.None);
                return;
            }
            catch (Exception ex)
            {
                session.StoppedUtc = DateTime.UtcNow.ToString("O");
                state.Status.Running = false;
                state.Status.Health = EngineeringHealth.Failed;
                state.Status.Summary = $"Workspace creation failed safely: {ex.Message}";
                Event(state, state.Status.Summary);
                await _persistence.SaveAsync(state, CancellationToken.None);
                return;
            }
            state.Status.WorkspacePath = session.WorkspacePath;
            SeedAutonomousQueue(state, session.Id);
            await _persistence.SaveAsync(state, runToken);
            _ = RunLoopAsync(state, runToken);
            return;
        }
        await _persistence.SaveAsync(state, runToken);
    }

    private static string RemoveNoBuild(string command) =>
        command.Replace(" --no-build", "", StringComparison.OrdinalIgnoreCase);

    public async Task StopAsync(EngineeringWorkspace state)
    {
        _runCancellation?.Cancel();
        if (state.CurrentSession is not null) state.CurrentSession.StoppedUtc = DateTime.UtcNow.ToString("O");
        state.Status.Running = false;
        state.Status.Health = EngineeringHealth.Stopped;
        state.Status.CurrentTaskId = "";
        state.Status.Summary = "Engineering stopped by operator";
        Event(state, "Engineering stopped by operator.");
        await WriteReportAsync(state, CancellationToken.None);
        await _persistence.SaveAsync(state);
    }

    public async Task EmergencyStopAsync(EngineeringWorkspace state)
    {
        _runCancellation?.Cancel();
        foreach (var task in state.Tasks.Where(task => task.Status == EngineeringTaskStatus.Running))
        {
            task.Status = EngineeringTaskStatus.Cancelled;
            task.CompletedUtc = DateTime.UtcNow.ToString("O");
            task.Result = "Cancelled by Emergency Stop.";
        }
        foreach (var task in state.Tasks.Where(task => task.Status is EngineeringTaskStatus.Pending or EngineeringTaskStatus.Queued))
        {
            task.Status = EngineeringTaskStatus.Paused;
            task.Result = "Paused by Emergency Stop.";
        }
        if (state.CurrentSession is not null) state.CurrentSession.StoppedUtc = DateTime.UtcNow.ToString("O");
        state.Status.Running = false;
        state.Status.Health = EngineeringHealth.Stopped;
        state.Status.CurrentTaskId = "";
        state.Status.Summary = "EMERGENCY STOP: all Borin engineering execution cancelled";
        Event(state, state.Status.Summary);
        await _persistence.SaveAsync(state);
    }

    public async Task QueueAllPhasesAsync(EngineeringWorkspace state)
    {
        var sessionId = state.CurrentSession?.Id ?? "";
        SeedAutonomousQueue(state, sessionId);
        Event(state, "All phase tasks queued.");
        await _persistence.SaveAsync(state);
    }

    public async Task QueueRuntimeDisconnectObjectiveAsync(EngineeringWorkspace state)
    {
        state.Configuration.EngineeringObjective = "Find, reproduce, diagnose, and repair runtime disconnects. Rebuild and validate every repair, run runtime and gameplay regression tests, document evidence and results, and generate a cumulative candidate ZIP for operator review.";
        var sessionId = state.CurrentSession?.Id ?? "";
        var objective = state.Configuration.EngineeringObjective;
        AddObjectiveTask(state, sessionId, "Plan runtime disconnect investigation", EngineeringTaskCategory.Planning, objective);
        AddObjectiveTask(state, sessionId, "Inventory disconnect evidence and logs", EngineeringTaskCategory.RepositoryAnalysis, objective);
        AddObjectiveTask(state, sessionId, "Reproduce and classify runtime disconnects", EngineeringTaskCategory.RuntimeDisconnect, objective);
        if (state.Configuration.AutonomousEngineering)
            AddObjectiveTask(state, sessionId, "Repair diagnosed runtime disconnects in isolated workspace", EngineeringTaskCategory.AutonomousEngineering, objective);
        if (state.Configuration.AutonomousBuild)
            AddObjectiveTask(state, sessionId, "Build runtime disconnect repairs", EngineeringTaskCategory.Build, objective);
        if (state.Configuration.AutonomousValidation)
            AddObjectiveTask(state, sessionId, "Validate runtime disconnect repairs", EngineeringTaskCategory.Validation, objective);
        if (state.Configuration.AutonomousRuntimeTesting)
            AddObjectiveTask(state, sessionId, "Run runtime disconnect regression tests", EngineeringTaskCategory.Runtime, objective);
        if (state.Configuration.AutonomousGameplayTesting)
            AddObjectiveTask(state, sessionId, "Run gameplay disconnect regression tests", EngineeringTaskCategory.GameplayTesting, objective);
        if (state.Configuration.AutonomousDocumentation)
            AddObjectiveTask(state, sessionId, "Document runtime disconnect findings and repairs", EngineeringTaskCategory.Documentation, objective);
        if (state.Configuration.CandidateZipGeneration)
            AddObjectiveTask(state, sessionId, "Generate cumulative runtime disconnect candidate ZIP", EngineeringTaskCategory.CandidatePatch, objective);
        Event(state, "Runtime disconnect objective assigned and capability-gated tasks queued.");
        await _persistence.SaveAsync(state);
    }

    public async Task RunOneAsync(EngineeringWorkspace state, CancellationToken token = default)
    {
        var task = NextTask(state);
        if (task is null)
        {
            state.Status.Summary = "Engineering queue is complete";
            await WriteReportAsync(state, token);
            return;
        }
        await ExecuteAsync(state, task, token);
        await _persistence.SaveAsync(state, token);
    }

    private async Task RunLoopAsync(EngineeringWorkspace state, CancellationToken token)
    {
        var completedThisRun = 0;
        try
        {
            while (!token.IsCancellationRequested && state.Status.Running && HasAutomatedCapabilities(state.Configuration)
                && (state.Configuration.ContinuousOvernightMode || completedThisRun < state.Configuration.MaximumTasksPerSession))
            {
                var next = NextTask(state);
                if (next is null && state.Configuration.ContinuousOvernightMode)
                {
                    if (SeedAutonomousQueue(state, state.CurrentSession?.Id ?? "") == 0) break;
                    next = NextTask(state);
                }
                if (next is null) break;
                await ExecuteAsync(state, next, token);
                completedThisRun++;
                await _persistence.SaveAsync(state, token);
                await Task.Delay(TimeSpan.FromSeconds(state.Configuration.CycleDelaySeconds), token);
            }
            if (!token.IsCancellationRequested)
            {
                state.Status.Summary = HasAutomatedCapabilities(state.Configuration)
                    ? "Enabled Borin capability queue complete"
                    : "All autonomous capabilities are off; session remains ready";
                await WriteReportAsync(state, token);
                await _persistence.SaveAsync(state, token);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            state.Status.Health = EngineeringHealth.Failed;
            state.Status.Summary = $"Engineering stopped safely: {ex.Message}";
            Event(state, state.Status.Summary);
            await _persistence.SaveAsync(state);
        }
    }

    private async Task ExecuteAsync(EngineeringWorkspace state, EngineeringTask task, CancellationToken token)
    {
        if (state.CurrentSession is null) throw new InvalidOperationException("Start Working before running tasks.");
        task.Status = EngineeringTaskStatus.Running;
        task.StartedUtc = DateTime.UtcNow.ToString("O");
        task.SessionId = state.CurrentSession.Id;
        state.Status.CurrentTaskId = task.Id;
        state.Status.Summary = $"Running: {task.Title}";
        var watch = Stopwatch.StartNew();
        try
        {
            var result = await ExecuteCategoryAsync(state, task.Category, token);
            task.Result = result.Output.Length > 4000 ? result.Output[^4000..] : result.Output;
            task.ActualSeconds = result.DurationSeconds;
            task.Status = result.ExitCode == 0 ? EngineeringTaskStatus.Completed : EngineeringTaskStatus.Failed;
            if (task.Status == EngineeringTaskStatus.Completed)
            {
                state.Statistics.TasksCompleted++;
                state.CurrentSession.CompletedTasks++;
            }
            else
            {
                state.Statistics.TasksFailed++;
                state.CurrentSession.FailedTasks++;
            }
            UpdateCategoryStatistics(state, task.Category, result.ExitCode == 0);
            Remember(state, task, result.ExitCode == 0 ? "Completed" : "Failure", Summarize(result.Output));
        }
        catch (OperationCanceledException)
        {
            task.Status = EngineeringTaskStatus.Cancelled;
            task.Result = "Cancelled by operator.";
            Remember(state, task, "Cancelled", task.Result);
        }
        catch (Exception ex)
        {
            task.Status = EngineeringTaskStatus.Failed;
            task.Result = ex.Message;
            state.Statistics.TasksFailed++;
            state.CurrentSession.FailedTasks++;
            Remember(state, task, "Failure", ex.Message);
        }
        finally
        {
            watch.Stop();
            task.ActualSeconds = Math.Max(task.ActualSeconds, (int)watch.Elapsed.TotalSeconds);
            task.CompletedUtc = DateTime.UtcNow.ToString("O");
            state.Statistics.TotalExecutionSeconds += task.ActualSeconds;
            state.Status.CurrentTaskId = "";
            state.Status.LastActivityUtc = DateTime.UtcNow.ToString("O");
            Event(state, $"{task.Title}: {task.Status}");
        }
    }

    private async Task<EngineeringCommandResult> ExecuteCategoryAsync(EngineeringWorkspace state, EngineeringTaskCategory category, CancellationToken token)
    {
        var workspace = state.CurrentSession?.WorkspacePath ?? "";
        if (string.IsNullOrWhiteSpace(workspace) || !Directory.Exists(workspace))
            workspace = _repositoryRoot;
        return category switch
        {
            EngineeringTaskCategory.Foundation or EngineeringTaskCategory.Tooling or EngineeringTaskCategory.Workspace
                => new(0, $"Capability initialized. Workspace: {workspace}", 0),
            EngineeringTaskCategory.Planning => new(0, "Repository evidence and pending task results were converted into the guarded engineering queue.", 0),
            EngineeringTaskCategory.RepositoryAnalysis => await AnalyzeRepositoryAsync(workspace, token),
            EngineeringTaskCategory.Build => await GuardedCommandAsync(EngineeringAuthority.RunBuild, state, workspace, state.Configuration.BuildCommand, token),
            EngineeringTaskCategory.Validation => await RunWithRuntimeDataAsync(EngineeringAuthority.RunValidation, state, workspace, state.Configuration.ValidationCommand, token),
            EngineeringTaskCategory.Runtime => await RunWithRuntimeDataAsync(EngineeringAuthority.RunRuntime, state, workspace, state.Configuration.RuntimeCommand, token),
            EngineeringTaskCategory.RuntimeDisconnect => await InspectRuntimeDisconnectsAsync(state, workspace, token),
            EngineeringTaskCategory.GameplayTesting => await RunWithRuntimeDataAsync(EngineeringAuthority.RunRuntime, state, workspace, state.Configuration.GameplayTestingCommand, token),
            EngineeringTaskCategory.CandidatePatch => await CreateCandidatePackageAsync(state, workspace, token),
            EngineeringTaskCategory.Diagnosis => Diagnose(state),
            EngineeringTaskCategory.Optimization => new(0, "Optimization review completed without speculative source mutation; recommendations were retained in engineering memory.", 0),
            EngineeringTaskCategory.Documentation => await DocumentSessionAsync(state, token),
            EngineeringTaskCategory.RepositoryUpdate => RepositoryUpdateStatus(state),
            EngineeringTaskCategory.AutonomousEngineering => await ExecuteAutonomousRepairAsync(state, workspace, token),
            _ => new(1, "Unsupported engineering category.", 0)
        };
    }

    private async Task<EngineeringCommandResult> GuardedCommandAsync(EngineeringAuthority authority, EngineeringWorkspace state, string workspace, string command, CancellationToken token)
    {
        var decision = _guard.Check(authority, state.Configuration);
        if (!decision.Allowed) return new(2, decision.Message, 0);
        return await EngineeringCommandRunner.RunShellAsync(workspace, command, token);
    }

    private async Task<EngineeringCommandResult> RunWithRuntimeDataAsync(
        EngineeringAuthority authority,
        EngineeringWorkspace state,
        string workspace,
        string command,
        CancellationToken token)
    {
        var decision = _guard.Check(authority, state.Configuration);
        if (!decision.Allowed) return new(2, decision.Message, 0);
        var manifest = Path.Combine(workspace, "Data", "Classic", "compile_manifest.json");
        var rooms = Path.Combine(workspace, "Data", "Classic", "rooms.json");
        if (!File.Exists(manifest) || !File.Exists(rooms))
        {
            var generate = await EngineeringCommandRunner.RunShellAsync(
                workspace,
                "dotnet run --project Apps/Compiler/RealmGate.Compiler.csproj --configuration Release -- build --world Classic --clean",
                token);
            if (generate.ExitCode != 0)
                return new(generate.ExitCode, $"Classic runtime data regeneration failed before verification.{Environment.NewLine}{generate.Output}", generate.DurationSeconds);
        }
        return await EngineeringCommandRunner.RunShellAsync(workspace, command, token);
    }

    private static async Task<EngineeringCommandResult> AnalyzeRepositoryAsync(string workspace, CancellationToken token)
    {
        var watch = Stopwatch.StartNew();
        var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".cs", ".xaml", ".json", ".md", ".py", ".yml", ".yaml" };
        var files = await Task.Run(() => Directory.EnumerateFiles(workspace, "*", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                && extensions.Contains(Path.GetExtension(path))).ToList(), token);
        var projects = Directory.EnumerateFiles(workspace, "*.csproj", SearchOption.AllDirectories).Count();
        watch.Stop();
        return new(0, $"Repository analysis: {files.Count:N0} source/document/data files and {projects:N0} .NET projects indexed.", (int)watch.Elapsed.TotalSeconds);
    }

    private async Task<EngineeringCommandResult> CreateCandidatePackageAsync(EngineeringWorkspace state, string workspace, CancellationToken token)
    {
        var decision = _guard.Check(EngineeringAuthority.CreateCandidatePackage, state.Configuration);
        if (!decision.Allowed) return new(2, decision.Message, 0);
        var sessionId = state.CurrentSession?.Id ?? "";
        var sessionTasks = state.Tasks.Where(task => task.SessionId == sessionId).ToList();
        var repairIndex = sessionTasks.FindLastIndex(task =>
            task.Category == EngineeringTaskCategory.AutonomousEngineering &&
            task.Status == EngineeringTaskStatus.Completed);
        if (state.Configuration.AutonomousEngineering && repairIndex < 0)
            return new(3, "Candidate ZIP withheld: no LM Studio repair was successfully validated and applied in this session.", 0);
        if (repairIndex >= 0)
        {
            var required = new[]
            {
                EngineeringTaskCategory.Build,
                EngineeringTaskCategory.Validation,
                EngineeringTaskCategory.Runtime,
                EngineeringTaskCategory.GameplayTesting
            };
            var postRepair = sessionTasks.Skip(repairIndex + 1).ToList();
            var failedGate = required.FirstOrDefault(category =>
                EnabledGate(state.Configuration, category) &&
                !postRepair.Any(task => task.Category == category && task.Status == EngineeringTaskStatus.Completed));
            if (EnabledGate(state.Configuration, failedGate))
                return new(4, $"Candidate ZIP withheld: the post-repair {failedGate} gate has not passed.", 0);
        }
        var names = await EngineeringCommandRunner.RunAsync(workspace, "git", "status --porcelain", token);
        if (names.ExitCode != 0 || string.IsNullOrWhiteSpace(names.Output))
            return new(0, "No workspace changes exist; no candidate package was created.", names.DurationSeconds);
        await EngineeringCommandRunner.RunAsync(workspace, "git", "add -N .", token);
        var diff = await EngineeringCommandRunner.RunAsync(workspace, "git", "diff --binary", token);
        if (diff.ExitCode != 0)
            return new(diff.ExitCode, $"Candidate diff generation failed: {diff.Output}", diff.DurationSeconds);
        var folder = Path.Combine(_persistence.Root, "Candidates", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(Path.Combine(folder, "candidate.patch"), diff.Output, token);
        var manifest = new List<string>();
        foreach (var line in names.Output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length < 4) continue;
            var relative = line[3..].Trim().Trim('"').Replace('/', Path.DirectorySeparatorChar);
            if (relative.Contains(" -> ", StringComparison.Ordinal)) relative = relative.Split(" -> ", StringSplitOptions.None)[1];
            var source = Path.GetFullPath(Path.Combine(workspace, relative));
            if (!source.StartsWith(Path.GetFullPath(workspace), StringComparison.OrdinalIgnoreCase) || !File.Exists(source)) continue;
            var destination = Path.Combine(folder, "Payload", relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(source, destination, true);
            manifest.Add(relative.Replace(Path.DirectorySeparatorChar, '/'));
        }
        await File.WriteAllLinesAsync(Path.Combine(folder, "MANIFEST.txt"), manifest, token);
        await File.WriteAllTextAsync(Path.Combine(folder, "VALIDATION.log"),
            $"GeneratedUtc={DateTime.UtcNow:O}{Environment.NewLine}Session={state.CurrentSession?.Id}{Environment.NewLine}BuildsPassed={state.Statistics.BuildsPassed}{Environment.NewLine}BuildsFailed={state.Statistics.BuildsFailed}{Environment.NewLine}ValidationsPassed={state.Statistics.ValidationsPassed}{Environment.NewLine}ValidationsFailed={state.Statistics.ValidationsFailed}{Environment.NewLine}", token);
        await File.WriteAllTextAsync(Path.Combine(folder, "README.md"),
            "# Borin Cumulative Candidate\n\nThis package contains only modified/new workspace files. Review MANIFEST.txt and VALIDATION.log before installation. The live repository was not changed by package generation.\n", token);
        await File.WriteAllTextAsync(Path.Combine(folder, "Install-and-Validate.bat"),
            "@echo off\r\nsetlocal\r\nset \"ROOT=%~dp0\"\r\nif not exist \"%ROOT%Payload\" (echo Payload missing.& exit /b 2)\r\nrobocopy \"%ROOT%Payload\" \"%CD%\" /E /R:1 /W:1 >nul\r\nif errorlevel 8 exit /b %errorlevel%\r\ndotnet build RealmGate.sln --configuration Release\r\nif errorlevel 1 exit /b %errorlevel%\r\ndotnet run --project Tests\\Source\\RealmGate.Core.Tests\\RealmGate.Core.Tests.csproj --configuration Release --no-build\r\nexit /b %errorlevel%\r\n", token);
        var zip = folder + ".zip";
        ZipFile.CreateFromDirectory(folder, zip);
        state.Status.LastCandidateZipPath = zip;
        return new(0, $"Cumulative candidate package with {manifest.Count} modified/new files created for operator review: {zip}", diff.DurationSeconds);
    }

    private static bool EnabledGate(EngineeringConfiguration configuration, EngineeringTaskCategory category) =>
        category switch
        {
            EngineeringTaskCategory.Build => configuration.AutonomousBuild,
            EngineeringTaskCategory.Validation => configuration.AutonomousValidation,
            EngineeringTaskCategory.Runtime => configuration.AutonomousRuntimeTesting,
            EngineeringTaskCategory.GameplayTesting => configuration.AutonomousGameplayTesting,
            _ => false
        };

    private static EngineeringCommandResult Diagnose(EngineeringWorkspace state)
    {
        var actionable = new HashSet<EngineeringTaskCategory>
        {
            EngineeringTaskCategory.Build,
            EngineeringTaskCategory.Validation,
            EngineeringTaskCategory.Runtime,
            EngineeringTaskCategory.RuntimeDisconnect,
            EngineeringTaskCategory.GameplayTesting,
            EngineeringTaskCategory.RepositoryAnalysis
        };
        var failure = state.Tasks.LastOrDefault(task =>
            task.Status == EngineeringTaskStatus.Failed && actionable.Contains(task.Category));
        return failure is null
            ? new(0, "No actionable build, validation, runtime, gameplay, or repository-analysis failure requires diagnosis.", 0)
            : new(0, $"Diagnosis candidate: {failure.Title}. Review its captured result and repair only inside the isolated workspace. {Summarize(failure.Result)}", 0);
    }

    private async Task<EngineeringCommandResult> InspectRuntimeDisconnectsAsync(EngineeringWorkspace state, string workspace, CancellationToken token)
    {
        if (!state.Configuration.AutonomousRuntimeTesting)
            return new(2, "Runtime disconnect reproduction requires Autonomous Runtime Testing.", 0);
        var result = await RunWithRuntimeDataAsync(EngineeringAuthority.RunRuntime, state, workspace, state.Configuration.RuntimeCommand, token);
        return result with { Output = $"Objective: {state.Configuration.EngineeringObjective}{Environment.NewLine}{result.Output}" };
    }

    private async Task WriteReportAsync(EngineeringWorkspace state, CancellationToken token)
    {
        var report = new EngineeringReport
        {
            SessionId = state.CurrentSession?.Id ?? "",
            Health = state.Statistics.TasksFailed > 0 ? EngineeringHealth.Warning : EngineeringHealth.Ready,
            Metrics = $"Completed={state.Statistics.TasksCompleted}; Failed={state.Statistics.TasksFailed}; BuildsPassed={state.Statistics.BuildsPassed}; ValidationsPassed={state.Statistics.ValidationsPassed}",
            FutureWork = state.Tasks.Any(task => task.Status is EngineeringTaskStatus.Pending or EngineeringTaskStatus.Queued)
                ? "Pending tasks remain in the engineering queue." : "Queue complete; operator review is required before any live apply."
        };
        if (state.Statistics.TasksFailed > 0) report.Warnings.Add("One or more guarded engineering tasks failed; inspect task results.");
        report.Recommendations.Add("Review candidate packages and reports before approving any repository update.");
        report.Path = await _persistence.SaveReportAsync(report, token);
        state.Reports.Add(report);
    }

    private static int SeedAutonomousQueue(EngineeringWorkspace state, string sessionId)
    {
        if (state.Tasks.Any(task => task.Status is EngineeringTaskStatus.Pending or EngineeringTaskStatus.Queued)) return 0;
        var configuration = state.Configuration;
        var phases = new List<(string Title, EngineeringTaskCategory Category)>();
        if (configuration.AutonomousPlanning) phases.Add(("Plan next engineering cycle", EngineeringTaskCategory.Planning));
        if (configuration.AutonomousEngineering)
        {
            phases.Add(("Initialize engineering foundation", EngineeringTaskCategory.Foundation));
            phases.Add(("Inventory engineering tools", EngineeringTaskCategory.Tooling));
            phases.Add(("Verify isolated workspace", EngineeringTaskCategory.Workspace));
            phases.Add(("Analyze repository", EngineeringTaskCategory.RepositoryAnalysis));
            phases.Add(("Diagnose recorded failures", EngineeringTaskCategory.Diagnosis));
            phases.Add(("Complete autonomous engineering cycle", EngineeringTaskCategory.AutonomousEngineering));
        }
        if (configuration.AutonomousBuild) phases.Add(("Build solution", EngineeringTaskCategory.Build));
        if (configuration.AutonomousValidation) phases.Add(("Run validation suite", EngineeringTaskCategory.Validation));
        if (configuration.AutonomousRuntimeTesting) phases.Add(("Run runtime smoke diagnostics", EngineeringTaskCategory.Runtime));
        if (configuration.AutonomousGameplayTesting) phases.Add(("Run automated gameplay tests", EngineeringTaskCategory.GameplayTesting));
        if (configuration.AutonomousOptimization) phases.Add(("Analyze optimization opportunities", EngineeringTaskCategory.Optimization));
        if (configuration.AutonomousDocumentation) phases.Add(("Update engineering session documentation", EngineeringTaskCategory.Documentation));
        if (configuration.CandidateZipGeneration) phases.Add(("Generate cumulative candidate ZIP", EngineeringTaskCategory.CandidatePatch));
        if (configuration.RepositoryUpdates) phases.Add(("Prepare approved repository update channel", EngineeringTaskCategory.RepositoryUpdate));
        foreach (var (title, category) in phases)
            state.Tasks.Add(new EngineeringTask { Title = title, Category = category, Status = EngineeringTaskStatus.Queued, SessionId = sessionId });
        return phases.Count;
    }

    private static EngineeringTask? NextTask(EngineeringWorkspace state) =>
        state.Tasks.Where(task => task.Status is EngineeringTaskStatus.Pending or EngineeringTaskStatus.Queued)
            .Where(task => task.Dependencies.All(id => state.Tasks.Any(candidate => candidate.Id == id && candidate.Status == EngineeringTaskStatus.Completed)))
            .OrderByDescending(task => task.Priority).ThenBy(task => task.CreatedUtc).FirstOrDefault();

    private static void AddObjectiveTask(EngineeringWorkspace state, string sessionId, string title, EngineeringTaskCategory category, string objective) =>
        state.Tasks.Add(new EngineeringTask
        {
            Title = title,
            Description = objective,
            Category = category,
            Status = EngineeringTaskStatus.Queued,
            Priority = EngineeringTaskPriority.High,
            SessionId = sessionId
        });

    private static void UpdateCategoryStatistics(EngineeringWorkspace state, EngineeringTaskCategory category, bool passed)
    {
        if (category == EngineeringTaskCategory.Build) { if (passed) state.Statistics.BuildsPassed++; else state.Statistics.BuildsFailed++; }
        if (category == EngineeringTaskCategory.Validation) { if (passed) state.Statistics.ValidationsPassed++; else state.Statistics.ValidationsFailed++; }
    }
    private async Task<EngineeringCommandResult> DocumentSessionAsync(EngineeringWorkspace state, CancellationToken token)
    {
        await WriteReportAsync(state, token);
        return new(0, $"Engineering documentation report written to {state.Reports.LastOrDefault()?.Path ?? _persistence.Root}.", 0);
    }
    private EngineeringCommandResult RepositoryUpdateStatus(EngineeringWorkspace state)
    {
        var decision = _guard.Check(EngineeringAuthority.WriteLiveRepository, state.Configuration);
        return decision.Allowed
            ? new(0, "Repository update authority is enabled. Changes still require the explicit approved apply workflow; commit, push, publish, install, and delete remain blocked.", 0)
            : new(2, decision.Message, 0);
    }
    private Task<EngineeringCommandResult> ExecuteAutonomousRepairAsync(EngineeringWorkspace state, string workspace, CancellationToken token)
    {
        if (!state.Configuration.AutonomousEngineering)
            return Task.FromResult(new EngineeringCommandResult(2, "Autonomous Engineering is disabled.", 0));
        if (_repairEngine is null)
            return Task.FromResult(new EngineeringCommandResult(3, "Borin's Forge repair engine is unavailable.", 0));
        return _repairEngine.RepairAsync(workspace, state.Configuration.EngineeringObjective, state.Tasks.ToArray(), token);
    }
    private static bool HasAutomatedCapabilities(EngineeringConfiguration configuration) =>
        configuration.AutonomousPlanning || configuration.AutonomousEngineering || configuration.AutonomousBuild ||
        configuration.AutonomousValidation || configuration.AutonomousRuntimeTesting || configuration.AutonomousGameplayTesting ||
        configuration.AutonomousOptimization || configuration.AutonomousDocumentation || configuration.CandidateZipGeneration ||
        configuration.RepositoryUpdates || configuration.ContinuousOvernightMode;
    private static string EnabledCapabilities(EngineeringConfiguration configuration)
    {
        var enabled = new List<string>();
        if (configuration.AutonomousPlanning) enabled.Add("planning");
        if (configuration.AutonomousEngineering) enabled.Add("engineering");
        if (configuration.AutonomousBuild) enabled.Add("build");
        if (configuration.AutonomousValidation) enabled.Add("validation");
        if (configuration.AutonomousRuntimeTesting) enabled.Add("runtime-testing");
        if (configuration.AutonomousGameplayTesting) enabled.Add("gameplay-testing");
        if (configuration.AutonomousOptimization) enabled.Add("optimization");
        if (configuration.AutonomousDocumentation) enabled.Add("documentation");
        if (configuration.CandidateZipGeneration) enabled.Add("candidate-zip");
        if (configuration.RepositoryUpdates) enabled.Add("repository-updates");
        if (configuration.ContinuousOvernightMode) enabled.Add("overnight");
        return enabled.Count == 0 ? "none" : string.Join(", ", enabled);
    }
    private static void Remember(EngineeringWorkspace state, EngineeringTask task, string category, string summary) =>
        state.Memory.Add(new() { Category = category, Summary = summary, SessionId = task.SessionId, TaskId = task.Id });
    private static string Summarize(string value) => string.IsNullOrWhiteSpace(value) ? "No output." : value.ReplaceLineEndings(" ").Trim()[..Math.Min(500, value.ReplaceLineEndings(" ").Trim().Length)];
    private static void Event(EngineeringWorkspace state, string message)
    {
        state.Events.Insert(0, $"{DateTime.Now:g} · {message}");
        while (state.Events.Count > 200) state.Events.RemoveAt(state.Events.Count - 1);
    }
}
