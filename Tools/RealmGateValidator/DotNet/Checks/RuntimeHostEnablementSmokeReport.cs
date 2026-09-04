using System.Text.Json;
namespace RealmGate.Server;
public sealed class RuntimeHostEnablementSmokeReport
{
    public bool Passed => Failures.Count == 0;
    public List<string> Checks { get; } = new();
    public List<string> Failures { get; } = new();
    public static RuntimeHostEnablementSmokeReport Run(string projectRoot)
    {
        var result = new RuntimeHostEnablementSmokeReport();
        var bootstrap = new ServerBootstrap().Run(projectRoot);
        void Check(bool ok, string name) { if (ok) result.Checks.Add(name); else result.Failures.Add(name); }
        Check(bootstrap.HostConfiguration?.GameplayEnabled == true, "gameplay-enabled");
        Check(bootstrap.HostConfiguration?.NetworkingEnabled == true, "networking-enabled");
        Check(bootstrap.HostConfiguration?.WorldSimulationEnabled == true, "world-simulation-enabled");
        Check(bootstrap.HostLifecycle?.Status == "runtime-enabled", "host-lifecycle-enabled");
        Check(bootstrap.SchedulerBootstrap?.DiagnosticOnly == false, "scheduler-not-diagnostic-only");
        Check(bootstrap.SchedulerBootstrap?.RuntimeEnabledChannelCount == bootstrap.SchedulerBootstrap?.ChannelCount, "scheduler-channels-enabled");
        var options = ServerRuntimeOptions.Load(projectRoot);
        Check(options.Network.Enabled, "telnet-network-enabled");
        var host = new ServerRuntimeHost(projectRoot, options); host.Bootstrap();
        Check(host.Worlds.Count > 0, "world-bootstrap");
        Check(host.State is not null && host.Events is not null, "authoritative-runtime-state");
        return result;
    }
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}
