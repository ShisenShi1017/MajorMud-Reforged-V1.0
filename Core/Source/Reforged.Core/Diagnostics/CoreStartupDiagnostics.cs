using Reforged.Core.Configuration;
using Reforged.Core.Events;
using Reforged.Core.Health;
using Reforged.Core.Logging;
using Reforged.Core.Services;
using Reforged.Core.Validation;

namespace Reforged.Core.Diagnostics;

/// <summary>
/// Runs Milestone 1 startup verification without requiring a full server runtime.
/// </summary>
public sealed class CoreStartupDiagnostics
{
    public StartupDiagnosticReport Run(string projectRoot)
    {
        var report = new StartupDiagnosticReport();
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
            .AddDefault("runtime:data", Path.Combine(projectRoot, "Data"));
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

        report.AddCheck("repository-health");
        report.Validation.Merge(new ApplicationCoreReferenceHealthCheck().Validate(Path.Combine(projectRoot, "Apps", "Source")));
        report.Validation.Merge(new DataJsonHealthCheck().ValidateDataFolder(Path.Combine(projectRoot, "Data")));
        report.Validation.Merge(new CoreDataRepositoryHealthCheck().ValidateDataFolder(projectRoot));
        report.Validation.Merge(new DataCrossReferenceHealthCheck().ValidateDataFolder(Path.Combine(projectRoot, "Data")));
        foreach (var issue in new DuplicateModelEnforcementHealthCheck().Check(projectRoot))
        {
            report.Validation.Add(ValidationSeverity.Error, "DUPLICATE_MODEL_ENFORCEMENT", issue);
        }

        logger.Write(LogSeverity.Information, "CoreStartupDiagnostics", report.Passed ? "Milestone 1 startup diagnostics passed." : "Milestone 1 startup diagnostics found issues.");
        return report;
    }
}
