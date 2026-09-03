using System.IO;
using System.Windows;
using Reforged.App.Services;
using Reforged.App.ViewModels;
using Reforged.App.Views;

namespace Reforged.App;

/// <summary>
/// Public product startup. This composition root intentionally registers only the
/// game/client/host/editor/librarium surfaces required by the released application.
/// Developer automation and private AI systems are not compiled into this build.
/// </summary>
public partial class App : Application
{
    private ServiceRegistry? _services;
    private int _shutdownStarted;
    private readonly CancellationTokenSource _applicationLifetime = new();

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            var settings = new ApplicationSettingsService();
            await settings.LoadAsync();

            var logger = new AppLogger(settings.RootPath);
            var errors = new ErrorReporter(logger);

            DispatcherUnhandledException += (_, args) =>
            {
                errors.Report(args.Exception, "Unhandled UI exception");
                args.Handled = true;
                MessageBox.Show(
                    args.Exception.Message,
                    "RealmGate",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            };

            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
                logger.Error("Unhandled application exception", args.ExceptionObject as Exception);

            TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                logger.Error("Unobserved task exception", args.Exception);
                args.SetObserved();
            };

            var host = new ReforgedHostController(logger);
            var cleanup = new HostCleanupScheduler(host, settings, logger);
            var layouts = new LayoutPersistenceService(settings);
            var manager = new WorkspaceManager(errors);
            var hostViewModel = new HostViewModel(host, cleanup, settings);
            var dashboardViewModel = new DashboardViewModel(host, cleanup, manager, settings);
            var settingsViewModel = new SettingsViewModel(settings);
            var clientData = new ClientDataService(settings);
            var clientAutomation = new ClientAutomationService(settings);
            var clientCoordination = new ClientCoordinationService(settings);
            var megaMudPaths = new MegaMudPathService(settings);
            var clientParsing = new ClientParsingProfileService(settings);
            var clientLibrariumBridge = new ClientLibrariumBridgeService();
            var clientSysGoto = new ClientSysGotoService(settings);
            var clientMapProfiles = new ClientMapProfileService(settings);
            var clientWorkspaceProfiles = new ClientWorkspaceProfileService(settings);
            await clientParsing.LoadAsync();

            var clientSessions = new ClientSessionService(clientParsing);
            var clientDiagnostics = new ClientDiagnosticsService(settings.RootPath, clientSessions, clientParsing);
            var clientDiagnosticsViewModel = new ClientDiagnosticsViewModel(clientDiagnostics);
            var playViewModel = new PlayViewModel(
                clientData,
                clientAutomation,
                clientCoordination,
                megaMudPaths,
                clientSessions,
                clientParsing,
                clientLibrariumBridge,
                clientSysGoto,
                clientMapProfiles,
                clientWorkspaceProfiles);
            await playViewModel.InitializeAsync();

            manager.Register(new LazyWorkspace("home", "Dashboard", () => new DashboardView { DataContext = dashboardViewModel }));
            manager.Register(new LazyWorkspace("play", "Play", () => new PlayView { DataContext = playViewModel }, true));
            manager.Register(new LazyWorkspace("host", "Host", () => new HostView { DataContext = hostViewModel }));
            manager.Register(new LazyWorkspace("editor", "Editor", () => new EditorView(), true));
            manager.Register(new LazyWorkspace("librarium", "Librarium", () => new LibrariumView(), true));
            manager.Register(new LazyWorkspace("characters", "Characters", () => new CharacterView(clientSessions), true));
            manager.Register(new LazyWorkspace("server", "Server", () => new PlaceholderView("Server", "Server profiles, connections, and dedicated-host controls will expand in later bundles."), true));
            manager.Register(new LazyWorkspace("settings", "Settings", () => new SettingsView { DataContext = settingsViewModel }));
            manager.Register(new LazyWorkspace("tools", "Tools", () => new PlaceholderView("Tools", "Compiler, validation, import, export, and diagnostics launch points."), true));
            manager.Register(new LazyWorkspace("diagnostics", "Diagnostics", () => new ClientDiagnosticsView { DataContext = clientDiagnosticsViewModel }, true));

            _services = new ServiceRegistry()
                .Add<IApplicationSettingsService>(settings)
                .Add<IAppLogger>(logger)
                .Add<IErrorReporter>(errors)
                .Add<IReforgedHostController>(host)
                .Add<IHostCleanupScheduler>(cleanup)
                .Add<ILayoutPersistenceService>(layouts)
                .Add<IWorkspaceManager>(manager)
                .Add<IClientDataService>(clientData)
                .Add<IClientAutomationService>(clientAutomation)
                .Add<IClientCoordinationService>(clientCoordination)
                .Add<IMegaMudPathService>(megaMudPaths)
                .Add<IClientParsingProfileService>(clientParsing)
                .Add<IClientSessionService>(clientSessions)
                .Add<IClientLibrariumBridgeService>(clientLibrariumBridge)
                .Add<IClientSysGotoService>(clientSysGoto)
                .Add<IClientMapProfileService>(clientMapProfiles)
                .Add<IClientWorkspaceProfileService>(clientWorkspaceProfiles)
                .Add<IClientDiagnosticsService>(clientDiagnostics);

            var viewModel = new MainWindowViewModel(manager, host);
            var window = new MainWindow(viewModel, layouts, manager);
            MainWindow = window;
            await window.RestoreAsync();
            window.Show();

            if (settings.Current.AutoStartLocalHost)
            {
                try
                {
                    await host.StartAsync(new Models.HostLaunchOptions(AppContext.BaseDirectory), CancellationToken.None);
                }
                catch (Exception exception)
                {
                    errors.Report(exception, "Configured Host auto-start failed");
                }
            }
        }
        catch (Exception exception)
        {
            WriteStartupFailure(exception);
            MessageBox.Show(
                exception.ToString(),
                "MajorMUD Reforged startup failure",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(91);
        }
    }

    public async Task ShutdownApplicationAsync(int exitCode = 0)
    {
        if (Interlocked.Exchange(ref _shutdownStarted, 1) != 0)
            return;

        _applicationLifetime.Cancel();

        try
        {
            if (_services is not null)
            {
                var logger = _services.Get<IAppLogger>();
                using var shutdownCts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                try
                {
                    await _services.Get<IHostCleanupScheduler>().DisposeAsync().AsTask();
                }
                catch (Exception exception)
                {
                    logger.Error("Cleanup scheduler shutdown failed", exception);
                }

                var shutdownTasks = new[]
                {
                    StopHostSafelyAsync(shutdownCts.Token, logger),
                    StopClientSessionsSafelyAsync(logger),
                    SaveSettingsSafelyAsync(logger),
                };

                try
                {
                    await Task.WhenAll(shutdownTasks).WaitAsync(TimeSpan.FromSeconds(9));
                }
                catch (Exception exception)
                {
                    logger.Error("Graceful shutdown timed out; forcing process termination", exception);
                }
            }
        }
        finally
        {
            Dispatcher.Invoke(() => Shutdown(exitCode));
            _ = Task.Run(async () =>
            {
                await Task.Delay(1500);
                Environment.Exit(exitCode);
            });
        }
    }

    private async Task StopHostSafelyAsync(CancellationToken token, IAppLogger logger)
    {
        try
        {
            if (_services is not null)
                await _services.Get<IReforgedHostController>().StopAsync(token);
        }
        catch (Exception exception)
        {
            logger.Error("Host shutdown failed", exception);
        }
    }

    private async Task StopClientSessionsSafelyAsync(IAppLogger logger)
    {
        try
        {
            if (_services is not null)
                await _services.Get<IClientSessionService>().DisposeAsync();
        }
        catch (Exception exception)
        {
            logger.Error("Client session shutdown failed", exception);
        }
    }

    private async Task SaveSettingsSafelyAsync(IAppLogger logger)
    {
        try
        {
            if (_services is not null)
                await _services.Get<IApplicationSettingsService>().SaveAsync();
        }
        catch (Exception exception)
        {
            logger.Error("Settings save during shutdown failed", exception);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _applicationLifetime.Cancel();
        _applicationLifetime.Dispose();
        base.OnExit(e);
    }

    private static void WriteStartupFailure(Exception exception)
    {
        try
        {
            var root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RealmGate",
                "Logs");
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "startup-failure.log"), exception.ToString());
        }
        catch
        {
        }
    }
}
