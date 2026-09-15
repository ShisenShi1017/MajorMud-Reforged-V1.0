using RealmGate.Compiler;
using RealmGate.Server;

namespace RealmGate.Desktop.Services;

public sealed record DesktopHostOptions(
    string? ProjectRoot,
    string BindAddress,
    int Port,
    int MaximumSessions = 64,
    string? WorldId = null);

public sealed class EmbeddedServerHostController : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CancellationTokenSource? _runCts;
    private Task? _runTask;
    private ServerRuntimeHost? _host;

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

            RealmGateNetworkBinding.ResolveIpv4Address(options.BindAddress);
            RealmGateNetworkBinding.ValidatePort(options.Port);
            if (options.MaximumSessions is < 1 or > 4096)
                throw new InvalidOperationException("Maximum sessions must be between 1 and 4096.");

            StatusMessage = "Preparing runtime data";
            var projectRoot = ResolveProjectRoot(options.ProjectRoot);
            var configured = PrepareRuntimeData(projectRoot, message =>
            {
                StatusMessage = message;
                Log?.Invoke(message);
            });

            StatusMessage = "Bootstrapping server";
            var bootstrap = new ServerBootstrap().Run(projectRoot);
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
                DefaultWorldId = options.WorldId ?? configured.DefaultWorldId,
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
                    AllowWorldSelection = configured.Network.AllowWorldSelection
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
                    StatusMessage = task.Exception?.GetBaseException().Message ?? "Server failed.";
                    Log?.Invoke(StatusMessage);
                }
                else
                {
                    StatusMessage = "Stopped";
                }
            }, TaskScheduler.Default);

            StatusMessage = $"Running on {options.BindAddress}:{options.Port}";
            Log?.Invoke("RealmGate server started.");
            Log?.Invoke($"Listening on {options.BindAddress}:{options.Port}");
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
            }

            _runCts?.Dispose();
            _runCts = null;
            _runTask = null;
            _host = null;
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
            var outputRoot = ServerDataRootResolver.Resolve(projectRoot, world.Id).DataRoot;
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
