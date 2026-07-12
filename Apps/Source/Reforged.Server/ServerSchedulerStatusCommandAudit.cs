namespace Reforged.Server;

/// <summary>
/// Registers guarded scheduler/status commands without executing runtime behavior.
/// </summary>
public sealed class ServerSchedulerStatusCommandAudit
{
    private static readonly SchedulerStatusCommandRegistration[] PlannedCommands =
    {
        new("status.overview", 10, "host", "Summarize host readiness, manifest identity, validation counts, and enabled runtime state.", true, false, false, "ServerBootstrapReport summary fields", "Runtime-enabled read-only status command."),
        new("status.lifecycle", 20, "host", "List host lifecycle stages and readiness notes.", true, false, false, "ServerHostLifecycleReport stages", "Runtime-enabled read-only lifecycle status command."),
        new("status.modules", 30, "modules", "List registered modules, rule profiles, and enabled gameplay/runtime flags.", true, false, false, "ServerModuleReadinessReport modules", "Runtime-enabled read-only module status command."),
        new("status.scheduler", 40, "scheduler", "Summarize scheduler bootstrap readiness and runtime-enabled state.", true, false, false, "ServerSchedulerBootstrapReport summary fields", "Runtime-enabled read-only scheduler status command."),
        new("status.scheduler.channels", 50, "scheduler", "List scheduler channels, evidence source, roles, and runtime-enabled flags.", true, false, false, "ServerSchedulerBootstrapReport channels", "Runtime-enabled read-only scheduler channel status command."),
        new("status.data", 60, "data", "Summarize canonical data catalog readiness and runtime blockers.", true, false, false, "ServerDataCatalogAuditReport categories", "Runtime-enabled read-only data status command."),
        new("status.validation", 70, "diagnostics", "Summarize validation issue counts and startup diagnostic readiness.", true, false, false, "Validation issue counts and startup diagnostics", "Runtime-enabled read-only validation status command.")
    };

    public ServerSchedulerStatusCommandReport Run(string projectRoot, ServerSchedulerBootstrapReport schedulerBootstrap)
    {
        var report = new ServerSchedulerStatusCommandReport
        {
            ProjectRoot = Path.GetFullPath(projectRoot),
            SchedulerBootstrapReady = schedulerBootstrap.ReadyForPlanning,
            RuntimeDisabled = false,
            DiagnosticOnly = false
        };

        foreach (var command in PlannedCommands.OrderBy(command => command.Order))
        {
            report.AddCommand(command);
        }

        return report;
    }
}
