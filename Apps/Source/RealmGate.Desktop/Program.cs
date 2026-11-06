using Avalonia;
using System.Net;
using System.Net.Sockets;
using RealmGate.Desktop.Services;
using RealmGate.Server;

namespace RealmGate.Desktop;

internal static class Program
{
    [STAThread]
    public static async Task<int> Main(string[] args)
    {
        if (args.Contains("--smoke-creator-test-realm", StringComparer.OrdinalIgnoreCase))
            return await RunCreatorTestRealmSmokeAsync();

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect();

    private static async Task<int> RunCreatorTestRealmSmokeAsync()
    {
        var projectRoot = Directory.GetCurrentDirectory();
        var port = ReserveAvailablePortPair();
        await using var host = new EmbeddedServerHostController();
        host.Log += message => Console.WriteLine($"host: {message}");

        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            await host.StartAsync(new DesktopHostOptions(
                projectRoot,
                RealmGateNetworkBinding.LocalOnlyAddress,
                port,
                MaximumSessions: 4,
                TestRealmSourceWorldId: "Custom1"), timeout.Token);

            if (!host.IsRunning)
                throw new InvalidOperationException("The embedded Test Realm host did not remain running.");

            var resolved = ServerDataRootResolver.Resolve(projectRoot, "Custom1-Test");
            if (!resolved.DataRoot.EndsWith(
                    Path.Combine("Data", "Custom1-Test"),
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"The Test Realm resolved an unexpected data root: {resolved.DataRoot}");
            }

            if (!ServerDataRootResolver.ResolveDataWorldId(projectRoot, "Custom1-Test")
                    .Equals("Custom1", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The Test Realm lost its generated Custom1 data authority identity.");
            }

            var saveRoot = RuntimeSavePaths.RealmRoot(projectRoot, "Custom1-Test");
            if (!RuntimeIdentity.Normalize(saveRoot, "save").Contains("custom1-test", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"The Test Realm save root is not isolated: {saveRoot}");

            await host.StopAsync(CancellationToken.None);
            if (host.IsRunning)
                throw new InvalidOperationException("The embedded Test Realm host did not stop cleanly.");

            Console.WriteLine($"PASS creator-test-realm: port={port}; data={resolved.DataRoot}; saves={saveRoot}");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"FAIL creator-test-realm: {exception}");
            return 1;
        }
    }

    private static int ReserveAvailablePortPair()
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var candidate = new TcpListener(IPAddress.Loopback, 0);
            candidate.Start();
            var port = ((IPEndPoint)candidate.LocalEndpoint).Port;
            candidate.Stop();
            if (port >= 65535) continue;

            TcpListener? telnet = null;
            TcpListener? clientData = null;
            try
            {
                telnet = new TcpListener(IPAddress.Loopback, port);
                clientData = new TcpListener(IPAddress.Loopback, port + 1);
                telnet.Start();
                clientData.Start();
                return port;
            }
            catch (SocketException)
            {
            }
            finally
            {
                clientData?.Stop();
                telnet?.Stop();
            }
        }

        throw new InvalidOperationException("A free Telnet/client-data port pair could not be reserved for the Test Realm smoke.");
    }
}
