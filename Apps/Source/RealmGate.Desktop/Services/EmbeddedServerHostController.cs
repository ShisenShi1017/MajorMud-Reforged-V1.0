using RealmGate.Compiler;
using RealmGate.Server;

namespace RealmGate.Desktop.Services;

public sealed record DesktopHostOptions(
    string? ProjectRoot,
    string BindAddress,
    int Port,
    int MaximumSessions = 64,
    string? WorldId = null,
    string? TestRealmSourceWorldId = null);

public sealed class EmbeddedServerHostController : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CancellationTokenSource? _runCts;
    private Task? _runTask;
    private ServerRuntimeHost? _host;
    private IDisposable? _runtimeDataRootOverride;

    public bool IsRunning => _host is not null && _runTask is { IsCompleted: false };
    public string StatusMessage { get; private set; } = "Stopped";
    public event Action<string>? Log;

    public async Task StartAsync(DesktopHostOptions options, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsRunning)
                throw new InvalidOperationException("A RealmGate server is already running.");

            _runtimeDataRootOverride?.Dispose();
            _runtimeDataRootOverride = null;
            _host = null;
            _runTask = null;

            RealmGateNetworkBinding.ResolveIpv4Address(options.BindAddress);
            RealmGateNetworkBinding.ValidatePort(options.Port);
            if (options.MaximumSessions is < 1 or > 4096)
                throw new InvalidOperationException("Maximum sessions must be between 1 and 4096.");

            StatusMessage = "Preparing runtime data";
            var projectRoot = ResolveProjectRoot(options.ProjectRoot);
            var status = new Action<string>(message =>
            {
                StatusMessage = message;
                Log?.Invoke(message);
            });
            var configured = string.IsNullOrWhiteSpace(options.TestRealmSourceWorldId)
                ? PrepareRuntimeData(projectRoot, status)
                : PrepareTestRealmRuntimeData(
                    projectRoot,
                    options.TestRealmSourceWorldId,
                    status,
                    out _runtimeDataRootOverride);
            var testWorld = string.IsNullOrWhiteSpace(options.TestRealmSourceWorldId)
                ? null
                : configured.Worlds.Single();

            StatusMessage = "Bootstrapping server";
            var bootstrap = new ServerBootstrap().Run(projectRoot, testWorld?.Id);
            if (!bootstrap.Passed || bootstrap.ProjectRoot is null)
            {
                var details = bootstrap.Validation.Issues
                    .Where(issue => issue.Severity is RealmGate.Core.Validation.ValidationSeverity.Error
                        or RealmGate.Core.Validation.ValidationSeverity.Fatal)
                    .Select(issue => $"{issue.Code}: {issue.Message}")
                    .Take(4)
                    .ToArray();
                throw new InvalidOperationException(details.Length == 0
                    ? "RealmGate server bootstrap failed."
                    : "RealmGate server bootstrap failed. " + string.Join(" | ", details));
            }

            var runtimeOptions = new ServerRuntimeOptions
            {
                HostName = configured.HostName,
                DefaultWorldId = testWorld?.Id ?? options.WorldId ?? configured.DefaultWorldId,
                WorldTickMilliseconds = configured.WorldTickMilliseconds,
                CombatTickMilliseconds = configured.CombatTickMilliseconds,
                Development = configured.Development,
                Worlds = configured.Worlds,
                Network = new NetworkRuntimeOptions
                {
                    Enabled = true,
                    BindAddress = options.BindAddress,
                    TelnetPort = options.Port,
                    ClientDataPort = RealmGateNetworkBinding.PairedClientDataPort(options.Port),
                    MaxSessions = options.MaximumSessions,
                    LoginTimeoutSeconds = configured.Network.LoginTimeoutSeconds,
                    AllowWorldSelection = testWorld is null && configured.Network.AllowWorldSelection
                }
            };

            _host = new ServerRuntimeHost(bootstrap.ProjectRoot, runtimeOptions, message => Log?.Invoke("ERROR: " + message));
            _host.Bootstrap();
            _runCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _runTask = _host.RunNetworkAsync(_runCts.Token);

            if (_runTask.IsCompleted)
                await _runTask.ConfigureAwait(false);

            _ = _runTask.ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    var exception = task.Exception?.GetBaseException();
                    StatusMessage = exception?.Message ?? "Server failed.";
                    var diagnostic = exception?.ToString() ?? StatusMessage;
                    // Log the full exception to stderr as well as the UI, so
                    // Linux terminal captures include actionable stack traces.
                    Console.Error.WriteLine("RealmGate embedded Host failure: " + diagnostic);
                    Log?.Invoke(diagnostic);
                }
                else
                {
                    StatusMessage = "Stopped";
                }
            }, TaskScheduler.Default);

            StatusMessage = $"Running on {options.BindAddress}:{options.Port}";
            Log?.Invoke("RealmGate server started.");
            Log?.Invoke($"Listening on {options.BindAddress}:{options.Port}");
            if (testWorld is not null)
            {
                Log?.Invoke($"Test Realm: {testWorld.Name} [{testWorld.Id}]");
                Log?.Invoke($"Test saves: {RuntimeSavePaths.RealmRoot(projectRoot, testWorld.Id)}");
            }
            var lan = RealmGateNetworkBinding.DiscoverLanEndpoints(options.Port);
            if (lan.Count > 0)
                Log?.Invoke("LAN: " + string.Join(", ", lan));
        }
        catch
        {
            _runCts?.Cancel();
            _runCts?.Dispose();
            _runCts = null;
            _runTask = null;
            _host = null;
            _runtimeDataRootOverride?.Dispose();
            _runtimeDataRootOverride = null;
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_host is null)
            {
                StatusMessage = "Stopped";
                return;
            }

            StatusMessage = "Stopping";
            _runCts?.Cancel();
            if (_runTask is not null)
            {
                try
                {
                    await _runTask.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (_runCts?.IsCancellationRequested == true)
                {
                }
                catch (TimeoutException)
                {
                    Log?.Invoke("Server did not stop within the graceful shutdown window.");
                }
                catch (Exception exception)
                {
                    Log?.Invoke($"Server runtime had already stopped with an error: {exception.GetBaseException().Message}");
                }
            }

            _runCts?.Dispose();
            _runCts = null;
            _runTask = null;
            _host = null;
            _runtimeDataRootOverride?.Dispose();
            _runtimeDataRootOverride = null;
            StatusMessage = "Stopped";
            Log?.Invoke("RealmGate server stopped.");
        }
        finally
        {
            _gate.Release();
        }
    }

    public ValueTask DisposeAsync() => new(StopAsync());

    private static ServerRuntimeOptions PrepareRuntimeData(string projectRoot, Action<string> status)
    {
        if (!File.Exists(Path.Combine(projectRoot, "ProjectManifest.json")))
            throw new InvalidOperationException($"RealmGate project manifest was not found at '{projectRoot}'.");
        if (!Directory.Exists(Path.Combine(projectRoot, "Librarium")))
            throw new InvalidOperationException($"RealmGate Librarium was not found under '{projectRoot}'.");

        foreach (var directory in new[] { "Data", "Reports", "Bin" })
            Directory.CreateDirectory(Path.Combine(projectRoot, directory));

        var configured = ServerRuntimeOptions.Load(projectRoot);
        foreach (var world in configured.Worlds.Where(world => world.RuntimeEnabled))
        {
            var outputRoot = ServerDataRootResolver.ResolveForGeneration(projectRoot, world.Id).DataRoot;
            var roomsPath = Path.Combine(outputRoot, world.RoomsFile);
            var manifestPath = Path.Combine(outputRoot, "world_manifest.json");
            if (File.Exists(roomsPath) && File.Exists(manifestPath))
                continue;

            status($"Building {world.Name} from Librarium");
            var result = new LibrariumDataWriter().Compile(new LibrariumCompilerOptions(
                projectRoot,
                world.Id,
                Clean: true,
                OutputRootOverride: outputRoot));

            if (!result.Success)
            {
                var errors = result.Diagnostics
                    .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                    .Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}")
                    .Take(5)
                    .ToArray();
                throw new InvalidOperationException(errors.Length == 0
                    ? $"World '{world.Id}' could not be generated from Librarium."
                    : string.Join(" | ", errors));
            }

            if (!File.Exists(roomsPath) || !File.Exists(manifestPath))
                throw new InvalidOperationException($"World '{world.Id}' generation did not produce required runtime files in '{outputRoot}'.");
        }

        return configured;
    }

    private static ServerRuntimeOptions PrepareTestRealmRuntimeData(
        string projectRoot,
        string sourceWorldId,
        Action<string> status,
        out IDisposable runtimeDataRootOverride)
    {
        runtimeDataRootOverride = null!;
        if (!File.Exists(Path.Combine(projectRoot, "ProjectManifest.json")))
            throw new InvalidOperationException($"RealmGate project manifest was not found at '{projectRoot}'.");
        if (!Directory.Exists(Path.Combine(projectRoot, "Librarium")))
            throw new InvalidOperationException($"RealmGate Librarium was not found under '{projectRoot}'.");

        foreach (var directory in new[] { "Data", "Reports", "Bin" })
            Directory.CreateDirectory(Path.Combine(projectRoot, directory));

        var configured = ServerRuntimeOptions.Load(projectRoot);
        var source = configured.Worlds.FirstOrDefault(world =>
            world.Id.Equals(sourceWorldId, StringComparison.OrdinalIgnoreCase));
        if (source is null)
            throw new InvalidOperationException($"Configured world '{sourceWorldId}' was not found.");
        if (source.ClassicProtected || source.Type.Equals("Classic", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Creator Studio Test Realm sessions cannot replace or clone protected Classic.");

        var testRealmRoot = Path.Combine(projectRoot, "Data", "TestRealms", source.Id);
        status($"Validating {source.Name} Test Realm");
        var validation = new GeneratedProjectionValidator().ValidateOutputRoot(testRealmRoot, source.Id);
        if (!validation.Success)
        {
            throw new InvalidOperationException(
                "The Test Realm is not ready to run. " + string.Join(" | ", validation.Errors.Take(5)));
        }

        var runtimeWorldId = $"{source.Id}-Test";
        var activationRoot = Path.Combine(projectRoot, "Data", runtimeWorldId);
        status($"Activating isolated {source.Name} Test Realm runtime");
        MirrorDirectory(testRealmRoot, activationRoot);
        var activatedValidation = new GeneratedProjectionValidator().ValidateOutputRoot(activationRoot, source.Id);
        if (!activatedValidation.Success)
        {
            throw new InvalidOperationException(
                "The activated Test Realm did not pass validation. " +
                string.Join(" | ", activatedValidation.Errors.Take(5)));
        }

        var dataRoot = Path.GetRelativePath(projectRoot, activationRoot);
        var testWorld = new WorldRuntimeDefinition
        {
            Id = runtimeWorldId,
            DataWorldId = source.Id,
            Name = $"{source.Name} Test Realm",
            Description = $"Isolated interactive Test Realm generated from {source.Name}.",
            Type = source.Type,
            RulesProfile = source.RulesProfile,
            DataRoot = dataRoot,
            RoomsFile = source.RoomsFile,
            ExitsFile = source.ExitsFile,
            TriggersFile = source.TriggersFile,
            NpcsFile = source.NpcsFile,
            SpawnsFile = source.SpawnsFile,
            DiagnosticRoom = source.DiagnosticRoom,
            AiMode = source.AiMode,
            BrandingLogo = source.BrandingLogo,
            LoginBanner = string.IsNullOrWhiteSpace(source.LoginBanner)
                ? $"{source.Name} Test Realm"
                : $"{source.LoginBanner} - TEST REALM",
            Motd = $"TEST REALM - changes and characters are isolated from live {source.Name}.\n{source.Motd}",
            RuntimeEnabled = true,
            TransfersAllowed = false,
            ClassicProtected = false
        };

        runtimeDataRootOverride = ServerDataRootResolver.RegisterRuntimeOverride(
            projectRoot,
            testWorld.Id,
            source.Id,
            activationRoot);

        return new ServerRuntimeOptions
        {
            HostName = $"{configured.HostName} - {source.Name} Test Realm",
            DefaultWorldId = testWorld.Id,
            WorldTickMilliseconds = configured.WorldTickMilliseconds,
            CombatTickMilliseconds = configured.CombatTickMilliseconds,
            Development = configured.Development,
            Network = configured.Network,
            Worlds = new[] { testWorld }
        };
    }

    private static void MirrorDirectory(string sourceRoot, string destinationRoot)
    {
        var stagingRoot = destinationRoot + ".staging-" + Guid.NewGuid().ToString("N");
        var previousRoot = destinationRoot + ".previous-" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(stagingRoot);
        try
        {
            foreach (var sourcePath in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(sourceRoot, sourcePath);
                var destinationPath = Path.Combine(stagingRoot, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                File.Copy(sourcePath, destinationPath, overwrite: true);
            }

            if (Directory.Exists(destinationRoot)) Directory.Move(destinationRoot, previousRoot);
            try
            {
                Directory.Move(stagingRoot, destinationRoot);
                if (Directory.Exists(previousRoot)) Directory.Delete(previousRoot, recursive: true);
            }
            catch
            {
                if (Directory.Exists(destinationRoot)) Directory.Delete(destinationRoot, recursive: true);
                if (Directory.Exists(previousRoot)) Directory.Move(previousRoot, destinationRoot);
                throw;
            }
        }
        finally
        {
            if (Directory.Exists(stagingRoot)) Directory.Delete(stagingRoot, recursive: true);
            if (Directory.Exists(previousRoot) && !Directory.Exists(destinationRoot))
                Directory.Move(previousRoot, destinationRoot);
        }
    }

    private static string ResolveProjectRoot(string? requestedRoot)
    {
        foreach (var start in new[] { requestedRoot, Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            if (string.IsNullOrWhiteSpace(start)) continue;
            DirectoryInfo? current;
            try { current = new DirectoryInfo(Path.GetFullPath(start)); }
            catch { continue; }

            while (current is not null)
            {
                if (File.Exists(Path.Combine(current.FullName, "ProjectManifest.json")) &&
                    Directory.Exists(Path.Combine(current.FullName, "Librarium")))
                    return current.FullName;
                current = current.Parent;
            }
        }

        throw new InvalidOperationException("RealmGate project root could not be located.");
    }
}
