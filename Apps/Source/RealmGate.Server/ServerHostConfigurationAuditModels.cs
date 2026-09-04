namespace RealmGate.Server;

/// <summary>
/// Read-only non-gameplay Server host configuration summary.
/// </summary>
public sealed class ServerHostConfigurationReport
{
    public string? ProjectRoot { get; internal set; }

    public string? ConfigurationPath { get; internal set; }

    public bool ConfigurationFileExists { get; internal set; }

    public string HostName { get; internal set; } = string.Empty;

    public string BindAddress { get; internal set; } = string.Empty;

    public int Port { get; internal set; }

    public int MaxSessions { get; internal set; }

    public int WorldTickMilliseconds { get; internal set; }

    public int CombatTickMilliseconds { get; internal set; }

    public bool GameplayEnabled { get; internal set; }

    public bool NetworkingEnabled { get; internal set; }

    public bool WorldSimulationEnabled { get; internal set; }

    public bool RuntimeDisabled => !GameplayEnabled && !NetworkingEnabled && !WorldSimulationEnabled;

    public string Status => RuntimeDisabled ? "configured-runtime-disabled" : "runtime-enabled";
}
