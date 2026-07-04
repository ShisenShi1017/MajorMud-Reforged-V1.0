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
    ("server data catalog loads canonical data", ServerDataCatalogAuditSmoke),
    ("server world projection indexes canonical rooms", ServerWorldProjectionAuditSmoke),
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

    foreach (var required in new[] { "project-root", "required-directories", "project-manifest", "server-data-catalog", "world-projection", "game-module-catalog", "startup-diagnostics", "classic-parity-audit" })
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
        report.WorldRoomCount != 1246 ||
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
