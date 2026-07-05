using Reforged.Server;

var bootstrap = new ServerBootstrap();
var startDirectory = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
var report = bootstrap.Run(startDirectory);

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
    foreach (var category in report.ServerDataCatalog.Categories)
    {
        Console.WriteLine($"ServerDataCategory: {category.Category}; status={category.Status}; runtimeReady={category.RuntimeReady}; placeholder={category.IsPlaceholder}; records={category.RecordCount}; file={category.RelativePath}; bytes={category.FileSizeBytes}; note={category.ReadinessNote}");
    }
}

if (report.WorldProjection is not null)
{
    Console.WriteLine($"WorldProjection: {report.WorldProjection.Status}; worldRooms={report.WorldProjection.WorldRoomCount}; uniqueWorldRooms={report.WorldProjection.UniqueWorldRoomCount}; canonicalRooms={report.WorldProjection.CanonicalRoomCount}; missingRooms={report.WorldProjection.MissingRoomCount}; duplicateWorldRooms={report.WorldProjection.DuplicateWorldRoomCount}; startRoom={report.WorldProjection.StartRoom ?? "<unknown>"}; startRoomExists={report.WorldProjection.StartRoomExists}");
}

if (report.GameModuleCatalog is not null)
{
    Console.WriteLine($"GameModules: {report.GameModuleCatalog.Status}; modules={report.GameModuleCatalog.ModuleCount}; gameplayEnabled={report.GameModuleCatalog.GameplayEnabledModuleCount}; gameplayDisabled={report.GameModuleCatalog.GameplayDisabledModuleCount}");
    foreach (var module in report.GameModuleCatalog.Modules)
    {
        Console.WriteLine($"GameModule: {module.Id}; name={module.Name}; role={module.Role}; dataRoot={module.DataRoot}; gameplayEnabled={module.GameplayEnabled}; ruleProfiles={module.RuleProfiles.Count}");
        foreach (var ruleProfile in module.RuleProfiles)
        {
            Console.WriteLine($"RuleProfile: {module.Id}/{ruleProfile.Id}; name={ruleProfile.Name}; status={ruleProfile.Status}; runtimeEnabled={ruleProfile.RuntimeEnabled}");
        }
    }
}

if (report.ModuleReadiness is not null)
{
    Console.WriteLine($"ModuleReadiness: {report.ModuleReadiness.Status}; modules={report.ModuleReadiness.ModuleCount}; hostReady={report.ModuleReadiness.HostReadyModuleCount}; gameplayEnabled={report.ModuleReadiness.GameplayEnabledModuleCount}; runtimeRuleProfilesEnabled={report.ModuleReadiness.RuntimeRuleProfileEnabledCount}");
    foreach (var module in report.ModuleReadiness.Modules)
    {
        Console.WriteLine($"ModuleReadinessItem: {module.ModuleId}; status={module.Status}; hostReady={module.HostReady}; dataRootExists={module.DataRootExists}; worldProjectionReady={module.WorldProjectionReady}; worldRooms={module.WorldRoomCount}; runtimeReadyCategories={module.RuntimeReadyCategoryCount}; runtimeBlockedCategories={module.RuntimeBlockedCategoryCount}; placeholders={module.EmptyPlaceholderCategoryCount}; gameplayEnabled={module.GameplayEnabled}; runtimeRuleProfilesEnabled={module.RuntimeRuleProfileEnabledCount}; note={module.ReadinessNote}");
    }
}

if (report.HostLifecycle is not null)
{
    Console.WriteLine($"HostLifecycle: {report.HostLifecycle.Status}; initialized={report.HostLifecycle.Initialized}; configured={report.HostLifecycle.Configured}; dataReady={report.HostLifecycle.DataReady}; worldProjectionReady={report.HostLifecycle.WorldProjectionReady}; moduleReady={report.HostLifecycle.ModuleReady}; runtimeDisabled={report.HostLifecycle.RuntimeDisabled}; gameplayEnabled={report.HostLifecycle.GameplayEnabled}; networkingEnabled={report.HostLifecycle.NetworkingEnabled}; worldSimulationEnabled={report.HostLifecycle.WorldSimulationEnabled}");
    foreach (var stage in report.HostLifecycle.Stages)
    {
        Console.WriteLine($"HostLifecycleStage: {stage.Name}; ready={stage.Ready}; status={stage.Status}; note={stage.Note}");
    }
}

if (report.SchedulerBootstrap is not null)
{
    Console.WriteLine($"SchedulerBootstrap: {report.SchedulerBootstrap.Status}; channels={report.SchedulerBootstrap.ChannelCount}; runtimeEnabled={report.SchedulerBootstrap.RuntimeEnabledChannelCount}; runtimeDisabled={report.SchedulerBootstrap.RuntimeDisabledChannelCount}; hostLifecycleReady={report.SchedulerBootstrap.HostLifecycleReady}; diagnosticOnly={report.SchedulerBootstrap.DiagnosticOnly}");
    foreach (var channel in report.SchedulerBootstrap.Channels)
    {
        Console.WriteLine($"SchedulerChannel: {channel.Order}; name={channel.Name}; runtimeEnabled={channel.RuntimeEnabled}; evidence={channel.EvidenceSource}; role={channel.Role}; note={channel.ReadinessNote}");
    }
}

if (report.SchedulerStatusCommands is not null)
{
    Console.WriteLine($"SchedulerStatusCommands: {report.SchedulerStatusCommands.Status}; commands={report.SchedulerStatusCommands.CommandCount}; runtimeEnabled={report.SchedulerStatusCommands.RuntimeEnabledCommandCount}; runtimeDisabled={report.SchedulerStatusCommands.RuntimeDisabledCommandCount}; guarded={report.SchedulerStatusCommands.GuardedCommandCount}; mutating={report.SchedulerStatusCommands.StateMutatingCommandCount}; schedulerReady={report.SchedulerStatusCommands.SchedulerBootstrapReady}; diagnosticOnly={report.SchedulerStatusCommands.DiagnosticOnly}");
    foreach (var command in report.SchedulerStatusCommands.Commands)
    {
        Console.WriteLine($"SchedulerStatusCommand: {command.Order}; name={command.Name}; category={command.Category}; runtimeEnabled={command.RuntimeEnabled}; guarded={command.Guarded}; mutatesState={command.MutatesState}; output={command.OutputContract}; note={command.ReadinessNote}");
    }
}

if (report.ClassicParityAudit is not null)
{
    Console.WriteLine($"ClassicParity: {report.ClassicParityAudit.Status}; canonicalFiles={report.ClassicParityAudit.CanonicalFileCount}; importedReferenceFiles={report.ClassicParityAudit.ImportedReferenceFileCount}; exactNameMatches={report.ClassicParityAudit.ExactNameMatchCount}");
    foreach (var category in report.ClassicParityAudit.Categories)
    {
        Console.WriteLine($"ClassicParityCategory: {category.Category}; canonical={category.CanonicalFileCount}; imported={category.ImportedReferenceFileCount}; matches={category.ExactNameMatchCount}; status={category.Status}");
    }
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

Console.WriteLine("Server bootstrap completed successfully. Gameplay, networking, and world simulation remain intentionally disabled.");
return report.ExitCode;
