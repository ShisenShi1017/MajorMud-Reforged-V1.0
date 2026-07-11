using Reforged.Server;

var startDirectory = args.FirstOrDefault(arg => !arg.StartsWith("--", StringComparison.Ordinal));
var bootstrap = new ServerBootstrap();
var report = bootstrap.Run(startDirectory ?? Directory.GetCurrentDirectory());

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
{
    Console.WriteLine($"Directory: {(directory.Exists ? "present" : "missing")} {directory.Name} -> {directory.Path}");
}

if (report.HostConfiguration is not null)
{
    Console.WriteLine($"HostConfiguration: {report.HostConfiguration.Status}; name={report.HostConfiguration.HostName}; bind={report.HostConfiguration.BindAddress}; port={report.HostConfiguration.Port}; maxSessions={report.HostConfiguration.MaxSessions}; worldTickMs={report.HostConfiguration.WorldTickMilliseconds}; combatTickMs={report.HostConfiguration.CombatTickMilliseconds}; gameplayEnabled={report.HostConfiguration.GameplayEnabled}; networkingEnabled={report.HostConfiguration.NetworkingEnabled}; worldSimulationEnabled={report.HostConfiguration.WorldSimulationEnabled}; configFileExists={report.HostConfiguration.ConfigurationFileExists}");
}

if (report.ServerDataCatalog is not null)
{
    Console.WriteLine($"ServerDataCatalog: {report.ServerDataCatalog.Status}; categories={report.ServerDataCatalog.PresentCategoryCount}/{report.ServerDataCatalog.CategoryCount}; runtimeReady={report.ServerDataCatalog.RuntimeReadyCategoryCount}; runtimeBlocked={report.ServerDataCatalog.RuntimeBlockedCategoryCount}; placeholders={report.ServerDataCatalog.EmptyPlaceholderCategoryCount}; records={report.ServerDataCatalog.LoadedRecordCount}");
}

if (report.WorldProjection is not null)
{
    Console.WriteLine($"WorldProjection: {report.WorldProjection.Status}; worldRooms={report.WorldProjection.WorldRoomCount}; uniqueWorldRooms={report.WorldProjection.UniqueWorldRoomCount}; canonicalRooms={report.WorldProjection.CanonicalRoomCount}; missingRooms={report.WorldProjection.MissingRoomCount}; duplicateWorldRooms={report.WorldProjection.DuplicateWorldRoomCount}; startRoom={report.WorldProjection.StartRoom ?? "<unknown>"}; startRoomExists={report.WorldProjection.StartRoomExists}");
}

if (report.GameModuleCatalog is not null)
{
    Console.WriteLine($"GameModules: {report.GameModuleCatalog.Status}; modules={report.GameModuleCatalog.ModuleCount}; gameplayEnabled={report.GameModuleCatalog.GameplayEnabledModuleCount}; gameplayDisabled={report.GameModuleCatalog.GameplayDisabledModuleCount}");
}

if (report.ModuleReadiness is not null)
{
    Console.WriteLine($"ModuleReadiness: {report.ModuleReadiness.Status}; modules={report.ModuleReadiness.ModuleCount}; hostReady={report.ModuleReadiness.HostReadyModuleCount}; gameplayEnabled={report.ModuleReadiness.GameplayEnabledModuleCount}; runtimeRuleProfilesEnabled={report.ModuleReadiness.RuntimeRuleProfileEnabledCount}");
}

if (report.HostLifecycle is not null)
{
    Console.WriteLine($"HostLifecycle: {report.HostLifecycle.Status}; initialized={report.HostLifecycle.Initialized}; configured={report.HostLifecycle.Configured}; dataReady={report.HostLifecycle.DataReady}; worldProjectionReady={report.HostLifecycle.WorldProjectionReady}; moduleReady={report.HostLifecycle.ModuleReady}; runtimeDisabled={report.HostLifecycle.RuntimeDisabled}; gameplayEnabled={report.HostLifecycle.GameplayEnabled}; networkingEnabled={report.HostLifecycle.NetworkingEnabled}; worldSimulationEnabled={report.HostLifecycle.WorldSimulationEnabled}");
}

if (report.SchedulerBootstrap is not null)
{
    Console.WriteLine($"SchedulerBootstrap: {report.SchedulerBootstrap.Status}; channels={report.SchedulerBootstrap.ChannelCount}; runtimeEnabled={report.SchedulerBootstrap.RuntimeEnabledChannelCount}; runtimeDisabled={report.SchedulerBootstrap.RuntimeDisabledChannelCount}; hostLifecycleReady={report.SchedulerBootstrap.HostLifecycleReady}; diagnosticOnly={report.SchedulerBootstrap.DiagnosticOnly}");
}

if (report.SchedulerStatusCommands is not null)
{
    Console.WriteLine($"SchedulerStatusCommands: {report.SchedulerStatusCommands.Status}; commands={report.SchedulerStatusCommands.CommandCount}; runtimeEnabled={report.SchedulerStatusCommands.RuntimeEnabledCommandCount}; runtimeDisabled={report.SchedulerStatusCommands.RuntimeDisabledCommandCount}; guarded={report.SchedulerStatusCommands.GuardedCommandCount}; mutating={report.SchedulerStatusCommands.StateMutatingCommandCount}; schedulerReady={report.SchedulerStatusCommands.SchedulerBootstrapReady}; diagnosticOnly={report.SchedulerStatusCommands.DiagnosticOnly}");
}

if (report.ClassicParityAudit is not null)
{
    Console.WriteLine($"ClassicParity: {report.ClassicParityAudit.Status}; canonicalFiles={report.ClassicParityAudit.CanonicalFileCount}; importedReferenceFiles={report.ClassicParityAudit.ImportedReferenceFileCount}; exactNameMatches={report.ClassicParityAudit.ExactNameMatchCount}");
}

foreach (var issue in report.Validation.Issues)
{
    Console.Error.WriteLine($"{issue.Severity}: {issue.Code}: {issue.Message}");
}

if (!report.Passed)
{
    Console.Error.WriteLine("Server bootstrap failed before gameplay startup.");
    return report.ExitCode;
}

if (args.Any(arg => string.Equals(arg, "--smoke-combat", StringComparison.OrdinalIgnoreCase)))
{
    var combatSmoke = ClassicCombatSmokeReport.Run();
    Console.WriteLine("Classic Combat Smoke Report");
    Console.WriteLine(combatSmoke.ToJson());
    return combatSmoke.Passed ? 0 : 6;
}

if (args.Any(arg => string.Equals(arg, "--smoke-npcs", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null)
    {
        Console.Error.WriteLine("Cannot validate NPC loading because project root was not resolved.");
        return 2;
    }

    var options = ServerRuntimeOptions.Load(report.ProjectRoot);
    var host = new ServerRuntimeHost(report.ProjectRoot, options);
    host.Bootstrap();
    var npcSmoke = host.CreateNpcSmokeReport();
    Console.WriteLine("Runtime NPC Smoke Report");
    Console.WriteLine(npcSmoke.ToJson());
    return npcSmoke.Passed ? 0 : 5;
}

if (args.Any(arg => string.Equals(arg, "--smoke-character-foundation", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null)
    {
        Console.Error.WriteLine("Cannot validate character foundation because project root was not resolved.");
        return 2;
    }

    var options = ServerRuntimeOptions.Load(report.ProjectRoot);
    var host = new ServerRuntimeHost(report.ProjectRoot, options);
    host.Bootstrap();
    var world = host.Worlds.TryGetValue(options.DefaultWorldId, out var selectedWorld)
        ? selectedWorld
        : host.Worlds.Values.First();
    var characterSmoke = RuntimeCharacterFoundationSmoke.Run(report.ProjectRoot, world);
    Console.WriteLine("Runtime Character Foundation Smoke Report");
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(characterSmoke, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    return characterSmoke.Passed ? 0 : 7;
}

if (args.Any(arg => string.Equals(arg, "--smoke-room-exits", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null)
    {
        Console.Error.WriteLine("Cannot validate room exits because project root was not resolved.");
        return 2;
    }

    var options = ServerRuntimeOptions.Load(report.ProjectRoot);
    var host = new ServerRuntimeHost(report.ProjectRoot, options);
    host.Bootstrap();
    var roomExitSmoke = host.CreateRoomExitSmokeReport();
    Console.WriteLine("Runtime Room/Exit Smoke Report");
    Console.WriteLine(roomExitSmoke.ToJson());
    return roomExitSmoke.Passed ? 0 : 4;
}

if (args.Any(arg => string.Equals(arg, "--smoke-generated-data", StringComparison.OrdinalIgnoreCase) || string.Equals(arg, "--validate-generated-data", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null)
    {
        Console.Error.WriteLine("Cannot validate generated data because project root was not resolved.");
        return 2;
    }

    var options = ServerRuntimeOptions.Load(report.ProjectRoot);
    var host = new ServerRuntimeHost(report.ProjectRoot, options);
    host.Bootstrap();
    var smoke = host.CreateSmokeReport();
    Console.WriteLine("Generated World Smoke Report");
    Console.WriteLine(smoke.ToJson());
    return smoke.Passed ? 0 : 3;
}

if (args.Any(arg => string.Equals(arg, "--run-local", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null)
    {
        Console.Error.WriteLine("Cannot start runtime because project root was not resolved.");
        return 2;
    }

    var options = ServerRuntimeOptions.Load(report.ProjectRoot);
    var host = new ServerRuntimeHost(report.ProjectRoot, options);
    host.Bootstrap();
    await host.RunLocalConsoleAsync(CancellationToken.None).ConfigureAwait(false);
    return 0;
}

if (args.Any(arg => string.Equals(arg, "--run-network", StringComparison.OrdinalIgnoreCase) || string.Equals(arg, "--run-telnet", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null)
    {
        Console.Error.WriteLine("Cannot start runtime because project root was not resolved.");
        return 2;
    }

    var options = ServerRuntimeOptions.Load(report.ProjectRoot);
    var host = new ServerRuntimeHost(report.ProjectRoot, options);
    host.Bootstrap();
    await host.RunNetworkAsync(CancellationToken.None).ConfigureAwait(false);
    return 0;
}

Console.WriteLine("Server bootstrap completed successfully. Runtime remains opt-in; pass --run-local for console or --run-network for the Milestone 1 telnet host.");
return report.ExitCode;
