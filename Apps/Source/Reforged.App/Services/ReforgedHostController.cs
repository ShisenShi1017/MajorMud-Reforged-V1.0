using MajorMud.Reforged.Compiler;
using Reforged.App.Models;
using Reforged.Server;

namespace Reforged.App.Services;

public sealed class ReforgedHostController(IAppLogger logger) : IReforgedHostController
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CancellationTokenSource? _runCts;
    private Task? _runTask;
    private ServerRuntimeHost? _host;

    public HostControllerState State { get; private set; } = HostControllerState.Stopped;
    public string StatusMessage { get; private set; } = "Stopped";
    public int ConnectedSessionCount => _host?.Sessions.ActiveSessionCount ?? 0;

    public IReadOnlyList<HostSessionSnapshot> ActiveSessions
    {
        get
        {
            var host = _host;
            if (host is null) return Array.Empty<HostSessionSnapshot>();

            return host.Sessions.ActiveSessions
                .OrderBy(session => session.ConnectedAtUtc)
                .Select(session => CreateSnapshot(host, session))
                .ToArray();
        }
    }

    public event EventHandler? StateChanged;

    public async Task StartAsync(HostLaunchOptions options, CancellationToken token)
    {
        await _gate.WaitAsync(token);
        try
        {
            if (State is HostControllerState.Starting or HostControllerState.Running)
                throw new InvalidOperationException("A Host instance is already active.");

            Set(HostControllerState.Starting, "Starting");
            var resolvedProjectRoot = ResolveProjectRoot(options.ProjectRoot);
            var bootstrap = new ServerBootstrap().Run(resolvedProjectRoot);
            if (!bootstrap.Passed || bootstrap.ProjectRoot is null)
            {
                var details = bootstrap.Validation.Issues
                    .Where(issue => issue.Severity is Reforged.Core.Validation.ValidationSeverity.Error
                        or Reforged.Core.Validation.ValidationSeverity.Fatal)
                    .Select(issue => $"{issue.Code}: {issue.Message}")
                    .Take(4)
                    .ToArray();

                var suffix = details.Length == 0
                    ? string.Empty
                    : $" {string.Join(" | ", details)}";

                throw new InvalidOperationException(
                    $"Host bootstrap failed with {bootstrap.ErrorCount + bootstrap.FatalCount} error(s). " +
                    $"Requested root: '{options.ProjectRoot}'. Resolved root: '{resolvedProjectRoot}'.{suffix}");
            }

            var configured = ServerRuntimeOptions.Load(bootstrap.ProjectRoot);
            EnsureGeneratedCustomWorlds(bootstrap.ProjectRoot, configured);
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
                    MaxSessions = options.MaximumSessions,
                    LoginTimeoutSeconds = configured.Network.LoginTimeoutSeconds,
                    AllowWorldSelection = configured.Network.AllowWorldSelection
                }
            };

            _host = new ServerRuntimeHost(bootstrap.ProjectRoot, runtimeOptions);
            _host.Sessions.SessionsChanged += SessionsChanged;
            _host.Bootstrap();
            _runCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            _runTask = _host.RunNetworkAsync(_runCts.Token);
            _ = _runTask.ContinueWith(task =>
            {
                if (task.IsFaulted)
                    Set(HostControllerState.Failed, task.Exception?.GetBaseException().Message ?? "Host failed");
                else if (State != HostControllerState.Stopping)
                    Set(HostControllerState.Stopped, "Stopped");
            }, TaskScheduler.Default);

            Set(HostControllerState.Running, $"Running on {options.BindAddress}:{options.Port}");
            logger.Info("Embedded Host started.");
        }
        catch (Exception exception)
        {
            Set(HostControllerState.Failed, exception.Message);
            logger.Error("Embedded Host start failed.", exception);
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task StopAsync(CancellationToken token)
    {
        await _gate.WaitAsync(token);
        try
        {
            if (State == HostControllerState.Stopped) return;
            Set(HostControllerState.Stopping, "Stopping");
            _runCts?.Cancel();
            if (_runTask is not null)
            {
                try
                {
                    await _runTask.WaitAsync(TimeSpan.FromSeconds(15), token);
                }
                catch (OperationCanceledException)
                {
                }
                catch (TimeoutException)
                {
                    logger.Error("Host did not stop within the graceful shutdown window.");
                }
            }

            if (_host is not null) _host.Sessions.SessionsChanged -= SessionsChanged;
            _runCts?.Dispose();
            _runCts = null;
            _runTask = null;
            _host = null;
            Set(HostControllerState.Stopped, "Stopped");
            logger.Info("Embedded Host stopped.");
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RestartAsync(HostLaunchOptions options, CancellationToken token)
    {
        await StopAsync(token);
        await StartAsync(options, token);
    }

    private static string ResolveProjectRoot(string? startDirectory)
    {
        foreach (var start in new[]
                 {
                     startDirectory,
                     Directory.GetCurrentDirectory(),
                     AppContext.BaseDirectory,
                 })
        {
            if (string.IsNullOrWhiteSpace(start)) continue;

            DirectoryInfo? directory;
            try
            {
                directory = new DirectoryInfo(Path.GetFullPath(start));
            }
            catch
            {
                continue;
            }

            while (directory is not null)
            {
                var root = directory.FullName;
                if (File.Exists(Path.Combine(root, "ProjectManifest.json")) &&
                    Directory.Exists(Path.Combine(root, "Apps")) &&
                    Directory.Exists(Path.Combine(root, "Core")))
                {
                    return root;
                }

                directory = directory.Parent;
            }
        }

        return Path.GetFullPath(startDirectory ?? Directory.GetCurrentDirectory());
    }

    private static void EnsureGeneratedCustomWorlds(string projectRoot, ServerRuntimeOptions options)
    {
        foreach (var world in options.Worlds.Where(world => world.RuntimeEnabled &&
                     world.Type.Equals("ReforgedCustom", StringComparison.OrdinalIgnoreCase)))
        {
            var roomsPath = Path.Combine(projectRoot, world.DataRoot, world.RoomsFile);
            var manifestPath = Path.Combine(projectRoot, world.DataRoot, "world_manifest.json");
            if (File.Exists(roomsPath) && File.Exists(manifestPath)) continue;

            var result = new LibrariumDataWriter().Compile(new LibrariumCompilerOptions(projectRoot, world.Id, Clean: true));
            if (!result.Success)
            {
                var errors = result.Diagnostics
                    .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                    .Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}")
                    .Take(5);
                throw new InvalidOperationException($"Generated runtime data for custom world '{world.Id}' is missing and automatic compilation failed: {string.Join(" | ", errors)}");
            }
        }
    }

    private void SessionsChanged(object? sender, EventArgs eventArgs) =>
        StateChanged?.Invoke(this, EventArgs.Empty);

    private static HostSessionSnapshot CreateSnapshot(ServerRuntimeHost host, RuntimeSession session)
    {
        var character = host.Characters.LoadCharacter(session.Actor.Id);
        var accountId = character?.AccountId ?? string.Empty;
        var access = "Player";

        if (character is not null)
        {
            var policy = RuntimeClassicAdministrationPolicyStore.Load(host.Characters.ProjectRoot, character.WorldId);
            var credential = new RuntimeAccountSecurityService(host.Characters.ProjectRoot).TryLoad(character.AccountId);
            if (policy.HasCharacterSysopAccess(character.CharacterId))
                access = "Sysop (character grant)";
            else if (credential is { Enabled: true })
                access = credential.Role.ToString();

            if (policy.EveryoneCanUseSysGoto && access.Equals("Player", StringComparison.OrdinalIgnoreCase))
                access = "Player + SYS GOTO";
        }

        return new HostSessionSnapshot(
            session.Id,
            session.Actor.Id,
            session.Actor.DisplayName,
            accountId,
            session.World.Definition.Id,
            session.CurrentRoom.Id,
            session.CurrentRoom.Name,
            session.ConnectedAtUtc,
            access);
    }

    private void Set(HostControllerState state, string message)
    {
        State = state;
        StatusMessage = message;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
