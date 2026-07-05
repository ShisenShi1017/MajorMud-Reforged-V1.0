namespace Reforged.Server;

/// <summary>
/// Registers guarded scheduler/status commands without executing runtime behavior.
/// </summary>
public sealed class ServerSchedulerStatusCommandAudit
{
    private static readonly SchedulerStatusCommandRegistration[] PlannedCommands =
    {
        new("status.overview", 10, "host", "Summarize host readiness, manifest identity, validation counts, and disabled runtime state.", false, true, false, "ServerBootstrapReport summary fields", "Registered for inspection only; no command dispatcher executes."),
        new("status.lifecycle", 20, "host", "List host lifecycle stages and readiness notes.", false, true, false, "ServerHostLifecycleReport stages", "Registered for inspection only; no lifecycle transition executes."),
        new("status.modules", 30, "modules", "List registered modules, rule profiles, and disabled gameplay/runtime flags.", false, true, false, "ServerModuleReadinessReport modules", "Registered for inspection only; no module or rule profile activates."),
        new("status.scheduler", 40, "scheduler", "Summarize scheduler bootstrap readiness and runtime-disabled state.", false, true, false, "ServerSchedulerBootstrapReport summary fields", "Registered for inspection only; no scheduler tick or queue executes."),
        new("status.scheduler.channels", 50, "scheduler", "List scheduler channels, evidence source, roles, and disabled runtime flags.", false, true, false, "ServerSchedulerBootstrapReport channels", "Registered for inspection only; no channel executes."),
        new("status.data", 60, "data", "Summarize canonical data catalog readiness and runtime blockers.", false, true, false, "ServerDataCatalogAuditReport categories", "Registered for inspection only; no Data files are rewritten."),
        new("status.validation", 70, "diagnostics", "Summarize validation issue counts and startup diagnostic readiness.", false, true, false, "Validation issue counts and startup diagnostics", "Registered for inspection only; no repair or mutation runs.")
    };

    public ServerSchedulerStatusCommandReport Run(string projectRoot, ServerSchedulerBootstrapReport schedulerBootstrap)
    {
        var report = new ServerSchedulerStatusCommandReport
        {
            ProjectRoot = Path.GetFullPath(projectRoot),
            SchedulerBootstrapReady = schedulerBootstrap.ReadyForPlanning,
            RuntimeDisabled = schedulerBootstrap.RuntimeDisabled &&
                schedulerBootstrap.RuntimeEnabledChannelCount == 0,
            DiagnosticOnly = schedulerBootstrap.DiagnosticOnly
        };

        foreach (var command in PlannedCommands.OrderBy(command => command.Order))
        {
            report.AddCommand(command);
        }

        return report;
    }
}
