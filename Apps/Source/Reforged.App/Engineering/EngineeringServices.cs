using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace Reforged.App.Engineering;

public sealed class AuthorityGuard
{
    public (bool Allowed, string Message) Check(EngineeringAuthority authority, EngineeringConfiguration configuration)
    {
        var allowed = authority switch
        {
            EngineeringAuthority.ReadRepository => true,
            EngineeringAuthority.WriteWorkspace => configuration.CreateIsolatedWorkspace,
            EngineeringAuthority.RunBuild => configuration.RunBuilds,
            EngineeringAuthority.RunValidation => configuration.RunValidation,
            EngineeringAuthority.RunRuntime => configuration.RunRuntimeDiagnostics,
            EngineeringAuthority.CreateCandidatePackage => configuration.GenerateCandidatePackages,
            EngineeringAuthority.WriteLiveRepository => configuration.AllowLiveRepositoryEdits,
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
        var temporary = StatePath + ".tmp";
        await using (var stream = File.Create(temporary))
            await JsonSerializer.SerializeAsync(stream, state, JsonOptions, token);
        File.Move(temporary, StatePath, true);
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
        var parent = Path.Combine(Directory.GetParent(repositoryRoot)?.FullName ?? repositoryRoot, ".ReforgedEngineeringWorkspaces");
        Directory.CreateDirectory(parent);
        var workspace = Path.Combine(parent, sessionId);
        var result = await EngineeringCommandRunner.RunAsync(repositoryRoot, "git", $"worktree add --detach \"{workspace}\" HEAD", token);
        if (result.ExitCode != 0) throw new InvalidOperationException($"Workspace creation failed: {result.Output}");
        return workspace;
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
    private CancellationTokenSource? _runCancellation;
    public EngineeringOrchestrator(string repositoryRoot, EngineeringPersistence persistence)
    {
        _repositoryRoot = repositoryRoot;
        _persistence = persistence;
        _guard = new();
        _workspaces = new(_guard);
    }

    public async Task StartAsync(EngineeringWorkspace state, CancellationToken token = default)
    {
        if (state.Status.Running) return;
        var session = new EngineeringSession();
        state.CurrentSession = session;
        state.Statistics.Sessions++;
        state.Status.Running = true;
        state.Status.Health = EngineeringHealth.Working;
        state.Status.CurrentSessionId = session.Id;
        state.Status.Summary = state.Configuration.AutonomousEngineeringEnabled
            ? "Autonomous engineering is on; preparing isolated workspace"
            : "Engineering session ready; autonomy is off";
        Event(state, $"Session {session.Id} started; autonomy {(state.Configuration.AutonomousEngineeringEnabled ? "on" : "off")}.");
        if (state.Configuration.AutonomousEngineeringEnabled)
        {
            session.WorkspacePath = await _workspaces.CreateAsync(_repositoryRoot, state.Configuration, session.Id, token);
            state.Status.WorkspacePath = session.WorkspacePath;
            SeedAutonomousQueue(state, session.Id);
            _runCancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
            _ = RunLoopAsync(state, _runCancellation.Token);
        }
        await _persistence.SaveAsync(state, token);
    }

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

    public async Task QueueAllPhasesAsync(EngineeringWorkspace state)
    {
        var sessionId = state.CurrentSession?.Id ?? "";
        SeedAutonomousQueue(state, sessionId);
        Event(state, "All phase tasks queued.");
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
            while (!token.IsCancellationRequested && state.Status.Running && state.Configuration.AutonomousEngineeringEnabled
                && completedThisRun < state.Configuration.MaximumTasksPerSession)
            {
                var next = NextTask(state);
                if (next is null) break;
                await ExecuteAsync(state, next, token);
                completedThisRun++;
                await _persistence.SaveAsync(state, token);
                await Task.Delay(TimeSpan.FromSeconds(state.Configuration.CycleDelaySeconds), token);
            }
            if (!token.IsCancellationRequested)
            {
                state.Status.Summary = state.Configuration.AutonomousEngineeringEnabled
                    ? "Autonomous engineering queue complete"
                    : "Autonomous engineering turned off; session remains ready";
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
            EngineeringTaskCategory.RepositoryAnalysis => await AnalyzeRepositoryAsync(workspace, token),
            EngineeringTaskCategory.Build => await GuardedCommandAsync(EngineeringAuthority.RunBuild, state, workspace, state.Configuration.BuildCommand, token),
            EngineeringTaskCategory.Validation => await GuardedCommandAsync(EngineeringAuthority.RunValidation, state, workspace, state.Configuration.ValidationCommand, token),
            EngineeringTaskCategory.Runtime => await GuardedCommandAsync(EngineeringAuthority.RunRuntime, state, workspace, state.Configuration.RuntimeCommand, token),
            EngineeringTaskCategory.CandidatePatch => await CreateCandidatePackageAsync(state, workspace, token),
            EngineeringTaskCategory.Diagnosis => Diagnose(state),
            EngineeringTaskCategory.AutonomousEngineering => new(0, "Autonomous coordinator completed the guarded phase queue.", 0),
            _ => new(1, "Unsupported engineering category.", 0)
        };
    }

    private async Task<EngineeringCommandResult> GuardedCommandAsync(EngineeringAuthority authority, EngineeringWorkspace state, string workspace, string command, CancellationToken token)
    {
        var decision = _guard.Check(authority, state.Configuration);
        if (!decision.Allowed) return new(2, decision.Message, 0);
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
            "# Guardian Hall Cumulative Candidate\n\nThis package contains only modified/new workspace files. Review MANIFEST.txt and VALIDATION.log before installation. The live repository was not changed by package generation.\n", token);
        await File.WriteAllTextAsync(Path.Combine(folder, "Install-and-Validate.bat"),
            "@echo off\r\nsetlocal\r\nset \"ROOT=%~dp0\"\r\nif not exist \"%ROOT%Payload\" (echo Payload missing.& exit /b 2)\r\nrobocopy \"%ROOT%Payload\" \"%CD%\" /E /R:1 /W:1 >nul\r\nif errorlevel 8 exit /b %errorlevel%\r\ndotnet build MajorMud.Reforged.sln --configuration Release\r\nif errorlevel 1 exit /b %errorlevel%\r\ndotnet run --project Tests\\Source\\Reforged.Core.Tests\\Reforged.Core.Tests.csproj --configuration Release --no-build\r\nexit /b %errorlevel%\r\n", token);
        var zip = folder + ".zip";
        ZipFile.CreateFromDirectory(folder, zip);
        return new(0, $"Cumulative candidate package with {manifest.Count} modified/new files created for operator review: {zip}", diff.DurationSeconds);
    }

    private static EngineeringCommandResult Diagnose(EngineeringWorkspace state)
    {
        var failure = state.Tasks.LastOrDefault(task => task.Status == EngineeringTaskStatus.Failed);
        return failure is null
            ? new(0, "No failed engineering task requires diagnosis.", 0)
            : new(0, $"Diagnosis candidate: {failure.Title}. Review its captured result and repair only inside the isolated workspace. {Summarize(failure.Result)}", 0);
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

    private static void SeedAutonomousQueue(EngineeringWorkspace state, string sessionId)
    {
        if (state.Tasks.Any(task => task.Status is EngineeringTaskStatus.Pending or EngineeringTaskStatus.Queued)) return;
        var phases = new[]
        {
            ("Initialize engineering foundation", EngineeringTaskCategory.Foundation),
            ("Inventory engineering tools", EngineeringTaskCategory.Tooling),
            ("Verify isolated workspace", EngineeringTaskCategory.Workspace),
            ("Analyze repository", EngineeringTaskCategory.RepositoryAnalysis),
            ("Build solution", EngineeringTaskCategory.Build),
            ("Run validation suite", EngineeringTaskCategory.Validation),
            ("Run runtime smoke diagnostics", EngineeringTaskCategory.Runtime),
            ("Generate candidate package", EngineeringTaskCategory.CandidatePatch),
            ("Diagnose recorded failures", EngineeringTaskCategory.Diagnosis),
            ("Complete autonomous engineering cycle", EngineeringTaskCategory.AutonomousEngineering)
        };
        foreach (var (title, category) in phases)
            state.Tasks.Add(new EngineeringTask { Title = title, Category = category, Status = EngineeringTaskStatus.Queued, SessionId = sessionId });
    }

    private static EngineeringTask? NextTask(EngineeringWorkspace state) =>
        state.Tasks.Where(task => task.Status is EngineeringTaskStatus.Pending or EngineeringTaskStatus.Queued)
            .Where(task => task.Dependencies.All(id => state.Tasks.Any(candidate => candidate.Id == id && candidate.Status == EngineeringTaskStatus.Completed)))
            .OrderByDescending(task => task.Priority).ThenBy(task => task.CreatedUtc).FirstOrDefault();

    private static void UpdateCategoryStatistics(EngineeringWorkspace state, EngineeringTaskCategory category, bool passed)
    {
        if (category == EngineeringTaskCategory.Build) { if (passed) state.Statistics.BuildsPassed++; else state.Statistics.BuildsFailed++; }
        if (category == EngineeringTaskCategory.Validation) { if (passed) state.Statistics.ValidationsPassed++; else state.Statistics.ValidationsFailed++; }
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
