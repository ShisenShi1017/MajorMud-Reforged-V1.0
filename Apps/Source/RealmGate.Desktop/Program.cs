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
        if (args.Contains("--smoke-packaged-host-first-run", StringComparer.OrdinalIgnoreCase))
            return await RunPackagedHostFirstRunSmokeAsync();

        if (args.Contains("--smoke-creator-test-realm", StringComparer.OrdinalIgnoreCase))
            return await RunCreatorTestRealmSmokeAsync();

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect();

    private static async Task<int> RunPackagedHostFirstRunSmokeAsync()
    {
        var projectRoot = Directory.GetCurrentDirectory();
        var classicRoot = Path.Combine(projectRoot, "Data", "Classic");
        if (Directory.Exists(classicRoot))
        {
            Console.Error.WriteLine("FAIL packaged-host-first-run: Data/Classic already exists; this smoke requires a clean package root.");
            return 1;
        }

        var port = ReserveAvailablePortPair();
        await using var host = new EmbeddedServerHostController();
        host.Log += message => Console.WriteLine($"host: {message}");

        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            await host.StartAsync(new DesktopHostOptions(
                projectRoot,
                RealmGateNetworkBinding.LocalOnlyAddress,
                port,
                MaximumSessions: 4,
                WorldId: "classic"), timeout.Token);

            if (!host.IsRunning)
                throw new InvalidOperationException("The embedded Host did not remain running.");

            var configured = ServerRuntimeOptions.Load(projectRoot);
            foreach (var world in configured.Worlds.Where(world => world.RuntimeEnabled))
            {
                var generated = ServerDataRootResolver.Resolve(projectRoot, world.Id).DataRoot;
                foreach (var required in new[] { world.RoomsFile, "world_manifest.json", "compile_manifest.json" })
                {
                    if (!File.Exists(Path.Combine(generated, required)))
                        throw new InvalidOperationException($"Generated world '{world.Id}' is missing '{required}'.");
                }
            }

            await host.StopAsync(CancellationToken.None);
            if (host.IsRunning)
                throw new InvalidOperationException("The embedded Host did not stop cleanly.");

            Console.WriteLine($"PASS packaged-host-first-run: port={port}; worlds={configured.Worlds.Count(world => world.RuntimeEnabled)}");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"FAIL packaged-host-first-run: {exception}");
            return 1;
        }
    }

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
        var candidates = new[] { 2323, 4000, 12000, 14000, 18000, 24000, 32000, 40000, 44000 }
            .Concat(Enumerable.Range(0, 50).Select(_ => Random.Shared.Next(10000, 45000)))
            .Select(port => port % 2 == 0 ? port : port - 1)
            .Distinct();

        foreach (var port in candidates)
        {
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
