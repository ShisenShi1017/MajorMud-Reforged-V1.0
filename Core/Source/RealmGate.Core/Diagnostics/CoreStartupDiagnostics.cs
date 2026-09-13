using RealmGate.Core.Configuration;
using RealmGate.Core.Events;
using RealmGate.Core.Health;
using RealmGate.Core.Logging;
using RealmGate.Core.Services;
using RealmGate.Core.Validation;

namespace RealmGate.Core.Diagnostics;

/// <summary>
/// Runs Milestone 1 startup verification without requiring a full server runtime.
/// </summary>
public sealed class CoreStartupDiagnostics
{
    public StartupDiagnosticReport Run(string projectRoot, string? dataFolder = null)
    {
        var report = new StartupDiagnosticReport();
        var usesConfiguredGeneratedData = !string.IsNullOrWhiteSpace(dataFolder);
        var resolvedDataFolder = Path.GetFullPath(dataFolder ?? Path.Combine(projectRoot, "Data"));
        var appsSourceFolder = Path.Combine(projectRoot, "Apps", "Source");
        var packagedRuntimeBundle = Path.Combine(projectRoot, "Librarium", "RealmGate.librarium");
        var isPackagedRuntime = File.Exists(packagedRuntimeBundle) && !Directory.Exists(appsSourceFolder);
        var logger = new InMemoryReforgedLogger();
        var events = new InMemoryEventBus();
        var services = new ServiceRegistry();

        report.AddCheck("dependency-injection");
        services.Register<IReforgedLogger>(logger);
        services.Register<IEventPublisher>(events);
        _ = services.ResolveRequired<IReforgedLogger>();
        _ = services.ResolveRequired<IEventPublisher>();

        report.AddCheck("configuration");
        var configuration = new RuntimeConfigurationProvider()
            .AddDefault("runtime:root", projectRoot)
            .AddDefault("runtime:data", resolvedDataFolder);
        services.Register(configuration);

        report.AddCheck("event-system");
        var received = 0;
        using (events.Subscribe<EventPublishedEvent>(_ => received++))
        {
            events.Publish(new EventPublishedEvent("startup-diagnostics", "Event bus smoke test", DateTimeOffset.UtcNow));
        }
        if (received != 1 || events.SubscriberCount != 0)
        {
            report.Validation.Add(ValidationSeverity.Error, "EVENT_BUS_SMOKE_FAILED", "Event bus did not publish and dispose subscriptions cleanly.");
        }

        report.AddCheck(isPackagedRuntime ? "packaged-runtime-health" : "repository-health");
        if (!isPackagedRuntime)
        {
            report.Validation.Merge(new ApplicationCoreReferenceHealthCheck().Validate(appsSourceFolder));
            foreach (var issue in new DuplicateModelEnforcementHealthCheck().Check(projectRoot))
            {
                report.Validation.Add(ValidationSeverity.Error, "DUPLICATE_MODEL_ENFORCEMENT", issue);
            }
        }

        report.Validation.Merge(new DataJsonHealthCheck().ValidateDataFolder(
            resolvedDataFolder,
            validateLegacyRepositoryContracts: !usesConfiguredGeneratedData && !isPackagedRuntime));

        logger.Write(LogSeverity.Information, "CoreStartupDiagnostics", report.Passed ? "Milestone 1 startup diagnostics passed." : "Milestone 1 startup diagnostics found issues.");
        return report;
    }
}
