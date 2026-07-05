using Reforged.Core.Configuration;
using Reforged.Core.Diagnostics;
using Reforged.Core.Events;
using Reforged.Core.Health;
using Reforged.Core.Logging;
using Reforged.Core.Services;
using Reforged.Server;

var root = LocateProjectRoot(AppContext.BaseDirectory);
var tests = new List<(string Name, Action Body)>
{
    ("dependency-injection resolves required services", DependencyInjectionSmoke),
    ("configuration loads defaults and flattened json", ConfigurationSmoke),
    ("event system publishes and disposes", EventSystemSmoke),
    ("repository health checks run", RepositoryHealthSmoke),
    ("startup diagnostics run", StartupDiagnosticsSmoke),
    ("server startup diagnostics run", ServerStartupDiagnosticsSmoke),
    ("server bootstrap runs", ServerBootstrapSmoke),
    ("server host configuration keeps runtime disabled", ServerHostConfigurationAuditSmoke),
    ("server data catalog loads canonical data", ServerDataCatalogAuditSmoke),
    ("server world projection indexes canonical rooms", ServerWorldProjectionAuditSmoke),
    ("server module readiness keeps gameplay disabled", ServerModuleReadinessAuditSmoke),
    ("server host lifecycle reports runtime-disabled readiness", ServerHostLifecycleAuditSmoke),
    ("server scheduler bootstrap keeps channels disabled", ServerSchedulerBootstrapAuditSmoke),
    ("server scheduler status commands stay guarded", ServerSchedulerStatusCommandAuditSmoke),
    ("classic parity audit inventories canonical and imported data", ClassicParityAuditSmoke)
};

var failures = new List<string>();
foreach (var test in tests)
{
    try
    {
        test.Body();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception ex)
    {
        failures.Add($"FAIL {test.Name}: {ex.Message}");
        Console.Error.WriteLine(failures[^1]);
    }
}

if (failures.Count > 0)
{
    Console.Error.WriteLine($"{failures.Count} smoke test(s) failed.");
    return 1;
}

Console.WriteLine("All Reforged core/server smoke tests passed.");
return 0;

void DependencyInjectionSmoke()
{
    var registry = new ServiceRegistry();
    var logger = new InMemoryReforgedLogger();
    registry.Register<IReforgedLogger>(logger);
    if (!registry.TryResolve<IReforgedLogger>(out var resolved) || !ReferenceEquals(logger, resolved))
    {
        throw new InvalidOperationException("Registered logger was not resolved.");
    }
}

void ConfigurationSmoke()
{
    var temp = Path.Combine(Path.GetTempPath(), $"reforged-config-{Guid.NewGuid():N}.json");
    File.WriteAllText(temp, "{\"server\":{\"port\":4000},\"enabled\":true}");
    try
    {
        var configuration = new RuntimeConfigurationProvider()
            .AddDefault("server:host", "localhost")
            .LoadJsonFile(temp, optional: false);

        if (configuration.GetString("server:host") != "localhost" || configuration.GetInt32("server:port") != 4000 || !configuration.GetBoolean("enabled"))
        {
            throw new InvalidOperationException("Configuration values did not load correctly.");
        }
    }
    finally
    {
        File.Delete(temp);
    }
}

void EventSystemSmoke()
{
    var bus = new InMemoryEventBus();
    var received = 0;
    var subscription = bus.Subscribe<EventPublishedEvent>(_ => received++);
    bus.Publish(new EventPublishedEvent("test", "detail", DateTimeOffset.UtcNow));
    subscription.Dispose();
    bus.Publish(new EventPublishedEvent("test", "after-dispose", DateTimeOffset.UtcNow));

    if (received != 1 || bus.SubscriberCount != 0)
    {
        throw new InvalidOperationException("Event publish or subscription disposal failed.");
    }
}

void RepositoryHealthSmoke()
{
    var result = new CoreDataRepositoryHealthCheck().ValidateDataFolder(root);
    if (result.HasErrors)
    {
        throw new InvalidOperationException(string.Join(" | ", result.Issues.Where(i => i.Severity.ToString() is "Error" or "Fatal").Select(i => $"{i.Code}: {i.Message}")));
    }
}

void StartupDiagnosticsSmoke()
{
    var report = new CoreStartupDiagnostics().Run(root);
    if (!report.Passed)
    {
        throw new InvalidOperationException(string.Join(" | ", report.Validation.Issues.Where(i => i.Severity.ToString() is "Error" or "Fatal").Select(i => $"{i.Code}: {i.Message}")));
    }

    foreach (var required in new[] { "dependency-injection", "configuration", "event-system", "repository-health" })
    {
        if (!report.Checks.Contains(required))
        {
            throw new InvalidOperationException($"Startup diagnostics did not run required check: {required}");
        }
    }
}

void ServerStartupDiagnosticsSmoke()
{
    var report = new ServerStartupDiagnostics().Run(root);
    if (!report.Passed)
    {
        throw new InvalidOperationException(string.Join(" | ", report.Validation.Issues.Where(i => i.Severity.ToString() is "Error" or "Fatal").Select(i => $"{i.Code}: {i.Message}")));
    }

    foreach (var required in new[] { "server-identity", "server-service-registry", "server-configuration", "server-event-system", "core-startup-diagnostics" })
    {
        if (!report.Checks.Contains(required))
        {
            throw new InvalidOperationException($"Server startup diagnostics did not run required check: {required}");
        }
    }

    if (report.CoreReport is null || !report.CoreReport.Checks.Contains("repository-health"))
    {
        throw new InvalidOperationException("Server startup diagnostics did not run Core repository health diagnostics.");
    }
}

void ServerBootstrapSmoke()
{
    var report = new ServerBootstrap().Run(root);
    if (!report.Passed)
    {
        throw new InvalidOperationException(string.Join(" | ", report.Validation.Issues.Where(i => i.Severity.ToString() is "Error" or "Fatal").Select(i => $"{i.Code}: {i.Message}")));
    }

    foreach (var required in new[] { "project-root", "required-directories", "project-manifest", "host-configuration", "server-data-catalog", "world-projection", "game-module-catalog", "module-readiness", "host-lifecycle", "scheduler-bootstrap", "scheduler-status-commands", "startup-diagnostics", "classic-parity-audit" })
    {
        if (!report.Checks.Contains(required))
        {
            throw new InvalidOperationException($"Server bootstrap did not run required check: {required}");
        }
    }

    if (report.StartupDiagnostics is null || !report.StartupDiagnostics.Checks.Contains("server-identity"))
    {
        throw new InvalidOperationException("Server bootstrap did not attach Server startup diagnostics.");
    }

    if (report.HostConfiguration is null ||
        report.HostConfiguration.Status != "configured-runtime-disabled" ||
        !report.HostConfiguration.RuntimeDisabled ||
        report.HostConfiguration.Port != 4000 ||
        report.HostConfiguration.MaxSessions != 64 ||
        report.HostConfiguration.WorldTickMilliseconds != 1000 ||
        report.HostConfiguration.CombatTickMilliseconds != 6000)
    {
        throw new InvalidOperationException("Server bootstrap did not attach expected non-gameplay host configuration.");
    }

    if (report.ServerDataCatalog is null ||
        report.ServerDataCatalog.CategoryCount < 10 ||
        report.ServerDataCatalog.MissingCategoryCount != 0 ||
        report.ServerDataCatalog.LoadedRecordCount == 0 ||
        report.ServerDataCatalog.RuntimeBlockedCategoryCount == 0 ||
        report.ServerDataCatalog.EmptyPlaceholderCategoryCount == 0)
    {
        throw new InvalidOperationException("Server bootstrap did not attach expected Server data catalog inventory.");
    }

    if (report.ClassicParityAudit is null || report.ClassicParityAudit.Categories.Count == 0)
    {
        throw new InvalidOperationException("Server bootstrap did not attach Classic parity audit inventory.");
    }

    if (report.WorldProjection is null ||
        !report.WorldProjection.ProjectionReady ||
        report.WorldProjection.WorldRoomCount == 0 ||
        report.WorldProjection.WorldRoomCount != report.WorldProjection.CanonicalRoomCount ||
        report.WorldProjection.MissingRoomCount != 0 ||
        report.WorldProjection.DuplicateWorldRoomCount != 0 ||
        !report.WorldProjection.StartRoomExists)
    {
        throw new InvalidOperationException("Server bootstrap did not attach the expected read-only world projection.");
    }

    if (report.GameModuleCatalog is null ||
        report.GameModuleCatalog.ModuleCount != 1 ||
        report.GameModuleCatalog.GameplayEnabledModuleCount != 0 ||
        report.GameModuleCatalog.GameplayDisabledModuleCount != 1)
    {
        throw new InvalidOperationException("Server bootstrap did not attach the expected non-gameplay game module catalog.");
    }

    var majorMudModule = report.GameModuleCatalog.Modules.FirstOrDefault(module => module.Id == "majormud-classic");
    if (majorMudModule is null ||
        majorMudModule.GameplayEnabled ||
        majorMudModule.DataRoot != "Data" ||
        majorMudModule.RuleProfiles.Count != 1 ||
        majorMudModule.RuleProfiles[0].RuntimeEnabled)
    {
        throw new InvalidOperationException("Server module catalog did not register MajorMUD Classic as a disabled data-driven module baseline.");
    }

    if (report.ModuleReadiness is null ||
        report.ModuleReadiness.Status != "host-ready-gameplay-disabled" ||
        report.ModuleReadiness.ModuleCount != 1 ||
        report.ModuleReadiness.HostReadyModuleCount != 1 ||
        report.ModuleReadiness.GameplayEnabledModuleCount != 0 ||
        report.ModuleReadiness.RuntimeRuleProfileEnabledCount != 0)
    {
        throw new InvalidOperationException("Server bootstrap did not attach the expected module readiness report.");
    }

    if (report.HostLifecycle is null ||
        report.HostLifecycle.Status != "ready-runtime-disabled" ||
        !report.HostLifecycle.ReadyForRuntimePlanning ||
        !report.HostLifecycle.Initialized ||
        !report.HostLifecycle.Configured ||
        !report.HostLifecycle.DataReady ||
        !report.HostLifecycle.WorldProjectionReady ||
        !report.HostLifecycle.ModuleReady ||
        !report.HostLifecycle.RuntimeDisabled ||
        report.HostLifecycle.GameplayEnabled ||
        report.HostLifecycle.NetworkingEnabled ||
        report.HostLifecycle.WorldSimulationEnabled ||
        report.HostLifecycle.Stages.Count != 6)
    {
        throw new InvalidOperationException("Server bootstrap did not attach the expected runtime-disabled host lifecycle report.");
    }

    if (report.SchedulerBootstrap is null ||
        report.SchedulerBootstrap.Status != "scheduler-bootstrap-registered-runtime-disabled" ||
        !report.SchedulerBootstrap.ReadyForPlanning ||
        !report.SchedulerBootstrap.HostLifecycleReady ||
        !report.SchedulerBootstrap.RuntimeDisabled ||
        !report.SchedulerBootstrap.DiagnosticOnly ||
        report.SchedulerBootstrap.ChannelCount != 10 ||
        report.SchedulerBootstrap.RuntimeEnabledChannelCount != 0 ||
        report.SchedulerBootstrap.RuntimeDisabledChannelCount != 10)
    {
        throw new InvalidOperationException("Server bootstrap did not attach the expected runtime-disabled scheduler bootstrap report.");
    }

    if (report.SchedulerStatusCommands is null ||
        report.SchedulerStatusCommands.Status != "scheduler-status-commands-registered-runtime-disabled" ||
        !report.SchedulerStatusCommands.ReadyForInspection ||
        !report.SchedulerStatusCommands.SchedulerBootstrapReady ||
        !report.SchedulerStatusCommands.RuntimeDisabled ||
        !report.SchedulerStatusCommands.DiagnosticOnly ||
        report.SchedulerStatusCommands.CommandCount != 7 ||
        report.SchedulerStatusCommands.RuntimeEnabledCommandCount != 0 ||
        report.SchedulerStatusCommands.StateMutatingCommandCount != 0 ||
        report.SchedulerStatusCommands.GuardedCommandCount != 7)
    {
        throw new InvalidOperationException("Server bootstrap did not attach the expected guarded scheduler status command surface.");
    }

    if (report.ExitCode != 0)
    {
        throw new InvalidOperationException("Server bootstrap success did not expose exit code 0.");
    }

    if (report.IssueCount != report.Validation.Issues.Count ||
        report.ErrorCount != 0 ||
        report.FatalCount != 0)
    {
        throw new InvalidOperationException("Server bootstrap success did not expose expected validation issue counts.");
    }

    if (report.RequiredDirectoryCount != 6 ||
        report.PresentRequiredDirectoryCount != 6 ||
        report.MissingRequiredDirectoryCount != 0 ||
        !report.RequiredDirectories.Any(directory => directory.Name == "Data" && directory.Exists))
    {
        throw new InvalidOperationException("Server bootstrap success did not expose expected required directory status.");
    }

    if (!report.Readiness.ProjectRootFound ||
        !report.Readiness.RequiredDirectoriesReady ||
        !report.Readiness.ManifestReady ||
        !report.Readiness.StartupDiagnosticsReady ||
        !report.Readiness.HostToolsReady ||
        report.Readiness.Status != "host-tools-ready" ||
        report.Readiness.GameplayStatus != "disabled")
    {
        throw new InvalidOperationException("Server bootstrap success did not expose expected host-tool readiness status.");
    }

    if (report.ProjectName != "Reforged" ||
        string.IsNullOrWhiteSpace(report.CodeUpdate) ||
        string.IsNullOrWhiteSpace(report.DatabaseVersion) ||
        string.IsNullOrWhiteSpace(report.Milestone))
    {
        throw new InvalidOperationException("Server bootstrap did not capture ProjectManifest identity fields.");
    }

    var failedReport = new ServerBootstrap().Run(Path.Combine(Path.GetTempPath(), $"reforged-missing-root-{Guid.NewGuid():N}"));
    if (failedReport.Passed || failedReport.ExitCode != 1)
    {
        throw new InvalidOperationException("Server bootstrap failure did not expose exit code 1.");
    }

    if (failedReport.IssueCount == 0 || failedReport.ErrorCount + failedReport.FatalCount == 0)
    {
        throw new InvalidOperationException("Server bootstrap failure did not expose expected validation error counts.");
    }
}

void ServerDataCatalogAuditSmoke()
{
    var validation = new Reforged.Core.Validation.ValidationResult();
    var report = new ServerDataCatalogAudit().Run(root, validation);
    if (validation.HasErrors)
    {
        throw new InvalidOperationException(string.Join(" | ", validation.Issues.Where(i => i.Severity.ToString() is "Error" or "Fatal").Select(i => $"{i.Code}: {i.Message}")));
    }

    if (report.DataRoot is null || !report.DataRoot.EndsWith("Data", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("Server data catalog did not capture Data root.");
    }

    if (report.CategoryCount < 10 ||
        report.PresentCategoryCount != report.CategoryCount ||
        report.MissingCategoryCount != 0 ||
        report.LoadedRecordCount == 0 ||
        report.RuntimeReadyCategoryCount == 0 ||
        report.RuntimeBlockedCategoryCount == 0 ||
        report.EmptyPlaceholderCategoryCount == 0 ||
        report.Status != "loaded-with-runtime-gaps" ||
        string.IsNullOrWhiteSpace(report.Status))
    {
        throw new InvalidOperationException("Server data catalog did not expose expected category readiness.");
    }

    foreach (var category in new[] { "items", "rooms", "world_rooms" })
    {
        if (!report.Categories.Any(summary => summary.Category == category && summary.FileExists))
        {
            throw new InvalidOperationException($"Server data catalog did not include expected category: {category}");
        }
    }

    foreach (var category in new[] { "loot_tables", "rooms", "shops", "trainers" })
    {
        var summary = report.Categories.FirstOrDefault(item => item.Category == category);
        if (summary is null ||
            summary.IsPlaceholder ||
            !summary.RuntimeReady ||
            summary.Status != "runtime-ready" ||
            summary.RecordCount == 0 ||
            string.IsNullOrWhiteSpace(summary.ReadinessNote))
        {
            throw new InvalidOperationException($"Server data catalog did not classify expected populated category: {category}");
        }
    }

    foreach (var category in new[] { "quests" })
    {
        var summary = report.Categories.FirstOrDefault(item => item.Category == category);
        if (summary is null ||
            !summary.IsPlaceholder ||
            summary.RuntimeReady ||
            summary.Status != "placeholder-empty-needs-data" ||
            string.IsNullOrWhiteSpace(summary.ReadinessNote))
        {
            throw new InvalidOperationException($"Server data catalog did not classify expected placeholder category: {category}");
        }
    }
}

void ServerHostConfigurationAuditSmoke()
{
    var validation = new Reforged.Core.Validation.ValidationResult();
    var report = new ServerHostConfigurationAudit().Run(root, validation);
    if (validation.HasErrors)
    {
        throw new InvalidOperationException(string.Join(" | ", validation.Issues.Where(i => i.Severity.ToString() is "Error" or "Fatal").Select(i => $"{i.Code}: {i.Message}")));
    }

    if (report.Status != "configured-runtime-disabled" ||
        !report.RuntimeDisabled ||
        report.HostName != "Reforged Local Host" ||
        report.BindAddress != "127.0.0.1" ||
        report.Port != 4000 ||
        report.MaxSessions != 64 ||
        report.WorldTickMilliseconds != 1000 ||
        report.CombatTickMilliseconds != 6000 ||
        report.GameplayEnabled ||
        report.NetworkingEnabled ||
        report.WorldSimulationEnabled)
    {
        throw new InvalidOperationException("Server host configuration did not expose the expected disabled runtime defaults.");
    }
}

void ServerWorldProjectionAuditSmoke()
{
    var validation = new Reforged.Core.Validation.ValidationResult();
    var report = new ServerWorldProjectionAudit().Run(root, validation);
    if (validation.HasErrors)
    {
        throw new InvalidOperationException(string.Join(" | ", validation.Issues.Where(i => i.Severity.ToString() is "Error" or "Fatal").Select(i => $"{i.Code}: {i.Message}")));
    }

    if (!report.ProjectionReady ||
        report.Status != "projection-ready" ||
        report.WorldRoomCount != 1248 ||
        report.UniqueWorldRoomCount != report.WorldRoomCount ||
        report.CanonicalRoomCount != report.WorldRoomCount ||
        report.MissingRoomCount != 0 ||
        report.DuplicateWorldRoomCount != 0 ||
        report.StartRoom != "wcc_map1_room1" ||
        !report.StartRoomExists)
    {
        throw new InvalidOperationException("Server world projection did not expose the expected canonical world room index.");
    }
}

void ServerModuleReadinessAuditSmoke()
{
    var validation = new Reforged.Core.Validation.ValidationResult();
    var dataCatalog = new ServerDataCatalogAudit().Run(root, validation);
    var worldProjection = new ServerWorldProjectionAudit().Run(root, validation);
    var moduleCatalog = new ServerGameModuleCatalog().Load();
    var report = new ServerModuleReadinessAudit().Run(root, moduleCatalog, dataCatalog, worldProjection);

    if (validation.HasErrors)
    {
        throw new InvalidOperationException(string.Join(" | ", validation.Issues.Where(i => i.Severity.ToString() is "Error" or "Fatal").Select(i => $"{i.Code}: {i.Message}")));
    }

    if (report.Status != "host-ready-gameplay-disabled" ||
        report.ModuleCount != 1 ||
        report.HostReadyModuleCount != 1 ||
        report.GameplayEnabledModuleCount != 0 ||
        report.RuntimeRuleProfileEnabledCount != 0)
    {
        throw new InvalidOperationException("Server module readiness did not expose the expected disabled host-ready module state.");
    }

    var module = report.Modules.FirstOrDefault(item => item.ModuleId == "majormud-classic");
    if (module is null ||
        !module.HostReady ||
        !module.DataRootExists ||
        !module.WorldProjectionReady ||
        module.WorldRoomCount != 1248 ||
        module.RuntimeBlockedCategoryCount != 1 ||
        module.EmptyPlaceholderCategoryCount != 1 ||
        module.GameplayEnabled ||
        !module.RuleProfilesDisabled ||
        module.RuntimeRuleProfileEnabledCount != 0)
    {
        throw new InvalidOperationException("MajorMUD Classic module readiness did not preserve the expected non-gameplay boundary.");
    }
}

void ServerHostLifecycleAuditSmoke()
{
    var validation = new Reforged.Core.Validation.ValidationResult();
    var hostConfiguration = new ServerHostConfigurationAudit().Run(root, validation);
    var dataCatalog = new ServerDataCatalogAudit().Run(root, validation);
    var worldProjection = new ServerWorldProjectionAudit().Run(root, validation);
    var moduleCatalog = new ServerGameModuleCatalog().Load();
    var moduleReadiness = new ServerModuleReadinessAudit().Run(root, moduleCatalog, dataCatalog, worldProjection);
    var report = new ServerHostLifecycleAudit().Run(root, hostConfiguration, dataCatalog, worldProjection, moduleCatalog, moduleReadiness);

    if (validation.HasErrors)
    {
        throw new InvalidOperationException(string.Join(" | ", validation.Issues.Where(i => i.Severity.ToString() is "Error" or "Fatal").Select(i => $"{i.Code}: {i.Message}")));
    }

    if (report.Status != "ready-runtime-disabled" ||
        !report.ReadyForRuntimePlanning ||
        !report.Initialized ||
        !report.Configured ||
        !report.DataReady ||
        !report.WorldProjectionReady ||
        !report.ModuleReady ||
        !report.RuntimeDisabled ||
        report.GameplayEnabled ||
        report.NetworkingEnabled ||
        report.WorldSimulationEnabled)
    {
        throw new InvalidOperationException("Server host lifecycle did not expose the expected runtime-disabled readiness state.");
    }

    foreach (var requiredStage in new[] { "initialized", "configured", "data-ready", "world-projection-ready", "module-ready", "runtime-disabled" })
    {
        if (!report.Stages.Any(stage => stage.Name == requiredStage && stage.Ready && !string.IsNullOrWhiteSpace(stage.Status)))
        {
            throw new InvalidOperationException($"Server host lifecycle did not expose expected stage: {requiredStage}");
        }
    }
}

void ServerSchedulerBootstrapAuditSmoke()
{
    var validation = new Reforged.Core.Validation.ValidationResult();
    var hostConfiguration = new ServerHostConfigurationAudit().Run(root, validation);
    var dataCatalog = new ServerDataCatalogAudit().Run(root, validation);
    var worldProjection = new ServerWorldProjectionAudit().Run(root, validation);
    var moduleCatalog = new ServerGameModuleCatalog().Load();
    var moduleReadiness = new ServerModuleReadinessAudit().Run(root, moduleCatalog, dataCatalog, worldProjection);
    var hostLifecycle = new ServerHostLifecycleAudit().Run(root, hostConfiguration, dataCatalog, worldProjection, moduleCatalog, moduleReadiness);
    var report = new ServerSchedulerBootstrapAudit().Run(root, hostLifecycle);

    if (validation.HasErrors)
    {
        throw new InvalidOperationException(string.Join(" | ", validation.Issues.Where(i => i.Severity.ToString() is "Error" or "Fatal").Select(i => $"{i.Code}: {i.Message}")));
    }

    if (report.Status != "scheduler-bootstrap-registered-runtime-disabled" ||
        !report.ReadyForPlanning ||
        !report.HostLifecycleReady ||
        !report.RuntimeDisabled ||
        !report.DiagnosticOnly ||
        report.ChannelCount != 10 ||
        report.RuntimeEnabledChannelCount != 0 ||
        report.RuntimeDisabledChannelCount != 10)
    {
        throw new InvalidOperationException("Server scheduler bootstrap did not expose the expected disabled channel registration state.");
    }

    foreach (var requiredChannel in new[] { "Ingress", "Validation", "Planning", "Dispatch", "Mutation", "TimedWork", "EventWork", "Persistence", "Observation", "Maintenance" })
    {
        if (!report.Channels.Any(channel => channel.Name == requiredChannel && !channel.RuntimeEnabled && channel.EvidenceSource == "ServerSim0036"))
        {
            throw new InvalidOperationException($"Server scheduler bootstrap did not expose expected disabled channel: {requiredChannel}");
        }
    }
}

void ServerSchedulerStatusCommandAuditSmoke()
{
    var validation = new Reforged.Core.Validation.ValidationResult();
    var hostConfiguration = new ServerHostConfigurationAudit().Run(root, validation);
    var dataCatalog = new ServerDataCatalogAudit().Run(root, validation);
    var worldProjection = new ServerWorldProjectionAudit().Run(root, validation);
    var moduleCatalog = new ServerGameModuleCatalog().Load();
    var moduleReadiness = new ServerModuleReadinessAudit().Run(root, moduleCatalog, dataCatalog, worldProjection);
    var hostLifecycle = new ServerHostLifecycleAudit().Run(root, hostConfiguration, dataCatalog, worldProjection, moduleCatalog, moduleReadiness);
    var schedulerBootstrap = new ServerSchedulerBootstrapAudit().Run(root, hostLifecycle);
    var report = new ServerSchedulerStatusCommandAudit().Run(root, schedulerBootstrap);

    if (validation.HasErrors)
    {
        throw new InvalidOperationException(string.Join(" | ", validation.Issues.Where(i => i.Severity.ToString() is "Error" or "Fatal").Select(i => $"{i.Code}: {i.Message}")));
    }

    if (report.Status != "scheduler-status-commands-registered-runtime-disabled" ||
        !report.ReadyForInspection ||
        !report.SchedulerBootstrapReady ||
        !report.RuntimeDisabled ||
        !report.DiagnosticOnly ||
        report.CommandCount != 7 ||
        report.RuntimeEnabledCommandCount != 0 ||
        report.RuntimeDisabledCommandCount != 7 ||
        report.StateMutatingCommandCount != 0 ||
        report.GuardedCommandCount != 7)
    {
        throw new InvalidOperationException("Scheduler status command audit did not expose the expected guarded command registrations.");
    }

    foreach (var requiredCommand in new[] { "status.overview", "status.lifecycle", "status.modules", "status.scheduler", "status.scheduler.channels", "status.data", "status.validation" })
    {
        if (!report.Commands.Any(command => command.Name == requiredCommand && command.Guarded && !command.RuntimeEnabled && !command.MutatesState))
        {
            throw new InvalidOperationException($"Scheduler status command audit did not expose expected guarded command: {requiredCommand}");
        }
    }
}

void ClassicParityAuditSmoke()
{
    var report = new ClassicParityAudit().Run(root);
    if (report.CanonicalDataPath is null || !report.CanonicalDataPath.EndsWith("Data", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("Classic parity audit did not capture canonical Data path.");
    }

    if (report.ImportedDatabasesPath is null || !report.ImportedDatabasesPath.EndsWith("ImportedDatabases", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("Classic parity audit did not capture ImportedDatabases path.");
    }

    if (report.Sources.Count != 2 || report.Categories.Count < 8)
    {
        throw new InvalidOperationException("Classic parity audit did not produce expected source/category inventory.");
    }

    if (string.IsNullOrWhiteSpace(report.Status))
    {
        throw new InvalidOperationException("Classic parity audit did not expose an inventory status.");
    }
}

static string LocateProjectRoot(string start)
{
    var directory = new DirectoryInfo(start);
    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "ProjectManifest.json")) && Directory.Exists(Path.Combine(directory.FullName, "Data")))
        {
            return directory.FullName;
        }
        directory = directory.Parent;
    }

    throw new DirectoryNotFoundException("Could not locate repository root from test output folder.");
}
