using Reforged.Server;

var startDirectory = args.FirstOrDefault(arg => !arg.StartsWith("--", StringComparison.Ordinal));
var report = new ServerBootstrap().Run(startDirectory ?? Directory.GetCurrentDirectory());

if (args.Any(arg => string.Equals(arg, "--headless-json", StringComparison.OrdinalIgnoreCase)))
{
    if (!report.Passed || report.ProjectRoot is null)
    {
        Console.Error.WriteLine("Headless server bootstrap failed before gameplay startup.");
        foreach (var issue in report.Validation.Issues)
            Console.Error.WriteLine($"{issue.Severity}: {issue.Code}: {issue.Message}");
        return report.ExitCode == 0 ? 2 : report.ExitCode;
    }

    var options = ServerRuntimeOptions.Load(report.ProjectRoot);
    var host = new ServerRuntimeHost(report.ProjectRoot, options);
    host.Bootstrap();
    var playerName = TryReadArgumentValue(args, "--player", out var requestedPlayer) ? requestedPlayer : "Borin";
    await host.RunHeadlessJsonAsync(playerName, CancellationToken.None).ConfigureAwait(false);
    return 0;
}

Console.WriteLine("Reforged Server Bootstrap");
Console.WriteLine($"ProjectRoot: {report.ProjectRoot ?? "<not found>"}");
Console.WriteLine($"Project: {report.ProjectName ?? "<unknown>"}");
Console.WriteLine($"CodeUpdate: {report.CodeUpdate ?? "<unknown>"}");
Console.WriteLine($"DatabaseVersion: {report.DatabaseVersion ?? "<unknown>"}");
Console.WriteLine($"Milestone: {report.Milestone ?? "<unknown>"}");
Console.WriteLine($"Checks: {string.Join(", ", report.Checks)}");
Console.WriteLine($"Readiness: {report.Readiness.Status}; gameplay={report.Readiness.GameplayStatus}");
Console.WriteLine($"RequiredDirectories: {report.PresentRequiredDirectoryCount}/{report.RequiredDirectoryCount} present, {report.MissingRequiredDirectoryCount} missing");
Console.WriteLine($"Validation: {report.IssueCount} issue(s), {report.InfoCount} info, {report.WarningCount} warning(s), {report.ErrorCount} error(s), {report.FatalCount} fatal");

foreach (var directory in report.RequiredDirectories)
    Console.WriteLine($"Directory: {(directory.Exists ? "present" : "missing")} {directory.Name} -> {directory.Path}");

if (report.HostConfiguration is not null)
    Console.WriteLine($"HostConfiguration: {report.HostConfiguration.Status}; name={report.HostConfiguration.HostName}; bind={report.HostConfiguration.BindAddress}; port={report.HostConfiguration.Port}; maxSessions={report.HostConfiguration.MaxSessions}; worldTickMs={report.HostConfiguration.WorldTickMilliseconds}; combatTickMs={report.HostConfiguration.CombatTickMilliseconds}; gameplayEnabled={report.HostConfiguration.GameplayEnabled}; networkingEnabled={report.HostConfiguration.NetworkingEnabled}; worldSimulationEnabled={report.HostConfiguration.WorldSimulationEnabled}; configFileExists={report.HostConfiguration.ConfigurationFileExists}");
if (report.ServerDataCatalog is not null)
    Console.WriteLine($"ServerDataCatalog: {report.ServerDataCatalog.Status}; categories={report.ServerDataCatalog.PresentCategoryCount}/{report.ServerDataCatalog.CategoryCount}; runtimeReady={report.ServerDataCatalog.RuntimeReadyCategoryCount}; runtimeBlocked={report.ServerDataCatalog.RuntimeBlockedCategoryCount}; placeholders={report.ServerDataCatalog.EmptyPlaceholderCategoryCount}; records={report.ServerDataCatalog.LoadedRecordCount}");
if (report.WorldProjection is not null)
    Console.WriteLine($"WorldProjection: {report.WorldProjection.Status}; worldRooms={report.WorldProjection.WorldRoomCount}; uniqueWorldRooms={report.WorldProjection.UniqueWorldRoomCount}; canonicalRooms={report.WorldProjection.CanonicalRoomCount}; missingRooms={report.WorldProjection.MissingRoomCount}; duplicateWorldRooms={report.WorldProjection.DuplicateWorldRoomCount}; startRoom={report.WorldProjection.StartRoom ?? "<unknown>"}; startRoomExists={report.WorldProjection.StartRoomExists}");
if (report.GameModuleCatalog is not null)
    Console.WriteLine($"GameModules: {report.GameModuleCatalog.Status}; modules={report.GameModuleCatalog.ModuleCount}; gameplayEnabled={report.GameModuleCatalog.GameplayEnabledModuleCount}; gameplayDisabled={report.GameModuleCatalog.GameplayDisabledModuleCount}");
if (report.ModuleReadiness is not null)
    Console.WriteLine($"ModuleReadiness: {report.ModuleReadiness.Status}; modules={report.ModuleReadiness.ModuleCount}; hostReady={report.ModuleReadiness.HostReadyModuleCount}; gameplayEnabled={report.ModuleReadiness.GameplayEnabledModuleCount}; runtimeRuleProfilesEnabled={report.ModuleReadiness.RuntimeRuleProfileEnabledCount}");
if (report.HostLifecycle is not null)
    Console.WriteLine($"HostLifecycle: {report.HostLifecycle.Status}; initialized={report.HostLifecycle.Initialized}; configured={report.HostLifecycle.Configured}; dataReady={report.HostLifecycle.DataReady}; worldProjectionReady={report.HostLifecycle.WorldProjectionReady}; moduleReady={report.HostLifecycle.ModuleReady}; runtimeDisabled={report.HostLifecycle.RuntimeDisabled}; gameplayEnabled={report.HostLifecycle.GameplayEnabled}; networkingEnabled={report.HostLifecycle.NetworkingEnabled}; worldSimulationEnabled={report.HostLifecycle.WorldSimulationEnabled}");
if (report.SchedulerBootstrap is not null)
    Console.WriteLine($"SchedulerBootstrap: {report.SchedulerBootstrap.Status}; channels={report.SchedulerBootstrap.ChannelCount}; runtimeEnabled={report.SchedulerBootstrap.RuntimeEnabledChannelCount}; runtimeDisabled={report.SchedulerBootstrap.RuntimeDisabledChannelCount}; hostLifecycleReady={report.SchedulerBootstrap.HostLifecycleReady}; diagnosticOnly={report.SchedulerBootstrap.DiagnosticOnly}");
if (report.SchedulerStatusCommands is not null)
    Console.WriteLine($"SchedulerStatusCommands: {report.SchedulerStatusCommands.Status}; commands={report.SchedulerStatusCommands.CommandCount}; runtimeEnabled={report.SchedulerStatusCommands.RuntimeEnabledCommandCount}; runtimeDisabled={report.SchedulerStatusCommands.RuntimeDisabledCommandCount}; guarded={report.SchedulerStatusCommands.GuardedCommandCount}; mutating={report.SchedulerStatusCommands.StateMutatingCommandCount}; schedulerReady={report.SchedulerStatusCommands.SchedulerBootstrapReady}; diagnosticOnly={report.SchedulerStatusCommands.DiagnosticOnly}");
if (report.ClassicParityAudit is not null)
    Console.WriteLine($"ClassicData: {report.ClassicParityAudit.Status}; canonicalFiles={report.ClassicParityAudit.CanonicalFileCount}");

foreach (var issue in report.Validation.Issues)
    Console.Error.WriteLine($"{issue.Severity}: {issue.Code}: {issue.Message}");

if (!report.Passed)
{
    Console.Error.WriteLine("Server bootstrap failed before gameplay startup.");
    return report.ExitCode;
}

var projectRoot = report.ProjectRoot!;
if (args.Any(arg => string.Equals(arg, "--run-local", StringComparison.OrdinalIgnoreCase)))
{
    var options = ServerRuntimeOptions.Load(projectRoot);
    var host = new ServerRuntimeHost(projectRoot, options);
    host.Bootstrap();
    Console.WriteLine(RenderStartupAudit(projectRoot, host, options));
    await host.RunLocalConsoleAsync(CancellationToken.None).ConfigureAwait(false);
    return 0;
}

if (args.Any(arg => string.Equals(arg, "--run-network", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(arg, "--run-telnet", StringComparison.OrdinalIgnoreCase)))
{
    var options = ServerRuntimeOptions.Load(projectRoot);
    var host = new ServerRuntimeHost(projectRoot, options);
    host.Bootstrap();
    Console.WriteLine(RenderStartupAudit(projectRoot, host, options));
    await host.RunNetworkAsync(CancellationToken.None).ConfigureAwait(false);
    return 0;
}

Console.WriteLine("Server bootstrap completed successfully. Host runtime is enabled; pass --run-local for console or --run-network for telnet hosting.");
return report.ExitCode;

static string RenderStartupAudit(string projectRoot, ServerRuntimeHost host, ServerRuntimeOptions options)
{
    var world = host.Worlds.TryGetValue(options.DefaultWorldId, out var configured)
        ? configured
        : host.Worlds.Values.First();
    var auditRoot = Path.Combine(Path.GetTempPath(), "reforged-classic-startup-audit-" + Guid.NewGuid().ToString("N"));
    var living = new RuntimeLivingWorldService(projectRoot, world, auditRoot);
    return ClassicRuntimeAudit.RenderRuntimeAudit(projectRoot, world, living);
}

static bool TryReadArgumentValue(string[] values, string name, out string value)
{
    value = string.Empty;
    for (var index = 0; index < values.Length; index++)
    {
        var current = values[index];
        if (current.StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
        {
            value = current[(name.Length + 1)..].Trim().Trim('"');
            return !string.IsNullOrWhiteSpace(value);
        }
        if (current.Equals(name, StringComparison.OrdinalIgnoreCase) && index + 1 < values.Length)
        {
            value = values[index + 1].Trim().Trim('"');
            return !string.IsNullOrWhiteSpace(value);
        }
    }
    return false;
}
