using Reforged.Core.Configuration;
using Reforged.Core.Validation;

namespace Reforged.Server;

/// <summary>
/// Loads the non-gameplay Server host configuration surface used by bootstrap diagnostics.
/// </summary>
public sealed class ServerHostConfigurationAudit
{
    public ServerHostConfigurationReport Run(string projectRoot, ValidationResult validation)
    {
        var root = Path.GetFullPath(projectRoot);
        var configPath = Path.Combine(root, "Config", "server.host.json");
        var configuration = new RuntimeConfigurationProvider()
            .AddDefault("server:host:name", "Reforged Local Host")
            .AddDefault("server:host:bindAddress", "127.0.0.1")
            .AddDefault("server:host:port", "4000")
            .AddDefault("server:host:maxSessions", "64")
            .AddDefault("server:host:worldTickMilliseconds", "1000")
            .AddDefault("server:host:combatTickMilliseconds", "5000")
            .AddDefault("server:host:gameplayEnabled", "true")
            .AddDefault("server:host:networkingEnabled", "true")
            .AddDefault("server:host:worldSimulationEnabled", "true")
            .LoadJsonFile(configPath, optional: true)
            .LoadEnvironment("REFORGED_");

        var report = new ServerHostConfigurationReport
        {
            ProjectRoot = root,
            ConfigurationPath = configPath,
            ConfigurationFileExists = File.Exists(configPath),
            HostName = configuration.GetString("server:host:name", "Reforged Local Host"),
            BindAddress = configuration.GetString("server:host:bindAddress", "127.0.0.1"),
            Port = configuration.GetInt32("server:host:port", 4000),
            MaxSessions = configuration.GetInt32("server:host:maxSessions", 64),
            WorldTickMilliseconds = configuration.GetInt32("server:host:worldTickMilliseconds", 1000),
            CombatTickMilliseconds = configuration.GetInt32("server:host:combatTickMilliseconds", 5000),
            GameplayEnabled = configuration.GetBoolean("server:host:gameplayEnabled", true),
            NetworkingEnabled = configuration.GetBoolean("server:host:networkingEnabled", true),
            WorldSimulationEnabled = configuration.GetBoolean("server:host:worldSimulationEnabled", true)
        };

        Validate(report, validation);
        return report;
    }

    private static void Validate(ServerHostConfigurationReport report, ValidationResult validation)
    {
        if (string.IsNullOrWhiteSpace(report.HostName))
        {
            validation.Add(ValidationSeverity.Error, "SERVER_HOST_NAME_EMPTY", "Server host name must not be empty.", report.ConfigurationPath, "server-host");
        }

        if (report.Port is <= 0 or > 65535)
        {
            validation.Add(ValidationSeverity.Error, "SERVER_HOST_PORT_INVALID", "Server host port must be between 1 and 65535.", report.ConfigurationPath, "server-host");
        }

        if (report.MaxSessions <= 0)
        {
            validation.Add(ValidationSeverity.Error, "SERVER_HOST_MAX_SESSIONS_INVALID", "Server max sessions must be greater than zero.", report.ConfigurationPath, "server-host");
        }

        if (report.WorldTickMilliseconds <= 0)
        {
            validation.Add(ValidationSeverity.Error, "SERVER_HOST_WORLD_TICK_INVALID", "Server world tick milliseconds must be greater than zero.", report.ConfigurationPath, "server-host");
        }

        if (report.CombatTickMilliseconds <= 0)
        {
            validation.Add(ValidationSeverity.Error, "SERVER_HOST_COMBAT_TICK_INVALID", "Server combat tick milliseconds must be greater than zero.", report.ConfigurationPath, "server-host");
        }

        var enabledCount = new[] { report.GameplayEnabled, report.NetworkingEnabled, report.WorldSimulationEnabled }.Count(value => value);
        if (enabledCount is > 0 and < 3)
        {
            validation.Add(ValidationSeverity.Error, "SERVER_HOST_PARTIALLY_ENABLED", "Gameplay, networking, and world simulation must be enabled or disabled as one host lifecycle boundary.", report.ConfigurationPath, "server-host");
        }
    }
}
