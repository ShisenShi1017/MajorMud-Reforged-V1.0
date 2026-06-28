using Reforged.Core.Configuration;
using Reforged.Core.Diagnostics;
using Reforged.Core.Events;
using Reforged.Core.Health;
using Reforged.Core.Logging;
using Reforged.Core.Services;

var root = LocateProjectRoot(AppContext.BaseDirectory);
var tests = new List<(string Name, Action Body)>
{
    ("dependency-injection resolves required services", DependencyInjectionSmoke),
    ("configuration loads defaults and flattened json", ConfigurationSmoke),
    ("event system publishes and disposes", EventSystemSmoke),
    ("repository health checks run", RepositoryHealthSmoke),
    ("startup diagnostics run", StartupDiagnosticsSmoke)
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

Console.WriteLine("All Milestone 1 core smoke tests passed.");
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
