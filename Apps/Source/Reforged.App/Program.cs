using System.IO;
using Reforged.App.Models;
using Reforged.App.Services;

namespace Reforged.App;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            if (args.Any(a => a.StartsWith("--smoke-", StringComparison.OrdinalIgnoreCase)))
            {
                return RunSmokeAsync(args).GetAwaiter().GetResult();
            }

            var app = new App();
            app.InitializeComponent();
            return app.Run();
        }
        catch (Exception exception)
        {
            WriteBootstrapFailure(exception);
            return 91;
        }
    }

    private static async Task<int> RunSmokeAsync(string[] args)
    {
        var smokeRoot = Path.Combine(
            Path.GetTempPath(),
            "MajorMudReforged0139",
            $"SmokeData-{Environment.ProcessId}");

        try
        {
            if (Directory.Exists(smokeRoot))
            {
                Directory.Delete(smokeRoot, recursive: true);
            }

            var settings = new ApplicationSettingsService(smokeRoot);
            await settings.LoadAsync();

            var logger = new AppLogger(settings.RootPath);
            var errors = new ErrorReporter(logger);
            var host = new ReforgedHostController(logger);
            var cleanup = new HostCleanupScheduler(host, settings, logger);
            var layouts = new LayoutPersistenceService(settings);
            var manager = new WorkspaceManager(errors);

            RegisterSmokeWorkspaces(manager);

            var services = new ServiceRegistry()
                .Add<IApplicationSettingsService>(settings)
                .Add<IAppLogger>(logger)
                .Add<IErrorReporter>(errors)
                .Add<IReforgedHostController>(host)
                .Add<IHostCleanupScheduler>(cleanup)
                .Add<ILayoutPersistenceService>(layouts)
                .Add<IWorkspaceManager>(manager);

            try
            {
                return await SmokeRunner.TryRunAsync(args, services) ?? 89;
            }
            finally
            {
                using var shutdownCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                try
                {
                    await host.StopAsync(shutdownCts.Token);
                }
                catch (Exception exception)
                {
                    logger.Error("Smoke Host shutdown failed.", exception);
                }

                await cleanup.DisposeAsync();
            }
        }
        catch (Exception exception)
        {
            WriteSmokeFailure(smokeRoot, exception);
            return 90;
        }
        finally
        {
            try
            {
                if (Directory.Exists(smokeRoot))
                {
                    Directory.Delete(smokeRoot, recursive: true);
                }
            }
            catch
            {
                // A smoke result must not be replaced by temporary-folder cleanup failure.
            }
        }
    }

    private static void RegisterSmokeWorkspaces(IWorkspaceManager manager)
    {
        manager.Register(new Workspace("home", "Dashboard", new object()));
        manager.Register(new Workspace("play", "Play", new object(), true));
        manager.Register(new Workspace("host", "Host", new object()));
        manager.Register(new Workspace("editor", "Editor", new object(), true));
        manager.Register(new Workspace("librarium", "Librarium", new object(), true));
        manager.Register(new Workspace("characters", "Characters", new object(), true));
        manager.Register(new Workspace("server", "Server", new object(), true));
        manager.Register(new Workspace("settings", "Settings", new object()));
        manager.Register(new Workspace("tools", "Tools", new object(), true));
        manager.Register(new Workspace("diagnostics", "Diagnostics", new object(), true));
    }

    private static void WriteSmokeFailure(string root, Exception exception)
    {
        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "smoke-error.log"), exception.ToString());
        }
        catch
        {
        }
    }

    private static void WriteBootstrapFailure(Exception exception)
    {
        try
        {
            var root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MajorMUD Reforged",
                "Logs");
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "startup-failure.log"), exception.ToString());
        }
        catch
        {
        }
    }
}
