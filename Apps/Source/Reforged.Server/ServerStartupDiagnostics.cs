using Reforged.Core.Applications;
using Reforged.Core.Configuration;
using Reforged.Core.Diagnostics;
using Reforged.Core.Events;
using Reforged.Core.Logging;
using Reforged.Core.Services;
using Reforged.Core.Validation;

namespace Reforged.Server;

/// <summary>
/// Runs the first Milestone 2 Server startup checks without starting gameplay systems.
/// </summary>
public sealed class ServerStartupDiagnostics
{
    public ServerStartupDiagnosticReport Run(string projectRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

        var report = new ServerStartupDiagnosticReport();
        var application = new ServerApplication();
        var logger = new InMemoryReforgedLogger();
        var events = new InMemoryEventBus();
        var services = new ServiceRegistry();
        var configuration = new RuntimeConfigurationProvider()
            .AddDefault("runtime:root", projectRoot)
            .AddDefault("runtime:data", ServerDataRootResolver.ResolveDefault(projectRoot).DataRoot)
            .AddDefault("server:role", ApplicationRole.Server.ToString())
            .AddDefault("server:diagnostics", "startup-only");

        report.AddCheck("server-identity");
        if (application.Identity.Role != ApplicationRole.Server)
        {
            report.Validation.Add(ValidationSeverity.Error, "SERVER_IDENTITY_ROLE", "Server application identity is not registered as the Server role.");
        }

        report.AddCheck("server-service-registry");
        services.Register<IReforgedLogger>(logger);
        services.Register<IEventPublisher>(events);
        services.Register(configuration);
        services.Register(application);
        _ = services.ResolveRequired<IReforgedLogger>();
        _ = services.ResolveRequired<IEventPublisher>();
        _ = services.ResolveRequired<RuntimeConfigurationProvider>();
        _ = services.ResolveRequired<ServerApplication>();

        report.AddCheck("server-configuration");
        if (configuration.GetString("server:role") != "Server" || configuration.GetString("server:diagnostics") != "startup-only")
        {
            report.Validation.Add(ValidationSeverity.Error, "SERVER_CONFIGURATION_DEFAULTS", "Server startup configuration defaults did not resolve.");
        }

        report.AddCheck("server-event-system");
        var received = 0;
        using (events.Subscribe<EventPublishedEvent>(_ => received++))
        {
            events.Publish(new EventPublishedEvent("server-startup-diagnostics", "Server startup diagnostics event check", DateTimeOffset.UtcNow));
        }

        if (received != 1 || events.SubscriberCount != 0)
        {
            report.Validation.Add(ValidationSeverity.Error, "SERVER_EVENT_SYSTEM", "Server startup event bus check did not publish and dispose subscriptions cleanly.");
        }

        report.AddCheck("core-startup-diagnostics");
        var configuredDataRoot = ServerDataRootResolver.ResolveDefault(projectRoot).DataRoot;
        var coreReport = new CoreStartupDiagnostics().Run(projectRoot, configuredDataRoot);
        report.AttachCoreReport(coreReport);

        logger.Write(
            report.Passed ? LogSeverity.Information : LogSeverity.Error,
            "ServerStartupDiagnostics",
            report.Passed ? "Server startup diagnostics passed." : "Server startup diagnostics found issues.");
        report.AttachLogEntries(logger.Entries);

        return report;
    }
}
