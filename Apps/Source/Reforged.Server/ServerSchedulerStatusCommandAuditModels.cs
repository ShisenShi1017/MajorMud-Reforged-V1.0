namespace Reforged.Server;

/// <summary>
/// Read-only status command catalog for scheduler and host diagnostics.
/// </summary>
public sealed class ServerSchedulerStatusCommandReport
{
    private readonly List<SchedulerStatusCommandRegistration> commands = new();

    public string? ProjectRoot { get; internal set; }

    public IReadOnlyList<SchedulerStatusCommandRegistration> Commands => commands;

    public bool SchedulerBootstrapReady { get; internal set; }

    public bool RuntimeDisabled { get; internal set; }

    public bool DiagnosticOnly { get; internal set; }

    public int CommandCount => commands.Count;

    public int RuntimeEnabledCommandCount => commands.Count(command => command.RuntimeEnabled);

    public int RuntimeDisabledCommandCount => commands.Count(command => !command.RuntimeEnabled);

    public int StateMutatingCommandCount => commands.Count(command => command.MutatesState);

    public int GuardedCommandCount => commands.Count(command => command.Guarded);

    public bool ReadyForInspection =>
        SchedulerBootstrapReady &&
        RuntimeDisabled &&
        DiagnosticOnly &&
        CommandCount > 0 &&
        RuntimeEnabledCommandCount == 0 &&
        StateMutatingCommandCount == 0 &&
        GuardedCommandCount == CommandCount;

    public string Status
    {
        get
        {
            if (RuntimeEnabledCommandCount > 0 || StateMutatingCommandCount > 0)
            {
                return "scheduler-status-command-runtime-enabled";
            }

            return ReadyForInspection ? "scheduler-status-commands-registered-runtime-disabled" : "scheduler-status-command-surface-has-gaps";
        }
    }

    internal void AddCommand(SchedulerStatusCommandRegistration command) => commands.Add(command);
}

public sealed record SchedulerStatusCommandRegistration(
    string Name,
    int Order,
    string Category,
    string Description,
    bool RuntimeEnabled,
    bool Guarded,
    bool MutatesState,
    string OutputContract,
    string ReadinessNote);
