namespace Reforged.Server;

/// <summary>
/// Read-only lifecycle state for the non-gameplay Server host path.
/// </summary>
public sealed class ServerHostLifecycleReport
{
    private readonly List<HostLifecycleStage> stages = new();

    public string? ProjectRoot { get; internal set; }

    public IReadOnlyList<HostLifecycleStage> Stages => stages;

    public bool Initialized { get; internal set; }

    public bool Configured { get; internal set; }

    public bool DataReady { get; internal set; }

    public bool WorldProjectionReady { get; internal set; }

    public bool ModuleReady { get; internal set; }

    public bool RuntimeDisabled { get; internal set; }

    public bool GameplayEnabled { get; internal set; }

    public bool NetworkingEnabled { get; internal set; }

    public bool WorldSimulationEnabled { get; internal set; }

    public bool ReadyForRuntimePlanning =>
        Initialized && Configured && DataReady && WorldProjectionReady && ModuleReady &&
        GameplayEnabled && NetworkingEnabled && WorldSimulationEnabled && !RuntimeDisabled;

    public string Status
    {
        get
        {
            if (GameplayEnabled || NetworkingEnabled || WorldSimulationEnabled)
            {
                return "runtime-enabled";
            }

            return ReadyForRuntimePlanning ? "runtime-enabled" : "readiness-has-gaps";
        }
    }

    internal void AddStage(HostLifecycleStage stage) => stages.Add(stage);
}

public sealed record HostLifecycleStage(
    string Name,
    bool Ready,
    string Status,
    string Note);
