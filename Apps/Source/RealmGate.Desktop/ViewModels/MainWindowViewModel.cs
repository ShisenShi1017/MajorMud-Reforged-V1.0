using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia.Threading;
using RealmGate.Desktop.Services;
using RealmGate.Server;

namespace RealmGate.Desktop.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged, IAsyncDisposable
{
    private readonly EmbeddedServerHostController _host = new();
    private readonly Action<string> _hostLogHandler;
    private string _bindAddress = RealmGateNetworkBinding.AllIpv4InterfacesAddress;
    private string _portText = RealmGateNetworkBinding.DefaultTelnetPort.ToString();
    private string _maximumSessionsText = "64";
    private string _statusMessage = "Stopped";
    private string _listenerText = "Not listening";
    private string _lanEndpointsText = "No LAN addresses discovered yet.";
    private string _logText = "Avalonia desktop host initialized.";

    public MainWindowViewModel()
    {
        _hostLogHandler = message => Dispatcher.UIThread.Post(() => AppendLog(message));
        _host.Log += _hostLogHandler;

        UseLocalOnlyCommand = new RelayCommand(() =>
        {
            BindAddress = RealmGateNetworkBinding.LocalOnlyAddress;
            RefreshLanEndpoints();
        });
        UseRemoteHostingCommand = new RelayCommand(() =>
        {
            BindAddress = RealmGateNetworkBinding.AllIpv4InterfacesAddress;
            RefreshLanEndpoints();
        });
        RefreshLanCommand = new RelayCommand(RefreshLanEndpoints);
        StartServerCommand = new AsyncCommand(StartServerAsync);
        StopServerCommand = new AsyncCommand(StopServerAsync);
        RefreshLanEndpoints();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand UseLocalOnlyCommand { get; }
    public ICommand UseRemoteHostingCommand { get; }
    public ICommand RefreshLanCommand { get; }
    public ICommand StartServerCommand { get; }
    public ICommand StopServerCommand { get; }

    public string BindAddress
    {
        get => _bindAddress;
        set => Set(ref _bindAddress, value);
    }

    public string PortText
    {
        get => _portText;
        set
        {
            if (Set(ref _portText, value)) RefreshLanEndpoints();
        }
    }

    public string MaximumSessionsText
    {
        get => _maximumSessionsText;
        set => Set(ref _maximumSessionsText, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => Set(ref _statusMessage, value);
    }

    public string ListenerText
    {
        get => _listenerText;
        private set => Set(ref _listenerText, value);
    }

    public string LanEndpointsText
    {
        get => _lanEndpointsText;
        private set => Set(ref _lanEndpointsText, value);
    }

    public string LogText
    {
        get => _logText;
        private set => Set(ref _logText, value);
    }

    private async Task StartServerAsync()
    {
        try
        {
            if (!int.TryParse(PortText, out var port))
                throw new InvalidOperationException("Port must be a number from 1 through 65535.");
            if (!int.TryParse(MaximumSessionsText, out var maximumSessions))
                throw new InvalidOperationException("Maximum sessions must be a number.");

            StatusMessage = "Starting";
            ListenerText = $"Requested: {BindAddress}:{port}";
            await _host.StartAsync(new DesktopHostOptions(null, BindAddress, port, maximumSessions));
            StatusMessage = _host.StatusMessage;
            ListenerText = $"Listening on {BindAddress}:{port}";
            RefreshLanEndpoints();
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
            ListenerText = "Not listening";
            AppendLog(exception.Message);
        }
    }

    private async Task StopServerAsync()
    {
        try
        {
            StatusMessage = "Stopping";
            await _host.StopAsync();
            StatusMessage = _host.StatusMessage;
            ListenerText = "Not listening";
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
            AppendLog(exception.Message);
        }
    }

    private void RefreshLanEndpoints()
    {
        var port = int.TryParse(PortText, out var parsed) && parsed is >= 1 and <= 65535
            ? parsed
            : RealmGateNetworkBinding.DefaultTelnetPort;
        var endpoints = RealmGateNetworkBinding.DiscoverLanEndpoints(port);
        LanEndpointsText = endpoints.Count == 0
            ? "No active non-loopback IPv4 address was discovered."
            : string.Join(Environment.NewLine, endpoints);
    }

    private void AppendLog(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        var line = $"[{DateTimeOffset.Now:HH:mm:ss}] {message}";
        LogText = string.IsNullOrWhiteSpace(LogText) ? line : LogText + Environment.NewLine + line;
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    public async ValueTask DisposeAsync()
    {
        _host.Log -= _hostLogHandler;
        await _host.DisposeAsync();
    }

    private sealed class RelayCommand(Action execute) : ICommand
    {
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => execute();
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }

    private sealed class AsyncCommand(Func<Task> execute) : ICommand
    {
        private int _running;
        public bool CanExecute(object? parameter) => Volatile.Read(ref _running) == 0;
        public event EventHandler? CanExecuteChanged;

        public async void Execute(object? parameter)
        {
            if (Interlocked.Exchange(ref _running, 1) != 0) return;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            try { await execute(); }
            finally
            {
                Volatile.Write(ref _running, 0);
                CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
