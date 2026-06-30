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
    ("server bootstrap runs", ServerBootstrapSmoke)
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

    foreach (var required in new[] { "project-root", "required-directories", "project-manifest", "startup-diagnostics" })
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
