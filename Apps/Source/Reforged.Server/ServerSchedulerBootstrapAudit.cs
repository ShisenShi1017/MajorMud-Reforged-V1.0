namespace Reforged.Server;

/// <summary>
/// Registers scheduler channels for diagnostics without starting scheduler execution.
/// </summary>
public sealed class ServerSchedulerBootstrapAudit
{
    private static readonly SchedulerChannelRegistration[] PlannedChannels =
    {
        new("PlayerState", 10, "Coordinate concurrent player activity and condition channels.", false, "ServerSim0036", "Planning registration only; no player state transitions execute."),
        new("Movement", 20, "Coordinate movement, follow timing, and room transition scheduling.", false, "ServerSim0036", "Planning registration only; no movement queue executes."),
        new("CombatRound", 30, "Coordinate future combat engagement, action batches, damage, death, XP, and continuation checks.", false, "ServerSim0036", "Planning registration only; no combat actions execute."),
        new("RegenHp", 40, "Coordinate future hit point recovery cadence.", false, "ServerSim0036", "Planning registration only; no HP recovery ticks execute."),
        new("RegenManaKai", 50, "Coordinate future mana and kai recovery cadence.", false, "ServerSim0036", "Planning registration only; no mana or kai recovery ticks execute."),
        new("Spawn", 60, "Coordinate future timed, instant, boss, quest, scripted, and event spawn policies.", false, "ServerSim0036", "Planning registration only; no spawn evaluation executes."),
        new("Cleanup", 70, "Coordinate future cleanup and lifecycle maintenance work.", false, "ServerSim0036", "Planning registration only; no cleanup jobs execute."),
        new("StatusEffect", 80, "Coordinate future condition and effect duration handling.", false, "ServerSim0036", "Planning registration only; no status effect timers execute."),
        new("ScriptEvent", 90, "Coordinate future textblock, trigger, quest, and script event dispatch.", false, "ServerSim0036", "Planning registration only; no script or event dispatch executes."),
        new("AI", 100, "Coordinate future AI and NPC decision scheduling.", false, "ServerSim0036", "Planning registration only; no AI decisions execute.")
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
