using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Threading;
using Reforged.App.Brains;
using Reforged.App.Services;
using Reforged.App.Forge;
using Reforged.App.BorinCapabilities;
using Reforged.App.ViewModels;
using Reforged.App.Views;

namespace Reforged.App;

public partial class App : Application
{
    private ServiceRegistry? _services;
    private IAsyncDisposable? _brainLifetime;
    private IAsyncDisposable? _playerLifetime;
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
            var playViewModel = new PlayViewModel(clientData, clientAutomation, clientCoordination, megaMudPaths, clientSessions, clientParsing, clientLibrariumBridge, clientSysGoto, clientMapProfiles, clientWorkspaceProfiles);
            var forgeStore = new ForgeStateStore(settings.RootPath);
            var forgeRepository = new ForgeRepositoryService();
            var repositoryRoot = forgeRepository.DiscoverRepositoryRoot();
            var borinMemory = new BorinMemoryStore(repositoryRoot, logger);
            var borinLivingCharacter = new BorinLivingCharacterStore(repositoryRoot, logger);
            var borinSocialMemory = new BorinSocialMemoryStore(repositoryRoot, logger);
            var borinEpisodicMemory = new BorinEpisodicMemoryStore(repositoryRoot, logger);
            var borinMemoryConsolidation = new BorinMemoryConsolidationStore(repositoryRoot, logger);
            var borinTemporalContinuity = new BorinTemporalContinuityStore(repositoryRoot, logger);
            var borinCharacterGrowth = new BorinCharacterGrowthStore(repositoryRoot, logger);
            var borinInitiative = new BorinInitiativeStore(repositoryRoot, logger);
            var borinVoice = new BorinVoiceStore(repositoryRoot, logger);
            var borinConversationRepair = new BorinConversationRepairStore(repositoryRoot, logger);
            var borinSituationalAwareness = new BorinSituationalAwarenessStore(repositoryRoot, logger);
            var borinGroupConversation = new BorinGroupConversationAwarenessStore(repositoryRoot, logger);
            var borinNaturalInitiative = new BorinNaturalInitiativeStore(repositoryRoot, logger);
            var borinGameplayLearning = new BorinGameplayLearningStore(repositoryRoot, logger);
            var borinSelfKnowledge = new BorinSelfKnowledgeStore();
            var borinConversationDirector = new BorinConversationDirector();
            var borinReadOnlyAccess = new BorinReadOnlyAccess();
            var borinClassicTraceAccess = new BorinClassicTraceAccess();
            var borinMajorMudKnowledge = new BorinMajorMudKnowledgeCatalog();
            var borinVerifiedLearning = new BorinVerifiedKnowledgeLearning(repositoryRoot);
            var borinKnowledgeIndex = new BorinKnowledgeCore(new BorinKnowledgeIndex(), borinMajorMudKnowledge, borinVerifiedLearning);
            var borinAcademy = new BorinAcademy(borinKnowledgeIndex, borinVerifiedLearning);
            var borinKnowledgeBootstrap = new BorinReforgedKnowledgeBootstrap();
            var borinMajorMudMastery = new BorinMajorMudMastery();
            var borinEngineeringWorkspace = new BorinEngineeringWorkspace();
            var borinEngineeringOrchestrator = new BorinEngineeringOrchestrator(borinEngineeringWorkspace);
            var borinEngineeringCorrectionLoop = new BorinEngineeringCorrectionLoop(borinEngineeringWorkspace);
            var borinCredentialStore = new WindowsBorinSecureCredentialStore();
            var sharedNvidiaCredentialStore = new WindowsNvidiaNimCredentialStore(NvidiaNimKeySlot.Shared);
            var legacyUltraCredentialStore = new WindowsNvidiaNimCredentialStore(NvidiaNimKeySlot.Ultra);
            await NvidiaNimCredentialMigration.EnsureSharedCredentialAsync(sharedNvidiaCredentialStore, legacyUltraCredentialStore);
            var forgeCommands = new ForgeCommandRunner();
            var forgeBrain = new ForgeBrain(forgeRepository, forgeCommands);
            var localGgufBrain = new LocalGgufBrain(
                new NullBrain(),
                logger,
                borinLivingCharacter,
                borinSocialMemory,
                borinEpisodicMemory,
                borinMemoryConsolidation,
                borinTemporalContinuity,
                borinCharacterGrowth,
                borinInitiative,
                borinVoice,
                borinConversationRepair,
                borinSituationalAwareness,
                borinGroupConversation,
                borinGameplayLearning,
                borinSelfKnowledge,
                borinConversationDirector);
            var overallNvidiaBudget = new SlidingWindowAiRateBudget();
            var lightningRateBudget = new SlidingWindowAiRateBudget();
            var superRateBudget = new SlidingWindowAiRateBudget(new AiRateBudgetOptions
            {
                NormalRequestsPerWindow = 6,
                HardRequestsPerWindow = 8
            });
            var ultraRateBudget = new SlidingWindowAiRateBudget(new AiRateBudgetOptions
            {
                NormalRequestsPerWindow = 8,
                HardRequestsPerWindow = 10
            });
            var rawLightningProvider = new NvidiaNimProvider(new HttpClient(), sharedNvidiaCredentialStore);
            var rawSuperProvider = new NvidiaNimProvider(new HttpClient(), sharedNvidiaCredentialStore, new NvidiaNimOptions
            {
                Model = "nvidia/nemotron-3-super-120b-a12b",
                Timeout = TimeSpan.FromMinutes(3),
                MaximumOutputTokens = 4096,
                Temperature = 0.12
            });
            var rawUltraProvider = new NvidiaNimProvider(new HttpClient(), sharedNvidiaCredentialStore, new NvidiaNimOptions
            {
                Model = "nvidia/nemotron-3-ultra-550b-a55b",
                Timeout = TimeSpan.FromMinutes(4),
                MaximumOutputTokens = 4096,
                Temperature = 0.15
            });
            var lightningProvider = new RateLimitedAiProvider(
                new RateLimitedAiProvider(
                    rawLightningProvider,
                    lightningRateBudget,
                    AiRatePriority.InteractiveBurst),
                overallNvidiaBudget,
                AiRatePriority.InteractiveBurst);
            var ultraProvider = new RateLimitedAiProvider(
                new RateLimitedAiProvider(
                    rawUltraProvider,
                    ultraRateBudget,
                    AiRatePriority.InteractiveBurst),
                overallNvidiaBudget,
                AiRatePriority.InteractiveBurst);
            var superProvider = new RateLimitedAiProvider(
                new RateLimitedAiProvider(rawSuperProvider, superRateBudget, AiRatePriority.InteractiveBurst),
                overallNvidiaBudget,
                AiRatePriority.InteractiveBurst);
            var aiRoutingSettings = new BorinAiRoutingSettings();
            var aiProviderRouter = new TieredBorinAiProviderRouter(
                new LocalAiProvider(localGgufBrain),
                lightningProvider,
                ultraProvider,
                aiRoutingSettings,
                super: superProvider);
            IBrain borinBrain = new AiProviderBrainAdapter(aiProviderRouter);
            _brainLifetime = localGgufBrain;
            var borinPlayerResearch = new BorinPlayerResearch(
                clientSessions,
                borinBrain,
                logger,
                borinCredentialStore,
                borinNaturalInitiative,
                borinGameplayLearning);
            _playerLifetime = borinPlayerResearch;
            var forgeBrainManager = new ForgeBrainManager();
            var forgeTools = new ForgeDeveloperTools();
            var forgeViewModel = new BorinForgeViewModel(
                forgeStore,
                forgeRepository,
                borinBrain,
                borinMemory,
                borinReadOnlyAccess,
                borinClassicTraceAccess,
                borinKnowledgeIndex,
                borinKnowledgeBootstrap,
                borinEngineeringWorkspace,
                borinEngineeringOrchestrator,
                borinEngineeringCorrectionLoop,
                borinPlayerResearch,
                borinVerifiedLearning,
                borinMajorMudKnowledge,
                borinAcademy,
                borinNaturalInitiative,
                playViewModel,
                forgeTools,
                forgeBrainManager,
                sharedNvidiaCredentialStore,
                lightningProvider,
                superProvider,
                ultraProvider,
                lightningRateBudget,
                superRateBudget,
                ultraRateBudget,
                overallNvidiaBudget,
                aiProviderRouter,
                rawLightningProvider,
                rawSuperProvider,
                rawUltraProvider,
                aiRoutingSettings,
                localGgufBrain);
            var guardianStore = new BorinCapabilityStateStore(settings.RootPath);
            var borinCapabilities = new BorinCapabilityCoordinator(
                guardianStore,
                forgeRepository.DiscoverRepositoryRoot(),
                borinPlayerResearch);
            forgeViewModel.AttachGuardianCapabilities(borinCapabilities);

            var editorViewModel = new Lazy<EditorViewModel>(() => new EditorViewModel(
                new ClassicAdministrationEditorService(repositoryRoot, host),
                new CustomWorldEditorService(repositoryRoot)));

            manager.Register(new LazyWorkspace("home", "Dashboard", () => new DashboardView { DataContext = dashboardViewModel }));
            manager.Register(new LazyWorkspace("play", "Play", () => new PlayView { DataContext = playViewModel }, true));
            manager.Register(new LazyWorkspace("host", "Host", () => new HostView { DataContext = hostViewModel }));
            manager.Register(new LazyWorkspace("editor", "Editor", () => new EditorView { DataContext = editorViewModel.Value }, true));
            manager.Register(new LazyWorkspace("librarium", "Librarium", () => new LibrariumView(), true));
            manager.Register(new LazyWorkspace("forge", "Borin", () => new BorinForgeView { DataContext = forgeViewModel }));
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
                .Add<IClientDiagnosticsService>(clientDiagnostics)
                .Add<IForgeStateStore>(forgeStore)
                .Add<IForgeRepositoryService>(forgeRepository)
                .Add<IForgeCommandRunner>(forgeCommands)
                .Add<IForgeBrain>(forgeBrain)
                .Add<LocalGgufBrain>(localGgufBrain)
                .Add<IBrain>(borinBrain)
                .Add<IBorinMemoryStore>(borinMemory)
                .Add<IBorinLivingCharacterStore>(borinLivingCharacter)
                .Add<IBorinSocialMemoryStore>(borinSocialMemory)
                .Add<IBorinEpisodicMemoryStore>(borinEpisodicMemory)
                .Add<IBorinMemoryConsolidationStore>(borinMemoryConsolidation)
                .Add<IBorinTemporalContinuityStore>(borinTemporalContinuity)
                .Add<IBorinCharacterGrowthStore>(borinCharacterGrowth)
                .Add<IBorinInitiativeStore>(borinInitiative)
                .Add<IBorinVoiceStore>(borinVoice)
                .Add<IBorinConversationRepairStore>(borinConversationRepair)
                .Add<IBorinSituationalAwarenessStore>(borinSituationalAwareness)
                .Add<IBorinGroupConversationAwarenessStore>(borinGroupConversation)
                .Add<IBorinNaturalInitiativeStore>(borinNaturalInitiative)
                .Add<IBorinGameplayLearningStore>(borinGameplayLearning)
                .Add<IBorinSelfKnowledgeStore>(borinSelfKnowledge)
                .Add<IBorinConversationDirector>(borinConversationDirector)
                .Add<IBorinReadOnlyAccess>(borinReadOnlyAccess)
                .Add<IBorinClassicTraceAccess>(borinClassicTraceAccess)
                .Add<IBorinKnowledgeIndex>(borinKnowledgeIndex)
                .Add<IBorinKnowledgeCore>(borinKnowledgeIndex)
                .Add<IBorinMajorMudKnowledgeCatalog>(borinMajorMudKnowledge)
                .Add<IBorinVerifiedKnowledgeLearning>(borinVerifiedLearning)
                .Add<IBorinAcademy>(borinAcademy)
                .Add<IBorinReforgedKnowledgeBootstrap>(borinKnowledgeBootstrap)
                .Add<IBorinMajorMudMastery>(borinMajorMudMastery)
                .Add<IBorinEngineeringWorkspace>(borinEngineeringWorkspace)
                .Add<IBorinEngineeringOrchestrator>(borinEngineeringOrchestrator)
                .Add<IBorinEngineeringCorrectionLoop>(borinEngineeringCorrectionLoop)
                .Add<IBorinSecureCredentialStore>(borinCredentialStore)
                .Add<INvidiaNimCredentialStore>(sharedNvidiaCredentialStore)
                .Add<IAiRateBudget>(overallNvidiaBudget)
                .Add<IBorinAiRouteTelemetry>(aiProviderRouter)
                .Add<IAiProvider>(aiProviderRouter)
                .Add<IBorinPlayerResearch>(borinPlayerResearch)
                .Add<IForgeBrainManager>(forgeBrainManager)
                .Add<IForgeDeveloperTools>(forgeTools)
                .Add<IBorinCapabilityStateStore>(guardianStore)
                .Add<IBorinCapabilities>(borinCapabilities);

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

            _ = InitializeDeferredWorkspacesAsync(
                playViewModel,
                forgeViewModel,
                borinCapabilities,
                errors,
                logger);
        }
        catch (Exception exception)
        {
            WriteStartupFailure(exception);
            MessageBox.Show(
                exception.ToString(),
                "RealmGate startup failure",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(91);
        }
    }

    private async Task InitializeDeferredWorkspacesAsync(
        PlayViewModel playViewModel,
        BorinForgeViewModel forgeViewModel,
        IBorinCapabilities borinCapabilities,
        IErrorReporter errors,
        IAppLogger logger)
    {
        // Let WPF render the shell before loading the large private knowledge and world catalogs.
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);

        try
        {
            await playViewModel.InitializeAsync();
            logger.Info("Deferred Play workspace initialization completed.");
        }
        catch (Exception exception)
        {
            errors.Report(exception, "Deferred Play workspace initialization failed");
        }

        try
        {
            await forgeViewModel.InitializeAsync();
            logger.Info("Deferred Borin workspace initialization completed.");
        }
        catch (Exception exception)
        {
            errors.Report(exception, "Deferred Borin workspace initialization failed");
        }

        try
        {
            await borinCapabilities.InitializeAsync(_applicationLifetime.Token);
            logger.Info("Deferred Borin capability initialization completed.");
        }
        catch (OperationCanceledException) when (_applicationLifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            errors.Report(exception, "Deferred Borin capability initialization failed");
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
                    DisposeBrainSafelyAsync(logger),
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

            // WPF shutdown can still be held open by a non-background worker or native socket.
            // Force termination after the coordinated cleanup window so Exit and X always finish.
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


    private async Task DisposeBrainSafelyAsync(IAppLogger logger)
    {
        if (_playerLifetime is not null)
        {
            try
            {
                await _playerLifetime.DisposeAsync();
                _playerLifetime = null;
            }
            catch (Exception exception)
            {
                logger.Error("Borin external-player shutdown failed", exception);
            }
        }

        if (_brainLifetime is null)
            return;

        try
        {
            await _brainLifetime.DisposeAsync();
            _brainLifetime = null;
        }
        catch (Exception exception)
        {
            logger.Error("Direct GGUF brain shutdown failed", exception);
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
