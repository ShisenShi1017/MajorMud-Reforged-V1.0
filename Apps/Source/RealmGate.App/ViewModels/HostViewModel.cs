using System.Windows.Input;
using RealmGate.App.Commands;
using RealmGate.App.Models;
using RealmGate.App.Services;
using RealmGate.Server;

namespace RealmGate.App.ViewModels;

public sealed class HostViewModel : ViewModelBase
{
    private readonly IReforgedHostController _host;
    private readonly IHostCleanupScheduler _cleanup;
    private readonly IApplicationSettingsService _settings;
    private readonly IAppLogger _logger;
    private string _bindAddress = RealmGateNetworkBinding.AllIpv4InterfacesAddress;
    private int _port = RealmGateNetworkBinding.DefaultTelnetPort;
    private int _maximumSessions = 64;
    private string _selectedWorld = "classic";

    public HostViewModel(IReforgedHostController host, IHostCleanupScheduler cleanup, IApplicationSettingsService settings, IAppLogger logger)
    {
        _host = host;
        _cleanup = cleanup;
        _settings = settings;
        _logger = logger;
        _bindAddress = string.IsNullOrWhiteSpace(settings.Current.HostBindAddress)
            ? RealmGateNetworkBinding.AllIpv4InterfacesAddress
            : settings.Current.HostBindAddress;
        _port = settings.Current.HostPort is >= 1 and <= 65535
            ? settings.Current.HostPort
            : RealmGateNetworkBinding.DefaultTelnetPort;
        _maximumSessions = Math.Max(1, settings.Current.HostMaximumSessions);
        _selectedWorld = settings.Current.DefaultWorld;
        Server = new ServerViewModel(host);
        StartCommand = new AsyncRelayCommand(() => ExecuteHostActionAsync(() => _host.StartAsync(Options(), CancellationToken.None), "start"));
        StopCommand = new AsyncRelayCommand(() => ExecuteHostActionAsync(() => _host.StopAsync(CancellationToken.None), "stop"));
        RestartCommand = new AsyncRelayCommand(() => ExecuteHostActionAsync(() => _host.RestartAsync(Options(), CancellationToken.None), "restart"));
        UseLocalOnlyCommand = new RelayCommand(() => BindAddress = RealmGateNetworkBinding.LocalOnlyAddress);
        UseRemoteHostingCommand = new RelayCommand(() => BindAddress = RealmGateNetworkBinding.AllIpv4InterfacesAddress);
        SaveCleanupCommand = new AsyncRelayCommand(() => _cleanup.UpdateScheduleAsync(Cleanup, CancellationToken.None));
        RunCleanupNowCommand = new AsyncRelayCommand(() => _cleanup.RunCleanupNowAsync(CancellationToken.None));
        PostponeCleanupCommand = new AsyncRelayCommand(() => _cleanup.PostponeAsync(TimeSpan.FromMinutes(30), CancellationToken.None));
        CancelCleanupCommand = new AsyncRelayCommand(() => _cleanup.CancelPendingCleanupAsync(CancellationToken.None));
        _host.StateChanged += (_, _) => RefreshHostState();
        _cleanup.StateChanged += (_, _) => Dispatch(() =>
        {
            Raise(nameof(CleanupState));
            Raise(nameof(NextCleanup));
            Raise(nameof(Cleanup));
        });
        _logger.Changed += (_, _) => Dispatch(() => Raise(nameof(RecentLogs)));
        SyncOptions();
    }

    public ServerViewModel Server { get; }
    public string BindAddress { get => _bindAddress; set { if (Set(ref _bindAddress, value)) SyncOptions(); } }
    public int Port { get => _port; set { if (Set(ref _port, value)) SyncOptions(); } }
    public int MaximumSessions { get => _maximumSessions; set { if (Set(ref _maximumSessions, value)) SyncOptions(); } }
    public string SelectedWorld { get => _selectedWorld; set { if (Set(ref _selectedWorld, value)) SyncOptions(); } }
    public string State => _host.State.ToString();
    public string Status => _host.StatusMessage;
    public int ConnectedSessions => _host.ConnectedSessionCount;
    public string LanAddresses
    {
        get
        {
            var endpoints = RealmGateNetworkBinding.DiscoverLanEndpoints(Port);
            return endpoints.Count == 0
                ? "LAN: no active IPv4 address detected"
                : "LAN: " + string.Join("   ", endpoints);
        }
    }
    public string RecentLogs => string.Join(Environment.NewLine + Environment.NewLine, _logger.Recent.Reverse());
    public HostCleanupSchedule Cleanup => _settings.Current.HostCleanup;
    public string CleanupState => _cleanup.StatusMessage;
    public DateTimeOffset? NextCleanup => _cleanup.NextCleanupAt;
    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand RestartCommand { get; }
    public ICommand UseLocalOnlyCommand { get; }
    public ICommand UseRemoteHostingCommand { get; }
    public ICommand SaveCleanupCommand { get; }
    public ICommand RunCleanupNowCommand { get; }
    public ICommand PostponeCleanupCommand { get; }
    public ICommand CancelCleanupCommand { get; }

    private async Task ExecuteHostActionAsync(Func<Task> action, string operation)
    {
        try { await action(); }
        catch (Exception exception) { _logger.Error($"Host {operation} request failed without closing RealmGate.", exception); }
        finally { RefreshHostState(); }
    }

    private void RefreshHostState() => Dispatch(() =>
    {
        Raise(nameof(State));
        Raise(nameof(Status));
        Raise(nameof(ConnectedSessions));
        Raise(nameof(LanAddresses));
    });

    private static void Dispatch(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess()) { _ = dispatcher.BeginInvoke(action); return; }
        action();
    }

    private HostLaunchOptions Options() => new(AppContext.BaseDirectory, BindAddress, Port, MaximumSessions, SelectedWorld);

    private void SyncOptions()
    {
        _settings.Current.HostBindAddress = BindAddress;
        _settings.Current.HostPort = Port;
        _settings.Current.HostMaximumSessions = MaximumSessions;
        _ = _settings.SaveAsync();
        _cleanup.UpdateLaunchOptions(Options());
        Raise(nameof(LanAddresses));
    }
}
