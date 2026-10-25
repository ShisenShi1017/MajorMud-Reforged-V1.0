using RealmGate.Core.Configuration;
using RealmGate.Core.Diagnostics;
using RealmGate.Core.Events;
using RealmGate.Core.Health;
using RealmGate.Core.Logging;
using RealmGate.Core.Services;
using RealmGate.Server;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

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
    ("server host configuration enables runtime", ServerHostConfigurationAuditSmoke),
    ("server data catalog loads canonical data", ServerDataCatalogAuditSmoke),
    ("server world projection indexes map-qualified rooms", ServerWorldProjectionAuditSmoke),
    ("server module readiness enables current host boundary", ServerModuleReadinessAuditSmoke),
    ("server host lifecycle reports runtime-enabled readiness", ServerHostLifecycleAuditSmoke),
    ("server scheduler bootstrap enables channels", ServerSchedulerBootstrapAuditSmoke),
    ("server scheduler status commands are runtime-enabled read-only", ServerSchedulerStatusCommandAuditSmoke),
    ("server runtime state and event dispatcher track host-owned changes", ServerRuntimeStateAndEventsSmoke),
    ("server delivers the selected-world navigation graph with verified framing", ServerNavigationGraphTransferSmoke),
    ("server delivers world-qualified structured inventory state", ServerInventoryStateSmoke),
    ("server delivers isolated character automation state", ServerAutomationStateSmoke),
    ("classic parity audit inventories canonical and imported data", ClassicParityAuditSmoke),
    ("classic bug-fix policies enforce corrected behavior", ClassicBugFixPolicySmoke)
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
        report.HostConfiguration.Status != "runtime-enabled" ||
        report.HostConfiguration.RuntimeDisabled ||
        report.HostConfiguration.Port != RealmGateNetworkBinding.DefaultTelnetPort ||
        report.HostConfiguration.MaxSessions != 64 ||
        report.HostConfiguration.WorldTickMilliseconds != 1000 ||
        report.HostConfiguration.CombatTickMilliseconds != 5000)
    {
        throw new InvalidOperationException("Server bootstrap did not attach expected runtime-enabled host configuration.");
    }

    if (report.ServerDataCatalog is null ||
        report.ServerDataCatalog.CategoryCount < 10 ||
        report.ServerDataCatalog.LoadedRecordCount == 0 ||
        report.ServerDataCatalog.RuntimeReadyCategoryCount == 0)
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
        throw new InvalidOperationException("Server bootstrap did not attach the expected map-qualified world projection.");
    }

    if (report.GameModuleCatalog is null ||
        report.GameModuleCatalog.ModuleCount != 1 ||
        report.GameModuleCatalog.GameplayEnabledModuleCount != 1 ||
        report.GameModuleCatalog.GameplayDisabledModuleCount != 0)
    {
        throw new InvalidOperationException("Server bootstrap did not attach the expected runtime-enabled game module catalog.");
    }

    var majorMudModule = report.GameModuleCatalog.Modules.FirstOrDefault(module => module.Id == "majormud-classic");
    if (majorMudModule is null ||
        !majorMudModule.GameplayEnabled ||
        majorMudModule.DataRoot != "Data" ||
        majorMudModule.RuleProfiles.Count != 1 ||
        !majorMudModule.RuleProfiles[0].RuntimeEnabled)
    {
        throw new InvalidOperationException("Server module catalog did not register MajorMUD Classic as an enabled data-driven module.");
    }

    if (report.ModuleReadiness is null ||
        report.ModuleReadiness.ModuleCount != 1 ||
        report.ModuleReadiness.HostReadyModuleCount != 1 ||
        report.ModuleReadiness.GameplayEnabledModuleCount != 1 ||
        report.ModuleReadiness.RuntimeRuleProfileEnabledCount != 1)
    {
        throw new InvalidOperationException("Server bootstrap did not attach the expected module readiness report.");
    }

    if (report.HostLifecycle is null ||
        !report.HostLifecycle.Initialized ||
        !report.HostLifecycle.Configured ||
        !report.HostLifecycle.DataReady ||
        !report.HostLifecycle.WorldProjectionReady ||
        !report.HostLifecycle.ModuleReady ||
        report.HostLifecycle.RuntimeDisabled ||
        !report.HostLifecycle.GameplayEnabled ||
        !report.HostLifecycle.NetworkingEnabled ||
        !report.HostLifecycle.WorldSimulationEnabled ||
        report.HostLifecycle.Stages.Count != 6)
    {
        throw new InvalidOperationException("Server bootstrap did not attach the expected runtime-enabled host lifecycle report.");
    }

    if (report.SchedulerBootstrap is null ||
        report.SchedulerBootstrap.Status != "scheduler-runtime-enabled" ||
        report.SchedulerBootstrap.RuntimeDisabled ||
        report.SchedulerBootstrap.DiagnosticOnly ||
        report.SchedulerBootstrap.ChannelCount != 10 ||
        report.SchedulerBootstrap.RuntimeEnabledChannelCount != 10 ||
        report.SchedulerBootstrap.RuntimeDisabledChannelCount != 0)
    {
        throw new InvalidOperationException("Server bootstrap did not attach the expected runtime-enabled scheduler bootstrap report.");
    }

    if (report.SchedulerStatusCommands is null ||
        report.SchedulerStatusCommands.Status != "scheduler-status-command-runtime-enabled" ||
        report.SchedulerStatusCommands.RuntimeDisabled ||
        report.SchedulerStatusCommands.DiagnosticOnly ||
        report.SchedulerStatusCommands.CommandCount != 7 ||
        report.SchedulerStatusCommands.RuntimeEnabledCommandCount != 7 ||
        report.SchedulerStatusCommands.StateMutatingCommandCount != 0 ||
        report.SchedulerStatusCommands.GuardedCommandCount != 0)
    {
        throw new InvalidOperationException("Server bootstrap did not attach the expected runtime-enabled scheduler status command surface.");
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

    if (report.ProjectName != "RealmGate" ||
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
    var validation = new RealmGate.Core.Validation.ValidationResult();
    var report = new ServerDataCatalogAudit().Run(root, validation);
    if (validation.HasErrors)
    {
        throw new InvalidOperationException(string.Join(" | ", validation.Issues.Where(i => i.Severity.ToString() is "Error" or "Fatal").Select(i => $"{i.Code}: {i.Message}")));
    }

    if (report.DataRoot is null || (!report.DataRoot.EndsWith("Data", StringComparison.Ordinal) && !report.DataRoot.EndsWith(Path.Combine("Data", "Classic"), StringComparison.Ordinal)))
    {
        throw new InvalidOperationException("Server data catalog did not capture Data root.");
    }

    if (report.CategoryCount < 10 ||
        report.LoadedRecordCount == 0 ||
        report.RuntimeReadyCategoryCount == 0 ||
        report.Status != "runtime-ready" && report.Status != "loaded-with-runtime-gaps" && report.Status != "missing-data-files" ||
        string.IsNullOrWhiteSpace(report.Status))
    {
        throw new InvalidOperationException("Server data catalog did not expose expected category readiness.");
    }

    foreach (var category in new[] { "items", "rooms" })
    {
        if (!report.Categories.Any(summary => summary.Category == category && summary.FileExists))
        {
            throw new InvalidOperationException($"Server data catalog did not include expected category: {category}");
        }
    }

    foreach (var category in new[] { "loot_rules", "rooms", "shops" })
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

}

void ServerHostConfigurationAuditSmoke()
{
    var validation = new RealmGate.Core.Validation.ValidationResult();
    var report = new ServerHostConfigurationAudit().Run(root, validation);
    if (validation.HasErrors)
    {
        throw new InvalidOperationException(string.Join(" | ", validation.Issues.Where(i => i.Severity.ToString() is "Error" or "Fatal").Select(i => $"{i.Code}: {i.Message}")));
    }

    if (report.Status != "runtime-enabled" ||
        report.RuntimeDisabled ||
        report.HostName != "RealmGate Host" ||
        report.BindAddress != RealmGateNetworkBinding.AllIpv4InterfacesAddress ||
        report.Port != RealmGateNetworkBinding.DefaultTelnetPort ||
        report.MaxSessions != 64 ||
        report.WorldTickMilliseconds != 1000 ||
        report.CombatTickMilliseconds != 5000 ||
        !report.GameplayEnabled ||
        !report.NetworkingEnabled ||
        !report.WorldSimulationEnabled)
    {
        throw new InvalidOperationException("Server host configuration did not expose the expected enabled runtime configuration.");
    }
}

void ServerWorldProjectionAuditSmoke()
{
    var validation = new RealmGate.Core.Validation.ValidationResult();
    var report = new ServerWorldProjectionAudit().Run(root, validation);
    if (validation.HasErrors)
    {
        throw new InvalidOperationException(string.Join(" | ", validation.Issues.Where(i => i.Severity.ToString() is "Error" or "Fatal").Select(i => $"{i.Code}: {i.Message}")));
    }

    if (!report.ProjectionReady ||
        report.Status != "projection-ready" ||
        report.WorldRoomCount == 0 ||
        report.UniqueWorldRoomCount != report.WorldRoomCount ||
        report.CanonicalRoomCount != report.WorldRoomCount ||
        report.MissingRoomCount != 0 ||
        report.DuplicateWorldRoomCount != 0 ||
        report.StartRoom != "room.map1.2140" ||
        !report.StartRoomExists)
    {
        throw new InvalidOperationException("Server world projection did not expose the expected map-qualified world room index.");
    }
}

void ServerModuleReadinessAuditSmoke()
{
    var validation = new RealmGate.Core.Validation.ValidationResult();
    var dataCatalog = new ServerDataCatalogAudit().Run(root, validation);
    var worldProjection = new ServerWorldProjectionAudit().Run(root, validation);
    var moduleCatalog = new ServerGameModuleCatalog().Load();
    var report = new ServerModuleReadinessAudit().Run(root, moduleCatalog, dataCatalog, worldProjection);

    if (validation.HasErrors)
    {
        throw new InvalidOperationException(string.Join(" | ", validation.Issues.Where(i => i.Severity.ToString() is "Error" or "Fatal").Select(i => $"{i.Code}: {i.Message}")));
    }

    if (report.ModuleCount != 1 ||
        report.HostReadyModuleCount != 1 ||
        report.GameplayEnabledModuleCount != 1 ||
        report.RuntimeRuleProfileEnabledCount != 1)
    {
        throw new InvalidOperationException("Server module readiness did not expose the expected enabled host-ready module state.");
    }

    var module = report.Modules.FirstOrDefault(item => item.ModuleId == "majormud-classic");
    if (module is null ||
        !module.DataRootExists ||
        !module.WorldProjectionReady ||
        module.WorldRoomCount == 0 ||
        !module.GameplayEnabled ||
        module.RuleProfilesDisabled ||
        module.RuntimeRuleProfileEnabledCount != 1)
    {
        throw new InvalidOperationException("MajorMUD Classic module readiness did not expose the expected runtime-enabled boundary.");
    }
}

void ServerHostLifecycleAuditSmoke()
{
    var validation = new RealmGate.Core.Validation.ValidationResult();
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

    if (!report.Initialized ||
        !report.Configured ||
        !report.DataReady ||
        !report.WorldProjectionReady ||
        !report.ModuleReady ||
        report.RuntimeDisabled ||
        !report.GameplayEnabled ||
        !report.NetworkingEnabled ||
        !report.WorldSimulationEnabled)
    {
        throw new InvalidOperationException("Server host lifecycle did not expose the expected runtime-enabled readiness state.");
    }

    foreach (var requiredStage in new[] { "initialized", "configured", "data-ready", "world-projection-ready", "module-ready", "runtime-enabled" })
    {
        if (!report.Stages.Any(stage => stage.Name == requiredStage && !string.IsNullOrWhiteSpace(stage.Status)))
        {
            throw new InvalidOperationException($"Server host lifecycle did not expose expected stage: {requiredStage}");
        }
    }
}

void ServerSchedulerBootstrapAuditSmoke()
{
    var validation = new RealmGate.Core.Validation.ValidationResult();
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

    if (report.Status != "scheduler-runtime-enabled" ||
        report.RuntimeDisabled ||
        report.DiagnosticOnly ||
        report.ChannelCount != 10 ||
        report.RuntimeEnabledChannelCount != 10 ||
        report.RuntimeDisabledChannelCount != 0)
    {
        throw new InvalidOperationException("Server scheduler bootstrap did not expose the expected enabled channel registration state.");
    }

    foreach (var requiredChannel in new[] { "Ingress", "Validation", "Planning", "Dispatch", "Mutation", "TimedWork", "EventWork", "Persistence", "Observation", "Maintenance" })
    {
        if (!report.Channels.Any(channel => channel.Name == requiredChannel && channel.RuntimeEnabled && channel.EvidenceSource == "RuntimeLivingWorld"))
        {
            throw new InvalidOperationException($"Server scheduler bootstrap did not expose expected enabled channel: {requiredChannel}");
        }
    }
}

void ServerSchedulerStatusCommandAuditSmoke()
{
    var validation = new RealmGate.Core.Validation.ValidationResult();
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

    if (report.Status != "scheduler-status-command-runtime-enabled" ||
        report.RuntimeDisabled ||
        report.DiagnosticOnly ||
        report.CommandCount != 7 ||
        report.RuntimeEnabledCommandCount != 7 ||
        report.RuntimeDisabledCommandCount != 0 ||
        report.StateMutatingCommandCount != 0 ||
        report.GuardedCommandCount != 0)
    {
        throw new InvalidOperationException("Scheduler status command audit did not expose the expected runtime-enabled command registrations.");
    }

    foreach (var requiredCommand in new[] { "status.overview", "status.lifecycle", "status.modules", "status.scheduler", "status.scheduler.channels", "status.data", "status.validation" })
    {
        if (!report.Commands.Any(command => command.Name == requiredCommand && !command.Guarded && command.RuntimeEnabled && !command.MutatesState))
        {
            throw new InvalidOperationException($"Scheduler status command audit did not expose expected runtime-enabled command: {requiredCommand}");
        }
    }
}

void ServerRuntimeStateAndEventsSmoke()
{
    var options = ServerRuntimeOptions.Load(root);
    var host = new ServerRuntimeHost(root, options);
    host.Bootstrap();

    if (!host.State.Worlds.Any(world => world.WorldId.Equals("classic", StringComparison.OrdinalIgnoreCase)) ||
        host.Events.EventCount == 0 ||
        !host.Events.Events.Any(item => item.Category == "world" && item.Name == "loaded" &&
            item.WorldId?.Equals("classic", StringComparison.OrdinalIgnoreCase) == true))
    {
        throw new InvalidOperationException("Runtime host did not publish world-loaded state/event records.");
    }

    var world = host.Worlds["classic"];
    var session = host.Sessions.CreateSession("smoke-session", world, "Smoke Tester");
    if (!host.Sessions.TryAdd(session, 4, out var addMessage) || addMessage != "accepted")
    {
        throw new InvalidOperationException($"Runtime session was not accepted: {addMessage}");
    }

    if (session.Actor.Kind != "developer-avatar" ||
        session.Actor.IsPersistentCharacter ||
        session.SpawnSource != "developer-diagnostic-room")
    {
        throw new InvalidOperationException("Runtime smoke session did not attach the expected non-persistent developer avatar.");
    }

    var dispatcher = new RuntimeCommandDispatcher(() => host.Worlds, host.Sessions, host.State, host.Events);
    var expectedStart = world.ResolveDiagnosticRoom() ?? world.ResolveGeneratedCharacterStartRoom();
    if (expectedStart is null || expectedStart.MapId <= 0 || expectedStart.RoomNumber <= 0)
    {
        throw new InvalidOperationException("Runtime world did not expose a map-qualified diagnostic or generated start room.");
    }

    var where = dispatcher.Dispatch(session, "where");
    if (!where.Contains("World=classic", StringComparison.Ordinal) ||
        !where.Contains($"Room={expectedStart.Id}", StringComparison.Ordinal) ||
        !string.Equals(session.CurrentRoom.Id, expectedStart.Id, StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException($"Runtime command dispatcher did not report the resolved starting location '{expectedStart.Id}'.");
    }

    var traversableExit = expectedStart.Exits.FirstOrDefault(exit => !exit.HasBlockedDestination && world.FindRoom(exit.TargetMapId, exit.TargetRoomNumber) is not null);
    if (traversableExit is null)
    {
        throw new InvalidOperationException($"Runtime starting room '{expectedStart.Id}' has no traversable generated exit for movement smoke testing.");
    }

    var expectedTarget = world.FindRoom(traversableExit.TargetMapId, traversableExit.TargetRoomNumber)!;
    var move = dispatcher.Dispatch(session, traversableExit.Direction);
    if (!string.Equals(session.CurrentRoom.Id, expectedTarget.Id, StringComparison.OrdinalIgnoreCase) ||
        !move.Contains(expectedTarget.Name, StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException($"Runtime movement did not resolve generated exit '{traversableExit.Direction}' from '{expectedStart.Id}' to '{expectedTarget.Id}'.");
    }

    var state = dispatcher.Dispatch(session, "state");
    var events = dispatcher.Dispatch(session, "events");
    if (!state.Contains($"Worlds={host.State.Worlds.Count}", StringComparison.Ordinal) ||
        !state.Contains("Sessions=1", StringComparison.Ordinal) ||
        !state.Contains("Characters=0", StringComparison.Ordinal) ||
        !events.Contains("movement.completed", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("Runtime state/events inspection commands did not expose expected host-owned records.");
    }

    if (!host.Sessions.Remove(session.Id) ||
        !host.Events.Events.Any(item => item.Category == "session" && item.Name == "ended"))
    {
        throw new InvalidOperationException("Runtime session removal did not publish session-ended event.");
    }
}

void ServerNavigationGraphTransferSmoke()
{
    var host = new ServerRuntimeHost(root, ServerRuntimeOptions.Load(root));
    host.Bootstrap();
    var world = host.Worlds["classic"];
    var session = host.Sessions.CreateSession("navigation-transfer-smoke", world, "Navigation Tester");
    var gameplay = new RuntimeLiveGameplayFacade(host.Characters, host.Sessions);
    var response = gameplay.RenderClientPanelData(
        session,
        RuntimeCommand.Parse("clientdata navigation", world.CommandCatalog));

    var frames = response.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);
    var begin = frames.Single(frame => frame.Contains("|NAVGRAPHBEGIN|", StringComparison.Ordinal));
    var end = frames.Single(frame => frame.Contains("|NAVGRAPHEND|", StringComparison.Ordinal));
    var chunks = frames.Where(frame => frame.Contains("|NAVGRAPHCHUNK|", StringComparison.Ordinal)).ToArray();
    using var beginDocument = JsonDocument.Parse(DecodePrivateFrame(begin));
    using var endDocument = JsonDocument.Parse(DecodePrivateFrame(end));
    var beginRoot = beginDocument.RootElement;
    var transferId = beginRoot.GetProperty("transferId").GetString() ?? string.Empty;
    var expectedWorld = beginRoot.GetProperty("worldId").GetString() ?? string.Empty;
    var expectedLength = beginRoot.GetProperty("uncompressedLength").GetInt32();
    var expectedCompressedLength = beginRoot.GetProperty("compressedLength").GetInt32();
    var expectedChunkCount = beginRoot.GetProperty("chunkCount").GetInt32();
    var expectedHash = beginRoot.GetProperty("sha256").GetString() ?? string.Empty;
    if (!expectedWorld.Equals("classic", StringComparison.OrdinalIgnoreCase) ||
        chunks.Length != expectedChunkCount ||
        endDocument.RootElement.GetProperty("transferId").GetString() != transferId)
        throw new InvalidOperationException("Navigation graph transfer framing does not match the selected Classic world.");

    var decodedChunks = chunks.Select(frame =>
    {
        using var document = JsonDocument.Parse(DecodePrivateFrame(frame));
        return (
            Index: document.RootElement.GetProperty("index").GetInt32(),
            Data: Convert.FromBase64String(document.RootElement.GetProperty("data").GetString() ?? string.Empty));
    }).OrderBy(chunk => chunk.Index).ToArray();
    var compressed = decodedChunks.SelectMany(chunk => chunk.Data).ToArray();
    if (compressed.Length != expectedCompressedLength)
        throw new InvalidOperationException("Navigation graph transfer compressed length is incorrect.");

    using var compressedStream = new MemoryStream(compressed, writable: false);
    using var gzip = new GZipStream(compressedStream, CompressionMode.Decompress);
    using var output = new MemoryStream();
    gzip.CopyTo(output);
    var graph = output.ToArray();
    var actualHash = Convert.ToHexString(SHA256.HashData(graph)).ToLowerInvariant();
    if (graph.Length != expectedLength || !actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("Navigation graph transfer did not preserve the generated graph bytes.");
    using var graphDocument = JsonDocument.Parse(graph);
    if (!graphDocument.RootElement.GetProperty("worldId").GetString()!.Equals("Classic", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("Navigation graph payload does not identify the selected world.");
}

void ServerInventoryStateSmoke()
{
    var host = new ServerRuntimeHost(root, ServerRuntimeOptions.Load(root));
    host.Bootstrap();
    var world = host.Worlds["classic"];
    var session = host.Sessions.CreateSession("inventory-state-smoke", world, "Inventory Tester");
    var gameplay = new RuntimeLiveGameplayFacade(host.Characters, host.Sessions);
    var response = gameplay.RenderClientPanelData(
        session,
        RuntimeCommand.Parse("clientdata inventory", world.CommandCatalog));
    var frames = response.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);
    var stateFrame = frames.Single(frame => frame.Contains("|INVENTORYSTATE|", StringComparison.Ordinal));
    if (!frames.Any(frame => frame.Contains("|PANELDATA|", StringComparison.Ordinal)))
        throw new InvalidOperationException("Inventory client data no longer includes the compatible visible panel frame.");

    using var document = JsonDocument.Parse(DecodePrivateFrame(stateFrame));
    var payload = document.RootElement;
    if (!payload.GetProperty("WorldId").GetString()!.Equals("classic", StringComparison.OrdinalIgnoreCase) ||
        payload.GetProperty("CharacterId").GetString() != session.Actor.Id ||
        payload.GetProperty("RoomId").GetString() != session.CurrentRoom.Id ||
        payload.GetProperty("Inventory").ValueKind != JsonValueKind.Array ||
        payload.GetProperty("Equipment").ValueKind != JsonValueKind.Array)
    {
        throw new InvalidOperationException("Inventory state payload is not qualified to the selected world, character, and room.");
    }
}

void ServerAutomationStateSmoke()
{
    var temporary = Path.Combine(Path.GetTempPath(), "realmgate-assistant-smoke-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(temporary);
    try
    {
        // Runtime input is copied; all character and living-world writes stay in this disposable root.
        foreach (var relative in new[] { "Config", Path.Combine("Data", "Classic") })
        {
            var source = Path.Combine(root, relative);
            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                var target = Path.Combine(temporary, relative, Path.GetRelativePath(source, file));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target);
            }
        }
        var host = new ServerRuntimeHost(root, ServerRuntimeOptions.Load(root));
        host.Bootstrap();
        var world = host.Worlds["classic"];
        var characters = new RuntimeCharacterService(temporary);
        var account = characters.GetOrCreateAccount("AssistantSmoke");
        var catalog = characters.GetCatalog("Classic");
        var character = characters.CreateCharacter(account, world, "Assist", "Male",
            catalog.Races.First().Id, catalog.Classes.First().Id);
        var sessions = new RuntimeSessionManager();
        var session = sessions.CreateCharacterSession("assistant-smoke", world, character);
        if (!sessions.TryAdd(session, 4, out var error)) throw new InvalidOperationException(error);
        var gameplay = new RuntimeLiveGameplayFacade(characters, sessions);
        var frame = gameplay.RenderClientPanelData(session, RuntimeCommand.Parse("clientdata automation", world.CommandCatalog));
        using var document = JsonDocument.Parse(DecodePrivateFrame(frame));
        var state = document.RootElement;
        if (!state.GetProperty("WorldId").GetString()!.Equals(world.Definition.Id, StringComparison.OrdinalIgnoreCase) ||
            state.GetProperty("CharacterId").GetString() != character.CharacterId ||
            state.GetProperty("RoomId").GetString() != session.CurrentRoom.Id ||
            state.GetProperty("HealthCurrent").GetInt32() != character.HitPoints ||
            state.GetProperty("InCombat").GetBoolean() || state.GetProperty("PartyMembers").GetArrayLength() != 0)
            throw new InvalidOperationException("Automation snapshot lost selected world/character/room state.");
        foreach (var family in new[] { "LearnedSpells", "Conditions", "ActiveEffects", "Npcs", "Players" })
            if (state.GetProperty(family).ValueKind != JsonValueKind.Array)
                throw new InvalidOperationException("Automation snapshot is missing " + family);
        if (state.GetProperty("Conditions").EnumerateArray().Any(value => value.GetString()!.Contains("ui.")))
            throw new InvalidOperationException("Internal character preferences leaked into visible conditions.");
        using var characterFrame = JsonDocument.Parse(DecodePrivateFrame(
            gameplay.RenderClientPanelData(session, RuntimeCommand.Parse("clientdata character", world.CommandCatalog))));
        var characterState = characterFrame.RootElement;
        if (characterState.GetProperty("WorldId").GetString() != character.WorldId ||
            characterState.GetProperty("CharacterId").GetString() != character.CharacterId ||
            characterState.GetProperty("RoomId").GetString() != session.CurrentRoom.Id ||
            characterState.GetProperty("RaceName").GetString() != character.RaceName ||
            characterState.GetProperty("ClassName").GetString() != character.ClassName ||
            characterState.GetProperty("Experience").GetInt64() != character.Experience ||
            characterState.GetProperty("BaseStats").ValueKind != JsonValueKind.Object ||
            characterState.GetProperty("Skills").ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("Character snapshot lost saved selected-world identity or statistics.");
        var itemId = RuntimeItemCatalog.Load(temporary, world.Definition.Id).Items
            .First(item => !string.IsNullOrWhiteSpace(item.Name)).Id;
        using var searchFrame = JsonDocument.Parse(DecodePrivateFrame(
            gameplay.RenderClientPanelData(session, RuntimeCommand.Parse(
                $"clientdata itemsearch {itemId}", world.CommandCatalog))));
        var search = searchFrame.RootElement;
        if (search.GetProperty("WorldId").GetString() != world.Definition.Id ||
            search.GetProperty("CharacterId").GetString() != character.CharacterId ||
            search.GetProperty("RoomId").GetString() != session.CurrentRoom.Id ||
            !search.GetProperty("Matches").EnumerateArray().Any(item =>
                item.GetProperty("Id").GetString() == itemId) ||
            search.GetProperty("Equipped").ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Item search did not use the selected world's item catalog.");
        gameplay.ExecuteRegenerationStateCommand(session, RuntimeCommand.Parse("rest", world.CommandCatalog));
        using var resting = JsonDocument.Parse(DecodePrivateFrame(gameplay.RenderClientAutomationStateData(session)));
        if (!resting.RootElement.GetProperty("IsResting").GetBoolean())
            throw new InvalidOperationException("Focused recovery did not appear in automation state.");
    }
    finally { Directory.Delete(temporary, recursive: true); }
}

static string DecodePrivateFrame(string frame)
{
    const string prefix = "@@REFORGED-PROTOCOL|";
    var separator = frame.IndexOf('|', prefix.Length);
    if (!frame.StartsWith(prefix, StringComparison.Ordinal) || separator < 0)
        throw new InvalidOperationException("Private client frame is malformed.");
    return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(frame[(separator + 1)..]));
}

void ClassicParityAuditSmoke()
{
    var report = new ClassicParityAudit().Run(root);
    if (report.CanonicalDataPath is null || !report.CanonicalDataPath.EndsWith("Data", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("Classic parity audit did not capture canonical Data path.");
    }

    if (report.ImportedDatabasesPath is not null || report.ImportedReferenceFileCount != 0)
    {
        throw new InvalidOperationException("Classic runtime data audit must not depend on ImportedDatabases.");
    }

    if (report.Sources.Count != 1 || report.Categories.Count < 8)
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


void ClassicBugFixPolicySmoke()
{
    if (ClassicShopPricingPolicy.ClampEffectivePercent(0) != 10 || ClassicShopPricingPolicy.ClampEffectivePercent(25) != 25)
        throw new InvalidOperationException("Bard shop minimum percentage policy failed.");
    if (!ClassicVisibilityPolicy.CanDetect(true, true, true))
        throw new InvalidOperationException("SEE HIDDEN was incorrectly suppressed by sneak.");
    var prep = ClassicAttackPreparationPolicy.PrepareBackstab(true, false, false, true, true);
    if (prep.AttackType != ClassicAttackType.NormalWeapon || !prep.EngageTarget || prep.BackstabQualified)
        throw new InvalidOperationException("Invalid two-handed backstab did not downgrade to engaged normal attack.");
    if (ClassicGangDeedPolicy.MayRetainDeed(false, true, true))
        throw new InvalidOperationException("Former non-leader retained a gang deed.");
    var coordinator = new ClassicDualLeverCoordinator();
    var value = coordinator.ExecuteAtomic(() => 42);
    if (value != 42) throw new InvalidOperationException("Dual lever atomic coordinator failed.");
}
