namespace Reforged.Server;

/// <summary>
/// Registers scheduler channels for diagnostics without starting scheduler execution.
/// </summary>
public sealed class ServerSchedulerBootstrapAudit
{
    private static readonly SchedulerChannelRegistration[] PlannedChannels =
    {
        new("Ingress", 10, "Coordinate future external and internal work intake.", false, "ServerSim0036", "Planning registration only; no work intake queue executes."),
        new("Validation", 20, "Coordinate future pre-execution validation and gating.", false, "ServerSim0036", "Planning registration only; no validation queue executes."),
        new("Planning", 30, "Coordinate future work planning and ordering before execution.", false, "ServerSim0036", "Planning registration only; no planning queue executes."),
        new("Dispatch", 40, "Coordinate future dispatch into registered engine systems.", false, "ServerSim0036", "Planning registration only; no dispatch queue executes."),
        new("Mutation", 50, "Coordinate future state mutation boundaries.", false, "ServerSim0036", "Planning registration only; no state mutation queue executes."),
        new("TimedWork", 60, "Coordinate future time-based work without naming gameplay systems.", false, "ServerSim0036", "Planning registration only; no timed work executes."),
        new("EventWork", 70, "Coordinate future event-driven work without naming game-specific events.", false, "ServerSim0036", "Planning registration only; no event work executes."),
        new("Persistence", 80, "Coordinate future persistence checkpoints and durable state work.", false, "ServerSim0036", "Planning registration only; no persistence queue executes."),
        new("Observation", 90, "Coordinate future diagnostics, tracing, and observer output.", false, "ServerSim0036", "Planning registration only; no observer queue executes."),
        new("Maintenance", 100, "Coordinate future lifecycle maintenance work.", false, "ServerSim0036", "Planning registration only; no maintenance jobs execute.")
    };

    public ServerSchedulerBootstrapReport Run(string projectRoot, ServerHostLifecycleReport hostLifecycle)
    {
        var report = new ServerSchedulerBootstrapReport
        {
            ProjectRoot = Path.GetFullPath(projectRoot),
            HostLifecycleReady = hostLifecycle.ReadyForRuntimePlanning,
            RuntimeDisabled = hostLifecycle.RuntimeDisabled &&
                !hostLifecycle.GameplayEnabled &&
                !hostLifecycle.NetworkingEnabled &&
                !hostLifecycle.WorldSimulationEnabled,
            DiagnosticOnly = true
        };

        foreach (var channel in PlannedChannels.OrderBy(channel => channel.Order))
        {
            report.AddChannel(channel);
        }

        return report;
    }
}
