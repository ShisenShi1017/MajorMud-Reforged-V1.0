using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace RealmGate.Server;

public sealed class RuntimeNetworkBindingSmokeReport
{
    public bool Passed => Failures.Count == 0;
    public List<string> Checks { get; } = new();
    public List<string> Failures { get; } = new();

    public static RuntimeNetworkBindingSmokeReport Run()
    {
        var report = new RuntimeNetworkBindingSmokeReport();
        void Require(bool condition, string name)
        {
            if (condition) report.Checks.Add(name);
            else report.Failures.Add(name);
        }

        Require(new NetworkRuntimeOptions().BindAddress == RealmGateNetworkBinding.AllIpv4InterfacesAddress,
            "default-bind-all-ipv4");
        Require(new NetworkRuntimeOptions().TelnetPort == RealmGateNetworkBinding.DefaultTelnetPort,
            "default-telnet-port-2323");
        Require(new NetworkRuntimeOptions().ClientDataPort == RealmGateNetworkBinding.DefaultClientDataPort,
            "default-client-data-port-2324");

        var lanAddress = RealmGateNetworkBinding.DiscoverLanIpv4Addresses().FirstOrDefault();

        var localPort = GetFreePort(IPAddress.Loopback);
        var localListener = RealmGateNetworkBinding.StartListener(RealmGateNetworkBinding.LocalOnlyAddress, localPort);
        try
        {
            var endpoint = (IPEndPoint)localListener.LocalEndpoint;
            Require(IPAddress.IsLoopback(endpoint.Address), "local-only-listener-address");
            Require(CanConnect(IPAddress.Loopback, localPort), "local-only-loopback-connects");
            if (lanAddress is not null)
                Require(!CanConnect(lanAddress, localPort), "local-only-lan-address-rejected");
            else
                report.Checks.Add("local-only-lan-address-rejected-skipped-no-lan-interface");
        }
        finally
        {
            localListener.Stop();
        }

        var allPort = GetFreePort(IPAddress.Loopback);
        var allListener = RealmGateNetworkBinding.StartListener(RealmGateNetworkBinding.AllIpv4InterfacesAddress, allPort);
        try
        {
            var endpoint = (IPEndPoint)allListener.LocalEndpoint;
            Require(endpoint.Address.Equals(IPAddress.Any), "all-interfaces-listener-address");
            Require(CanConnect(IPAddress.Loopback, allPort), "all-interfaces-loopback-connects");
            if (lanAddress is not null)
                Require(CanConnect(lanAddress, allPort), "all-interfaces-lan-connects");
            else
                report.Checks.Add("all-interfaces-lan-connects-skipped-no-lan-interface");
        }
        finally
        {
            allListener.Stop();
        }

        if (lanAddress is not null)
        {
            var specificPort = GetFreePort(lanAddress);
            var specificListener = RealmGateNetworkBinding.StartListener(lanAddress.ToString(), specificPort);
            try
            {
                var endpoint = (IPEndPoint)specificListener.LocalEndpoint;
                Require(endpoint.Address.Equals(lanAddress), "specific-interface-listener-address");
                Require(CanConnect(lanAddress, specificPort), "specific-interface-connects");
            }
            finally
            {
                specificListener.Stop();
            }
        }
        else
        {
            report.Checks.Add("specific-interface-listener-address-skipped-no-lan-interface");
            report.Checks.Add("specific-interface-connects-skipped-no-lan-interface");
        }

        try
        {
            _ = RealmGateNetworkBinding.ResolveIpv4Address("not-a-valid-ip");
            report.Failures.Add("invalid-address-clean-error");
        }
        catch (InvalidOperationException exception)
        {
            Require(exception.Message.Contains("not-a-valid-ip", StringComparison.Ordinal), "invalid-address-clean-error");
        }

        var occupiedPort = GetFreePort(IPAddress.Loopback);
        var occupied = RealmGateNetworkBinding.StartListener(RealmGateNetworkBinding.LocalOnlyAddress, occupiedPort);
        try
        {
            try
            {
                var duplicate = RealmGateNetworkBinding.StartListener(RealmGateNetworkBinding.LocalOnlyAddress, occupiedPort);
                duplicate.Stop();
                report.Failures.Add("occupied-port-clean-error");
            }
            catch (RealmGateNetworkBindException exception)
            {
                Require(exception.RequestedBindAddress == RealmGateNetworkBinding.LocalOnlyAddress
                    && exception.RequestedPort == occupiedPort
                    && exception.Message.Contains("Address:", StringComparison.Ordinal)
                    && exception.Message.Contains("Port:", StringComparison.Ordinal)
                    && exception.Message.Contains("Socket error:", StringComparison.Ordinal),
                    "occupied-port-clean-error");
            }
        }
        finally
        {
            occupied.Stop();
        }

        var restartPort = GetFreePort(IPAddress.Loopback);
        var first = RealmGateNetworkBinding.StartListener(RealmGateNetworkBinding.LocalOnlyAddress, restartPort);
        first.Stop();
        var second = RealmGateNetworkBinding.StartListener(RealmGateNetworkBinding.LocalOnlyAddress, restartPort);
        try
        {
            Require(CanConnect(IPAddress.Loopback, restartPort), "restart-rebind-same-port");
        }
        finally
        {
            second.Stop();
        }

        var shutdownProbe = RealmGateNetworkBinding.StartListener(
            RealmGateNetworkBinding.LocalOnlyAddress,
            restartPort);
        shutdownProbe.Stop();
        Require(!CanConnect(IPAddress.Loopback, restartPort), "shutdown-closes-listener");

        return report;
    }

    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

    private static int GetFreePort(IPAddress address)
    {
        var listener = new TcpListener(address, 0);
        listener.Start();
        try { return ((IPEndPoint)listener.LocalEndpoint).Port; }
        finally { listener.Stop(); }
    }

    private static bool CanConnect(IPAddress address, int port)
    {
        try
        {
            using var client = new TcpClient(AddressFamily.InterNetwork);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            client.ConnectAsync(address, port, timeout.Token).GetAwaiter().GetResult();
            return client.Connected;
        }
        catch (Exception exception) when (exception is SocketException or OperationCanceledException)
        {
            return false;
        }
    }
}
