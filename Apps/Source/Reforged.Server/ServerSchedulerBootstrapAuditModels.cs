namespace Reforged.Server;

/// <summary>
/// Read-only scheduler bootstrap surface for future runtime systems.
/// </summary>
public sealed class ServerSchedulerBootstrapReport
{
    private readonly List<SchedulerChannelRegistration> channels = new();

    public string? ProjectRoot { get; internal set; }

    public IReadOnlyList<SchedulerChannelRegistration> Channels => channels;

    public bool HostLifecycleReady { get; internal set; }

    public bool RuntimeDisabled { get; internal set; }

    public bool DiagnosticOnly { get; internal set; }

    public int ChannelCount => channels.Count;

    public int RuntimeEnabledChannelCount => channels.Count(channel => channel.RuntimeEnabled);

    public int RuntimeDisabledChannelCount => channels.Count(channel => !channel.RuntimeEnabled);

    public bool ReadyForPlanning =>
        HostLifecycleReady &&
        RuntimeDisabled &&
        DiagnosticOnly &&
        ChannelCount > 0 &&
        RuntimeEnabledChannelCount == 0;

    public string Status
    {
        get
        {
            if (RuntimeEnabledChannelCount > 0)
            {
                return "scheduler-runtime-enabled";
            }

            return ReadyForPlanning ? "scheduler-bootstrap-registered-runtime-disabled" : "scheduler-bootstrap-has-gaps";
        }
    }

    internal void AddChannel(SchedulerChannelRegistration channel) => channels.Add(channel);
}

public sealed record SchedulerChannelRegistration(
    string Name,
    int Order,
    string Role,
    bool RuntimeEnabled,
    string EvidenceSource,
    string ReadinessNote);
